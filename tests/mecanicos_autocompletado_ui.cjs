const assert = require('node:assert/strict');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE);

(async () => {
    const browser = await chromium.launch({ channel: 'msedge', headless: true });
    try {
        const context = await browser.newContext({ ignoreHTTPSErrors: true });
        // Reutilizar la sesión aislada evita consumir intentos adicionales del límite de login real.
        await context.addCookies(JSON.parse(process.env.TALLER_MECANICOS_COOKIES));
        const page = await context.newPage();
        const errores = [];
        page.on('pageerror', error => errores.push(error.message));
        page.on('response', response => {
            if (response.status() >= 400) console.error(response.status(), response.url());
        });
        const base = process.env.TALLER_MECANICOS_URL;
        await page.goto(base + '/Mecanicos');
        const input = page.locator('#mecanicos-autocompletado-texto');
        const id = page.locator('#mecanicos-autocompletado-id');
        const opciones = page.locator('#mecanicos-autocompletado-sugerencias [role="option"]');
        const filas = page.locator('[data-editar-mecanico]');
        await input.fill('123');
        await opciones.first().waitFor();
        assert.equal(await opciones.count(), 2);
        assert.match(await opciones.first().innerText(), /CI:.*Motores/);
        await Promise.all([
            page.waitForURL(url => url.searchParams.has('MecanicoSeleccionadoId')),
            opciones.filter({ hasText: '1234568' }).click()
        ]);
        assert.equal(await filas.count(), 1);
        assert.equal(await filas.first().getAttribute('data-ci'), '1234568');
        assert.equal(await id.inputValue(), await filas.first().getAttribute('data-id'));
        await filas.first().click();
        assert.equal(await page.locator('#MecanicoInput_Ci').inputValue(), '1234568');
        await page.locator('#mecanico-form-dialog [data-close-dialog]').first().click();

        await input.fill('Motores');
        assert.equal(await id.inputValue(), '');
        await input.press('Enter');
        await page.waitForURL(url => url.searchParams.get('TerminoBusqueda') === 'Motores');
        assert.equal(await filas.count(), 3);
        await input.fill('123');
        await opciones.first().waitFor();
        await input.press('ArrowDown');
        assert.ok(await input.getAttribute('aria-activedescendant'));
        await Promise.all([
            page.waitForURL(url => Boolean(url.searchParams.get('MecanicoSeleccionadoId'))),
            input.press('Enter')
        ]);
        assert.equal(await filas.count(), 1);
        await Promise.all([page.waitForURL(url => !url.search), page.getByRole('link', { name: 'Limpiar', exact: true }).click()]);
        assert.equal(await filas.count(), 3);
        assert.equal(await input.inputValue(), '');
        assert.equal(await id.inputValue(), '');

        await page.setViewportSize({ width: 390, height: 844 });
        await input.fill('maria');
        await opciones.first().waitFor();
        assert.match(await opciones.first().innerText(), /María Fernanda Muñoz/);
        assert.equal(await input.getAttribute('aria-expanded'), 'true');
        const caja = await opciones.first().boundingBox();
        assert.ok(caja && caja.width >= 100 && caja.x >= 0 && caja.x + caja.width <= 391);
        assert.ok(caja.height >= 44);
        const captura = process.env.TALLER_MECANICOS_CAPTURA;
        if (captura) await page.screenshot({ path: captura, fullPage: true });
        await input.press('Escape');
        assert.equal(await input.getAttribute('aria-expanded'), 'false');
        assert.deepEqual(errores, []);
        console.log('Mecánicos real: ratón, teclado, ID exacto, edición, búsqueda manual, invalidación, limpiar y móvil OK.');
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
