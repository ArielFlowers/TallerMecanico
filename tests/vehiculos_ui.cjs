// Requires Playwright and Microsoft Edge. Run after dotnet build.
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || "playwright");
const { spawn } = require("node:child_process");
const { once } = require("node:events");
const { mkdtempSync, rmSync } = require("node:fs");
const { tmpdir } = require("node:os");
const { join, resolve } = require("node:path");
const assert = require("node:assert/strict");
const net = require("node:net");

(async () => {
    const directory = mkdtempSync(join(tmpdir(), "vehiculos-ui-"));
    const socket = net.createServer();
    socket.listen(0, "127.0.0.1");
    await once(socket, "listening");
    const base = `http://127.0.0.1:${socket.address().port}`;
    await new Promise(resolve => socket.close(resolve));
    const root = resolve(__dirname, "..");
    const server = spawn("dotnet", [join(root, "bin/Debug/net10.0/TallerMecanico.dll")], {
        cwd: root, windowsHide: true, stdio: "ignore",
        env: { ...process.env, ASPNETCORE_ENVIRONMENT: "Development", ASPNETCORE_URLS: base,
            ConnectionStrings__DefaultConnection: `Data Source=${join(directory, "test.db")}` }
    });
    let browser;
    try {
        for (let attempt = 0; attempt < 100; attempt++) {
            try {
                if ((await fetch(base + "/Vehiculos")).ok) break;
            } catch {}
            await new Promise(resolve => setTimeout(resolve, 100));
        }
        browser = await chromium.launch({ channel: "msedge", headless: true });
        const page = await browser.newPage();
        const errors = [];
        page.on("pageerror", error => errors.push(error.message));
        await page.goto(base + "/Vehiculos");
        await page.locator('[data-modal-abrir="modal-crear"]').click();
        assert(await page.locator("#crear-modelo").isDisabled());
        await page.locator("#crear-marca").selectOption("Toyota");
        await page.locator("#crear-modelo").selectOption("Hilux");
        await page.locator("#crear-marca").selectOption("Suzuki");
        assert.equal(await page.locator("#crear-modelo").inputValue(), "");
        assert(!(await page.locator("#crear-modelo").textContent()).includes("Hilux"));
        await page.locator("#crear-marca").selectOption("Toyota");
        await page.locator("#crear-modelo").selectOption("Hilux");
        await page.locator("#crear-placa").fill("12ABC");
        assert(!(await page.locator("#crear-placa").evaluate(input => input.checkValidity())));
        await page.locator("#crear-placa").fill("123abc");
        assert.equal(await page.locator("#crear-placa").inputValue(), "123ABC");
        assert(await page.locator("#crear-placa").evaluate(input => input.checkValidity()));
        await page.locator("#crear-placa").fill("1234abc");
        assert.equal(await page.locator("#crear-placa").inputValue(), "1234ABC");
        await page.locator('#modal-crear button[type="submit"]').click();
        await page.waitForURL(base + "/Vehiculos");
        await page.locator('[data-modal-abrir="modal-editar"]').waitFor();

        // A duplicate must reopen the form with both dependent selections intact.
        await page.locator('[data-modal-abrir="modal-crear"]').click();
        await page.locator("#crear-placa").fill("1234ABC");
        await page.locator("#crear-marca").selectOption("Toyota");
        await page.locator("#crear-modelo").selectOption("Corolla");
        await page.locator('#modal-crear button[type="submit"]').click();
        await page.locator("#modal-crear.is-open .service-validation").filter({ hasText: "ya está registrada" }).waitFor();
        assert.equal(await page.locator("#crear-marca").inputValue(), "Toyota");
        assert.equal(await page.locator("#crear-modelo").inputValue(), "Corolla");
        await page.locator("#modal-crear .modal-close").click();

        await page.locator('[data-modal-abrir="modal-editar"]').click();
        assert.equal(await page.locator("#editar-marca").inputValue(), "Toyota");
        assert.equal(await page.locator("#editar-modelo").inputValue(), "Hilux");
        assert(await page.locator("#editar-modelo-anterior").isHidden());
        await page.locator("#editar-modelo").selectOption("Land Cruiser");
        await page.locator('#modal-editar button[type="submit"]').click();
        await page.locator(".vehiculos-table td").filter({ hasText: "Land Cruiser" }).waitFor();
        await page.locator('[data-modal-abrir="modal-eliminar"]').click();
        await page.locator('#modal-eliminar button[type="submit"]').click();
        await page.getByText("No existen vehículos registrados").waitFor();
        assert.deepEqual(errors, []);
        console.log("PASS: dependent dropdowns, plate feedback, validation recovery, edit and delete in Edge");
    } finally {
        if (browser) await browser.close();
        server.kill();
        await once(server, "exit");
        rmSync(directory, { recursive: true, force: true });
    }
})().catch(error => { console.error(error); process.exitCode = 1; });
