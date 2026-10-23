const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE);

(async () => {
    const browser = await chromium.launch({ channel: 'msedge', headless: true });
    const base = process.env.TALLER_NUEVA_ORDEN_URL;
    const evidencias = process.env.TALLER_SERVICIOS_EVIDENCIAS;
    const contexto = await browser.newContext({ ignoreHTTPSErrors: true, viewport: { width: 1280, height: 900 } });
    await contexto.addCookies(JSON.parse(process.env.TALLER_NUEVA_ORDEN_COOKIES));
    const pagina = await contexto.newPage();
    const errores = [], envios = [], solicitudesPdf = [];
    pagina.on('pageerror', error => errores.push(error.message));
    contexto.on('request', peticion => {
        const url = new URL(peticion.url());
        if (url.pathname === '/Ordenes/Crear' && url.searchParams.get('handler') === 'Guardar' && peticion.method() === 'POST') envios.push(peticion.postData());
        if (url.pathname === '/Ordenes/Comprobante') solicitudesPdf.push(peticion.url());
    });
    const fila = id => pagina.locator(`[data-servicio-seleccionado][data-servicio-id="${id}"]`);
    const seleccionar = async (tipo, termino) => {
        await pagina.locator(`#orden-${tipo}-texto`).fill(termino);
        const opcion = pagina.locator(`#orden-${tipo}-sugerencias [role="option"]`).first();
        await opcion.waitFor();
        await opcion.click();
    };
    const agregar = async servicio => {
        await seleccionar('servicio', servicio.nombre);
        await pagina.waitForFunction(() => !document.getElementById('orden-agregar-servicio').disabled);
        await pagina.locator('#orden-agregar-servicio').click();
        await fila(servicio.id).waitFor();
    };
    const guardar = async () => {
        await pagina.locator('[type="submit"][form="formulario-orden"]').click();
        await pagina.locator('#confirmar-orden-dialog[open]').waitFor();
        await Promise.all([
            pagina.waitForNavigation({ waitUntil: 'domcontentloaded' }),
            pagina.locator('[data-confirmar-orden]').click()
        ]);
    };
    try {
        await pagina.goto(`${base}/Ordenes/Crear`);
        const respuesta = await contexto.request.get(`${base}/Ordenes/Crear?handler=SugerenciasServicios`);
        const sugerencias = await respuesta.json();
        assert.equal(sugerencias.length, 10);
        const servicios = [];
        for (const sugerencia of sugerencias) {
            const detalle = await contexto.request.get(`${base}/Ordenes/Crear?handler=ServicioSeleccionado&id=${sugerencia.id}`);
            servicios.push(await detalle.json());
        }
        for (const servicio of servicios) {
            await agregar(servicio);
            assert.equal(await fila(servicio.id).locator('[data-cantidad-servicio]').inputValue(), '1');
            if (servicio.unidadCantidad) {
                await fila(servicio.id).locator('[data-cantidad-visible]').fill('3');
                assert.equal(await fila(servicio.id).locator('[data-servicio-subtotal]').innerText(), `Subtotal: Bs. ${(servicio.costo * 3).toFixed(2)}`);
                await fila(servicio.id).locator('[data-cantidad-visible]').fill('1');
            } else {
                assert.equal(await fila(servicio.id).locator('[data-cantidad-visible]').count(), 0);
                assert.match(await fila(servicio.id).innerText(), /Cantidad fija: 1/);
            }
        }
        assert.equal(await pagina.locator('[data-cantidad-visible]').count(), 4);
        await agregar(servicios[0]);
        assert.match(await pagina.locator('#orden-servicio-mensaje').innerText(), /ya está agregado/);
        assert.equal(await pagina.locator('[data-servicio-seleccionado]').count(), 10);
        const filtros = servicios.find(servicio => servicio.nombre === 'Cambio de filtros');
        const aceite = servicios.find(servicio => servicio.nombre === 'Cambio de Aceite' || servicio.nombre === 'Cambio de aceite');
        assert.equal(filtros.costo, 105.5, 'Debe utilizar el precio vigente de MySQL, no el inicial 100.');
        const cantidad = fila(filtros.id).locator('[data-cantidad-visible]');
        for (const invalida of ['0', '-1', '1001', '1.5', '']) {
            await cantidad.fill(invalida);
            assert.equal(await cantidad.evaluate(campo => campo.checkValidity()), false);
            assert.equal(await fila(filtros.id).locator('[data-servicio-subtotal]').innerText(), 'Cantidad inválida');
            assert.equal(await pagina.locator('[data-total-estimado]').innerText(), 'Revisa las cantidades de servicios');
        }
        await cantidad.fill('3');
        for (const servicio of servicios.filter(servicio => servicio.id !== filtros.id && servicio.id !== aceite.id))
            await fila(servicio.id).locator('[data-quitar-servicio]').click();
        assert.equal(await pagina.locator('[data-total-estimado]').innerText(), 'Bs. 566.50');
        await seleccionar('vehiculo', '123');
        await seleccionar('mecanico', 'jose');
        await seleccionar('producto', 'fil');
        await pagina.locator('#Cantidad').fill('2');
        await Promise.all([
            pagina.waitForNavigation({ waitUntil: 'domcontentloaded' }),
            pagina.locator('#orden-boton-agregar-repuesto').click()
        ]);
        assert.equal(await fila(filtros.id).locator('[data-cantidad-visible]').inputValue(), '3');
        assert.equal(await pagina.locator('[data-total-estimado]').innerText(), 'Bs. 591.50');
        const borrador = new URL(pagina.url()).searchParams.get('BorradorId');
        assert.ok(borrador);
        await pagina.goto(`${base}/Ordenes/Crear?BorradorId=${borrador}`);
        assert.equal(await pagina.locator('#orden-vehiculo-id').inputValue(), '1');
        assert.equal(await pagina.locator('#orden-mecanico-id').inputValue(), '1');
        assert.equal(await fila(filtros.id).locator('[data-cantidad-visible]').inputValue(), '3');
        assert.equal(await pagina.locator('[data-orden-linea]').count(), 1);
        assert.equal(await pagina.locator('[data-total-estimado]').innerText(), 'Bs. 591.50');
        if (evidencias) {
            fs.mkdirSync(evidencias, { recursive: true });
            for (const ancho of [360, 768, 1280]) {
                await pagina.setViewportSize({ width: ancho, height: 900 });
                assert.ok(await pagina.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), `Desbordamiento horizontal a ${ancho}px`);
                await pagina.locator('.ordenes-servicios-selector').screenshot({ path: path.join(evidencias, `servicios-${ancho}.png`) });
            }
        }
        await pagina.evaluate(() => { document.getElementById('orden-mecanico-id').value = '0'; });
        await guardar();
        assert.equal(new URL(pagina.url()).pathname, '/Ordenes/Crear');
        assert.equal(await fila(filtros.id).locator('[data-cantidad-visible]').inputValue(), '3');
        assert.equal(await pagina.locator('[data-total-estimado]').innerText(), 'Bs. 591.50');
        assert.equal(solicitudesPdf.length, 0);
        await seleccionar('mecanico', 'jose');
        const pdfAutomatico = contexto.waitForEvent('response', respuesta => new URL(respuesta.url()).pathname === '/Ordenes/Comprobante');
        await guardar();
        assert.equal((await pdfAutomatico).status(), 200);
        assert.equal(new URL(pagina.url()).pathname, '/Ordenes');
        await pagina.locator('#comprobante-orden-dialog[open]').waitFor();
        assert.equal(contexto.pages().length, 1);
        const comprobanteUrl = await pagina.locator('[data-comprobante-frame]').getAttribute('src');
        const pdf = await contexto.request.get(new URL(comprobanteUrl, base).href);
        assert.equal(pdf.status(), 200);
        if (evidencias) fs.writeFileSync(path.join(evidencias, 'servicios-orden-real.pdf'), await pdf.body());
        await pagina.locator('[data-cerrar-comprobante]').click();
        await pagina.locator('[data-ver-orden]').first().click();
        const detalle = pagina.locator('#orden-detalle-contenido dl').filter({ hasText: 'Cambio de filtros' });
        await detalle.waitFor();
        assert.match(await detalle.innerText(), /Precio unitario\s+Bs\. 105,50/);
        assert.match(await detalle.innerText(), /Subtotal\s+Bs\. 316,50/);
        assert.match(await detalle.innerText(), /Cantidad\s+3/);
        if (evidencias) await pagina.locator('#orden-detalle-dialog').screenshot({ path: path.join(evidencias, 'detalle-servicios.png') });
        const repetido = await contexto.request.post(`${base}/Ordenes/Crear?handler=Guardar`, { form: Object.fromEntries(new URLSearchParams(envios.at(-1))) });
        assert.equal(repetido.status(), 200);
        const solicitudesAntes = solicitudesPdf.length;
        await pagina.reload({ waitUntil: 'domcontentloaded' });
        assert.equal(await pagina.locator('#comprobante-orden-dialog[open]').count(), 0);
        assert.equal(solicitudesPdf.length, solicitudesAntes);
        assert.deepEqual(errores, []);
        console.log('OK: diez servicios, cantidades, subtotales, móvil/tablet, borrador, error de servidor, modal, PDF automático y reenvío.');
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
