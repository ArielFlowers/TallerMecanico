const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE);

// Prueba el comportamiento del navegador sin iniciar la aplicación ni usar una base de datos.
const usuarios = Array.from({ length: 31 }, (_, indice) => ({
    id: indice + 1,
    nombre: indice === 14 ? 'ana.lejos' : `bruno${indice + 1}`,
    rol: indice % 2 === 0 ? 'Recepcionista' : 'Administrador',
    activo: indice !== 2
}));
const escapar = valor => String(valor).replaceAll('&', '&amp;').replaceAll('"', '&quot;').replaceAll('<', '&lt;');
function parcial(parametros = new URLSearchParams()) {
    const termino = (parametros.get('Busqueda') || '').toLowerCase();
    const rol = parametros.get('RolFiltro');
    const activo = parametros.get('ActivoFiltro');
    const filtrados = usuarios.filter(usuario => usuario.nombre.includes(termino)
        && (!rol || usuario.rol === rol) && (!activo || String(usuario.activo) === activo));
    const pagina = Math.min(Number(parametros.get('Pagina') || 1), Math.max(1, Math.ceil(filtrados.length / 10)));
    const filas = filtrados.slice((pagina - 1) * 10, pagina * 10).map(usuario => `
        <tr><td>${escapar(usuario.nombre)}</td><td>
            <form method="post" action="/Administracion/Usuarios?handler=${usuario.activo ? 'Desactivar' : 'Reactivar'}"
                  data-confirmar-usuario="${usuario.activo ? 'desactivar' : 'reactivar'}" data-usuario="${escapar(usuario.nombre)}">
                <input type="hidden" name="id" value="${usuario.id}" />
                <input type="hidden" name="__RequestVerificationToken" value="token-prueba" />
                <button type="submit">${usuario.activo ? 'Desactivar' : 'Activar'}</button>
            </form>
        </td></tr>`).join('');
    const enlaces = Array.from({ length: Math.ceil(filtrados.length / 10) }, (_, indice) => {
        const destino = new URLSearchParams(parametros);
        destino.set('Pagina', String(indice + 1));
        return `<li class="page-item ${pagina === indice + 1 ? 'active' : ''}">
            <a class="page-link" data-usuarios-pagina href="/Administracion/Usuarios?${escapar(destino)}">${indice + 1}</a></li>`;
    }).join('');
    return `<div data-usuarios-resultados data-pagina="${pagina}" data-total="${filtrados.length}">
        <p>${filtrados.length} usuario(s) encontrados.</p><table><tbody>${filas}</tbody></table><ul>${enlaces}</ul></div>`;
}

const html = `<!doctype html><html><body><div class="hu3-usuarios">
    <form id="hu3AutoFilterForm" method="get" action="/Administracion/Usuarios"
          data-resultados-url="/Administracion/Usuarios?handler=Resultados">
        <label>Buscar<input id="hu3Busqueda" name="Busqueda" /></label>
        <select name="RolFiltro"><option value="">Todos</option><option>Recepcionista</option><option>Administrador</option></select>
        <select name="ActivoFiltro"><option value="">Todos</option><option value="true">Activos</option><option value="false">Inactivos</option></select>
        <input type="hidden" name="TamanoPagina" value="10" />
        <a href="/Administracion/Usuarios" data-limpiar-usuarios>Limpiar filtros</a>
    </form>
    <p id="usuarios-busqueda-estado" role="status"></p>
    <div id="usuarios-resultados" aria-busy="false">${parcial()}</div>
    <dialog id="usuario-estado-dialog" aria-labelledby="usuario-estado-titulo" aria-describedby="usuario-estado-descripcion">
        <h2 id="usuario-estado-titulo"></h2><p data-usuario-confirmacion></p><p id="usuario-estado-descripcion"></p>
        <button type="button" data-cancelar-usuario autofocus>Cancelar</button>
        <button type="button" data-confirmar-usuario>Confirmar</button>
    </dialog>
</div></body></html>`;

