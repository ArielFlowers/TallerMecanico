const assert = require('node:assert/strict');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE);

(async () => {
    const browser = await chromium.launch({ channel: 'msedge', headless: true });
    try {
        const context = await browser.newContext({ ignoreHTTPSErrors: true });
        await context.addCookies(JSON.parse(process.env.TALLER_VEHICULOS_COOKIES));
        const page = await context.newPage();
        const errores = [];
        page.on('pageerror', error => errores.push(error.message));
        const orden = '22222222-2222-4222-8222-222222222222';
        const cliente = process.env.TALLER_VEHICULOS_CLIENTE;
        await page.goto(`${process.env.TALLER_VEHICULOS_URL}/Vehiculos?handler=Buscar&OrdenBorradorId=${orden}&ClientePreseleccionadoId=${cliente}`);
        const input = page.locator('#vehiculos-autocompletado-texto');
        const id = page.locator('#vehiculos-autocompletado-id');
        const opciones = page.locator('#vehiculos-autocompletado-sugerencias [role="option"]');
        const filas = page.locator('button[data-modal-abrir="modal-editar"]');
        assert.equal(await input.getAttribute('placeholder'), 'Buscar por placa, marca o modelo...');
        assert.equal(await page.locator('form form').count(), 0);
        assert.equal(await page.locator('#vehiculos-autocompletado').count(), 1);
        await input.fill('123');
        await opciones.first().waitFor();
        assert.equal(await opciones.count(), 2);
        assert.match(await opciones.first().innerText(), /Toyota.*Corolla/s);
        await Promise.all([
            page.waitForURL(url => Boolean(url.searchParams.get('VehiculoSeleccionadoId'))),
            opciones.filter({ hasText: '123ABC' }).click()
        ]);
        assert.equal(await filas.count(), 1);
        assert.equal(await filas.first().getAttribute('data-placa'), '123ABC');
        assert.equal(await id.inputValue(), await filas.first().getAttribute('data-id'));
        assert.equal(new URL(page.url()).searchParams.get('OrdenBorradorId'), orden);
        assert.equal(new URL(page.url()).searchParams.get('handler'), 'Buscar');
        await filas.first().click();
        assert.equal(await page.locator('#editar-cliente').evaluate(elemento => elemento.tagName), 'SELECT');
        assert.equal(await page.locator('#editar-cliente').inputValue(), cliente);
        assert.equal(await page.locator('#editar-marca').inputValue(), 'Toyota');
        assert.equal(await page.locator('#editar-modelo').inputValue(), 'Corolla');
        await page.locator('#editar-observaciones').fill('Dato sin guardar');
        await page.locator('#modal-editar').getByRole('button', { name: 'Registrar cliente', exact: true }).click();
        await page.waitForURL(url => url.pathname === '/Clientes');
        await page.getByRole('link', { name: 'Cancelar y volver al vehículo' }).click();
        await page.waitForURL(url => url.pathname === '/Vehiculos' && url.searchParams.get('handler') === 'Retomar');
        assert.equal(await filas.count(), 1);
        assert.equal(await page.locator('#editar-observaciones').inputValue(), 'Dato sin guardar');
        assert.equal(await page.locator('#editar-cliente').inputValue(), cliente);
        assert.equal(await id.inputValue(), await filas.first().getAttribute('data-id'));
        await page.locator('#modal-editar .modal-close').click();

        await input.fill('123');
        assert.equal(await id.inputValue(), '');
        await Promise.all([
            page.waitForURL(url => !url.searchParams.get('VehiculoSeleccionadoId') && url.searchParams.get('handler') === 'Buscar'),
            page.locator('#vehiculos-busqueda button[type="submit"]').click()
        ]);
        assert.equal(await filas.count(), 3);
        await input.fill('toy cor');
        await opciones.first().waitFor();
        assert.equal(await opciones.count(), 10);
        await input.press('ArrowDown');
        assert.ok(await input.getAttribute('aria-activedescendant'));
        await Promise.all([
            page.waitForURL(url => Boolean(url.searchParams.get('VehiculoSeleccionadoId'))),
            input.press('Enter')
        ]);
        assert.equal(await filas.count(), 1);
        await Promise.all([
            page.waitForURL(url => !url.searchParams.has('Buscar') && !url.searchParams.has('VehiculoSeleccionadoId')),
            page.getByRole('link', { name: 'Limpiar', exact: true }).click()
        ]);
        assert.equal(await filas.count(), 13);
        assert.equal(await input.inputValue(), '');
        assert.equal(new URL(page.url()).searchParams.get('ordenBorradorId'), orden);
        assert.equal(await page.locator('.modal-overlay.is-open').count(), 0);
        await input.fill('oyota');
        await Promise.all([
            page.waitForURL(url => url.searchParams.get('Buscar') === 'oyota'),
            input.press('Enter')
        ]);
        assert.equal(await filas.count(), 13);
        await page.getByRole('link', { name: 'Limpiar', exact: true }).click();
        await input.fill('ab12');
        await opciones.first().waitFor();
        assert.equal(await opciones.count(), 1);
        assert.match(await opciones.first().innerText(), /AB123CD/);

        await page.setViewportSize({ width: 390, height: 844 });
        await input.fill('toy');
        await opciones.first().waitFor();
        const caja = await opciones.first().boundingBox();
        assert.ok(caja && caja.x >= 0 && caja.x + caja.width <= 391 && caja.width >= 100 && caja.height >= 44);
        if (process.env.TALLER_VEHICULOS_CAPTURA)
            await page.screenshot({ path: process.env.TALLER_VEHICULOS_CAPTURA });
        await input.press('Escape');
        assert.equal(await input.getAttribute('aria-expanded'), 'false');
        assert.deepEqual(errores, []);
        console.log('Vehículos real: placa, marca/modelo, ID exacto, edición, selector de cliente intacto, borrador/retorno, búsqueda manual, teclado, ratón y móvil OK.');
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
