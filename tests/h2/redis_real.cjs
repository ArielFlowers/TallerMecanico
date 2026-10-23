const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { execFileSync } = require('node:child_process');
const { request } = require(process.env.PLAYWRIGHT_MODULE);

const root = path.resolve(__dirname, '../..');
const composeArgs = ['compose', '--env-file', 'tests/h2/local.env', '-f', 'tests/h2/docker-compose.yml'];
const clave = 'autotaller:dashboard:resumen';
function docker(args, input) {
    return execFileSync('docker', args, { cwd: root, input, encoding: 'utf8', timeout: 30000 }).trim();
}
const compose = (...args) => docker([...composeArgs, ...args]);
const redis = (...args) => compose('exec', '-T', 'redis', 'redis-cli', '--raw', ...args);
function estadoEnHtml(html) {
    const etiqueta = html.match(/<span class="cache-estado [^"]*">([^<]+)<\/span>/);
    assert.ok(etiqueta, 'Debe existir el indicador real de caché.');
    return etiqueta[1].replace(/&#(?:x([0-9a-f]+)|(\d+));/gi,
        (_, hexadecimal, decimal) => String.fromCodePoint(parseInt(hexadecimal ?? decimal, hexadecimal ? 16 : 10))).trim();
}
function mysql(sql) {
    return docker([...composeArgs, 'exec', '-T', 'mysql', 'sh', '-c',
        'MYSQL_PWD="$MYSQL_ROOT_PASSWORD" exec mysql -uroot --batch --raw --skip-column-names'], sql);
}
function conteos() {
    const filas = mysql(`SELECT
        CASE WHEN LOWER(CONVERT(argument USING utf8mb4)) LIKE '%mecanicos%' THEN 'Mecanicos'
             WHEN LOWER(CONVERT(argument USING utf8mb4)) LIKE '%vehiculos%' THEN 'Vehiculos' ELSE 'Servicios' END,
        COUNT(*)
        FROM mysql.general_log
        WHERE user_host LIKE 'h2_app%' AND command_type IN ('Query', 'Execute')
          AND LOWER(TRIM(CONVERT(argument USING utf8mb4))) REGEXP
          '^select[[:space:]]+count[(][*][)][[:space:]]+from[[:space:]]+(mecanicos|vehiculos|servicios)[[:space:]]*;?$'
        GROUP BY 1 ORDER BY 1;`);
    const resultado = { Mecanicos: 0, Vehiculos: 0, Servicios: 0 };
    for (const fila of filas.split('\n').filter(Boolean)) {
        const [tabla, cantidad] = fila.trim().split('\t');
        assert.ok(Object.hasOwn(resultado, tabla));
        resultado[tabla] = Number(cantidad);
    }
    return resultado;
}
function consultasUsuarios() {
    return Number(mysql(`SELECT COUNT(*) FROM mysql.general_log
        WHERE user_host LIKE 'h2_app%' AND command_type IN ('Query', 'Execute')
          AND LOWER(TRIM(CONVERT(argument USING utf8mb4))) LIKE 'select%'
          AND LOWER(CONVERT(argument USING utf8mb4)) REGEXP 'from[[:space:]]+usuarios[[:space:]]';`));
}

