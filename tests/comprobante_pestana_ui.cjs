const assert = require('node:assert/strict');
const http = require('node:http');
const fs = require('node:fs');
const path = require('node:path');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE);

// Navegación HTTP aislada: usa el JavaScript y la página de espera reales, sin MySQL.
const raiz = path.resolve(__dirname, '..');
let posts = 0, lecturasPdf = 0, creada = false, resultado = 'ok';
const visor = '<dialog id="comprobante-orden-dialog"><iframe data-comprobante-frame></iframe><button data-cerrar-comprobante>Cerrar</button></dialog>';
const documento = contenido => `<!doctype html><html><head><meta charset="utf-8"></head><body>${contenido}${visor}<script src="/js/orden-comprobante.js"></script></body></html>`;
const formulario = () => documento(`<form id="formulario-orden" method="post" action="/crear" data-comprobante-espera-url="/comprobante-espera.html"><input name="dato" required value="válido"></form>
    <button type="submit" form="formulario-orden">Revisar</button>
    <dialog id="confirmar-orden-dialog"><button data-confirmar-orden>Confirmar</button><button data-cancelar-confirmacion>Cancelar</button></dialog>`);
const servidor = http.createServer((peticion, respuesta) => {
    const url = new URL(peticion.url, 'http://localhost');
    if (url.pathname === '/js/orden-comprobante.js') {
        respuesta.setHeader('Content-Type', 'application/javascript');
        return respuesta.end(fs.readFileSync(path.join(raiz, 'wwwroot/js/orden-comprobante.js')));
    }
    if (url.pathname === '/comprobante-espera.html') {
        respuesta.setHeader('Content-Type', 'text/html');
        return respuesta.end(fs.readFileSync(path.join(raiz, 'wwwroot/comprobante-espera.html')));
    }
    if (url.pathname === '/Ordenes/Comprobante') {
        lecturasPdf++;
        return respuesta.end('Destino del comprobante de prueba');
    }
    respuesta.setHeader('Content-Type', 'text/html');
    if (peticion.method === 'POST' && url.pathname === '/crear') {
        posts++;
        if (resultado === 'validacion') return respuesta.end(formulario());
        if (resultado === 'error') { respuesta.statusCode = 500; return respuesta.end(documento('No se pudo guardar.')); }
        creada = true;
        respuesta.writeHead(302, { Location: '/Ordenes' });
        return respuesta.end();
    }
    if (url.pathname === '/Ordenes') {
        const enlace = creada ? '<a href="/Ordenes/Comprobante?id=42" target="_blank" rel="noopener" data-comprobante-creado>Abrir comprobante</a>' : '';
        creada = false;
        return respuesta.end(documento(`${enlace}<a href="/Ordenes/Comprobante?id=42" data-comprobante-url="/Ordenes/Comprobante?id=42">Comprobante manual</a>`));
    }
    respuesta.end(formulario());
});

(async () => {
    await new Promise(resolve => servidor.listen(0, '127.0.0.1', resolve));
    const base = `http://127.0.0.1:${servidor.address().port}`;
    const navegador = await chromium.launch({ channel: process.env.PLAYWRIGHT_CHANNEL || 'msedge', headless: true });
    let verificadas = 0;
    try {
        for (const escenario of ['ok', 'bloqueado', 'excepcion', 'validacion', 'error']) {
            resultado = escenario;
            const contexto = await navegador.newContext();
            if (escenario === 'bloqueado') await contexto.addInitScript(() => { window.open = () => null; });
            if (escenario === 'excepcion') await contexto.addInitScript(() => { window.open = () => { throw new Error('Bloqueada'); }; });
            const pagina = await contexto.newPage();
            const errores = [];
            pagina.on('pageerror', error => errores.push(error.message));
            await pagina.goto(base);
            const antes = posts, pdfAntes = lecturasPdf;
            await pagina.locator('input').fill('');
            await pagina.getByText('Revisar', { exact: true }).click();
            assert.equal(posts, antes);
            assert.equal(contexto.pages().length, 1);
            await pagina.locator('input').fill('válido');
            await pagina.getByText('Revisar', { exact: true }).click();
            await pagina.getByText('Cancelar', { exact: true }).click();
            assert.equal(posts, antes);
            assert.equal(contexto.pages().length, 1);
            await pagina.getByText('Revisar', { exact: true }).click();
            await pagina.evaluate(() => document.getElementById('formulario-orden').addEventListener('submit', () =>
                document.getElementById('formulario-orden').requestSubmit(), { once: true }));
            await Promise.all([pagina.waitForNavigation(), pagina.getByText('Confirmar', { exact: true }).click()]);
            assert.equal(posts, antes + 1);
            const fallo = ['validacion', 'error'].includes(escenario);
            if (fallo) {
                for (let intento = 0; intento < 100 && contexto.pages().length > 1; intento++)
                    await new Promise(resolve => setTimeout(resolve, 30));
                assert.equal(contexto.pages().length, 1);
                assert.equal(lecturasPdf, pdfAntes);
                assert.equal(await pagina.locator('[data-comprobante-creado]').count(), 0);
            } else {
                const respaldo = pagina.locator('[data-comprobante-creado]');
                assert.equal(await respaldo.isVisible(), true);
                assert.equal(await respaldo.getAttribute('target'), '_blank');
                if (escenario === 'ok') {
                    const pestaña = contexto.pages().find(otra => otra !== pagina);
                    assert(pestaña);
                    await pestaña.waitForURL('**/Ordenes/Comprobante?id=42');
                    assert.equal(contexto.pages().length, 2);
                    assert.equal(lecturasPdf, pdfAntes + 1);
                    await pestaña.close();
                } else {
                    assert.equal(contexto.pages().length, 1);
                    const nueva = contexto.waitForEvent('page');
                    await respaldo.click();
                    const pestaña = await nueva;
                    await pestaña.waitForURL('**/Ordenes/Comprobante?id=42');
                    await pestaña.close();
                }
                assert.equal(new URL(pagina.url()).pathname, '/Ordenes');
                await pagina.getByText('Comprobante manual', { exact: true }).click();
                assert.equal(await pagina.locator('#comprobante-orden-dialog').getAttribute('open'), '');
                await pagina.getByText('Cerrar', { exact: true }).click();
                const consultas = lecturasPdf;
                await pagina.reload();
                assert.equal(lecturasPdf, consultas);
                assert.equal(contexto.pages().length, 1);
                assert.equal(await pagina.locator('#comprobante-orden-dialog').getAttribute('open'), null);
            }
            assert.deepEqual(errores, []);
            await contexto.close();
            verificadas++;
            console.log(`OK ${escenario}: confirmación, cancelación, POST único y comportamiento del comprobante.`);
        }
        console.log(`${verificadas} escenarios aprobados sin base de datos.`);
    } finally { await navegador.close(); servidor.close(); }
})().catch(error => { console.error(error); servidor.close(); process.exitCode = 1; });
