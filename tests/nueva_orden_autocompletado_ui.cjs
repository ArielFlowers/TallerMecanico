const assert = require('node:assert/strict');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE);

(async () => {
    const browser = await chromium.launch({ channel: 'msedge', headless: true });
    try {
        const context = await browser.newContext({ ignoreHTTPSErrors: true });
        await context.addCookies(JSON.parse(process.env.TALLER_NUEVA_ORDEN_COOKIES));
        const page = await context.newPage();
        const errores = [];
        page.on('pageerror', error => errores.push(error.message));
        page.on('dialog', dialog => dialog.accept());
        await page.goto(`${process.env.TALLER_NUEVA_ORDEN_URL}/Ordenes/Crear`);
        const texto = tipo => page.locator(`#orden-${tipo}-texto`);
        const id = tipo => page.locator(`#orden-${tipo}-id`);
        const opciones = tipo => page.locator(`#orden-${tipo}-sugerencias [role="option"]`);
        const lineas = () => page.locator('[data-orden-linea]');
        const total = async () => Number((await page.locator('[data-total-estimado]').innerText()).replace('Bs. ', '').replace(',', '.'));
        const enviar = async boton => Promise.all([page.waitForNavigation({ waitUntil: 'domcontentloaded' }), boton.click()]);
        const seleccionar = async (tipo, termino, teclado = false) => {
            await texto(tipo).fill(termino);
            await opciones(tipo).first().waitFor();
            if (teclado) { await texto(tipo).press('ArrowDown'); await texto(tipo).press('Enter'); }
            else await opciones(tipo).first().click();
            assert.ok(Number(await id(tipo).inputValue()) > 0);
        };
        const comprobarContexto = async (placa = '123ABC', cantidadLineas = 2) => {
            assert.equal(await texto('vehiculo').inputValue(), placa);
            assert.equal(await texto('servicio').inputValue(), 'Cambio de Aceite');
            assert.equal(await texto('mecanico').inputValue(), 'José Muñoz');
            assert.equal(await id('servicio').inputValue(), '1');
            assert.equal(await id('mecanico').inputValue(), '1');
            assert.equal(await lineas().count(), cantidadLineas);
        };

        assert.equal(await page.locator('[data-autocompletado][data-modo="seleccionar"]').count(), 4);
        const identificadores = await page.locator('[id]').evaluateAll(elementos => elementos.map(elemento => elemento.id));
        assert.equal(identificadores.length, new Set(identificadores).size);
        assert.equal(await page.locator('form form').count(), 0);
        assert.equal(await page.locator('.nueva-orden-page select').count(), 0);
        await seleccionar('vehiculo', 'toy cru', true);
        assert.equal(await id('vehiculo').inputValue(), '1');
        await seleccionar('mecanico', 'jose munoz');
        assert.equal(await id('vehiculo').inputValue(), '1');
        await texto('producto').fill('noexiste');
        await page.locator('#orden-producto-estado').filter({ hasText: 'No se encontraron' }).waitFor();
        assert.equal(await id('mecanico').inputValue(), '1');

        let notificar;
        const iniciada = new Promise(resolve => { notificar = resolve; });
        await page.route(url => url.searchParams.get('handler') === 'ServicioSeleccionado', async route => {
            notificar();
            await new Promise(resolve => setTimeout(resolve, 350));
            try { await route.fulfill({ json: { id: 1, costo: 999, tiempoEstimadoHoras: 1 } }); } catch { }
        }, { times: 1 });
        await seleccionar('servicio', 'lubri');
        await iniciada;
        await texto('servicio').fill('otra búsqueda');
        assert.equal(await id('servicio').inputValue(), '');
        await page.waitForTimeout(450);
        assert.equal(await page.locator('#formulario-orden').getAttribute('data-costo-servicio'), '0');
        await seleccionar('servicio', 'lubrica', true);
        await page.waitForFunction(() => document.getElementById('formulario-orden').dataset.costoServicio === '25');
        assert.equal(await total(), 25);

        await seleccionar('producto', 'fil');
        assert.equal(await lineas().count(), 0, 'Seleccionar no debe añadir el producto.');
        await page.locator('#Cantidad').fill('2');
        await enviar(page.getByRole('button', { name: 'Añadir producto al detalle', exact: true }));
        await comprobarContexto('123ABC', 1);
        assert.equal(await id('producto').inputValue(), '');
        await seleccionar('producto', 'buj', true);
        await page.locator('#Cantidad').fill('3');
        await enviar(page.getByRole('button', { name: 'Añadir producto al detalle', exact: true }));
        await comprobarContexto();
        assert.equal(await total(), 71.6);

        const primera = lineas().filter({ hasText: 'FIL-001' });
        await primera.locator('[data-cantidad]').fill('4');
        await enviar(primera.getByRole('button', { name: 'Actualizar', exact: true }));
        assert.equal(await total(), 96.6);
        await enviar(lineas().filter({ hasText: 'BUJ-001' }).getByRole('button', { name: 'Quitar', exact: true }));
        assert.equal(await lineas().count(), 1);
        await seleccionar('producto', 'buj');
        await page.locator('#Cantidad').fill('3');
        await enviar(page.getByRole('button', { name: 'Añadir producto al detalle', exact: true }));
        await seleccionar('producto', 'fil');
        await page.locator('#Cantidad').fill('2');
        await enviar(page.getByRole('button', { name: 'Añadir producto al detalle', exact: true }));
        await comprobarContexto();
        assert.equal(await total(), 71.6);

        await enviar(page.getByRole('button', { name: '+ Nuevo cliente', exact: true }));
        assert.ok(new URL(page.url()).searchParams.get('OrdenBorradorId'));
        await page.locator('#cliente-form-dialog').waitFor({ state: 'visible' });
        await page.locator('#cliente-ci').fill('7654321');
        await page.locator('#cliente-nombres').fill('Lucia');
        await page.locator('#cliente-primer-apellido').fill('Mamani');
        await page.locator('#cliente-segundo-apellido').fill('Perez');
        await page.locator('#cliente-celular').fill('71234567');
        await enviar(page.locator('#cliente-boton-crear'));
        await comprobarContexto();
        assert.match(await page.locator('.nueva-orden-page').innerText(), /Cliente seleccionado para vincular/);

        await enviar(page.getByRole('button', { name: '+ Nuevo vehículo', exact: true }));
        assert.ok(new URL(page.url()).searchParams.get('OrdenBorradorId'));
        assert.notEqual(await page.locator('#crear-cliente').inputValue(), '');
        await page.locator('#crear-placa').fill('321XYZ');
        await page.locator('#crear-marca').selectOption('Toyota');
        await page.locator('#crear-modelo').selectOption('Hilux');
        await page.locator('#crear-kilometraje').fill('100');
        await enviar(page.locator('#modal-crear').getByRole('button', { name: 'Guardar', exact: true }));
        await comprobarContexto('321XYZ');

        await enviar(page.getByRole('button', { name: '+ Nuevo producto', exact: true }));
        assert.ok(new URL(page.url()).searchParams.get('OrdenBorradorId'));
        await page.locator('#abrir-crear-producto').click();
        await page.locator('#producto-codigo').fill('NVO-001');
        await page.locator('#producto-nombre').fill('Repuesto Nuevo');
        await page.locator('#producto-precio').fill('3');
        await page.locator('#producto-stock').fill('5');
        await page.locator('#producto-stock-minimo').fill('0');
        await enviar(page.locator('#producto-boton-crear'));
        await comprobarContexto('321XYZ', 3);
        assert.equal(await total(), 74.6);

        await seleccionar('producto', 'buj');
        await texto('mecanico').fill('sin selección');
        assert.equal(await id('mecanico').inputValue(), '');
        await enviar(page.getByRole('button', { name: '✓ Guardar orden', exact: true }));
        assert.match(await page.locator('[data-valmsg-for="Formulario.MecanicoId"]').innerText(), /Selecciona un mecánico/);
        assert.equal(await id('producto').inputValue(), '2');
        assert.equal(await texto('producto').inputValue(), 'Bujía de Iridio');
        assert.equal(await texto('vehiculo').inputValue(), '321XYZ');
        assert.equal(await texto('servicio').inputValue(), 'Cambio de Aceite');
        assert.equal(await lineas().count(), 3);
        await seleccionar('mecanico', 'jose');

        await page.setViewportSize({ width: 768, height: 1024 });
        const tablet = await texto('servicio').boundingBox();
        assert.ok(tablet.width > 200 && tablet.x + tablet.width <= 769);
        await page.setViewportSize({ width: 390, height: 844 });
        for (const [tipo, termino] of [['vehiculo', '321'], ['servicio', 'lubri'], ['mecanico', 'jose'], ['producto', 'buj']]) {
            await texto(tipo).fill(termino);
            await opciones(tipo).first().waitFor();
            await texto(tipo).press('ArrowDown');
            const caja = await opciones(tipo).first().boundingBox();
            assert.ok(caja.width >= 200 && caja.x >= 0 && caja.x + caja.width <= 391 && caja.height >= 44);
            const visibilidad = await opciones(tipo).first().evaluate(elemento => {
                const caja = elemento.getBoundingClientRect();
                const encima = document.elementFromPoint(caja.x + 15, caja.y + 15);
                return { visible: elemento.contains(encima), y: caja.y, altoPantalla: innerHeight, encima: encima?.className };
            });
            if (process.env.TALLER_NUEVA_ORDEN_CAPTURA && tipo === 'producto')
                await page.screenshot({ path: process.env.TALLER_NUEVA_ORDEN_CAPTURA.replace('.png', '-producto.png') });
            assert.ok(visibilidad.visible, `El desplegable ${tipo} no debe quedar recortado: ${JSON.stringify(visibilidad)}`);
            if (process.env.TALLER_NUEVA_ORDEN_CAPTURA && tipo === 'servicio')
                await page.screenshot({ path: process.env.TALLER_NUEVA_ORDEN_CAPTURA });
            await texto(tipo).press('Enter');
        }
        await page.waitForFunction(() => document.getElementById('formulario-orden').dataset.costoServicio === '25');
        await page.setViewportSize({ width: 1280, height: 900 });
        await page.locator('#formulario-orden input[name$=".Precio"]').evaluateAll(elementos => elementos.forEach(elemento => elemento.value = '0.01'));
        await enviar(page.getByRole('button', { name: '✓ Guardar orden', exact: true }));
        assert.equal(new URL(page.url()).pathname, '/Ordenes');
        assert.match(await page.locator('.ordenes-total').innerText(), /74,60/);
        assert.deepEqual(errores, []);
        console.log('Nueva Orden: cuatro selectores independientes, invalidación, costo sin respuestas antiguas, productos/cantidades, retornos reales Cliente/Vehículo/Producto, errores, móvil y creación con precio del servidor OK.');
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