(async () => {
    const variables = Object.fromEntries(fs.readFileSync(path.join(__dirname, 'local.env'), 'utf8')
        .split(/\r?\n/).filter(linea => /^[A-Z0-9_]+=/.test(linea))
        .map(linea => [linea.slice(0, linea.indexOf('=')), linea.slice(linea.indexOf('=') + 1)]));
    const credenciales = { username: variables.H2_TEST_USERNAME, password: variables.H2_TEST_PASSWORD };
    assert.equal(credenciales.username, 'h2_recepcionista');
    assert.ok(credenciales.password);
    const evidencia = { fechaUtc: new Date().toISOString(), clave, replicas: [] };
    const http = await request.newContext();
    try {
        const bases = {};
        for (const servicio of ['app1', 'app2', 'app3']) {
            const id = compose('ps', '-q', servicio);
            const contenedor = JSON.parse(docker(['inspect', id]))[0];
            assert.equal(contenedor.Config.Labels['com.docker.compose.project'], 'autotaller-h2-integracion');
            const entorno = Object.fromEntries(contenedor.Config.Env.map(entrada => {
                const separador = entrada.indexOf('=');
                return [entrada.slice(0, separador), entrada.slice(separador + 1)];
            }));
            assert.match(entorno.ConnectionStrings__MySqlConnection,
                /^Server=mysql;Port=3306;Database=autotaller_h2_integracion;User ID=h2_app;Password=/);
            assert.equal(entorno.REDIS_HOST, 'redis');
            assert.equal(entorno.REDIS_PORT, '6379');
            assert.equal(entorno.DataProtection__KeysPath, '/app/shared-keys');
            bases[servicio] = `http://${compose('port', servicio, '8080')}`;
            const respuesta = await http.get(`${bases[servicio]}/h2/redis`);
            assert.equal(respuesta.status(), 200);
            const diagnostico = await respuesta.json();
            assert.equal(diagnostico.redis, 'PONG');
            evidencia.replicas.push({ servicio, url: bases[servicio], ...diagnostico });
        }
        assert.equal(new Set(evidencia.replicas.map(replica => replica.replica)).size, 3);
        evidencia.ping = redis('PING');
        assert.equal(evidencia.ping, 'PONG');
        const clientes = redis('CLIENT', 'LIST');
        for (const replica of evidencia.replicas) assert.ok(clientes.includes(`name=autotaller-${replica.replica}`));
        evidencia.clientesDeLasTresReplicasEnElMismoRedis = true;
        evidencia.nginxUrl = `http://${compose('port', 'nginx', '80')}`;
        const balanceadas = new Set();
        for (let intento = 0; intento < 9; intento++) {
            const respuesta = await http.get(`${evidencia.nginxUrl}/h1/replica`);
            assert.equal(respuesta.status(), 200);
            balanceadas.add((await respuesta.json()).replica);
        }
        assert.equal(balanceadas.size, 3);
        evidencia.replicasObservadasPorNginx = [...balanceadas];

        const login = await http.get(`${bases.app1}/Auth/Login`);
        assert.equal(login.status(), 200);
        const token = (await login.text()).match(/name="__RequestVerificationToken"[^>]*value="([^"]+)"/);
        assert.ok(token, 'Se requiere el antiforgery real del formulario.');
        const autenticacion = await http.post(`${bases.app1}/Auth/Login`, {
            form: { 'Input.Username': credenciales.username, 'Input.Password': credenciales.password,
                __RequestVerificationToken: token[1] }, maxRedirects: 0
        });
        assert.equal(autenticacion.status(), 302, 'El login real debe aprobar sin seguir aún al dashboard.');
        assert.equal(autenticacion.headers().location, '/');
        evidencia.loginReal = '302 a /; no se siguió la redirección antes de medir';

        const restantePrevio = Number(redis('PTTL', clave));
        if (restantePrevio > 0) await new Promise(resolve => setTimeout(resolve, restantePrevio + 250));
        assert.equal(redis('EXISTS', clave), '0');
        evidencia.ttlInicial = Number(redis('TTL', clave));
        assert.equal(evidencia.ttlInicial, -2);
        evidencia.antes = conteos();
        const respuestaA = await http.get(`${bases.app1}/`, { maxRedirects: 0 });
        assert.equal(respuestaA.status(), 200);
        const htmlA = await respuestaA.text();
        assert.equal(estadoEnHtml(htmlA), 'Cache: MISS');
        assert.ok(htmlA.includes(`Generado por: <strong>${evidencia.replicas[0].replica}</strong>`));
        evidencia.despuesMiss = conteos();
        for (const tabla of Object.keys(evidencia.antes))
            assert.equal(evidencia.despuesMiss[tabla] - evidencia.antes[tabla], 1, `MISS debe ejecutar un COUNT de ${tabla}.`);
        const jsonA = redis('GET', clave);
        evidencia.entrada = JSON.parse(jsonA);
        assert.equal(evidencia.entrada.Replica, evidencia.replicas[0].replica);
        evidencia.pttlDespuesMiss = Number(redis('PTTL', clave));
        assert.ok(evidencia.pttlDespuesMiss > 0 && evidencia.pttlDespuesMiss <= 60000);
        const usuariosAntes = consultasUsuarios();

        const respuestaB = await http.get(`${bases.app2}/`, { maxRedirects: 0 });
        assert.equal(respuestaB.status(), 200, 'La cookie emitida por A debe autenticar también en B.');
        const htmlB = await respuestaB.text();
        assert.equal(estadoEnHtml(htmlB), 'Cache: HIT');
        assert.ok(htmlB.includes(`Generado por: <strong>${evidencia.replicas[0].replica}</strong>`));
        assert.ok(htmlB.includes(`Atendido por: <strong>${evidencia.replicas[1].replica}</strong>`));
        evidencia.despuesHit = conteos();
        assert.deepEqual(evidencia.despuesHit, evidencia.despuesMiss);
        assert.equal(redis('GET', clave), jsonA, 'B debe reutilizar la misma entrada, sin regenerarla.');
        evidencia.pttlDespuesHit = Number(redis('PTTL', clave));
        assert.ok(evidencia.pttlDespuesHit > 0 && evidencia.pttlDespuesHit <= evidencia.pttlDespuesMiss);
        evidencia.consultasUsuariosDuranteHit = consultasUsuarios() - usuariosAntes;
        if (process.argv.includes('--validar-datos')) {
            evidencia.entradasInvalidas = [];
            for (const [caso, contenido] of [
                ['JSON mal formado', '{no es json'],
                ['Resumen nulo', JSON.stringify({ Dashboard: null, Replica: 'prueba-h2', GeneradoEn: new Date().toISOString() })],
                ['Conteo obligatorio ausente', JSON.stringify({
                    Dashboard: { MecanicosDisponibles: 0, VehiculosRegistrados: 0 },
                    Replica: 'prueba-h2', GeneradoEn: new Date().toISOString()
                })]
            ]) {
                const antes = conteos();
                assert.equal(redis('SET', clave, contenido, 'EX', '60'), 'OK');
                const respuesta = await http.get(`${bases.app1}/`, { maxRedirects: 0 });
                assert.equal(respuesta.status(), 200);
                const html = await respuesta.text();
                assert.equal(estadoEnHtml(html), 'Cache: DATOS INVÁLIDOS');
                const despues = conteos();
                for (const tabla of Object.keys(antes)) assert.equal(despues[tabla] - antes[tabla], 1);
                const recuperada = JSON.parse(redis('GET', clave));
                assert.ok(recuperada.Dashboard);
                assert.equal(recuperada.Replica, evidencia.replicas[0].replica);
                const hitPosterior = await http.get(`${bases.app2}/`, { maxRedirects: 0 });
                assert.equal(hitPosterior.status(), 200);
                assert.equal(estadoEnHtml(await hitPosterior.text()), 'Cache: HIT');
                assert.deepEqual(conteos(), despues);
                evidencia.entradasInvalidas.push({ caso, antes, despues, estado: 'DATOS INVÁLIDOS -> MySQL; siguiente B HIT sin COUNT' });
            }
        }
        evidencia.logsCache = compose('logs', '--no-color', '--no-log-prefix', 'app1', 'app2')
            .split(/\r?\n/).filter(linea => linea.includes('MISS -> MySQL -> Redis')
                || linea.includes('HIT -> Redis') || linea.includes('ENTRADA INVALIDA -> MySQL'));
        assert.ok(evidencia.logsCache.some(linea => linea.includes('MISS -> MySQL -> Redis')));
        assert.ok(evidencia.logsCache.some(linea => linea.includes('HIT -> Redis')));
        if (process.argv.includes('--esperar-expiracion')) {
            const restante = Number(redis('PTTL', clave));
            assert.ok(restante > 0 && restante <= 60000);
            await new Promise(resolve => setTimeout(resolve, restante + 250));
            evidencia.ttlTrasExpiracionNatural = Number(redis('TTL', clave));
            assert.equal(evidencia.ttlTrasExpiracionNatural, -2);
        }
        evidencia.resultado = 'PASS: Redis real, 3 replicas, Nginx, login real, MISS A y HIT B; +3 COUNT en MISS, +0 en HIT.';
        console.log(JSON.stringify(evidencia, null, 2));
    } finally {
        await http.dispose();
    }
})().catch(error => { console.error(error.message); process.exitCode = 1; });
