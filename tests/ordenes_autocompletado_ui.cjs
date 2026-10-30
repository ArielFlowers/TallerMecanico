const assert = require('node:assert/strict');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE);

(async () => {
    const browser = await chromium.launch({ channel: 'msedge', headless: true });
    try {
        const context = await browser.newContext({ ignoreHTTPSErrors: true });
        await context.addCookies(JSON.parse(process.env.TALLER_ORDENES_COOKIES));
        const page = await context.newPage();
        const errores = [];
        page.on('pageerror', error => errores.push(error.message));
        await page.goto(`${process.env.TALLER_ORDENES_URL}/Ordenes`);
        const input = page.locator('#ordenes-autocompletado-texto');
        const id = page.locator('#ordenes-autocompletado-id');
        const opciones = page.locator('#ordenes-autocompletado-sugerencias [role="option"]');
        const opcionOrden = numero => opciones.filter({
            has: page.locator('.autocompletado-principal', { hasText: new RegExp(`^Orden #${numero}$`) })
        });
        const filas = page.locator('[data-ver-orden]');
        const estado = page.locator('#Estado');
        const fecha = page.locator('#Fecha');
        const aplicarFiltros = () => Promise.all([
            page.waitForNavigation({ waitUntil: 'load' }),
            page.getByRole('button', { name: 'Aplicar filtros', exact: true }).click()
        ]);
        assert.equal(await input.getAttribute('placeholder'), 'Buscar por número de orden o placa...');
        const ids = await page.locator('[id]').evaluateAll(elementos => elementos.map(elemento => elemento.id));
        assert.equal(ids.length, new Set(ids).size);
        assert.equal(await filas.count(), 4);
        assert.equal(await page.locator('form form').count(), 0);
        const encabezado = await page.locator('.dashboard-header').boundingBox();
        const toolbar = await page.locator('.ordenes-buscador-toolbar').boundingBox();
        assert.ok(toolbar.y >= encabezado.y + encabezado.height);
        assert.ok((await input.boundingBox()).width > 450);
        await input.fill('15');
        await opciones.first().waitFor();
        assert.equal(await opciones.count(), 3);
        assert.match(await opciones.first().innerText(), /Orden #151/);
        assert.match(await opciones.first().innerText(), /123ABC · Ana María Arce Pérez/);
        await Promise.all([
            page.waitForURL(url => url.searchParams.get('OrdenSeleccionadaId') === '15'),
            opcionOrden(15).click()
        ]);
        assert.equal(await filas.count(), 1);
        assert.equal(await id.inputValue(), '15');
        await estado.selectOption('Anulada');
        await aplicarFiltros();
        assert.equal(await filas.count(), 0);
        assert.match(await page.locator('.services-empty').innerText(), /No se encontraron órdenes/);
        assert.equal(await id.inputValue(), '15');
        await estado.selectOption('Activa');
        await fecha.fill('2026-10-12');
        await aplicarFiltros();
        assert.equal(await filas.count(), 1);
        assert.equal(new URL(page.url()).searchParams.get('Estado'), 'Activa');
        assert.equal(new URL(page.url()).searchParams.get('Fecha'), '2026-10-12');
        await input.fill('123');
        await page.waitForFunction(() => document.getElementById('ordenes-autocompletado-id').value === '');
        assert.equal(await id.inputValue(), '');
        await opciones.first().waitFor();
        assert.equal(await opciones.count(), 2);
        await input.press('ArrowDown');
        assert.ok(await input.getAttribute('aria-activedescendant'));
        await Promise.all([
            page.waitForURL(url => Boolean(url.searchParams.get('OrdenSeleccionadaId'))), input.press('Enter')
        ]);
        assert.equal(await filas.count(), 1);
        await page.getByRole('link', { name: 'Limpiar filtros', exact: true }).click();
        assert.equal(await filas.count(), 4);
        assert.equal(await estado.inputValue(), '');
        assert.equal(await fecha.inputValue(), '');
        assert.equal(await input.inputValue(), '');
        assert.equal(await id.inputValue(), '');
        await input.fill('1');
        await input.press('Enter');
        assert.equal(await filas.count(), 4, 'La búsqueda manual admite un dígito.');
        await input.fill('123');
        await estado.selectOption('Anulada');
        await fecha.fill('2026-10-12');
        await input.fill('123A');
        await opciones.first().waitFor();
        assert.equal(await opciones.count(), 1, 'Las sugerencias usan los filtros actuales antes de enviar el formulario.');
        assert.match(await opciones.first().innerText(), /Orden #150/);
        await page.getByRole('button', { name: 'Buscar', exact: true }).click();
        assert.equal(await filas.count(), 1);
        await page.getByRole('link', { name: 'Limpiar filtros', exact: true }).click();
        await input.fill('123');
        await opciones.first().waitFor();
        await opcionOrden(1).click();
        const modal = page.locator('#orden-detalle-dialog');
        await filas.first().click();
        await page.locator('#orden-detalle-general').filter({ hasText: '#1' }).waitFor();
        assert.match(await page.locator('#orden-detalle-contenido').innerText(), /Bs\. 31,00/);
        assert.equal(await page.getByRole('button', { name: 'Anular orden 1', exact: true }).count(), 0);
        await page.keyboard.press('Escape');
        await modal.waitFor({ state: 'hidden' });
        assert.ok(await filas.first().evaluate(elemento => document.activeElement === elemento));
        const enlace = page.getByRole('link', { name: 'Ver comprobante de orden 1', exact: true });
        assert.equal(await enlace.getAttribute('aria-controls'), 'comprobante-orden-dialog');
        assert.equal(await enlace.getAttribute('target'), null);
        const pdf = await context.request.get(new URL(await enlace.getAttribute('href'), page.url()).href);
        assert.equal(pdf.status(), 200);
        assert.ok((await pdf.body()).subarray(0, 5).equals(Buffer.from('%PDF-')));
        await page.getByRole('link', { name: 'Limpiar filtros', exact: true }).click();
        await page.setViewportSize({ width: 768, height: 1024 });
        const tablet = await input.boundingBox();
        assert.ok(tablet.x >= 0 && tablet.x + tablet.width <= 769 && tablet.width > 200);
        await page.setViewportSize({ width: 390, height: 844 });
        await input.fill('123');
        await opciones.first().waitFor();
        const caja = await opciones.first().boundingBox();
        assert.ok(caja.x >= 0 && caja.x + caja.width <= 391 && caja.width >= 200 && caja.height >= 44);
        if (process.env.TALLER_ORDENES_CAPTURA) await page.screenshot({ path: process.env.TALLER_ORDENES_CAPTURA });
        await input.press('Escape');
        assert.equal(await input.getAttribute('aria-expanded'), 'false');
        assert.deepEqual(errores, []);
        console.log('Órdenes: prefijos, selección exacta, filtros combinados, refrescar, limpiar, teclado/ratón, detalle, PDF, permisos y móvil OK.');
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
