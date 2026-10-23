const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE);

(async () => {
    const raiz = path.resolve(__dirname, '..');
    const vista = fs.readFileSync(path.join(raiz, 'Pages/Mecanicos/Index.cshtml'), 'utf8');
    const inputReal = vista.match(/<input asp-for="MecanicoInput\.Celular"[\s\S]*?\/>/)[0]
        .replace('asp-for="MecanicoInput.Celular"', 'id="MecanicoInput_Celular" name="MecanicoInput.Celular"');
    assert.match(vista, /asp-validation-for="MecanicoInput\.Celular"/);
    const browser = await chromium.launch({ channel: 'msedge', headless: true });
    try {
        const page = await browser.newPage();
        const errores = [];
        page.on('pageerror', error => errores.push(error.message));
        const campos = ['Ci', 'ComplementoCi', 'Nombres', 'PrimerApellido', 'SegundoApellido']
            .map(nombre => `<input id="MecanicoInput_${nombre}">`).join('');
        await page.setContent(`<!doctype html><html><body>
            <button id="abrir-crear-mecanico">Nuevo</button>
            <button data-editar-mecanico data-id="1" data-celular="71234567">Editar</button>
            <dialog id="mecanico-form-dialog"><form id="mecanico-form">
            <input id="MecanicoId">${campos}${inputReal}
            <select id="MecanicoInput_Genero"></select><select id="MecanicoInput_Especialidad"></select>
            <h2 id="mecanico-form-title"></h2><p id="mecanico-form-description"></p>
            <button id="guardar-nuevo-mecanico" type="button">Crear</button>
            <button id="guardar-edicion-mecanico" type="button">Actualizar</button>
            <button data-close-dialog type="button">Cerrar</button></form></dialog>
            <dialog id="mecanico-delete-dialog"><input id="mecanico-eliminar-id"><span id="mecanico-eliminar-nombre"></span></dialog>
            </body></html>`);
        await page.addScriptTag({ path: path.join(raiz, 'wwwroot/js/mecanicos.js') });
        await page.evaluate(() => document.dispatchEvent(new Event('DOMContentLoaded')));
        const celular = page.locator('#MecanicoInput_Celular');
        assert.equal(await celular.getAttribute('type'), 'tel');
        assert.equal(await celular.getAttribute('inputmode'), 'numeric');
        assert.equal(await celular.getAttribute('maxlength'), '8');
        const pegar = texto => celular.evaluate((input, texto) => {
            const portapapeles = new DataTransfer();
            portapapeles.setData('text/plain', texto);
            input.dispatchEvent(new ClipboardEvent('paste', { clipboardData: portapapeles, bubbles: true, cancelable: true }));
        }, texto);
        for (const modo of ['crear', 'editar']) {
            await page.locator(modo === 'crear' ? '#abrir-crear-mecanico' : '[data-editar-mecanico]').click();
            assert.equal(await celular.inputValue(), modo === 'editar' ? '71234567' : '');
            await celular.fill('');
            await celular.pressSequentially('61234567');
            assert.equal(await celular.inputValue(), '61234567');
            await celular.press('9');
            assert.equal(await celular.inputValue(), '61234567');
            await celular.press('Backspace');
            await celular.press('8');
            assert.equal(await celular.inputValue(), '61234568');
            await celular.evaluate(input => input.setSelectionRange(2, 4));
            await celular.press('9');
            assert.equal(await celular.inputValue(), '6194568');
            await celular.fill('');
            await celular.pressSequentially('añ+.- /ｅ١');
            assert.equal(await celular.inputValue(), '');
            await pegar('a7b1 c2-3.4/5+6x789');
            assert.equal(await celular.inputValue(), '71234567');
            await pegar('999');
            assert.equal(await celular.inputValue(), '71234567');
            await celular.evaluate(input => input.setSelectionRange(1, 3));
            await pegar('a98b765');
            assert.equal(await celular.inputValue(), '79834567');
            await celular.evaluate(input => input.setSelectionRange(0, 8));
            await pegar('letras +.-');
            assert.equal(await celular.inputValue(), '79834567');
            await celular.fill('');
            await pegar('6');
            assert.equal(await celular.inputValue(), '6', 'La entrada parcial debe permitirse; la longitud exacta la valida el servidor.');
            await page.locator('[data-close-dialog]').click();
        }
        assert.deepEqual(errores, []);
        console.log('Celular: creación y edición, límite de 8, noveno bloqueado, borrado/reemplazo, solo ASCII numérico y pegado filtrado OK. Sin acceso a base de datos.');
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
