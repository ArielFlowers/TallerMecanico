const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const { request } = require(process.env.PLAYWRIGHT_MODULE);
const { docker, compose, redis, mysql, conteos, estadoEnHtml, clave } = require('./redis_real.cjs');

const variables = Object.fromEntries(fs.readFileSync(path.join(__dirname, 'local.env'), 'utf8')
    .split(/\r?\n/).filter(linea => /^[A-Z0-9_]+=/.test(linea))
    .map(linea => [linea.slice(0, linea.indexOf('=')), linea.slice(linea.indexOf('=') + 1)]));
const evidencia = { fechaUtc: new Date().toISOString(), clave, replicas: [], operaciones: [], sinInvalidacion: [] };
const decodificar = texto => texto.replace(/&#(?:x([0-9a-f]+)|(\d+));/gi,
    (_, hexadecimal, decimal) => String.fromCodePoint(parseInt(hexadecimal ?? decimal, hexadecimal ? 16 : 10)));
function antiforgery(html) {
    const token = html.match(/name="__RequestVerificationToken"[^>]*value="([^"]+)"/);
    assert.ok(token, 'Debe existir la protección antiforgery real.');
    return token[1];
}
async function login(http, base, username, password) {
    assert.ok(password, 'Faltan credenciales aisladas.');
    const pagina = await http.get(`${base}/Auth/Login`);
    assert.equal(pagina.status(), 200);
    const respuesta = await http.post(`${base}/Auth/Login`, { form: {
        'Input.Username': username, 'Input.Password': password,
        __RequestVerificationToken: antiforgery(await pagina.text())
    }, maxRedirects: 0 });
    assert.equal(respuesta.status(), 302, 'Debe aprobar el login real sin seguir al dashboard.');
}
async function post(http, base, destino, formulario, paginaToken) {
    const pagina = await http.get(`${base}${paginaToken}`);
    assert.equal(pagina.status(), 200, `Formulario accesible: ${paginaToken}`);
    return http.post(`${base}${destino}`, { form: { ...formulario,
        __RequestVerificationToken: antiforgery(await pagina.text()) }, maxRedirects: 0 });
}
async function dashboard(http, base, esperado) {
    const antes = conteos();
    const respuesta = await http.get(`${base}/`, { maxRedirects: 0 });
    assert.equal(respuesta.status(), 200);
    const html = await respuesta.text();
    const estado = estadoEnHtml(html);
    if (esperado) assert.equal(estado, `Cache: ${esperado}`);
    assert.ok(['Cache: HIT', 'Cache: MISS'].includes(estado));
    const despues = conteos();
    for (const tabla of Object.keys(antes))
        assert.equal(despues[tabla] - antes[tabla], estado === 'Cache: MISS' ? 1 : 0);
    const entrada = JSON.parse(redis('GET', clave));
    const ttlMs = Number(redis('PTTL', clave));
    assert.ok(ttlMs > 0 && ttlMs <= 60000);
    const replica = (await (await http.get(`${base}/h1/replica`)).json()).replica;
    const mensaje = html.match(/class="cache-mensaje">([^<]+)<\/p>/);
    return { estado, replica, origen: entrada.Replica, ttlMs, resumen: entrada.Dashboard, antes, despues,
        mensajeCache: mensaje ? decodificar(mensaje[1]).trim() : null };
}
function valoresMySql() {
    return {
        MecanicosDisponibles: Number(mysql('SELECT COUNT(*) FROM autotaller_h2_integracion.Mecanicos;')),
        VehiculosRegistrados: Number(mysql('SELECT COUNT(*) FROM autotaller_h2_integracion.Vehiculos;')),
        ServiciosRegistrados: Number(mysql('SELECT COUNT(*) FROM autotaller_h2_integracion.Servicios;'))
    };
}

