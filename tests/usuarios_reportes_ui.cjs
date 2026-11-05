const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE);

(async () => {
    const channel = process.env.PLAYWRIGHT_CHANNEL || (process.platform === 'darwin' ? 'chrome' : undefined);
    const browser = await chromium.launch({ headless: true, ...(channel ? { channel } : {}) });
    try {
        const context = await browser.newContext({ ignoreHTTPSErrors: true });
        await context.addCookies(JSON.parse(process.env.TALLER_USUARIOS_REPORTES_COOKIES));
        const page = await context.newPage();
        const screenshot = async name => {
            if (!process.env.TALLER_UI_MUESTRAS) return;
            fs.mkdirSync(process.env.TALLER_UI_MUESTRAS, { recursive: true });
            await page.screenshot({ path: path.join(process.env.TALLER_UI_MUESTRAS, name), fullPage: true });
        };
        page.setDefaultTimeout(10000);
        const base = process.env.TALLER_USUARIOS_REPORTES_URL;
        const errors = [], nativeDialogs = [], passwordPosts = [];
        page.on('pageerror', error => errors.push(error.message));
        page.on('dialog', async dialog => { nativeDialogs.push(dialog.type()); await dialog.dismiss(); });
        page.on('request', request => {
            if (request.method() === 'POST' && /\/(Crear|CambiarContrasena)(\?|$)/.test(new URL(request.url()).pathname))
                passwordPosts.push(request.url());
        });

        const waitUsers = async (term, action) => {
            const response = page.waitForResponse(response => {
                const url = new URL(response.url());
                return url.pathname === '/Administracion/Usuarios' && url.searchParams.get('handler') === 'Resultados'
                    && url.searchParams.get('Busqueda') === term;
            });
            await action();
            assert.equal((await response).status(), 200);
            await page.waitForFunction(() => document.querySelector('#usuarios-resultados').getAttribute('aria-busy') === 'false');
        };

        await page.goto(`${base}/Administracion/Usuarios`);
        assert.equal(await page.locator('a[href="/Administracion/Solicitudes"]').count(), 0);
        let navigations = 0;
        const countNavigation = request => {
            if (request.isNavigationRequest() && request.frame() === page.mainFrame()) navigations++;
        };
        page.on('request', countNavigation);
        const search = page.getByRole('searchbox', { name: 'Usuario o correo electrónico' });
        await waitUsers('a', () => search.fill('a'));
        assert.ok(await page.locator('#usuarios-resultados tbody tr').count() > 0);
        assert.equal(await search.evaluate(input => document.activeElement === input), true);
        await waitUsers('ana_foco', () => search.pressSequentially('na_foco', { delay: 40 }));
        assert.equal(await search.inputValue(), 'ana_foco');
        assert.equal(await search.evaluate(input => document.activeElement === input), true);
        assert.equal(await page.locator('#usuarios-resultados tbody tr').count(), 1);
        assert.match(await page.locator('#usuarios-resultados tbody').innerText(), /ana_foco/);
        assert.equal(navigations, 0, 'Escribir no debe recargar la página.');

        await waitUsers('nadie_xyz_987', () => search.fill('nadie_xyz_987'));
        assert.equal(await page.locator('#usuarios-resultados tbody tr').count(), 0);
        assert.equal(await page.getByText('No se encontraron usuarios con los criterios seleccionados.').isVisible(), true);
        await waitUsers('', () => page.getByRole('link', { name: 'Limpiar filtros' }).click());
        const nextPage = page.waitForResponse(r => new URL(r.url()).searchParams.get('Pagina') === '2');
        await page.getByRole('link', { name: 'Siguiente', exact: true }).click();
        assert.equal((await nextPage).status(), 200);
        await page.waitForFunction(() => document.querySelector('[data-usuarios-resultados]').dataset.pagina === '2');
        assert.match(await page.getByRole('navigation', { name: 'Paginación de usuarios' }).innerText(), /2/);
        await waitUsers('', () => page.getByRole('combobox', { name: 'Estado', exact: true }).selectOption('false'));
        assert.equal(await page.locator('#usuarios-resultados tbody tr').count(), 1);
        assert.match(await page.locator('#usuarios-resultados tbody').innerText(), /usuario_inactivo/);
        assert.equal(navigations, 0, 'Filtros y paginación deben conservar la página.');

        await waitUsers('', () => page.getByRole('link', { name: 'Limpiar filtros' }).click());
        await waitUsers('ana_foco', () => search.fill('ana_foco'));
        const row = page.locator('#usuarios-resultados tbody tr').filter({ hasText: 'ana_foco' });
        await row.getByRole('button', { name: 'Desactivar', exact: true }).click();
        const confirmation = page.getByRole('dialog', { name: 'Desactivar usuario', exact: true });
        await confirmation.waitFor();
        assert.match(await confirmation.innerText(), /ana_foco/);
        await screenshot('confirmacion-usuario.png');
        await confirmation.getByRole('button', { name: 'Cancelar', exact: true }).click();
        await confirmation.waitFor({ state: 'hidden' });
        assert.equal(await row.getByRole('button', { name: 'Desactivar', exact: true }).isVisible(), true);
        assert.equal(navigations, 0);
        await row.getByRole('button', { name: 'Desactivar', exact: true }).click();
        await confirmation.waitFor();
        await Promise.all([
            page.waitForNavigation(),
            confirmation.getByRole('button', { name: 'Desactivar usuario', exact: true }).click()
        ]);
        assert.equal(await page.locator('#usuarios-resultados tbody tr').filter({ hasText: 'ana_foco' })
            .getByRole('button', { name: 'Activar', exact: true }).isVisible(), true);
        page.off('request', countNavigation);

        const verifyPasswords = async (path, ids) => {
            await page.goto(`${base}${path}`);
            for (const id of ids) await page.locator(`#${id}`).fill('PruebaSecreta123!');
            for (const id of ids) {
                const button = page.locator(`button[aria-controls="${id}"]`);
                await button.waitFor();
                assert.equal(await button.getAttribute('type'), 'button');
                assert.equal(await button.getAttribute('aria-pressed'), 'false');
                await button.focus();
                await button.press('Enter');
                assert.equal(await page.locator(`#${id}`).getAttribute('type'), 'text');
                assert.equal(await button.getAttribute('aria-pressed'), 'true');
                assert.match(await button.getAttribute('aria-label'), /^Ocultar /);
                for (const other of ids.filter(other => other !== id))
                    assert.equal(await page.locator(`#${other}`).getAttribute('type'), 'password');
                await button.press('Space');
                assert.equal(await page.locator(`#${id}`).getAttribute('type'), 'password');
                assert.equal(await button.getAttribute('aria-pressed'), 'false');
                assert.equal(await page.locator(`#${id}`).inputValue(), 'PruebaSecreta123!');
            }
        };
        await verifyPasswords('/Administracion/Usuarios/Crear', ['Input_Password', 'Input_ConfirmarPassword']);
        await screenshot('crear-usuario.png');
        await verifyPasswords('/Auth/CambiarContrasena', ['Input_Actual', 'Input_Nueva', 'Input_Confirmacion']);
        await screenshot('cambiar-contrasena.png');
        assert.deepEqual(passwordPosts, [], 'Los ojitos nunca deben enviar el formulario.');
        assert.equal((await context.request.get(`${base}/Administracion/Solicitudes`)).status(), 404);

        await page.goto(`${base}/Reportes/Ordenes`);
        const states = (await page.locator('.hu3-reporte-ordenes tbody tr').allTextContents()).map(text => text.trim());
        assert.equal(states.filter(text => /Activa\s*$/.test(text)).length, 3);
        assert.equal(states.filter(text => /Anulada\s*$/.test(text)).length, 1);
        await screenshot('estados-ordenes.png');

        await page.goto(`${base}/Reportes/Mecanicos`);
        assert.equal(await page.locator('#reporte-mecanicos-filtros input[type="date"]').count(), 0);
        const mechanicSearch = page.getByRole('searchbox', { name: 'Nombre del mecánico' });
        const rows = page.locator('#reporte-mecanicos-resultados tbody tr');
        assert.equal(await rows.count(), 2);
        navigations = 0;
        page.on('request', countNavigation);
        const filterMechanics = async term => {
            const response = page.waitForResponse(r => {
                const url = new URL(r.url());
                return url.pathname === '/Reportes/Mecanicos' && url.searchParams.get('handler') === 'Resultados'
                    && url.searchParams.get('Filtro.NombreMecanico') === term;
            });
            await mechanicSearch.fill(term);
            assert.equal((await response).status(), 200);
            await page.waitForFunction(() => document.querySelector('#reporte-mecanicos-resultados').getAttribute('aria-busy') === 'false');
            assert.equal(await mechanicSearch.evaluate(input => document.activeElement === input), true);
            const exportFilters = await page.locator('#reporte-mecanicos-resultados input[name="Filtro.NombreMecanico"]')
                .evaluateAll(inputs => inputs.map(input => input.value));
            assert.equal(exportFilters.length, 2);
            for (const value of exportFilters)
                assert.equal(value, term);
        };
        await filterMechanics('Alberto');
        assert.equal(await rows.count(), 1);
        assert.match(await rows.innerText(), /Alberto Rojas Uno/);
        assert.match(await rows.innerText(), /350[.,]00/);
        assert.equal(await page.locator('.grafico-barra').count(), 1);
        assert.equal(await page.locator('.grafico-etiqueta').innerText(), 'Alberto Rojas Uno');
        await screenshot('filtro-mecanicos.png');
        await filterMechanics('sin_coincidencias_987');
        assert.equal(await rows.count(), 0);
        assert.equal(await page.locator('.grafico-barra').count(), 0);
        assert.equal(await page.getByText('No se encontraron registros de mecánicos con los criterios seleccionados.').isVisible(), true);
        await filterMechanics('');
        assert.equal(await rows.count(), 2);
        assert.equal(await page.locator('.grafico-barra').count(), 2);
        assert.equal(navigations, 0, 'Buscar mecánicos no debe recargar ni perder el foco.');
        assert.deepEqual(nativeDialogs, []);
        assert.deepEqual(errors, []);
        console.log('Usuarios y reportes: búsqueda continua, filtros, paginación, diálogo, cinco ojitos, pantalla retirada, estados y gráfico sincronizado OK.');
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
