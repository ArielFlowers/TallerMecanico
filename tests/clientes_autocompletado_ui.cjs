const assert = require('node:assert/strict');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE);

(async () => {
    const browser = await chromium.launch({ channel: 'msedge', headless: true });
    try {
        const context = await browser.newContext({ ignoreHTTPSErrors: true });
        await context.addCookies(JSON.parse(process.env.TALLER_CLIENTES_COOKIES));
        const page = await context.newPage();
        const errores = [];
        page.on('pageerror', error => errores.push(error.message));
        const base = process.env.TALLER_CLIENTES_URL;
        const vehiculo = '11111111-1111-4111-8111-111111111111';
        const orden = '22222222-2222-4222-8222-222222222222';
        await page.goto(`${base}/Clientes?BorradorVehiculoId=${vehiculo}&OrdenBorradorId=${orden}`);
        const input = page.locator('#clientes-autocompletado-texto');
        const id = page.locator('#clientes-autocompletado-id');
        const opciones = page.locator('#clientes-autocompletado-sugerencias [role="option"]');
        const filas = page.locator('[data-editar-cliente]');
        assert.equal(await input.getAttribute('placeholder'), 'Buscar por CI, Nombres o Apellidos');
        const ids = await page.locator('[id]').evaluateAll(elementos => elementos.map(elemento => elemento.id));
        assert.equal(ids.length, new Set(ids).size);
        assert.equal(await page.locator('form form').count(), 0);
        await input.fill('781');
        await opciones.first().waitFor();
        assert.equal(await opciones.count(), 2);
        assert.match(await opciones.first().innerText(), /María Fernanda Pérez De La Cruz/);
        await Promise.all([
            page.waitForURL(url => Boolean(url.searchParams.get('ClienteSeleccionadoId'))),
            opciones.filter({ hasText: '7812345-2B' }).click()
        ]);
        assert.equal(await filas.count(), 1);
        assert.equal(await filas.first().getAttribute('data-complemento-ci'), '2B');
        assert.equal(await id.inputValue(), await filas.first().getAttribute('data-id'));
        let actual = new URL(page.url());
        assert.equal(actual.searchParams.get('BorradorVehiculoId'), vehiculo);
        assert.equal(actual.searchParams.get('OrdenBorradorId'), orden);
        await filas.first().click();
        assert.equal(await page.locator('#cliente-ci').inputValue(), '7812345');
        assert.equal(await page.locator('#cliente-complemento-ci').inputValue(), '2B');
        await page.locator('#cliente-form-dialog [data-cerrar-cliente]').first().click();

        await input.fill('71234567');
        assert.equal(await id.inputValue(), '');
        await Promise.all([
            page.waitForURL(url => url.searchParams.get('TerminoBusqueda') === '71234567'),
            page.locator('#clientes-busqueda button[type="submit"]').click()
        ]);
        assert.equal(await filas.count(), 13);
        await input.fill('maria per');
        await opciones.first().waitFor();
        await input.press('ArrowDown');
        assert.ok(await input.getAttribute('aria-activedescendant'));
        await Promise.all([
            page.waitForURL(url => Boolean(url.searchParams.get('ClienteSeleccionadoId'))),
            input.press('Enter')
        ]);
        assert.equal(await filas.count(), 1);
        await Promise.all([
            page.waitForURL(url => !url.searchParams.has('TerminoBusqueda') && !url.searchParams.has('ClienteSeleccionadoId')),
            page.getByRole('link', { name: 'Limpiar', exact: true }).click()
        ]);
        assert.equal(await filas.count(), 13);
        assert.equal(await input.inputValue(), '');
        actual = new URL(page.url());
        assert.equal(actual.searchParams.get('borradorVehiculoId'), vehiculo);
        assert.equal(actual.searchParams.get('ordenBorradorId'), orden);
        await input.fill('2B');
        await Promise.all([
            page.waitForURL(url => url.searchParams.get('TerminoBusqueda') === '2B'),
            input.press('Enter')
        ]);
        assert.equal(await filas.count(), 1);
        await page.getByRole('link', { name: 'Limpiar', exact: true }).click();

        await page.setViewportSize({ width: 390, height: 844 });
        await input.fill('781');
        await opciones.first().waitFor();
        const caja = await opciones.first().boundingBox();
        assert.ok(caja && caja.x >= 0 && caja.x + caja.width <= 391 && caja.width >= 100 && caja.height >= 44);
        if (process.env.TALLER_CLIENTES_CAPTURA)
            await page.screenshot({ path: process.env.TALLER_CLIENTES_CAPTURA });
        await input.press('Escape');
        assert.equal(await input.getAttribute('aria-expanded'), 'false');
        assert.deepEqual(errores, []);
        console.log('Clientes real: CI con complementos, ID exacto, ratón, teclado, edición, búsquedas manuales, limpiar, contextos y móvil OK.');
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
