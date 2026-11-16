// Página Razor real con APIs simuladas por VehiculosModalTests; no utiliza MySQL.
const assert = require('node:assert/strict');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE);

(async () => {
    const base = process.env.TALLER_VEHICULOS_MODAL_URL;
    assert.equal(new URL(base).hostname, '127.0.0.1');
    const browser = await chromium.launch({ channel: process.env.PLAYWRIGHT_CHANNEL || 'msedge', headless: true });
    try {
        const page = await browser.newPage();
        const errores = [];
        page.on('pageerror', error => errores.push(error.message));
        const texto = modal => page.locator(`#${modal}-cliente-autocompletado-texto`);
        const id = modal => page.locator(`#${modal}-cliente-autocompletado-id`);
        const opciones = modal => page.locator(`#${modal}-cliente-autocompletado-sugerencias [role="option"]`);
        const valido = modal => texto(modal).evaluate(input => input.checkValidity());
        const abrirCrear = () => page.locator('[data-modal-abrir="modal-crear"]').click();
        const guardar = modal => page.locator(`#modal-${modal}`).getByRole('button', { name: 'Guardar', exact: true });
        const navegar = accion => Promise.all([page.waitForNavigation(), accion()]);

        await page.goto(base + '/Vehiculos');
        await abrirCrear();
        assert.equal(await valido('crear'), false);
        assert.equal(await texto('crear').getAttribute('placeholder'), 'Busca por CI, nombres o apellidos...');
        assert.equal(await page.locator('#crear-placa').getAttribute('placeholder'), 'Ej. 1234ABC');
        assert.equal(await page.locator('#crear-modelo option:checked').textContent(), 'Primero selecciona una marca');
        assert.equal(await page.locator('#crear-modelo').isDisabled(), true);
        assert.equal(await page.locator('#crear-kilometraje').getAttribute('placeholder'), 'Ej. 45000');
        assert.match(await page.locator('#crear-observaciones').getAttribute('placeholder'), /Ej\..*frenos/);
        await page.locator('#crear-extranjera').check();
        assert.equal(await page.locator('#crear-placa').getAttribute('placeholder'), 'Ej. AB123CD');
        await page.locator('#crear-extranjera').uncheck();
        await page.locator('#crear-placa').fill('123ABC');
        await page.locator('#crear-marca').selectOption('Toyota');
        assert.equal(await page.locator('#crear-modelo option:checked').textContent(), 'Selecciona un modelo');
        await page.locator('#crear-modelo').selectOption('Corolla');
        await page.locator('#crear-kilometraje').fill('45000');
        await page.locator('#crear-observaciones').fill('Revisar frenos');

        // Escribir un nombre no debe permitir guardar sin elegir una sugerencia.
        await texto('crear').fill('Ana');
        await opciones('crear').first().waitFor();
        assert.equal(await opciones('crear').count(), 2);
        assert.equal(await valido('crear'), false);
        let envios = 0;
        page.on('request', request => {
            if (request.method() === 'POST' && new URL(request.url()).searchParams.get('handler') === 'Create') envios++;
        });
        await guardar('crear').click();
        assert.equal(envios, 0);
        await texto('crear').fill('222');
        await opciones('crear').filter({ hasText: '2222222-1A' }).click();
        assert.equal(await id('crear').inputValue(), '2');
        assert.equal(await valido('crear'), true);
        assert.match(await page.locator('#crear-cliente-autocompletado-estado').innerText(), /CI: 2222222-1A/);
        await texto('crear').fill('Arce');
        assert.equal(await id('crear').inputValue(), '');
        assert.equal(await valido('crear'), false);
        await opciones('crear').first().waitFor();
        await texto('crear').press('ArrowDown');
        await texto('crear').press('Enter');
        assert.equal(await id('crear').inputValue(), '1');
        assert.equal(envios, 0);
        await texto('crear').fill('222');
        await opciones('crear').filter({ hasText: '2222222-1A' }).click();

        // Un error del servidor conserva al cliente seleccionado y el resto del formulario.
        await navegar(() => guardar('crear').click());
        assert.equal(envios, 1);
        assert.equal(await page.locator('#modal-crear').evaluate(modal => modal.classList.contains('is-open')), true);
        assert.match(await page.locator('#modal-crear').innerText(), /Esta placa ya está registrada/);
        assert.equal(await id('crear').inputValue(), '2');
        assert.match(await texto('crear').inputValue(), /Arce\s+Ana/);
        assert.match(await page.locator('#crear-cliente-autocompletado-estado').innerText(), /CI: 2222222-1A/);
        assert.equal(await valido('crear'), true);
        assert.equal(await page.locator('#crear-kilometraje').inputValue(), '45000');
        await page.locator('#modal-crear .modal-close').click();
        await abrirCrear();
        assert.equal(await id('crear').inputValue(), '');
        assert.equal(await texto('crear').inputValue(), '');
        assert.equal(await valido('crear'), false);
        await page.locator('#modal-crear .modal-close').click();

        for (const [vehiculo, cliente] of [[7, '1'], [8, '2']]) {
            await page.locator(`[data-modal-abrir="modal-editar"][data-id="${vehiculo}"]`).click();
            assert.equal(await id('editar').inputValue(), cliente);
            assert.equal(await valido('editar'), true);
            assert.match(await page.locator('#editar-cliente-autocompletado-estado').innerText(),
                cliente === '1' ? /CI: 1111111/ : /CI: 2222222-1A/);
            if (vehiculo === 7) await page.locator('#modal-editar .modal-close').click();
        }
        await page.locator('#editar-observaciones').fill('Conservar al registrar cliente');
        await navegar(() => page.locator('#modal-editar').getByRole('button', { name: 'Registrar cliente', exact: true }).click());
        await navegar(() => page.getByRole('link', { name: 'Cancelar y volver al vehículo' }).click());
        assert.equal(await id('editar').inputValue(), '2');
        assert.equal(await valido('editar'), true);
        assert.match(await page.locator('#editar-cliente-autocompletado-estado').innerText(), /CI: 2222222-1A/);
        assert.equal(await page.locator('#editar-observaciones').inputValue(), 'Conservar al registrar cliente');

        // Registrar un cliente nuevo debe asociarlo al borrador de edición.
        await navegar(() => page.locator('#modal-editar').getByRole('button', { name: 'Registrar cliente', exact: true }).click());
        for (const [campo, valor] of Object.entries({
            ci: '3333333', nombres: 'Lucia', 'primer-apellido': 'Mamani', 'segundo-apellido': 'Perez', celular: '73333333'
        })) await page.locator(`#cliente-${campo}`).fill(valor);
        await navegar(() => page.locator('#cliente-boton-crear').click());
        assert.equal(await id('editar').inputValue(), '3');
        assert.match(await texto('editar').inputValue(), /Mamani.*Lucia/);
        assert.equal(await page.locator('#editar-id').inputValue(), '8');
        assert.equal(await valido('editar'), true);
        await navegar(() => guardar('editar').click());

        // Sugerencias dentro del modal en una pantalla pequeña.
        await page.setViewportSize({ width: 390, height: 844 });
        await abrirCrear();
        await texto('crear').fill('Ana');
        await opciones('crear').first().waitFor();
        const caja = await opciones('crear').first().boundingBox();
        assert(caja && caja.x >= 0 && caja.x + caja.width <= 390 && caja.height >= 44);
        await opciones('crear').filter({ hasText: '2222222-1A' }).click();
        assert.equal(await id('crear').inputValue(), '2');
        assert.deepEqual(errores, []);
        console.log('RF-01/RF-02 OK: cliente obligatorio, CI y homónimos, teclado/ratón, invalidación, errores, reapertura, edición, borradores, cliente nuevo y móvil.');
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
