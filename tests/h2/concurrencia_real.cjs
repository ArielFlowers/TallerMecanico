const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const readline = require('node:readline');
const { spawn } = require('node:child_process');
const { request } = require(process.env.PLAYWRIGHT_MODULE);
const { docker, compose, redis, mysql, conteos, estadoEnHtml, clave } = require('./redis_real.cjs');

const claveGeneracion = 'autotaller:dashboard:generacion';
const variables = Object.fromEntries(fs.readFileSync(path.join(__dirname, 'local.env'), 'utf8')
    .split(/\r?\n/).filter(linea => /^[A-Z0-9_]+=/.test(linea))
    .map(linea => [linea.slice(0, linea.indexOf('=')), linea.slice(linea.indexOf('=') + 1)]));
const evidencia = { fechaUtc: new Date().toISOString(), clave, claveGeneracion, replicas: [], casos: [] };
const procesos = new Set();

function antiforgery(html) {
    const token = html.match(/name="__RequestVerificationToken"[^>]*value="([^"]+)"/);
    assert.ok(token, 'Se requiere el antiforgery real.');
    return token[1];
}
async function post(http, base, ruta, form = {}) {
    // Obtener el token desde Servicios evita consultar/poblar el dashboard.
    const pagina = await http.get(`${base}/Servicios`);
    assert.equal(pagina.status(), 200);
    const respuesta = await http.post(`${base}${ruta}`, { form: {
        ...form, __RequestVerificationToken: antiforgery(await pagina.text())
    }, maxRedirects: 0 });
    assert.equal(respuesta.status(), 302);
    return respuesta;
}
async function invalidar(http, base) {
    await post(http, base, '/?handler=InvalidarCache');
    assert.equal(redis('EXISTS', clave), '0');
    assert.equal(redis('TTL', claveGeneracion), '-1');
    return redis('GET', claveGeneracion);
}
function iniciar(servicio) {
    const hijo = spawn('docker', ['compose', '--env-file', 'tests/h2/local.env', '-f', 'tests/h2/docker-compose.yml',
        'exec', '-T', servicio, 'dotnet', '/tmp/h2-concurrencia/ConcurrenciaDashboard.dll'],
    { cwd: path.resolve(__dirname, '../..'), stdio: ['pipe', 'pipe', 'pipe'] });
    procesos.add(hijo);
    let listo, final, rechazarListo, rechazarFinal;
    const preparado = new Promise((resolve, reject) => { listo = resolve; rechazarListo = reject; });
    const terminado = new Promise((resolve, reject) => { final = resolve; rechazarFinal = reject; });
    // Evita rechazos no observados mientras el anfitrión ejecuta la operación B.
    terminado.catch(() => {});
    const tiempoMaximo = setTimeout(() => hijo.stdin.end(), 60000);
    readline.createInterface({ input: hijo.stdout }).on('line', linea => {
        if (!linea.startsWith('{')) return;
        const mensaje = JSON.parse(linea);
        if (mensaje.evento === 'listo') listo(mensaje);
        if (mensaje.evento === 'final') final(mensaje);
    });
    hijo.on('error', error => { rechazarListo(error); rechazarFinal(error); });
    hijo.on('exit', codigo => {
        clearTimeout(tiempoMaximo);
        procesos.delete(hijo);
        if (codigo !== 0) {
            const error = new Error(`El auxiliar H2 ${servicio} terminó con código ${codigo}; no se muestran secretos del entorno.`);
            rechazarListo(error); rechazarFinal(error);
        }
    });
    return { preparado, terminar: async () => { hijo.stdin.end('continuar\n'); return terminado; } };
}
function diferencia(antes, despues, esperado) {
    for (const tabla of Object.keys(antes)) assert.equal(despues[tabla] - antes[tabla], esperado, tabla);
}
async function comprobarHit(http, base, resumenEsperado) {
    const antes = conteos();
    const jsonAnterior = redis('GET', clave);
    const ttlAntes = Number(redis('PTTL', clave));
    const respuesta = await http.get(`${base}/`);
    assert.equal(respuesta.status(), 200);
    assert.equal(estadoEnHtml(await respuesta.text()), 'Cache: HIT');
    const despues = conteos();
    diferencia(antes, despues, 0);
    assert.equal(redis('GET', clave), jsonAnterior);
    const ttlDespues = Number(redis('PTTL', clave));
    assert.ok(ttlDespues > 0 && ttlDespues < ttlAntes && ttlAntes <= 60000);
    assert.deepEqual(JSON.parse(jsonAnterior).Dashboard, resumenEsperado);
    assert.equal(redis('TTL', claveGeneracion), '-1');
    return { antes, despues, ttlAntes, ttlDespues };
}
function comprobarRechazo(final, resumenEsperado) {
    assert.deepEqual(final.publicaciones, ['RechazadaPorGeneracion', 'Publicada']);
    assert.deepEqual(final.resultado.Dashboard, resumenEsperado);
    assert.equal(final.resultado.Almacenado, true);
    assert.ok(final.logs.some(mensaje => mensaje.includes('PUBLICACION RECHAZADA') && mensaje.includes('RechazadaPorGeneracion')));
    assert.deepEqual(JSON.parse(redis('GET', clave)).Dashboard, resumenEsperado);
}