(async () => {
    const browser = await chromium.launch({ headless: true, ...(process.env.PLAYWRIGHT_CHANNEL ? { channel: process.env.PLAYWRIGHT_CHANNEL } : {}) });
    try {
        const page = await browser.newPage();
        const errores = [];
        const consultas = [];
        page.on('pageerror', error => errores.push(error.message));
        page.on('dialog', dialog => { errores.push(`Diálogo nativo inesperado: ${dialog.message()}`); dialog.dismiss(); });
        await page.route('https://taller.test/**', async route => {
            const url = new URL(route.request().url());
            if (url.searchParams.get('handler') !== 'Resultados') {
                await route.fulfill({ contentType: 'text/html', body: html });
                return;
            }
            consultas.push(url.searchParams);
            // Una respuesta anterior llega después de que el usuario continúa escribiendo.
            if (url.searchParams.get('Busqueda') === 'a') await new Promise(resolve => setTimeout(resolve, 450));
            try { await route.fulfill({ contentType: 'text/html', body: parcial(url.searchParams) }); }
            catch { /* La petición anterior puede haber sido cancelada. */ }
        });
        await page.goto('https://taller.test/Administracion/Usuarios');
        await page.addScriptTag({ content: fs.readFileSync(path.join(__dirname, '../wwwroot/js/usuarios.js'), 'utf8') });
        await page.evaluate(() => {
            window.documentoOriginal = 'sin-recarga';
            window.envios = [];
            document.addEventListener('submit', evento => {
                if (evento.target.matches('form[data-confirmar-usuario]') && !evento.defaultPrevented) {
                    window.envios.push(Object.fromEntries(new FormData(evento.target)));
                    evento.preventDefault();
                }
            });
        });

        const busqueda = page.locator('#hu3Busqueda');
        const esperar = async total => page.waitForFunction(esperado =>
            document.querySelector('#usuarios-resultados [data-usuarios-resultados]').dataset.total === esperado
            && document.getElementById('usuarios-resultados').getAttribute('aria-busy') === 'false', String(total));
        await Promise.all([
            page.waitForRequest(peticion => new URL(peticion.url()).searchParams.get('Busqueda') === 'a'),
            busqueda.fill('a')
        ]);
        await esperar(1);
        assert.match(await page.locator('#usuarios-resultados').innerText(), /ana\.lejos/,
            'Un carácter debe buscar también usuarios fuera de la primera página original.');
        assert.equal(await busqueda.evaluate(campo => campo === document.activeElement), true);

        await Promise.all([
            page.waitForRequest(peticion => new URL(peticion.url()).searchParams.get('Busqueda') === 'a'),
            busqueda.fill('a')
        ]);
        await busqueda.fill('br');
        await esperar(30);
        await page.waitForTimeout(500);
        assert.doesNotMatch(await page.locator('#usuarios-resultados').innerText(), /ana\.lejos/);
        assert.equal(await busqueda.inputValue(), 'br');
        assert.equal(await busqueda.evaluate(campo => campo === document.activeElement), true);

        await page.locator('[name="RolFiltro"]').selectOption('Recepcionista');
        await esperar(15);
        await page.locator('[name="ActivoFiltro"]').selectOption('true');
        await esperar(14);
        await page.locator('a[data-usuarios-pagina]').filter({ hasText: /^2$/ }).click();
        await page.waitForFunction(() => document.querySelector('[data-usuarios-resultados]').dataset.pagina === '2'
            && document.getElementById('usuarios-resultados').getAttribute('aria-busy') === 'false');
        const ultima = consultas.at(-1);
        assert.equal(ultima.get('Busqueda'), 'br');
        assert.equal(ultima.get('RolFiltro'), 'Recepcionista');
        assert.equal(ultima.get('ActivoFiltro'), 'true');
        assert.equal(ultima.get('Pagina'), '2');

        const desactivar = page.locator('form[data-confirmar-usuario="desactivar"] button').first();
        await desactivar.click();
        await page.locator('dialog[open]').waitFor();
        assert.equal(await page.evaluate(() => window.envios.length), 0);
        assert.match(await page.locator('[data-usuario-confirmacion]').innerText(), /bruno/);
        await page.getByRole('button', { name: 'Cancelar', exact: true }).click();
        assert.equal(await page.evaluate(() => window.envios.length), 0);
        assert.equal(await desactivar.evaluate(boton => boton === document.activeElement), true);
        await desactivar.click();
        await page.keyboard.press('Escape');
        assert.equal(await page.evaluate(() => window.envios.length), 0);
        await desactivar.click();
        await page.locator('dialog [data-confirmar-usuario]').click();
        assert.deepEqual(await page.evaluate(() => window.envios.map(envio => envio.__RequestVerificationToken)), ['token-prueba']);

        await page.locator('[data-limpiar-usuarios]').click();
        await esperar(31);
        await page.locator('[name="ActivoFiltro"]').selectOption('false');
        await esperar(1);
        await page.getByRole('button', { name: 'Activar', exact: true }).click();
        await page.locator('dialog[open]').waitFor();
        assert.equal(await page.locator('#usuario-estado-titulo').innerText(), 'Reactivar usuario');
        await page.locator('dialog [data-confirmar-usuario]').click();
        assert.equal(await page.evaluate(() => window.envios.length), 2);
        assert.equal(await page.evaluate(() => window.documentoOriginal), 'sin-recarga');
        assert.deepEqual(errores, []);
        console.log('Usuarios: búsqueda global desde un carácter, foco, filtros, paginación y confirmación verificados.');
    } finally {
        await browser.close();
    }
})().catch(error => { console.error(error); process.exit(1); });