(async () => {
    const admin = await request.newContext();
    const recepcion = await request.newContext();
    try {
        const bases = {};
        for (const servicio of ['app1', 'app2', 'app3']) {
            const contenedor = JSON.parse(docker(['inspect', compose('ps', '-q', servicio)]))[0];
            assert.equal(contenedor.Config.Labels['com.docker.compose.project'], 'autotaller-h2-integracion');
            const conexion = contenedor.Config.Env.find(entrada => entrada.startsWith('ConnectionStrings__MySqlConnection='));
            assert.ok(conexion.includes('Server=mysql;Port=3306;Database=autotaller_h2_integracion;User ID=h2_app;'));
            bases[servicio] = `http://${compose('port', servicio, '8080')}`;
            const diagnostico = await (await admin.get(`${bases[servicio]}/h2/redis`)).json();
            assert.equal(diagnostico.redis, 'PONG');
            evidencia.replicas.push({ servicio, url: bases[servicio], ...diagnostico });
        }
        assert.equal(redis('PING'), 'PONG');
        await login(admin, bases.app1, variables.H2_ADMIN_USERNAME, variables.H2_ADMIN_PASSWORD);
        await login(recepcion, bases.app1, variables.H2_TEST_USERNAME, variables.H2_TEST_PASSWORD);
        const canario = `autotaller:h2:preservar:${crypto.randomUUID()}`;
        assert.equal(redis('SET', canario, 'preservar', 'EX', '900'), 'OK');
        const sesiones = redis('--scan', '--pattern', 'autotaller:sesion:*').split(/\r?\n/).filter(Boolean);
        assert.ok(sesiones.length > 0);
        function comprobarOtrasClaves() {
            assert.equal(redis('GET', canario), 'preservar');
            for (const sesion of sesiones) assert.equal(redis('EXISTS', sesion), '1');
        }
        await dashboard(admin, bases.app1);
        await dashboard(admin, bases.app2, 'HIT');
        const ci = String(crypto.randomInt(20000000, 89999999));
        mysql(`INSERT INTO autotaller_h2_integracion.Clientes
            (Ci,Nombres,PrimerApellido,SegundoApellido,Celular,CreadoPor)
            VALUES ('${ci}','Prueba','Redis','','71234567','h2_integracion');`);
        const clienteId = Number(mysql(`SELECT Id FROM autotaller_h2_integracion.Clientes WHERE Ci='${ci}' AND ComplementoCi='';`));
        assert.ok(clienteId > 0);
        evidencia.clienteFixtureId = clienteId;
        const placa = `${crypto.randomInt(1000, 9999)}HRE`;
        const placaEditada = `${crypto.randomInt(1000, 9999)}HRT`;
        const nombreServicio = `Prueba Redis ${crypto.randomInt(10000, 99999)}`;
        const mecanico = { 'MecanicoInput.Ci': ci, 'MecanicoInput.ComplementoCi': '',
            'MecanicoInput.Nombres': 'Prueba', 'MecanicoInput.PrimerApellido': 'Redis',
            'MecanicoInput.SegundoApellido': '', 'MecanicoInput.Genero': 'Masculino',
            'MecanicoInput.Especialidad': 'Motores', 'MecanicoInput.Celular': '71234567' };
        const vehiculo = { 'Formulario.Placa': placa, 'Formulario.EsPlacaExtranjera': 'false',
            'Formulario.Marca': 'Toyota', 'Formulario.Modelo': 'Corolla', 'Formulario.Kilometraje': '100',
            'Formulario.Observaciones': 'Prueba Redis', 'Formulario.ClienteId': String(clienteId) };
        const servicio = { 'Formulario.Nombre': nombreServicio, 'Formulario.Descripcion': 'Prueba aislada Redis',
            'Formulario.Costo': '25', 'Formulario.TiempoEstimadoHoras': '1' };

        async function cambio(nombre, campo, diferencia, guardar) {
            await dashboard(admin, bases.app1);
            const previo = await dashboard(admin, bases.app2, 'HIT');
            assert.ok(previo.ttlMs > 10000, 'Se requiere margen para distinguir DEL de expiración.');
            const inicio = Date.now();
            const respuesta = await guardar();
            assert.equal(respuesta.status(), 302, `${nombre}: escritura válida debe redirigir.`);
            assert.ok(Date.now() - inicio < previo.ttlMs, 'No confundir DEL con expiración natural.');
            assert.equal(redis('EXISTS', clave), '0', `${nombre}: debe eliminar solo el resumen.`);
            comprobarOtrasClaves();
            const miss = await dashboard(admin, bases.app1, 'MISS');
            assert.equal(miss.resumen[campo], previo.resumen[campo] + diferencia);
            assert.deepEqual(miss.resumen, valoresMySql());
            const hit = await dashboard(admin, bases.app2, 'HIT');
            assert.deepEqual(hit.resumen, miss.resumen);
            assert.equal(hit.origen, miss.replica);
            evidencia.operaciones.push({ nombre, previo, resultadoDel: 'clave ausente antes de redirección, dentro del TTL', miss, hit });
        }
        async function sinInvalidar(nombre, accion, esperado = 302) {
            await dashboard(admin, bases.app1);
            const contenido = redis('GET', clave);
            const ttlAntes = Number(redis('PTTL', clave));
            assert.ok(ttlAntes > 10000);
            const respuesta = await accion();
            assert.equal(respuesta.status(), esperado, nombre);
            assert.equal(redis('GET', clave), contenido, `${nombre}: no debe invalidar ni reescribir.`);
            const ttlDespues = Number(redis('PTTL', clave));
            assert.ok(ttlDespues > 0 && ttlDespues <= ttlAntes);
            const hit = await dashboard(admin, bases.app2, 'HIT');
            evidencia.sinInvalidacion.push({ nombre, ttlAntes, ttlDespues, hit });
        }
        await cambio('Crear mecánico', 'MecanicosDisponibles', 1,
            () => post(admin, bases.app1, '/Mecanicos?handler=Crear', mecanico, '/Mecanicos'));
        const mecanicoId = Number(mysql(`SELECT Id FROM autotaller_h2_integracion.Mecanicos WHERE Ci='${ci}' AND ComplementoCi='';`));
        assert.ok(mecanicoId > 0);
        await sinInvalidar('Mecánico con validación fallida', () => post(admin, bases.app1, '/Mecanicos?handler=Crear',
            { ...mecanico, 'MecanicoInput.Ci': '' }, '/Mecanicos'), 200);
        await sinInvalidar('Mecánico con CI duplicado', () => post(admin, bases.app1,
            '/Mecanicos?handler=Crear', mecanico, '/Mecanicos'), 200);
        await sinInvalidar('Editar nombre del mecánico', () => post(admin, bases.app1, '/Mecanicos?handler=Actualizar',
            { ...mecanico, MecanicoId: String(mecanicoId), 'MecanicoInput.Nombres': 'Prueba Actualizada' }, '/Mecanicos'));
        assert.equal(mysql(`SELECT Nombres FROM autotaller_h2_integracion.Mecanicos WHERE Id=${mecanicoId};`), 'Prueba Actualizada');
        await cambio('Eliminar mecánico', 'MecanicosDisponibles', -1,
            () => post(admin, bases.app1, '/Mecanicos?handler=Eliminar', { MecanicoId: String(mecanicoId) }, '/Mecanicos'));
        await sinInvalidar('Eliminar mecánico inexistente', () => post(admin, bases.app1,
            '/Mecanicos?handler=Eliminar', { MecanicoId: String(mecanicoId) }, '/Mecanicos'));

        await cambio('Crear vehículo', 'VehiculosRegistrados', 1,
            () => post(admin, bases.app1, '/Vehiculos?handler=Create', vehiculo, '/Vehiculos'));
        const vehiculoId = Number(mysql(`SELECT Id FROM autotaller_h2_integracion.Vehiculos WHERE Placa='${placa}';`));
        assert.ok(vehiculoId > 0);
        await sinInvalidar('Vehículo con validación fallida', () => post(admin, bases.app1, '/Vehiculos?handler=Create',
            { ...vehiculo, 'Formulario.Placa': '' }, '/Vehiculos'), 200);
        await sinInvalidar('Editar placa del vehículo', () => post(admin, bases.app1, '/Vehiculos?handler=Update',
            { ...vehiculo, 'Formulario.Id': String(vehiculoId), 'Formulario.Placa': placaEditada }, '/Vehiculos'));
        assert.equal(mysql(`SELECT Placa FROM autotaller_h2_integracion.Vehiculos WHERE Id=${vehiculoId};`), placaEditada);
        await cambio('Eliminar vehículo', 'VehiculosRegistrados', -1,
            () => post(admin, bases.app1, '/Vehiculos?handler=Delete', { id: String(vehiculoId) }, '/Vehiculos'));
        await sinInvalidar('Eliminar vehículo inexistente', () => post(admin, bases.app1,
            '/Vehiculos?handler=Delete', { id: String(vehiculoId) }, '/Vehiculos'));

        await cambio('Crear servicio', 'ServiciosRegistrados', 1,
            () => post(admin, bases.app1, '/Servicios/Create', servicio, '/Servicios/Create'));
        const servicioId = Number(mysql(`SELECT Id FROM autotaller_h2_integracion.Servicios WHERE Nombre='${nombreServicio}';`));
        assert.ok(servicioId > 0);
        await sinInvalidar('Servicio con validación fallida', () => post(admin, bases.app1, '/Servicios/Create',
            { ...servicio, 'Formulario.Nombre': '' }, '/Servicios/Create'), 200);
        await sinInvalidar('Editar nombre y precio del servicio', () => post(admin, bases.app1, `/Servicios/Edit?id=${servicioId}`,
            { ...servicio, 'Formulario.Nombre': 'Servicio Redis Editado', 'Formulario.Costo': '30' }, `/Servicios/Edit?id=${servicioId}`));
        assert.equal(Number(mysql(`SELECT Costo FROM autotaller_h2_integracion.Servicios WHERE Id=${servicioId};`)), 30);
        await cambio('Eliminar servicio', 'ServiciosRegistrados', -1,
            () => post(admin, bases.app1, `/Servicios/Delete?id=${servicioId}`, {}, '/Servicios'));
        await sinInvalidar('Eliminar servicio inexistente', () => post(admin, bases.app1,
            `/Servicios/Delete?id=${servicioId}`, {}, '/Servicios'));

        const paginaAdmin = await admin.get(`${bases.app1}/`);
        const htmlAdmin = await paginaAdmin.text();
        const formularioManual = [...htmlAdmin.matchAll(/<form\b[^>]*>[\s\S]*?<\/form>/g)]
            .map(coincidencia => coincidencia[0]).find(formulario => formulario.includes('handler=InvalidarCache'));
        assert.ok(formularioManual, 'Debe existir el formulario manual solo para Administrador.');
        const sinToken = await admin.post(`${bases.app1}/?handler=InvalidarCache`, { form: {}, maxRedirects: 0 });
        assert.equal(sinToken.status(), 400);
        assert.equal(redis('EXISTS', clave), '1');
        const paginaRecepcion = await recepcion.get(`${bases.app1}/`);
        const htmlRecepcion = await paginaRecepcion.text();
        assert.ok(!htmlRecepcion.includes('handler=InvalidarCache'));
        const prohibida = await recepcion.post(`${bases.app1}/?handler=InvalidarCache`, {
            form: { __RequestVerificationToken: antiforgery(htmlRecepcion) }, maxRedirects: 0 });
        assert.equal(prohibida.status(), 302);
        assert.ok(prohibida.headers().location.includes('/Auth/AccessDenied'));
        assert.equal(redis('EXISTS', clave), '1');
        const manual = await admin.post(`${bases.app1}/?handler=InvalidarCache`, {
            form: { __RequestVerificationToken: antiforgery(formularioManual) }, maxRedirects: 0 });
        assert.equal(manual.status(), 302);
        assert.equal(redis('EXISTS', clave), '0');
        comprobarOtrasClaves();
        const missManual = await dashboard(admin, bases.app1, 'MISS');
        assert.ok(missManual.mensajeCache?.includes('Redis confirmó la invalidación'));
        const hitManual = await dashboard(admin, bases.app2, 'HIT');
        const quitarOtraVez = await admin.post(`${bases.app1}/?handler=InvalidarCache`, {
            form: { __RequestVerificationToken: antiforgery(formularioManual) }, maxRedirects: 0 });
        assert.equal(quitarOtraVez.status(), 302);
        assert.equal(redis('EXISTS', clave), '0');
        const yaInexistente = await admin.post(`${bases.app1}/?handler=InvalidarCache`, {
            form: { __RequestVerificationToken: antiforgery(formularioManual) }, maxRedirects: 0 });
        assert.equal(yaInexistente.status(), 302);
        assert.equal(redis('EXISTS', clave), '0');
        const missInexistente = await dashboard(admin, bases.app1, 'MISS');
        assert.ok(missInexistente.mensajeCache?.includes('ya no estaba'));
        await dashboard(admin, bases.app2, 'HIT');
        comprobarOtrasClaves();
        evidencia.manual = { sinAntiforgery: sinToken.status(), recepcionista: prohibida.status(),
            redireccionDenegada: true, administrador: manual.status(), claveAusenteAntesDeSeguirRedireccion: true,
            missManual, hitManual, missInexistente, otrasClavesPreservadas: true, sesionesPreservadas: sesiones.length };
        evidencia.logsInvalidacion = compose('logs', '--since', evidencia.fechaUtc, '--no-color', '--no-log-prefix', 'app1')
            .split(/\r?\n/).filter(linea => linea.includes('INVALIDACION'));
        assert.ok(evidencia.logsInvalidacion.filter(linea => linea.includes('resultado Eliminada')).length >= 7);
        evidencia.resultado = 'PASS: seis escrituras HTTP invalidan; siguientes MISS/HIT y conteos MySQL correctos; ediciones, errores y no-op conservan caché; permisos y antiforgery manual intactos.';
        console.log(JSON.stringify(evidencia, null, 2));
    } finally {
        await admin.dispose(); await recepcion.dispose();
    }
})().catch(error => { console.error(error.message); process.exitCode = 1; });