(async () => {
    const http = await request.newContext();
    let servicioCreadoId = null;
    let baseB;
    try {
        const bases = {};
        for (const servicio of ['app1', 'app2', 'app3']) {
            const inspeccion = JSON.parse(docker(['inspect', compose('ps', '-q', servicio)]))[0];
            assert.equal(inspeccion.Config.Labels['com.docker.compose.project'], 'autotaller-h2-integracion');
            assert.ok(inspeccion.Config.Env.some(variable => variable.startsWith(
                'ConnectionStrings__MySqlConnection=Server=mysql;Port=3306;Database=autotaller_h2_integracion;User ID=h2_app;')));
            bases[servicio] = `http://${compose('port', servicio, '8080')}`;
            const diagnostico = await (await http.get(`${bases[servicio]}/h2/redis`)).json();
            assert.equal(diagnostico.redis, 'PONG');
            evidencia.replicas.push({ servicio, url: bases[servicio], ...diagnostico });
        }
        assert.equal(redis('PING'), 'PONG');
        evidencia.ping = 'PONG';
        baseB = bases.app2;
        const login = await http.get(`${baseB}/Auth/Login`);
        assert.equal((await http.post(`${baseB}/Auth/Login`, { form: {
            'Input.Username': variables.H2_ADMIN_USERNAME, 'Input.Password': variables.H2_ADMIN_PASSWORD,
            __RequestVerificationToken: antiforgery(await login.text())
        }, maxRedirects: 0 })).status(), 302);

        // Dos procesos .NET reales, cada uno dentro de una réplica, comparten generación.
        await invalidar(http, baseB);
        let antes = conteos();
        const procesoA = iniciar('app1');
        const listoA = await procesoA.preparado;
        const procesoB = iniciar('app2');
        const listoB = await procesoB.preparado;
        assert.notEqual(listoA.replica, listoB.replica);
        assert.equal(listoA.generacion, listoB.generacion);
        diferencia(antes, conteos(), 2);
        const finalA = await procesoA.terminar();
        assert.deepEqual(finalA.publicaciones, ['Publicada']);
        const ttlPrimeraPublicacion = Number(redis('PTTL', clave));
        const finalB = await procesoB.terminar();
        assert.deepEqual(finalB.publicaciones, ['EntradaExistente']);
        assert.equal(finalB.resultado.Estado, 0); // EstadoCache.Hit
        assert.ok(Number(redis('PTTL', clave)) < ttlPrimeraPublicacion);
        diferencia(antes, conteos(), 2);
        evidencia.casos.push({ caso: 'Dos MISS simultáneos: primer publicador gana, segundo lee HIT sin renovar TTL',
            listoA, listoB, finalA, finalB, hit: await comprobarHit(http, baseB, listoA.entrada.Dashboard) });

        // A contó antes del INSERT; el servidor HTTP B confirma MySQL e invalida.
        await invalidar(http, baseB);
        antes = conteos();
        const antiguo = iniciar('app1');
        const listoAntiguo = await antiguo.preparado;
        const nombre = `Prueba Concurrencia ${crypto.randomInt(100000, 999999)}`;
        await post(http, baseB, '/Servicios/Create', {
            'Formulario.Nombre': nombre, 'Formulario.Descripcion': 'Prueba aislada de generación Redis',
            'Formulario.Costo': '25', 'Formulario.TiempoEstimadoHoras': '1'
        });
        servicioCreadoId = Number(mysql(`SELECT Id FROM autotaller_h2_integracion.Servicios WHERE Nombre='${nombre}';`));
        assert.ok(servicioCreadoId > 0);
        const generacionNueva = redis('GET', claveGeneracion);
        assert.notEqual(generacionNueva, listoAntiguo.generacion);
        assert.equal(redis('EXISTS', clave), '0');
        const esperado = { ...listoAntiguo.entrada.Dashboard,
            ServiciosRegistrados: listoAntiguo.entrada.Dashboard.ServiciosRegistrados + 1 };
        const finalAntiguo = await antiguo.terminar();
        comprobarRechazo(finalAntiguo, esperado);
        diferencia(antes, conteos(), 2); // MISS antiguo y único reintento actualizado.
        evidencia.casos.push({ caso: 'INSERT real desde B invalida la publicación antigua de A', listo: listoAntiguo,
            generacionNueva, final: finalAntiguo, hit: await comprobarHit(http, baseB, esperado), servicioCreadoId });
        await post(http, baseB, `/Servicios/Delete?id=${servicioCreadoId}`, { id: String(servicioCreadoId) });
        assert.equal(mysql(`SELECT COUNT(*) FROM autotaller_h2_integracion.Servicios WHERE Id=${servicioCreadoId};`), '0');
        servicioCreadoId = null;

        await invalidar(http, baseB);
        const manual = iniciar('app1');
        const listoManual = await manual.preparado;
        const nuevaManual = await invalidar(http, baseB); // Inexistente también avanza generación.
        assert.notEqual(nuevaManual, listoManual.generacion);
        const finalManual = await manual.terminar();
        comprobarRechazo(finalManual, listoManual.entrada.Dashboard);
        evidencia.casos.push({ caso: 'Administrador B invalida mientras A está en MISS', listo: listoManual,
            nuevaManual, final: finalManual, hit: await comprobarHit(http, baseB, listoManual.entrada.Dashboard) });

        await invalidar(http, baseB);
        const perdido = iniciar('app1');
        const listoPerdido = await perdido.preparado;
        assert.equal(redis('DEL', claveGeneracion), '1'); // Solo metadato H2: simula expulsión/reinicio, no FLUSH.
        assert.equal(redis('SET', clave, JSON.stringify(listoPerdido.entrada), 'EX', '60'), 'OK');
        const finalPerdido = await perdido.terminar();
        comprobarRechazo(finalPerdido, listoPerdido.entrada.Dashboard);
        assert.notEqual(redis('GET', claveGeneracion), listoPerdido.generacion);
        evidencia.casos.push({ caso: 'Generación desaparecida: rechaza escritor antiguo y descarta entrada huérfana',
            listo: listoPerdido, nuevaGeneracion: redis('GET', claveGeneracion), final: finalPerdido,
            hit: await comprobarHit(http, baseB, listoPerdido.entrada.Dashboard) });

        await invalidar(http, baseB);
        const anteriorAlReinicio = iniciar('app1');
        const listoAnterior = await anteriorAlReinicio.preparado;
        assert.equal(redis('DEL', claveGeneracion), '1');
        const posteriorAlReinicio = iniciar('app2');
        const listoPosterior = await posteriorAlReinicio.preparado;
        assert.notEqual(listoAnterior.generacion, listoPosterior.generacion);
        const finalAnterior = await anteriorAlReinicio.terminar();
        comprobarRechazo(finalAnterior, listoAnterior.entrada.Dashboard);
        const finalPosterior = await posteriorAlReinicio.terminar();
        assert.deepEqual(finalPosterior.publicaciones, ['EntradaExistente']);
        evidencia.casos.push({ caso: 'Metadato recreado por B no reutiliza la generación antigua de A (sin ABA)',
            listoAnterior, listoPosterior, finalAnterior, finalPosterior,
            hit: await comprobarHit(http, baseB, listoAnterior.entrada.Dashboard) });

        evidencia.ttlGeneracion = Number(redis('TTL', claveGeneracion));
        evidencia.ttlResumenMs = Number(redis('PTTL', clave));
        evidencia.logsInvalidacion = compose('logs', '--no-color', '--since', evidencia.fechaUtc, 'app2')
            .split(/\r?\n/).filter(linea => linea.includes('INVALIDACION'));
        assert.ok(evidencia.logsInvalidacion.length > 0);
        console.log(JSON.stringify(evidencia, null, 2));
    } finally {
        for (const proceso of procesos) proceso.stdin.end();
        if (servicioCreadoId && baseB) await post(http, baseB,
            `/Servicios/Delete?id=${servicioCreadoId}`, { id: String(servicioCreadoId) });
        await http.dispose();
    }
})().catch(error => { console.error(error.message); process.exitCode = 1; });
