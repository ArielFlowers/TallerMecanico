const assert = require('node:assert/strict');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE);

(async () => {
    const browser = await chromium.launch({ channel: 'msedge', headless: true });
    try {
        const context = await browser.newContext({ ignoreHTTPSErrors: true });
        await context.addCookies(JSON.parse(process.env.TALLER_PRODUCTOS_COOKIES));
        const page = await context.newPage();
        const errores = [];
        page.on('pageerror', error => errores.push(error.message));
        const orden = '22222222-2222-4222-8222-222222222222';
        await page.goto(`${process.env.TALLER_PRODUCTOS_URL}/Productos?OrdenBorradorId=${orden}`);
        const input = page.locator('#productos-autocompletado-texto');
        const id = page.locator('#productos-autocompletado-id');
        const opciones = page.locator('#productos-autocompletado-sugerencias [role="option"]');
        const filas = page.locator('[data-editar-producto]');
        assert.equal(await input.getAttribute('placeholder'), 'Buscar por código o nombre...');
        const ids = await page.locator('[id]').evaluateAll(elementos => elementos.map(elemento => elemento.id));
        assert.equal(ids.length, new Set(ids).size);
        assert.equal(await page.locator('form form').count(), 0);
        await input.fill('fil');
        await opciones.first().waitFor();
        assert.equal(await opciones.count(), 2);
        assert.match(await opciones.first().innerText(), /Filtro De Aceite/);
        assert.match(await opciones.first().innerText(), /Código: FIL-001/);
        await Promise.all([
            page.waitForURL(url => Boolean(url.searchParams.get('ProductoSeleccionadoId'))),
            opciones.filter({ hasText: 'FIL-002' }).click()
        ]);
        assert.equal(await filas.count(), 1);
        assert.equal(await filas.first().getAttribute('data-codigo'), 'FIL-002');
        assert.equal(await filas.first().getAttribute('data-stock'), '0');
        assert.equal(await id.inputValue(), await filas.first().getAttribute('data-id'));
        assert.equal(new URL(page.url()).searchParams.get('OrdenBorradorId'), orden);
        await filas.first().click();
        assert.equal(await page.locator('#producto-codigo').inputValue(), 'FIL-002');
        assert.equal(await page.locator('#producto-stock').inputValue(), '0');
        await page.locator('#producto-form-dialog [data-cerrar-producto]').first().click();
        await input.fill('FIL');
        assert.equal(await id.inputValue(), '');
        await Promise.all([
            page.waitForURL(url => !url.searchParams.get('ProductoSeleccionadoId')),
            page.locator('#productos-busqueda button[type="submit"]').click()
        ]);
        assert.equal(await filas.count(), 3);
        await input.fill('filtro acei');
        await opciones.first().waitFor();
        await input.press('ArrowDown');
        assert.ok(await input.getAttribute('aria-activedescendant'));
        await Promise.all([
            page.waitForURL(url => Boolean(url.searchParams.get('ProductoSeleccionadoId'))),
            input.press('Enter')
        ]);
        assert.equal(await filas.count(), 1);
        await Promise.all([
            page.waitForURL(url => !url.searchParams.has('TerminoBusqueda') && !url.searchParams.has('ProductoSeleccionadoId')),
            page.getByRole('link', { name: 'Limpiar', exact: true }).click()
        ]);
        assert.equal(await filas.count(), 14);
        assert.equal(await input.inputValue(), '');
        assert.equal(new URL(page.url()).searchParams.get('ordenBorradorId'), orden);
        await input.fill('iridio');
        await Promise.all([
            page.waitForURL(url => url.searchParams.get('TerminoBusqueda') === 'iridio'),
            input.press('Enter')
        ]);
        assert.equal(await filas.count(), 1);
        await page.getByRole('link', { name: 'Limpiar', exact: true }).click();

        await page.setViewportSize({ width: 390, height: 844 });
        await input.fill('presion');
        await opciones.first().waitFor();
        assert.equal(await opciones.count(), 10);
        assert.match(await opciones.first().innerText(), /Presión/);
        const caja = await opciones.first().boundingBox();
        assert.ok(caja && caja.x >= 0 && caja.x + caja.width <= 391 && caja.width >= 100 && caja.height >= 44);
        if (process.env.TALLER_PRODUCTOS_CAPTURA)
            await page.screenshot({ path: process.env.TALLER_PRODUCTOS_CAPTURA });
        await input.press('Escape');
        assert.equal(await input.getAttribute('aria-expanded'), 'false');
        assert.deepEqual(errores, []);
        console.log('Productos real: prefijos, nombres, ID exacto, ratón, teclado, editar, búsqueda manual, limpiar, contexto, stock cero y móvil OK.');
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
