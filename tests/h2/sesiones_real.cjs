const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { request } = require(process.env.PLAYWRIGHT_MODULE);
const { docker, compose, redis, mysql, conteos, estadoEnHtml, clave } = require('./redis_real.cjs');

const auditoria = process.argv.includes('--auditoria');
const composeArgs = ['compose', '--env-file', 'tests/h2/local.env', '-f', 'tests/h2/docker-compose.yml'];
const variables = Object.fromEntries(fs.readFileSync(path.join(__dirname, 'local.env'), 'utf8')
    .split(/\r?\n/).filter(linea => /^[A-Z0-9_]+=/.test(linea))
    .map(linea => [linea.slice(0, linea.indexOf('=')), linea.slice(linea.indexOf('=') + 1)]));
const evidencia = { fechaUtc: new Date().toISOString(), modo: auditoria ? 'auditoria previa' : 'verificacion corregida' };
const decodificar = texto => texto.replace(/&#(?:x([0-9a-f]+)|(\d+));/gi,
    (_, hex, decimal) => String.fromCodePoint(parseInt(hex ?? decimal, hex ? 16 : 10)));
function campo(html, nombre) {
    const etiquetas = html.match(/<input\b[^>]*>/g) ?? [];
    const etiqueta = etiquetas.find(input => input.includes(`name="${nombre}"`));
    assert.ok(etiqueta, `Debe existir el campo ${nombre}.`);
    return decodificar(etiqueta.match(/value="([^"]*)"/)?.[1] ?? '');
}
const antiforgery = html => campo(html, '__RequestVerificationToken');
const clavesSesion = () => redis('--scan', '--pattern', 'autotaller:sesion:*').split(/\r?\n/).filter(Boolean);
function comprobarAislamiento() {
    const id = compose('ps', '-a', '-q', 'redis');
    const contenedor = JSON.parse(docker(['inspect', id]))[0];
    assert.equal(contenedor.Config.Labels['com.docker.compose.project'], 'autotaller-h2-integracion');
    assert.equal(contenedor.Config.Labels['com.docker.compose.service'], 'redis');
    assert.deepEqual(Object.keys(contenedor.NetworkSettings.Networks), ['autotaller-h2-integracion_default']);
    assert.ok(!contenedor.HostConfig.PortBindings || Object.keys(contenedor.HostConfig.PortBindings).length === 0);
    const red = JSON.parse(docker(['network', 'inspect', 'autotaller-h2-integracion_default']))[0];
    assert.equal(red.Internal, true);
    for (const miembro of Object.values(red.Containers))
        assert.match(miembro.Name, /^autotaller-h2-integracion-(app[123]|mysql|redis)-1$/);
    return { redisId: id.slice(0, 12), redInterna: true, soloServiciosH2: true, sinPuertoPublicado: true };
}
function instantanea() {
    return mysql(`SELECT (SELECT COUNT(*) FROM autotaller_h2_integracion.Ordenes),
        (SELECT COUNT(*) FROM autotaller_h2_integracion.Mecanicos),
        (SELECT COUNT(*) FROM autotaller_h2_integracion.Vehiculos),
        (SELECT COUNT(*) FROM autotaller_h2_integracion.Servicios),
        (SELECT COUNT(*) FROM autotaller_h2_integracion.Clientes),
        (SELECT COALESCE(SUM(Stock),0) FROM autotaller_h2_integracion.Productos);`);
}
async function post(http, base, ruta, html, form = {}) {
    return http.post(`${base}${ruta}`, { form: { ...form, __RequestVerificationToken: antiforgery(html) }, maxRedirects: 0 });
}
function auxiliar(modo, datos) {
    return JSON.parse(docker([...composeArgs, 'exec', '-T', 'app3', 'dotnet',
        '/tmp/h2-sesiones/SesionRedisProbe.dll'], JSON.stringify({ ...datos, modo }) + '\n'));
}
async function esperarRedis(http, bases) {
    const limite = Date.now() + 60000;
    while (Date.now() < limite) {
        const estados = await Promise.all(Object.values(bases).map(async base => {
            try { return (await (await http.get(`${base}/h2/redis`, { timeout: 5000 })).json()).redis; }
            catch { return 'NO DISPONIBLE'; }
        }));
        if (estados.every(estado => estado === 'PONG')) return estados;
        await new Promise(resolve => setTimeout(resolve, 150));
    }
    throw new Error('No se recuperaron las conexiones Redis de las tres réplicas.');
}

