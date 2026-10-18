const assert = require('node:assert/strict');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE);

(async () => {
    const browser = await chromium.launch({ channel: 'msedge', headless: true, ignoreDefaultArgs: ['--disable-popup-blocking'] });
    const base = process.env.TALLER_NUEVA_ORDEN_URL;
    const cookies = JSON.parse(process.env.TALLER_ORDEN_COOKIES_POR_ROL);
    try {
        for (const [rol, escenario] of [['Recepcionista', 'normal'], ['Administrador', 'normal'], ['Recepcionista', 'bloqueado'], ['Administrador', 'fallo-pdf']]) {
            const context = await browser.newContext({ ignoreHTTPSErrors: true });
            await context.addCookies(cookies[rol]);
            if (escenario === 'bloqueado') await context.addInitScript(() => { window.open = () => null; });
            const page = await context.newPage();
            let confirmar = true;
            page.on('dialog', dialog => confirmar ? dialog.accept() : dialog.dismiss());
            const errores = [], posts = [], comprobantes = [], pestanas = [];
            page.on('pageerror', error => errores.push(error.message));
            context.on('page', pestana => pestanas.push(pestana));
            context.on('request', request => {
                const url = new URL(request.url());
                if (request.method() === 'POST' && url.pathname === '/Ordenes/Crear' && url.searchParams.get('handler') === 'Guardar') posts.push(request);
                if (url.pathname === '/Ordenes/Comprobante') comprobantes.push(request);
            });
            const enviar = boton => Promise.all([page.waitForNavigation({ waitUntil: 'domcontentloaded' }), boton.click()]);
            const guardar = () => page.getByRole('button', { name: '✓ Guardar orden', exact: true });
            const fechaEsperada = () => new Intl.DateTimeFormat('en-GB', { timeZone: 'America/La_Paz', day: '2-digit', month: '2-digit', year: 'numeric' }).format(new Date());
            const verificarFecha = async () => {
                assert.equal(await page.locator('time[aria-labelledby="orden-fecha-etiqueta"]').innerText(), fechaEsperada());
                assert.equal(await page.locator('input[type="date"], input[name="Formulario.Fecha"]').count(), 0);
                assert.equal(await page.locator('#orden-fecha-etiqueta').innerText(), 'Fecha de la orden');
            };
            const seleccionar = async (tipo, termino) => {
                await page.locator(`#orden-${tipo}-texto`).fill(termino);
                const opcion = page.locator(`#orden-${tipo}-sugerencias [role="option"]`).first();
                await opcion.waitFor();
                await opcion.click();
            };
            await page.goto(`${base}/Ordenes/Crear`);
            await verificarFecha();
            if (rol === 'Recepcionista' && escenario === 'normal') {
                await enviar(guardar());
                assert.equal(pestanas.length, 0, 'Una selección incompleta no debe reservar pestañas.');
                assert.equal(comprobantes.length, 0);
                await verificarFecha();
            }
            await seleccionar('vehiculo', '123');
            await seleccionar('mecanico', 'jose');
            await seleccionar('servicio', 'lubri');
            await seleccionar('producto', 'fil');
            await page.locator('#Cantidad').fill(rol === 'Recepcionista' && escenario === 'normal' ? '11' : '1');
            await enviar(page.getByRole('button', { name: 'Añadir producto al detalle', exact: true }));
            await verificarFecha();

            if (rol === 'Recepcionista' && escenario === 'normal') {
                const reservada = context.waitForEvent('page');
                await enviar(guardar());
                const fallida = await reservada;
                if (!fallida.isClosed()) await fallida.waitForEvent('close');
                assert.equal(context.pages().length, 1);
                assert.equal(comprobantes.length, 0, 'Un fallo de stock no debe solicitar un PDF.');
                assert.match(await page.locator('.validation-summary-errors').innerText(), /Stock insuficiente/);
                await verificarFecha();
                await page.locator('[data-cantidad]').fill('1');
                await enviar(page.getByRole('button', { name: 'Actualizar', exact: true }));
                const antes = pestanas.length, envios = posts.length;
                await page.evaluate(() => {
                    const requerido = document.createElement('input');
                    requerido.id = 'requerido-prueba'; requerido.required = true;
                    document.getElementById('formulario-orden').append(requerido);
                });
                await guardar().click();
                assert.equal(await page.locator('#requerido-prueba').evaluate(input => input.validity.valueMissing), true);
                assert.equal(pestanas.length, antes);
                assert.equal(posts.length, envios);
                await page.locator('#requerido-prueba').evaluate(input => input.remove());
                confirmar = false;
                await guardar().click();
                assert.equal(pestanas.length, antes);
                assert.equal(posts.length, envios);
                confirmar = true;
            }

            // La fecha ni siquiera se enlaza al DTO: tampoco una fecha malformada debe intervenir.
            await page.evaluate(fecha => {
                const campo = document.createElement('input');
                campo.type = 'hidden'; campo.name = 'Formulario.Fecha'; campo.value = fecha;
                document.getElementById('formulario-orden').append(campo);
            }, rol === 'Administrador' ? 'no-es-una-fecha' : '1999-01-01');
            if (escenario === 'fallo-pdf') await context.route('**/Ordenes/Comprobante?*', route => route.fulfill({ status: 500, contentType: 'text/plain', body: 'PDF temporalmente no disponible.' }));
            const enviosAntes = posts.length, pestanasAntes = pestanas.length;
            if (rol === 'Recepcionista' && escenario === 'normal') {
                await page.evaluate(() => {
                    const formulario = document.getElementById('formulario-orden');
                    formulario.addEventListener('submit', () => formulario.requestSubmit(), { once: true });
                });
            }
            const respuestaPdf = escenario === 'bloqueado' ? null : context.waitForEvent('response', respuesta => new URL(respuesta.url()).pathname === '/Ordenes/Comprobante');
            await enviar(guardar());
            assert.equal(new URL(page.url()).pathname, '/Ordenes');
            assert.equal(posts.length, enviosAntes + 1);
            const enlace = page.locator('[data-comprobante-creado]');
            await enlace.waitFor();
            const urlPdf = new URL(await enlace.getAttribute('href'), base);
            const id = Number(urlPdf.searchParams.get('id'));
            assert.ok(id > 0);
            assert.match(await page.locator('.ordenes-page').innerText(), new RegExp(`Orden #${id} guardada correctamente`));
            if (respuestaPdf) {
                const respuesta = await respuestaPdf;
                assert.equal(respuesta.url(), urlPdf.href);
                assert.equal(respuesta.status(), escenario === 'fallo-pdf' ? 500 : 200);
                if (escenario !== 'fallo-pdf') assert.match(respuesta.headers()['content-type'], /application\/pdf/);
                assert.equal(pestanas.length, pestanasAntes + 1, 'Solo debe abrirse una pestaña por guardado.');
            } else {
                assert.equal(pestanas.length, pestanasAntes);
                assert.equal(comprobantes.length, 0);
            }

            if (escenario === 'bloqueado' || escenario === 'fallo-pdf') {
                if (escenario === 'fallo-pdf') await context.unroute('**/Ordenes/Comprobante?*');
                const manual = context.waitForEvent('response', respuesta => respuesta.url() === urlPdf.href);
                const pestanaManual = context.waitForEvent('page');
                await enlace.click();
                assert.equal((await manual).status(), 200);
                await pestanaManual;
                assert.equal(posts.length, enviosAntes + 1, 'Reabrir el PDF no debe repetir el POST.');
            }
            if (rol === 'Recepcionista' && escenario === 'normal') {
                const repetido = await context.request.post(`${base}/Ordenes/Crear?handler=Guardar`, {
                    form: Object.fromEntries(new URLSearchParams(posts.at(-1).postData()))
                });
                assert.equal(repetido.status(), 200);
                const confirmacion = await page.evaluate(html => new DOMParser().parseFromString(html, 'text/html')
                    .querySelector('.ordenes-page > .service-validation[role="status"]').textContent, await repetido.text());
                assert.match(confirmacion, new RegExp(`orden #${id} ya se había guardado`));
            }
            for (const pestana of context.pages()) if (pestana !== page) await pestana.close();
            const abiertas = pestanas.length, solicitudesPdf = comprobantes.length;
            await page.reload({ waitUntil: 'domcontentloaded' });
            assert.equal(await page.locator('[data-comprobante-creado]').count(), 0);
            await page.goto(`${base}/Ordenes/Crear`);
            await verificarFecha();
            await page.goto(`${base}/Ordenes`);
            assert.equal(pestanas.length, abiertas);
            assert.equal(comprobantes.length, solicitudesPdf);
            assert.equal(context.pages().length, 1);
            assert.deepEqual(errores, []);
            console.log(`${rol}/${escenario}: fecha Bolivia no editable, POST/redirección, ID/PDF, pestaña única, alternativa manual y recarga OK.`);
            await context.close();
        }
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
