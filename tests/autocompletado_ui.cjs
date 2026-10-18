const assert = require("node:assert/strict");
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || "playwright");

(async () => {
    const base = process.env.TALLER_AUTOCOMPLETADO_URL;
    assert(base && new URL(base).hostname === "127.0.0.1", "Se requiere el host aislado de pruebas.");
    const browser = await chromium.launch({ channel: "msedge", headless: true });
    let aprobadas = 0;

    async function probar(nombre, accion) {
        const page = await browser.newPage();
        const errores = [];
        page.on("pageerror", error => errores.push(error.message));
        await page.clock.install({ time: new Date("2026-01-01T00:00:00Z") });
        await page.clock.pauseAt(new Date("2026-01-01T00:00:01Z"));
        await page.addInitScript(() => {
            window.prueba = { planes: {}, consultas: [], selecciones: [], invalidaciones: [], envios: 0 };
            document.addEventListener("autocompletado:seleccion", evento => window.prueba.selecciones.push(evento.detail));
            document.addEventListener("autocompletado:invalidacion", evento => window.prueba.invalidaciones.push(evento.detail));
            document.addEventListener("submit", evento => { evento.preventDefault(); window.prueba.envios++; });
            window.fetch = (ruta, opciones) => {
                const url = new URL(ruta);
                const termino = url.searchParams.get("termino");
                const consulta = { termino, grupo: url.searchParams.get("grupo"), abortada: opciones.signal.aborted,
                    metodo: opciones.method, accept: opciones.headers.Accept };
                window.prueba.consultas.push(consulta);
                opciones.signal.addEventListener("abort", () => { consulta.abortada = true; });
                const plan = window.prueba.planes[termino] || {};
                // Se ignora deliberadamente abort para demostrar también la protección de versión.
                return new Promise((resolver, rechazar) => setTimeout(() => {
                    if (plan.red) { rechazar(new TypeError("Fallo simulado")); return; }
                    resolver({ ok: (plan.status || 200) < 400, status: plan.status || 200,
                        redirected: plan.redireccion || false,
                        headers: new Headers({ "Content-Type": plan.tipo || "application/json; charset=utf-8" }),
                        json: async () => {
                            if (plan.jsonInvalido) throw new SyntaxError("JSON inválido");
                            return plan.cuerpo ?? [
                                { id: 1, textoPrincipal: `Resultado ${termino}`, textoSecundario: "Información adicional" },
                                { id: 2, textoPrincipal: `Otra opción ${termino}` }
                            ];
                        }
                    });
                }, plan.demora || 0));
            };
        });
        try {
            await page.goto(base);
            await accion(page);
            assert.deepEqual(errores, []);
            aprobadas++;
            console.log(`OK: ${nombre}`);
        } finally { await page.close(); }
    }

    const consultas = page => page.evaluate(() => window.prueba.consultas);
    const avanzar = (page, tiempo = 301) => page.clock.runFor(tiempo);
    const plan = (page, termino, configuracion) => page.evaluate(({ termino, configuracion }) => {
        window.prueba.planes[termino] = configuracion;
    }, { termino, configuracion });
    const escribir = async (page, termino, raiz = "filtro") => {
        await page.locator(`#${raiz}-texto`).fill(termino);
        await avanzar(page);
    };

    try {
        await probar("partial real, etiquetas, ARIA, IDs únicos y valor inicial", async page => {
            assert.equal(await page.locator("[data-autocompletado]").count(), 2);
            assert.equal(await page.locator("form form").count(), 0);
            assert(await page.evaluate(() => {
                const ids = [...document.querySelectorAll("[id]")].map(elemento => elemento.id);
                return new Set(ids).size === ids.length;
            }));
            const input = page.getByRole("combobox", { name: "Seleccionar registro", exact: true });
            assert.equal(await input.inputValue(), "Registro inicial");
            assert.equal(await page.locator("#seleccion-id").inputValue(), "42");
            assert.equal(await input.getAttribute("aria-controls"), "seleccion-sugerencias");
            assert.equal(await input.getAttribute("aria-expanded"), "false");
            assert.equal(await page.locator("#seleccion-estado").getAttribute("role"), "status");
            assert.equal((await consultas(page)).length, 0);
        });

        await probar("mínimo de dos caracteres y debounce de 300 ms", async page => {
            await escribir(page, "a");
            assert.equal((await consultas(page)).length, 0);
            await page.locator("#filtro-texto").fill("ab");
            await avanzar(page, 299);
            assert.equal((await consultas(page)).length, 0);
            await page.locator("#filtro-texto").fill("abc");
            await avanzar(page, 299);
            assert.equal((await consultas(page)).length, 0);
            await avanzar(page, 2);
            assert.equal((await consultas(page)).length, 1);
            assert.equal((await consultas(page))[0].termino, "abc");
            assert.equal((await consultas(page))[0].metodo, "GET");
            assert.equal((await consultas(page))[0].accept, "application/json");
        });

        await probar("teclado en modo seleccionar sin enviar el formulario", async page => {
            await escribir(page, "ju", "seleccion");
            const input = page.locator("#seleccion-texto");
            await input.press("ArrowUp");
            assert.equal(await input.getAttribute("aria-activedescendant"), "seleccion-opcion-1");
            await input.press("ArrowDown");
            assert.equal(await input.getAttribute("aria-activedescendant"), "seleccion-opcion-0");
            assert.equal(await page.locator("#seleccion-opcion-0").getAttribute("aria-selected"), "true");
            await input.press("Enter");
            assert.equal(await page.locator("#seleccion-id").inputValue(), "1");
            assert.equal(await input.inputValue(), "Resultado ju");
            assert.equal(await input.getAttribute("aria-expanded"), "false");
            assert(await input.evaluate(elemento => elemento === document.activeElement));
            const resultado = await page.evaluate(() => window.prueba);
            assert.equal(resultado.envios, 0);
            assert.equal(resultado.selecciones[0].modo, "seleccionar");
            assert.equal(resultado.selecciones[0].sugerencia.id, 1);
        });

        await probar("ratón en modo filtrar, evento compartido sin navegación automática", async page => {
            await escribir(page, "pe");
            await page.locator("#filtro-opcion-1").click({ force: true });
            assert.equal(await page.locator("#filtro-id").inputValue(), "2");
            const resultado = await page.evaluate(() => window.prueba);
            assert.equal(resultado.selecciones[0].modo, "filtrar");
            assert.equal(resultado.selecciones[0].idInstancia, "filtro");
            assert.equal(resultado.envios, 0);
            assert.equal(page.url(), `${base}/`);
        });

        await probar("editar invalida inmediatamente el ID seleccionado", async page => {
            await page.locator("#seleccion-texto").fill("Registro modificado");
            assert.equal(await page.locator("#seleccion-id").inputValue(), "");
            assert.equal((await page.evaluate(() => window.prueba.invalidaciones))[0].idAnterior, 42);
            await escribir(page, "pe");
            await page.locator("#filtro-opcion-0").click({ force: true });
            await page.locator("#filtro-texto").fill("otro");
            assert.equal(await page.locator("#filtro-id").inputValue(), "");
        });

        await probar("instancias independientes e inicialización repetible", async page => {
            await page.evaluate(() => {
                window.AutoTallerAutocompletado.inicializar();
                for (const id of ["filtro", "seleccion"]) {
                    const input = document.getElementById(`${id}-texto`);
                    input.value = id;
                    input.dispatchEvent(new Event("input", { bubbles: true }));
                }
            });
            await avanzar(page);
            assert.equal((await consultas(page)).length, 2);
            assert.equal(await page.locator("#filtro-opcion-0").textContent(), "Resultado filtroInformación adicional");
            assert.equal(await page.locator("#seleccion-opcion-0").textContent(), "Resultado seleccionInformación adicional");
            assert.deepEqual((await consultas(page)).map(consulta => consulta.grupo).sort(), ["filtro", "seleccion"]);
        });

        await probar("cancelación y descarte de una respuesta antigua aunque ignore abort", async page => {
            await plan(page, "antigua", { demora: 1000 });
            await escribir(page, "antigua");
            assert.equal(await page.locator("#filtro").getAttribute("data-estado"), "cargando");
            assert.equal(await page.locator("#filtro-texto").getAttribute("aria-busy"), "true");
            await escribir(page, "nueva");
            assert.equal((await consultas(page))[0].abortada, true);
            await avanzar(page, 1001);
            assert.equal(await page.locator("#filtro-opcion-0 .autocompletado-principal").textContent(), "Resultado nueva");
            assert.equal(await page.locator("#filtro-texto").getAttribute("aria-busy"), "false");
        });

        await probar("borrar el texto impide que una respuesta pendiente reabra la lista", async page => {
            await plan(page, "pendiente", { demora: 1000 });
            await escribir(page, "pendiente");
            await page.locator("#filtro-texto").fill("");
            await avanzar(page, 1001);
            assert.equal(await page.locator("#filtro-texto").getAttribute("aria-expanded"), "false");
            assert.equal(await page.locator("#filtro").getAttribute("data-estado"), "inicial");
        });

        await probar("Escape y Tab cierran sin seleccionar y cancelan búsquedas", async page => {
            await escribir(page, "pr");
            await page.locator("#filtro-texto").press("Escape");
            assert.equal(await page.locator("#filtro-texto").getAttribute("aria-expanded"), "false");
            assert.equal(await page.locator("#filtro-id").inputValue(), "");
            await plan(page, "lenta", { demora: 1000 });
            await escribir(page, "lenta");
            await page.locator("#filtro-texto").press("Tab");
            await avanzar(page, 1001);
            assert.equal(await page.locator("#filtro-texto").getAttribute("aria-expanded"), "false");
            assert.equal((await page.evaluate(() => window.prueba.selecciones)).length, 0);
        });

        await probar("resultado vacío y mensajes configurables", async page => {
            await plan(page, "vacio", { cuerpo: [] });
            await escribir(page, "vacio", "seleccion");
            assert.equal(await page.locator("#seleccion-estado").textContent(), "No hay sugerencias de prueba.");
            assert.equal(await page.locator("#seleccion-texto").getAttribute("aria-expanded"), "false");
        });

        for (const [nombre, configuracion] of [
            ["HTTP 500", { status: 500 }], ["respuesta HTML", { tipo: "text/html" }],
            ["JSON inválido", { jsonInvalido: true }], ["objeto en lugar de arreglo", { cuerpo: {} }],
            ["ID inválido", { cuerpo: [{ id: "1", textoPrincipal: "Prueba" }] }],
            ["texto inválido", { cuerpo: [{ id: 1, textoPrincipal: "" }] }],
            ["fallo de red", { red: true }], ["sesión vencida", { status: 401 }],
            ["sin permiso", { status: 403 }], ["redirección al login", { redireccion: true }]
        ]) {
            await probar(`manejo seguro: ${nombre}`, async page => {
                await plan(page, "error", configuracion);
                await escribir(page, "error");
                assert.equal(await page.locator("#filtro").getAttribute("data-estado"), "error");
                const mensaje = await page.locator("#filtro-estado").textContent();
                assert(mensaje.includes(configuracion.status === 401 || configuracion.status === 403 || configuracion.redireccion
                    ? "sesión" : "No se pudo"));
                assert.equal(await page.locator("#filtro-texto").getAttribute("aria-busy"), "false");
            });
        }

        await probar("límite de diez opciones y texto seguro contra XSS", async page => {
            await plan(page, "muchas", { cuerpo: Array.from({ length: 15 }, (_, indice) => ({
                id: indice + 1, textoPrincipal: `<img src=x onerror=alert(1)> ${indice}`,
                textoSecundario: "<script>alert(1)</script>"
            })) });
            await escribir(page, "muchas");
            assert.equal(await page.locator("#filtro-sugerencias [role=option]").count(), 10);
            assert.equal(await page.locator("#filtro-sugerencias img, #filtro-sugerencias script").count(), 0);
            assert((await page.locator("#filtro-opcion-0").textContent()).includes("<img"));
        });

        await probar("reset restaura valor e ID iniciales y descarta peticiones", async page => {
            await escribir(page, "otro", "seleccion");
            await page.locator("#seleccion-opcion-0").click({ force: true });
            assert.equal(await page.locator("#seleccion-id").inputValue(), "1");
            await page.evaluate(() => document.getElementById("form-seleccion").reset());
            assert.equal(await page.locator("#seleccion-texto").inputValue(), "Registro inicial");
            assert.equal(await page.locator("#seleccion-id").inputValue(), "42");
            assert.equal(await page.locator("#seleccion-texto").getAttribute("aria-expanded"), "false");
        });

        await probar("composición de texto no dispara consultas parciales", async page => {
            await page.locator("#filtro-texto").dispatchEvent("compositionstart");
            await page.locator("#filtro-texto").fill("mu");
            await avanzar(page, 600);
            assert.equal((await consultas(page)).length, 0);
            await page.locator("#filtro-texto").dispatchEvent("compositionend");
            await avanzar(page);
            assert.equal((await consultas(page)).length, 1);
        });

        await probar("pantalla pequeña, texto largo y lista desplazable", async page => {
            await page.setViewportSize({ width: 320, height: 640 });
            await plan(page, "movil", { cuerpo: Array.from({ length: 10 }, (_, indice) => ({
                id: indice + 1, textoPrincipal: "Nombre largo con acentos: María Muñoz Pérez ".repeat(2),
                textoSecundario: "Información adicional del registro"
            })) });
            await escribir(page, "movil");
            assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
            assert(await page.locator("#filtro-sugerencias").evaluate(lista => lista.scrollHeight > lista.clientHeight));
            assert((await page.locator("#filtro-sugerencias").boundingBox()).width <= 320);
            if (process.env.TALLER_AUTOCOMPLETADO_CAPTURA)
                await page.screenshot({ path: process.env.TALLER_AUTOCOMPLETADO_CAPTURA, fullPage: true });
        });

        console.log(`${aprobadas} escenarios de autocompletado aprobados; sin MySQL ni endpoints de producción.`);
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exit(1); });