(async () => {
    const http = await request.newContext({ timeout: 15000 });
    let redisDetenido = false;
    const bases = {};
    try {
        evidencia.aislamiento = comprobarAislamiento();
        evidencia.politica = redis('CONFIG', 'GET', 'maxmemory', 'maxmemory-policy', 'save', 'appendonly');
        const inicial = instantanea();
        evidencia.replicas = [];
        for (const servicio of ['app1', 'app2', 'app3']) {
            const contenedor = JSON.parse(docker(['inspect', compose('ps', '-q', servicio)]))[0];
            assert.ok(contenedor.Config.Env.some(variable => variable.startsWith(
                'ConnectionStrings__MySqlConnection=Server=mysql;Port=3306;Database=autotaller_h2_integracion;User ID=h2_app;')));
            const volumen = contenedor.Mounts.find(montaje => montaje.Destination === '/app/shared-keys');
            assert.equal(volumen.Name, 'autotaller-h2-integracion_h2-dpkeys');
            bases[servicio] = `http://${compose('port', servicio, '8080')}`;
            const diagnostico = await (await http.get(`${bases[servicio]}/h2/redis`)).json();
            assert.equal(diagnostico.redis, 'PONG');
            evidencia.replicas.push({ servicio, replica: diagnostico.replica, clavesDataProtection: volumen.Name });
        }
        const previas = new Set(clavesSesion());
        const paginaLogin = await http.get(`${bases.app1}/Auth/Login`);
        const login = await post(http, bases.app1, '/Auth/Login', await paginaLogin.text(), {
            'Input.Username': variables.H2_ADMIN_USERNAME, 'Input.Password': variables.H2_ADMIN_PASSWORD
        });
        assert.equal(login.status(), 302);
        for (const base of Object.values(bases)) assert.equal((await http.get(`${base}/Servicios`, { maxRedirects: 0 })).status(), 200);
        evidencia.autenticacion = 'Cookie emitida por A aceptada por A/B/C; Data Protection compartida.';

        async function prepararBorradores() {
            const crear = await http.get(`${bases.app1}/Ordenes/Crear`);
            assert.equal(crear.status(), 200);
            const htmlCrear = await crear.text();
            const token = campo(htmlCrear, 'Formulario.Token');
            assert.ok(token);
            const salidaOrden = await post(http, bases.app2, '/Ordenes/Crear?handler=NuevoVehiculo', htmlCrear, {
                'Formulario.Token': token, 'Formulario.VehiculoId': '0', 'Formulario.MecanicoId': '0'
            });
            assert.equal(salidaOrden.status(), 302);
            const ordenId = new URL(salidaOrden.headers().location, bases.app2).searchParams.get('OrdenBorradorId');
            assert.ok(ordenId);
            const paginaVehiculo = await http.get(new URL(salidaOrden.headers().location, bases.app3).href);
            assert.equal(paginaVehiculo.status(), 200);
            const salidaVehiculo = await post(http, bases.app3, '/Vehiculos?handler=RegistrarCliente&modal=crear', await paginaVehiculo.text(), {
                'OrdenBorradorId': ordenId, 'Formulario.Placa': '8642HSS', 'Formulario.Marca': 'Toyota',
                'Formulario.Modelo': 'Corolla', 'Formulario.Kilometraje': '123', 'Formulario.Observaciones': 'Prueba aislada de sesión'
            });
            assert.equal(salidaVehiculo.status(), 302);
            const vehiculoId = new URL(salidaVehiculo.headers().location, bases.app3).searchParams.get('BorradorVehiculoId');
            assert.ok(vehiculoId);
            const cliente = await http.get(new URL(salidaVehiculo.headers().location, bases.app2).href);
            assert.equal(cliente.status(), 200);
            const retomar = await http.get(`${bases.app1}/Vehiculos?handler=Retomar&borradorId=${vehiculoId}`);
            assert.equal(retomar.status(), 200);
            assert.equal(campo(await retomar.text(), 'Formulario.Placa'), '8642HSS');
            const orden = await http.get(`${bases.app2}/Ordenes/Crear?BorradorId=${ordenId}`);
            assert.equal(orden.status(), 200);
            const htmlOrden = await orden.text();
            assert.equal(campo(htmlOrden, 'Formulario.Token'), token);
            return { token, ordenId, vehiculoId, htmlOrden };
        }
        let borradores = await prepararBorradores();
        const nuevas = clavesSesion().filter(clave => !previas.has(clave));
        assert.equal(nuevas.length, 1, 'El flujo debe generar una única sesión compartida.');
        const claveSesion = nuevas[0];
        const datosAuxiliar = () => ({ claveSesion: claveSesion.slice('autotaller:sesion:'.length),
            token: borradores.token, ordenId: borradores.ordenId, vehiculoId: borradores.vehiculoId });
        evidencia.estadoCompartido = auxiliar('consultar', datosAuxiliar());
        assert.equal(evidencia.estadoCompartido.tokenValido, true);
        assert.equal(evidencia.estadoCompartido.ordenDisponible, true);
        assert.equal(evidencia.estadoCompartido.vehiculoDisponible, true);
        const nginx = `http://${compose('port', 'nginx', '80')}`;
        for (let intento = 0; intento < 3; intento++) {
            const balanceada = await http.get(`${nginx}/Ordenes/Crear?BorradorId=${borradores.ordenId}`);
            assert.equal(balanceada.status(), 200);
            assert.equal(campo(await balanceada.text(), 'Formulario.Token'), borradores.token);
        }
        evidencia.borradorPorNginx = 'Tres solicitudes balanceadas conservan el token y el borrador.';
        evidencia.sesion = { ttlInicial: Number(redis('PTTL', claveSesion)),
            expiracionDeslizanteTicks: redis('HGET', claveSesion, 'sldexp') };
        assert.ok(evidencia.sesion.ttlInicial > 1700000 && evidencia.sesion.ttlInicial <= 1800000);
        assert.equal(evidencia.sesion.expiracionDeslizanteTicks, '18000000000');
        const csrfInvalido = await http.post(`${bases.app2}/Ordenes/Crear?handler=NuevoVehiculo`, {
            form: { 'Formulario.Token': borradores.token }, maxRedirects: 0 });
        assert.equal(csrfInvalido.status(), 400);
        evidencia.antiforgery = 'Emitido por A y validado por B/C; ausencia rechazada con 400.';

        // Acortar únicamente esta clave de prueba permite observar la renovación real.
        assert.equal(redis('PEXPIRE', claveSesion, '10000'), '1');
        assert.equal((await http.get(`${bases.app2}/Ordenes/Crear?BorradorId=${borradores.ordenId}`)).status(), 200);
        evidencia.sesion.ttlTrasActividad = Number(redis('PTTL', claveSesion));
        assert.ok(evidencia.sesion.ttlTrasActividad > 1700000);
        evidencia.vencimientoSobresSimulado = auxiliar('vencerSobres', datosAuxiliar());
        assert.equal(evidencia.vencimientoSobresSimulado.tokenValido, false);
        assert.equal(evidencia.vencimientoSobresSimulado.ordenDisponible, false);
        assert.equal(evidencia.vencimientoSobresSimulado.vehiculoDisponible, false);
        borradores = await prepararBorradores();
        assert.equal(redis('PEXPIRE', claveSesion, '1'), '1');
        assert.equal(redis('EXISTS', claveSesion), '0');
        const expirada = await http.get(`${bases.app2}/Ordenes/Crear?BorradorId=${borradores.ordenId}`, { maxRedirects: 0 });
        assert.equal(expirada.status(), 302);
        assert.equal((await http.get(`${bases.app3}/Servicios`, { maxRedirects: 0 })).status(), 200);
        evidencia.sesion.expiracionForzadaSoloClaveDePrueba = 'Sesión vencida: borrador no recuperable, autenticación por cookie aún válida.';

        borradores = await prepararBorradores();
        await http.get(`${bases.app1}/`);
        const generacionAnterior = redis('GET', 'autotaller:dashboard:generacion');
        comprobarAislamiento();
        redisDetenido = true;
        compose('stop', 'redis');
        const antesCaida = conteos();
        const dashboard = await http.get(`${bases.app1}/`, { maxRedirects: 0 });
        assert.equal(dashboard.status(), 200);
        assert.equal(estadoEnHtml(await dashboard.text()), 'Cache: NO DISPONIBLE');
        const despuesCaida = conteos();
        for (const tabla of Object.keys(antesCaida)) assert.equal(despuesCaida[tabla] - antesCaida[tabla], 1);
        const lectura = await http.get(`${bases.app2}/Ordenes/Crear?BorradorId=${borradores.ordenId}`, { maxRedirects: 0 });
        const escritura = await post(http, bases.app3, '/Ordenes/Crear?handler=NuevoVehiculo', borradores.htmlOrden, {
            'Formulario.Token': borradores.token, 'Formulario.VehiculoId': '0', 'Formulario.MecanicoId': '0'
        });
        const htmlLectura = await lectura.text(), htmlEscritura = await escritura.text();
        evidencia.caida = { dashboard: dashboard.status(), estado: 'NO DISPONIBLE', antesCaida, despuesCaida,
            lecturaBorrador: lectura.status(), escrituraBorrador: escritura.status(),
            errorTecnicoExpuesto: /StackExchange\.Redis|at Microsoft\./.test(htmlLectura + htmlEscritura),
            cookieAutenticacionSigueValida: (await http.get(`${bases.app2}/Servicios`, { maxRedirects: 0 })).status() === 200 };
        if (!auditoria) {
            assert.equal(lectura.status(), 503);
            assert.equal(escritura.status(), 503);
            assert.equal(evidencia.caida.errorTecnicoExpuesto, false);
            assert.ok(htmlLectura.includes('No se pudo confirmar el estado de la sesión'));
        }
        const postSinSesion = await post(http, bases.app2, '/Mecanicos?handler=Crear', borradores.htmlOrden, {});
        assert.equal(postSinSesion.status(), 200); // Formulario inválido: no escribe MySQL y no requiere sesión.
        evidencia.caida.antiforgerySinSesionDistribuida = 'POST válido por antiforgery conserva validación de formulario (200); no se guardó ningún registro.';

        compose('start', 'redis');
        redisDetenido = false;
        evidencia.recuperacion = { replicas: await esperarRedis(http, bases) };
        assert.equal(redis('EXISTS', claveSesion), '0');
        const antesMiss = conteos();
        const miss = await http.get(`${bases.app1}/`);
        assert.equal(estadoEnHtml(await miss.text()), 'Cache: MISS');
        const despuesMiss = conteos();
        for (const tabla of Object.keys(antesMiss)) assert.equal(despuesMiss[tabla] - antesMiss[tabla], 1);
        const hit = await http.get(`${bases.app2}/`);
        assert.equal(estadoEnHtml(await hit.text()), 'Cache: HIT');
        assert.deepEqual(conteos(), despuesMiss);
        const generacionNueva = redis('GET', 'autotaller:dashboard:generacion');
        assert.notEqual(generacionNueva, generacionAnterior);
        assert.equal(redis('TTL', 'autotaller:dashboard:generacion'), '-1');
        evidencia.recuperacion.conteos = { antesMiss, despuesMiss, despuesHit: conteos() };
        evidencia.recuperacion.generacionNueva = generacionNueva !== generacionAnterior;
        evidencia.recuperacion.sesionAnteriorPerdidaPorReinicioSinPersistencia = true;
        evidencia.recuperacion.estadoAnterior = auxiliar('consultar', datosAuxiliar());
        assert.equal(evidencia.recuperacion.estadoAnterior.tokenValido, false);
        assert.equal(evidencia.recuperacion.estadoAnterior.ordenDisponible, false);
        assert.equal(evidencia.recuperacion.estadoAnterior.vehiculoDisponible, false);
        const antiguo = await http.get(`${bases.app2}/Ordenes/Crear?BorradorId=${borradores.ordenId}`, { maxRedirects: 0 });
        assert.equal(antiguo.status(), 302);
        borradores = await prepararBorradores();
        evidencia.recuperacion.nuevosBorradores = auxiliar('consultar', datosAuxiliar());
        assert.equal(evidencia.recuperacion.nuevosBorradores.tokenValido, true);
        assert.equal(evidencia.recuperacion.nuevosBorradores.ordenDisponible, true);
        assert.equal(evidencia.recuperacion.nuevosBorradores.vehiculoDisponible, true);
        evidencia.logsSesion = compose('logs', '--no-color', '--since', evidencia.fechaUtc, 'app1', 'app2', 'app3')
            .split(/\r?\n/).filter(linea => linea.includes('SESION NO DISPONIBLE'));
        if (!auditoria) assert.ok(evidencia.logsSesion.length >= 2);
        assert.equal(instantanea(), inicial);
        evidencia.mysqlOrdenesStockYEntidadesSinCambios = true;
        evidencia.resultado = 'PASS';
        console.log(JSON.stringify(evidencia, null, 2));
    } finally {
        if (redisDetenido) { compose('start', 'redis'); await esperarRedis(http, bases); }
        await http.dispose();
    }
})().catch(error => { console.error(error.message); process.exitCode = 1; });
