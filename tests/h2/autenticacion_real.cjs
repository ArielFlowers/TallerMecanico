const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { request } = require(process.env.PLAYWRIGHT_MODULE);
const { docker, compose } = require('./redis_real.cjs');

const variables = Object.fromEntries(fs.readFileSync(path.join(__dirname, 'local.env'), 'utf8')
    .split(/\r?\n/).filter(linea => /^[A-Z0-9_]+=/.test(linea))
    .map(linea => [linea.slice(0, linea.indexOf('=')), linea.slice(linea.indexOf('=') + 1)]));

function antiforgery(html) {
    const campo = html.match(/name="__RequestVerificationToken"[^>]*value="([^"]+)"/);
    assert.ok(campo, 'Se requiere el token antiforgery real.');
    return campo[1];
}

(async () => {
    const bases = {};
    for (const servicio of ['app1', 'app2', 'app3']) {
        const contenedor = JSON.parse(docker(['inspect', compose('ps', '-q', servicio)]))[0];
        assert.equal(contenedor.Config.Labels['com.docker.compose.project'], 'autotaller-h2-integracion');
        assert.ok(contenedor.Config.Env.some(entrada => entrada.startsWith(
            'ConnectionStrings__MySqlConnection=Server=mysql;Port=3306;Database=autotaller_h2_integracion;User ID=h2_app;')));
        bases[servicio] = `http://${compose('port', servicio, '8080')}`;
    }
    assert.ok(variables.H2_ADMIN_USERNAME && variables.H2_ADMIN_PASSWORD);
    const http = await request.newContext();
    try {
        const login = await http.get(`${bases.app1}/Auth/Login`);
        assert.equal(login.status(), 200);
        const autenticado = await http.post(`${bases.app1}/Auth/Login`, { form: {
            'Input.Username': variables.H2_ADMIN_USERNAME,
            'Input.Password': variables.H2_ADMIN_PASSWORD,
            __RequestVerificationToken: antiforgery(await login.text())
        }, maxRedirects: 0 });
        assert.equal(autenticado.status(), 302);
        const pagina = await http.get(`${bases.app2}/Ordenes/Crear`, { maxRedirects: 0 });
        assert.equal(pagina.status(), 200);
        const sinToken = await http.post(`${bases.app2}/Auth/Logout`, { maxRedirects: 0 });
        assert.equal(sinToken.status(), 400);
        assert.equal((await http.get(`${bases.app3}/Ordenes`, { maxRedirects: 0 })).status(), 200);
        const salida = await http.post(`${bases.app2}/Auth/Logout`, { form: {
            __RequestVerificationToken: antiforgery(await pagina.text())
        }, maxRedirects: 0 });
        assert.equal(salida.status(), 302);
        assert.match(salida.headers().location, /\/Auth\/Login/);
        const posterior = await http.get(`${bases.app3}/Ordenes`, { maxRedirects: 0 });
        assert.equal(posterior.status(), 302);
        assert.match(posterior.headers().location, /\/Auth\/Login/);
        console.log(JSON.stringify({ resultado: 'PASS', loginA: 302, nuevaOrdenB: 200,
            logoutSinAntiforgeryB: 400, autenticadoCTrasRechazo: 200,
            logoutB: 302, accesoProtegidoCTrasLogout: 302 }, null, 2));
    } finally {
        await http.dispose();
    }
})().catch(error => { console.error(error.message); process.exitCode = 1; });
