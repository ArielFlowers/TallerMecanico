const assert = require('node:assert/strict');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE);

(async () => {
    const browser = await chromium.launch({ channel: 'msedge', headless: true });
    try {
        const context = await browser.newContext({ ignoreHTTPSErrors: true });
        await context.addCookies(JSON.parse(process.env.TALLER_SERVICIOS_COOKIES));
        const page = await context.newPage();
        const errores = [];
        page.on('pageerror', error => errores.push(error.message));
        await page.goto(`${process.env.TALLER_SERVICIOS_URL}/Servicios`);
        const input = page.locator('#servicios-autocompletado-texto');
        const id = page.locator('#servicios-autocompletado-id');
        const opciones = page.locator('#servicios-autocompletado-sugerencias [role="option"]');
        const filas = page.locator('.orders-table tbody tr');
        assert.equal(await input.getAttribute('placeholder'), 'Buscar por nombre o descripción...');
        assert.equal(await input.getAttribute('aria-describedby'), 'servicios-autocompletado-estado');
        assert.equal(await filas.count(), 15);
        assert.equal(await page.locator('form form').count(), 0);
        const ids = await page.locator('[id]').evaluateAll(elementos => elementos.map(elemento => elemento.id));
        assert.equal(ids.length, new Set(ids).size);
        const encabezado = await page.locator('.dashboard-header').boundingBox();
        const buscador = await page.locator('.servicios-toolbar').boundingBox();
        const campo = await input.boundingBox();
        assert.ok(buscador.y >= encabezado.y + encabezado.height);
        assert.ok(campo.width > 450, `El campo debe ocupar el ancho disponible: ${campo.width}`);
        await input.fill('acei');
        await opciones.first().waitFor();
        assert.equal(await opciones.count(), 2);
        assert.match(await opciones.first().innerText(), /Cambio de Aceite/);
        assert.match(await opciones.first().innerText(), /Lubricación del motor/);
        await Promise.all([
            page.waitForURL(url => Boolean(url.searchParams.get('ServicioSeleccionadoId'))),
            opciones.nth(1).click()
        ]);
        assert.equal(await filas.count(), 1);
        const seleccionado = await id.inputValue();
        assert.ok(seleccionado);
        assert.match(await filas.first().getByRole('link', { name: 'Editar', exact: true }).getAttribute('href'), new RegExp(`id=${seleccionado}$`));
        assert.equal(await filas.first().getByRole('link', { name: 'Eliminar', exact: true }).count(), 1);
        await filas.first().getByRole('link', { name: 'Editar', exact: true }).click();
        assert.equal(await page.locator('#Formulario_Nombre').inputValue(), 'Cambio de Aceite');
        await page.goBack();
        await input.fill('cambio');
        assert.equal(await id.inputValue(), '');
        await Promise.all([
            page.waitForURL(url => !url.searchParams.get('ServicioSeleccionadoId')),
            page.locator('#servicios-busqueda button[type="submit"]').click()
        ]);
        assert.equal(await filas.count(), 2);
        await input.fill('diagnostico elec');
        await opciones.first().waitFor();
        await input.press('ArrowDown');
        assert.ok(await input.getAttribute('aria-activedescendant'));
        await Promise.all([
            page.waitForURL(url => Boolean(url.searchParams.get('ServicioSeleccionadoId'))),
            input.press('Enter')
        ]);
        assert.equal(await filas.count(), 1);
        assert.match(await filas.first().innerText(), /Diagnóstico Electrónico/);
        await page.getByRole('link', { name: 'Limpiar', exact: true }).click();
        assert.equal(await input.inputValue(), '');
        assert.equal(await id.inputValue(), '');
        assert.equal(await filas.count(), 15);
        await input.fill('evisi');
        await Promise.all([
            page.waitForURL(url => url.searchParams.get('buscar') === 'evisi'), input.press('Enter')
        ]);
        assert.equal(await filas.count(), 13);
        await page.getByRole('link', { name: 'Refrescar', exact: true }).click();
        assert.equal(await filas.count(), 15);
        await page.setViewportSize({ width: 768, height: 1024 });
        const tablet = await input.boundingBox();
        assert.ok(tablet.x >= 0 && tablet.x + tablet.width <= 769 && tablet.width > 200);
        await page.setViewportSize({ width: 390, height: 844 });
        await input.fill('revision');
        await opciones.first().waitFor();
        assert.equal(await opciones.count(), 10);
        const caja = await opciones.first().boundingBox();
        assert.ok(caja && caja.x >= 0 && caja.x + caja.width <= 391 && caja.width >= 200 && caja.height >= 44);
        if (process.env.TALLER_SERVICIOS_CAPTURA)
            await page.screenshot({ path: process.env.TALLER_SERVICIOS_CAPTURA });
        await input.press('Escape');
        assert.equal(await input.getAttribute('aria-expanded'), 'false');
        assert.deepEqual(errores, []);
        console.log('Servicios: nombre/descripción, ID exacto, ratón, teclado, acciones, búsqueda parcial sin límite, limpiar, escritorio/tablet/móvil y accesibilidad OK.');
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
