const { chromium } = require(process.env.PLAYWRIGHT_MODULE || "playwright");
const assert = require("node:assert/strict");

(async () => {
    assert(process.env.TALLER_UI_DATABASE?.startsWith("autotaller_test_"), "Se requiere una base aislada.");
    const base = process.env.TALLER_UI_URL;
    const activa = process.env.TALLER_UI_ACTIVA;
    const anulada = process.env.TALLER_UI_ANULADA;
    const browser = await chromium.launch({ channel: "msedge", headless: true });
    try {
        const context = await browser.newContext({ ignoreHTTPSErrors: true });
        await context.addCookies(JSON.parse(process.env.TALLER_UI_COOKIES));
        const page = await context.newPage();
        const errores = [];
        const consultas = [];
        page.on("pageerror", error => errores.push(error.message));
        page.on("request", request => {
            if (new URL(request.url()).searchParams.get("handler") === "Detalle") consultas.push(request.method());
        });
        await page.goto(base + "/Ordenes");
        const modal = page.locator("#orden-detalle-dialog");
        const contenido = page.locator("#orden-detalle-contenido");
        const estado = page.locator("#orden-detalle-estado");
        const botonActiva = page.getByRole("button", { name: `Ver orden ${activa}`, exact: true });
        const botonAnulada = page.getByRole("button", { name: `Ver orden ${anulada}`, exact: true });
        const esperarDetalle = async id => {
            await page.locator("#orden-detalle-general").filter({ hasText: `#${id}` }).waitFor();
            assert.equal(await contenido.getAttribute("aria-busy"), "false");
            assert.equal(await contenido.locator("tbody tr").count(), 2);
            assert.equal(await contenido.locator("input, select, textarea, button").count(), 0);
        };

        assert.equal(consultas.length, 0, "El listado no debe cargar detalles anticipadamente.");
        await botonActiva.click();
        await esperarDetalle(activa);
        assert(await modal.isVisible());
        assert.equal(await page.evaluate(() => window.modalInjected), undefined);
        assert((await contenido.textContent()).includes("Bs. 10,00"));
        await page.keyboard.press("Escape");
        await modal.waitFor({ state: "hidden" });
        await page.waitForFunction(() => document.getElementById("orden-detalle-contenido").textContent === "");
        assert(await botonActiva.evaluate(boton => document.activeElement === boton));
        assert.equal(await contenido.textContent(), "");

        await botonAnulada.click();
        await esperarDetalle(anulada);
        assert(!(await contenido.textContent()).includes(`orden #${activa}`));
        assert((await contenido.textContent()).includes("admin_prueba"));
        await modal.getByRole("button", { name: "Cerrar", exact: true }).click();
        await modal.waitFor({ state: "hidden" });
        assert(await botonAnulada.evaluate(boton => document.activeElement === boton));

        const urlActiva = await botonActiva.getAttribute("data-detalle-url");
        const ruta = new URL(urlActiva, base).href;
        await page.route(ruta, route => route.fulfill({ status: 404, body: "No existe" }), { times: 1 });
        await botonActiva.click();
        await estado.filter({ hasText: "La orden seleccionada no existe." }).waitFor();
        assert.equal(await contenido.textContent(), "");
        await page.keyboard.press("Escape");
        await modal.waitFor({ state: "hidden" });

        await page.route(ruta, route => route.fulfill({ status: 503, body: "Error técnico" }), { times: 1 });
        await botonActiva.click();
        await estado.filter({ hasText: "No se pudo consultar la orden." }).waitFor();
        assert.equal(await contenido.textContent(), "");
        await page.keyboard.press("Escape");
        await modal.waitFor({ state: "hidden" });

        await page.route(ruta, async route => {
            await new Promise(resolve => setTimeout(resolve, 300));
            try { await route.continue(); } catch { }
        }, { times: 1 });
        await botonActiva.click();
        await estado.filter({ hasText: "Cargando detalles" }).waitFor();
        await page.keyboard.press("Escape");
        await modal.waitFor({ state: "hidden" });
        await page.waitForFunction(() => document.getElementById("orden-detalle-contenido").getAttribute("aria-busy") === "false");
        await botonAnulada.click();
        await esperarDetalle(anulada);
        assert((await contenido.textContent()).includes("admin_prueba"));
        await page.keyboard.press("Escape");
        await modal.waitFor({ state: "hidden" });

        await page.setViewportSize({ width: 480, height: 720 });
        await botonActiva.click();
        await esperarDetalle(activa);
        const caja = await modal.boundingBox();
        assert(caja.width <= 480 && caja.height <= 720, "El modal debe caber en el viewport.");
        assert((await modal.getByRole("button", { name: "Cerrar", exact: true }).boundingBox()).width < caja.width / 2);
        assert(await page.locator(".ordenes-dialog-body").evaluate(elemento => elemento.scrollHeight > elemento.clientHeight));
        if (process.env.TALLER_UI_CAPTURE) await page.screenshot({ path: process.env.TALLER_UI_CAPTURE });
        await page.keyboard.press("Escape");
        assert(consultas.length >= 5 && consultas.every(metodo => metodo === "GET"));
        assert.deepEqual(errores, []);
        console.log("OK: modal Edge, carga bajo demanda, importes, XSS, Escape, foco, cambio de orden, errores y scroll móvil.");
    } finally {
        await browser.close();
    }
})().catch(error => { console.error(error); process.exit(1); });
