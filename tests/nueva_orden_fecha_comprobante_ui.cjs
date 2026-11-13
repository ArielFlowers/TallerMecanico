const assert = require('node:assert/strict');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE);

(async () => {
    const browser = await chromium.launch({ channel: process.env.PLAYWRIGHT_CHANNEL || 'msedge', headless: true });
    const base = process.env.TALLER_NUEVA_ORDEN_URL;
    const cookies = JSON.parse(process.env.TALLER_ORDEN_COOKIES_POR_ROL);
    try {
        for (const [rol, escenario] of [['Recepcionista', 'normal'], ['Administrador', 'normal'], ['Recepcionista', 'sin-ventanas'], ['Administrador', 'fallo-pdf']]) {
            console.log(`${rol}/${escenario}: iniciando formulario.`);
            const context = await browser.newContext({ ignoreHTTPSErrors: true });
            context.setDefaultTimeout(15000);
            context.setDefaultNavigationTimeout(15000);
            await context.addCookies(cookies[rol]);
            if (escenario === 'sin-ventanas') await context.addInitScript(() => { window.open = () => null; });
            const page = await context.newPage();
            const errores = [], posts = [], comprobantes = [], pestanas = [];
            page.on('pageerror', error => errores.push(error.message));
            context.on('page', pestana => pestanas.push(pestana));
            const esperarCierreDeReservas = async () => {
                for (let intento = 0; intento < 100 && context.pages().length > 1; intento++)
                    await new Promise(resolve => setTimeout(resolve, 50));
                assert.equal(context.pages().length, 1, 'Una operación fallida no debe dejar pestañas de espera.');
            };
            context.on('request', request => {
                const url = new URL(request.url());
                if (request.method() === 'POST' && url.pathname === '/Ordenes/Crear' && url.searchParams.get('handler') === 'Guardar') posts.push(request);
                if (url.pathname === '/Ordenes/Comprobante') comprobantes.push(request);
            });
            const enviar = boton => Promise.all([page.waitForNavigation({ waitUntil: 'domcontentloaded' }), boton.click()]);
            const revisar = () => page.getByRole('button', { name: 'Revisar y confirmar orden', exact: true });
            const guardar = async () => {
                await revisar().click();
                await page.locator('#confirmar-orden-dialog[open]').waitFor();
                await enviar(page.locator('[data-confirmar-orden]'));
            };
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
                await guardar();
                assert.equal(new URL(page.url()).pathname, '/Ordenes/Crear');
                await esperarCierreDeReservas();
                assert.equal(comprobantes.length, 0);
                assert.match(await page.locator('[data-valmsg-for="Formulario.ServicioIds"]').first().innerText(), /al menos un servicio/);
            }
            await seleccionar('vehiculo', '123');
            await seleccionar('mecanico', 'jose');
            await seleccionar('servicio', 'lubri');
            await page.locator('#orden-agregar-servicio:not([disabled])').click();
            await seleccionar('producto', 'fil');
            await page.locator('#Cantidad').fill('1');
            await enviar(page.getByRole('button', { name: 'Agregar repuesto', exact: true }));
            await verificarFecha();

            if (rol === 'Recepcionista' && escenario === 'normal') {
                // La validacion del servidor debe rechazar una cantidad oculta manipulada.
                await page.locator('#formulario-orden input[name="Formulario.Lineas[0].Cantidad"]').evaluate(input => { input.value = '11'; });
                const pdfAntes = comprobantes.length;
                await guardar();
                assert.equal(new URL(page.url()).pathname, '/Ordenes/Crear');
                assert.equal(comprobantes.length, pdfAntes);
                await esperarCierreDeReservas();
                assert.equal(await page.locator('#comprobante-orden-dialog[open]').count(), 0);
                assert.match(await page.locator('.validation-summary-errors').innerText(), /Stock insuficiente/);
                await verificarFecha();
                await page.locator('[data-cantidad]').fill('1');
                await enviar(page.getByRole('button', { name: 'Guardar cantidad de Filtro de Aceite', exact: true }));
                const envios = posts.length;
                await page.evaluate(() => {
                    const requerido = document.createElement('input');
                    requerido.id = 'requerido-prueba'; requerido.required = true;
                    document.getElementById('formulario-orden').append(requerido);
                });
                await revisar().click();
                assert.equal(await page.locator('#requerido-prueba').evaluate(input => input.validity.valueMissing), true);
                assert.equal(posts.length, envios);
                assert.equal(await page.locator('#confirmar-orden-dialog[open]').count(), 0);
                await page.locator('#requerido-prueba').evaluate(input => input.remove());
                await revisar().click();
                await page.locator('#confirmar-orden-dialog[open]').waitFor();
                await page.locator('[data-cancelar-confirmacion]').click();
                assert.equal(await page.locator('#confirmar-orden-dialog[open]').count(), 0);
                assert.equal(posts.length, envios, 'Cancelar la confirmacion no debe enviar la orden.');
            }

            // La fecha se calcula en el servidor, incluso si se fuerza un campo antiguo.
            await page.evaluate(fecha => {
                const campo = document.createElement('input');
                campo.type = 'hidden'; campo.name = 'Formulario.Fecha'; campo.value = fecha;
                document.getElementById('formulario-orden').append(campo);
            }, rol === 'Administrador' ? 'no-es-una-fecha' : '1999-01-01');
            if (escenario === 'fallo-pdf') await context.route('**/Ordenes/Comprobante?*', route => route.fulfill({ status: 500, contentType: 'text/plain', body: 'PDF temporalmente no disponible.' }));
            const enviosAntes = posts.length;
            if (rol === 'Recepcionista' && escenario === 'normal') {
                await page.evaluate(() => {
                    const formulario = document.getElementById('formulario-orden');
                    formulario.addEventListener('submit', () => formulario.requestSubmit(), { once: true });
                });
            }
            const pestañasAntes = pestanas.length;
            console.log(`${rol}/${escenario}: guardando orden y esperando comprobante.`);
            const esperaPdf = escenario === 'sin-ventanas' ? null : context.waitForEvent('response', respuesta => new URL(respuesta.url()).pathname === '/Ordenes/Comprobante');
            const [, respuestaPdf] = await Promise.all([guardar(), esperaPdf]);
            assert.equal(new URL(page.url()).pathname, '/Ordenes');
            assert.equal(posts.length, enviosAntes + 1);
            const modal = page.locator('#comprobante-orden-dialog');
            assert.equal(await modal.getAttribute('open'), null);
            const respaldo = page.locator('[data-comprobante-creado]');
            const urlPdf = new URL(await respaldo.getAttribute('href'), base);
            assert.equal(await respaldo.getAttribute('target'), '_blank');
            assert.equal(await respaldo.isVisible(), true);
            const id = Number(urlPdf.searchParams.get('id'));
            assert.ok(id > 0);
            const enlace = page.getByRole('link', { name: `Ver comprobante de orden ${id}`, exact: true });
            assert.equal(await enlace.getAttribute('target'), null);
            if (respuestaPdf) {
                const respuesta = respuestaPdf;
                assert.equal(respuesta.url(), urlPdf.href);
                assert.equal(respuesta.status(), escenario === 'fallo-pdf' ? 500 : 200);
                if (escenario !== 'fallo-pdf') assert.match(respuesta.headers()['content-type'], /application\/pdf/);
                assert.equal(pestanas.length, pestañasAntes + 1, 'Solo debe reservarse una pestaña por operación.');
            } else {
                assert.equal(pestanas.length, pestañasAntes);
                const [manualPdf] = await Promise.all([
                    context.waitForEvent('response', respuesta => respuesta.url() === urlPdf.href),
                    context.waitForEvent('page'),
                    respaldo.click()
                ]);
                assert.equal(manualPdf.status(), 200);
                assert.equal(pestanas.length, pestañasAntes + 1, 'El respaldo debe abrir una sola pestaña.');
            }
            assert.equal(context.pages().length, 2, 'Deben permanecer la aplicación y un solo comprobante.');
            console.log(`${rol}/${escenario}: comprobante recibido; verificando respaldo e idempotencia.`);

            if (escenario === 'sin-ventanas' || escenario === 'fallo-pdf') {
                if (escenario === 'fallo-pdf') await context.unroute('**/Ordenes/Comprobante?*');
                const [manual] = await Promise.all([
                    context.waitForEvent('response', respuesta => respuesta.url() === urlPdf.href),
                    enlace.click()
                ]);
                assert.equal(manual.status(), 200);
                assert.equal(await modal.getAttribute('open'), '');
                assert.equal(posts.length, enviosAntes + 1, 'Reabrir el PDF no debe repetir el POST.');
                await modal.locator('[data-cerrar-comprobante]').click();
            }
            if (rol === 'Recepcionista' && escenario === 'normal') {
                const repetido = await context.request.post(`${base}/Ordenes/Crear?handler=Guardar`, {
                    form: Object.fromEntries(new URLSearchParams(posts.at(-1).postData())), timeout: 15000
                });
                assert.equal(repetido.status(), 200);
                const confirmacion = await page.evaluate(html => new DOMParser().parseFromString(html, 'text/html')
                    .querySelector('.ordenes-page > .orden-comprobante-aviso[role="status"]').textContent, await repetido.text());
                assert.match(confirmacion, /La orden ya se había guardado/);
                assert(!confirmacion.includes(`#${id}`));
            }
            console.log(`${rol}/${escenario}: verificando recarga con el comprobante abierto.`);
            const solicitudesPdf = comprobantes.length, totalPestañas = pestanas.length;
            await page.reload({ waitUntil: 'domcontentloaded' });
            assert.equal(await page.locator('#comprobante-orden-dialog[open]').count(), 0);
            assert.equal(comprobantes.length, solicitudesPdf);
            await page.goto(`${base}/Ordenes/Crear`);
            await verificarFecha();
            await page.goto(`${base}/Ordenes`);
            assert.equal(pestanas.length, totalPestañas);
            assert.equal(comprobantes.length, solicitudesPdf);
            assert.equal(context.pages().length, 2, 'Recargar no debe cerrar el comprobante ni abrir otro.');
            assert.deepEqual(errores, []);
            console.log(`${rol}/${escenario}: confirmación, POST único, pestaña automática, respaldo, modal manual y recarga OK.`);
            // El visor PDF puede bloquear page.close(); se cierran sus pestañas junto con el contexto.
            await context.close();
        }
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
