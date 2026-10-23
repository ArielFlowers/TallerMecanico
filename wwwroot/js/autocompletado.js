(() => {
    "use strict";

    const instancias = new WeakMap();

    class BuscadorAutocompletado {
        constructor(raiz) {
            this.raiz = raiz;
            this.input = raiz.querySelector("[data-autocompletado-texto]");
            this.campoId = raiz.querySelector("[data-autocompletado-id]");
            this.lista = raiz.querySelector("[data-autocompletado-sugerencias]");
            this.estado = raiz.querySelector("[data-autocompletado-estado]");
            this.configuracion = raiz.dataset;
            this.minimo = Number(this.configuracion.minimoCaracteres);
            this.espera = Number(this.configuracion.espera);
            this.maximo = Number(this.configuracion.maximoSugerencias);
            this.temporizador = null;
            this.peticion = null;
            this.version = 0;
            this.sugerencias = [];
            this.indiceActivo = -1;
            this.componiendo = false;
            this.seleccion = this.obtenerSeleccionInicial();
            if (this.campoId) this.campoId.value = this.seleccion ? String(this.seleccion.id) : "";
            this.escucharEventos();
            if (this.seleccion) this.mostrarEstado("seleccionado", "mensajeSeleccionado");
        }

        obtenerSeleccionInicial() {
            const id = Number(this.configuracion.idSeleccionado);
            return Number.isSafeInteger(id) && id > 0 && this.input.value.trim()
                ? { id, textoPrincipal: this.input.value, textoSecundario: "" }
                : null;
        }

        escucharEventos() {
            this.input.addEventListener("input", () => {
                if (!this.componiendo) this.programarBusqueda();
            });
            this.input.addEventListener("focus", () => {
                if (this.configuracion.mostrarAlEnfocar !== "true") return;
                if (this.seleccion) {
                    this.input.select();
                    this.programarBusqueda(true, "", true);
                } else {
                    this.programarBusqueda(false, "", true);
                }
            });
            this.input.addEventListener("compositionstart", () => {
                this.componiendo = true;
                this.cancelar();
                this.cerrar();
                this.sugerencias = [];
                this.lista.replaceChildren();
                this.invalidarSeleccion();
            });
            this.input.addEventListener("compositionend", () => {
                this.componiendo = false;
                this.programarBusqueda();
            });
            this.input.addEventListener("keydown", evento => this.navegar(evento));
            this.raiz.addEventListener("focusout", evento => {
                if (!this.raiz.contains(evento.relatedTarget)) {
                    this.cancelar();
                    this.cerrar();
                    this.mostrarEstado(this.seleccion ? "seleccionado" : "inicial",
                        this.seleccion ? "mensajeSeleccionado" : "mensajeInicial");
                }
            });
            this.lista.addEventListener("pointerdown", evento => {
                if (evento.button === 0) evento.preventDefault();
            });
            this.lista.addEventListener("click", evento => {
                const opcion = evento.target.closest("[data-indice]");
                if (opcion) this.seleccionar(Number(opcion.dataset.indice));
            });
            this.input.form?.addEventListener("reset", () => queueMicrotask(() => {
                this.cancelar();
                this.cerrar();
                this.sugerencias = [];
                this.lista.replaceChildren();
                this.seleccion = this.obtenerSeleccionInicial();
                if (this.campoId) this.campoId.value = this.seleccion ? String(this.seleccion.id) : "";
                this.mostrarEstado(this.seleccion ? "seleccionado" : "inicial",
                    this.seleccion ? "mensajeSeleccionado" : "mensajeInicial");
            }));
        }

        cancelar() {
            clearTimeout(this.temporizador);
            this.temporizador = null;
            this.peticion?.abort();
            this.peticion = null;
            this.version++;
            this.input.setAttribute("aria-busy", "false");
        }

        cerrar() {
            this.lista.hidden = true;
            this.indiceActivo = -1;
            this.input.setAttribute("aria-expanded", "false");
            this.input.removeAttribute("aria-activedescendant");
            this.lista.querySelectorAll("[aria-selected]").forEach(opcion =>
                opcion.setAttribute("aria-selected", "false"));
        }

        mostrarEstado(estado, mensaje) {
            this.raiz.dataset.estado = estado;
            this.estado.textContent = mensaje ? this.configuracion[mensaje] : "";
        }

        comunicar(nombre, detalle) {
            this.raiz.dispatchEvent(new CustomEvent(`autocompletado:${nombre}`, {
                bubbles: true,
                detail: { idInstancia: this.raiz.id, modo: this.configuracion.modo, ...detalle }
            }));
        }

        invalidarSeleccion() {
            const idAnterior = this.seleccion?.id;
            this.seleccion = null;
            if (this.campoId) this.campoId.value = "";
            if (idAnterior) this.comunicar("invalidacion", { idAnterior });
        }

        programarBusqueda(preservarSeleccion = false, terminoForzado = null, permitirVacio = false) {
            this.cancelar();
            this.cerrar();
            this.sugerencias = [];
            this.lista.replaceChildren();
            if (!preservarSeleccion) this.invalidarSeleccion();
            const termino = terminoForzado === null ? this.input.value.trim() : terminoForzado;
            if (!termino && !permitirVacio) {
                this.mostrarEstado("inicial", "mensajeInicial");
                return;
            }
            if (termino.length < this.minimo) {
                this.mostrarEstado("inicial", "mensajeInicial");
                return;
            }
            this.mostrarEstado("esperando");
            const version = this.version;
            this.temporizador = setTimeout(() => this.buscar(termino, version), this.espera);
        }

        async buscar(termino, version) {
            const peticion = new AbortController();
            this.peticion = peticion;
            this.temporizador = null;
            this.mostrarEstado("cargando", "mensajeCargando");
            this.input.setAttribute("aria-busy", "true");
            try {
                const url = new URL(this.configuracion.url, document.baseURI);
                if (url.origin !== location.origin) throw new Error("mensajeError");
                url.searchParams.set("termino", termino);
                const respuesta = await fetch(url, {
                    method: "GET", credentials: "same-origin", cache: "no-store",
                    headers: { "Accept": "application/json", "X-Requested-With": "XMLHttpRequest" },
                    signal: peticion.signal
                });
                if (respuesta.redirected || respuesta.status === 401 || respuesta.status === 403)
                    throw new Error("mensajeSesion");
                const tipo = respuesta.headers.get("Content-Type")?.split(";")[0].trim().toLowerCase();
                if (!respuesta.ok || tipo !== "application/json") throw new Error("mensajeError");
                const sugerencias = this.validarRespuesta(await respuesta.json());
                if (version !== this.version || peticion.signal.aborted) return;
                this.mostrarSugerencias(sugerencias);
            } catch (error) {
                if (version !== this.version || peticion.signal.aborted) return;
                this.mostrarEstado("error", error instanceof Error && error.message === "mensajeSesion" ? "mensajeSesion" : "mensajeError");
            } finally {
                if (version === this.version) {
                    this.peticion = null;
                    this.input.setAttribute("aria-busy", "false");
                }
            }
        }

        validarRespuesta(respuesta) {
            if (!Array.isArray(respuesta)) throw new Error("mensajeError");
            const ids = new Set();
            return respuesta.slice(0, this.maximo).map(sugerencia => {
                if (!sugerencia || !Number.isSafeInteger(sugerencia.id) || sugerencia.id <= 0 ||
                    ids.has(sugerencia.id) || typeof sugerencia.textoPrincipal !== "string" ||
                    !sugerencia.textoPrincipal.trim() ||
                    (sugerencia.textoSecundario != null && typeof sugerencia.textoSecundario !== "string")) {
                    throw new Error("mensajeError");
                }
                ids.add(sugerencia.id);
                const normalizada = { id: sugerencia.id, textoPrincipal: sugerencia.textoPrincipal,
                    textoSecundario: sugerencia.textoSecundario ?? "" };
                if (sugerencia.stock !== undefined) {
                    if (!Number.isSafeInteger(sugerencia.stock) || sugerencia.stock < 0) throw new Error("mensajeError");
                    normalizada.stock = sugerencia.stock;
                }
                return normalizada;
            });
        }

        mostrarSugerencias(sugerencias) {
            this.sugerencias = sugerencias;
            this.lista.replaceChildren();
            if (sugerencias.length === 0) {
                this.mostrarEstado("vacio", "mensajeVacio");
                return;
            }
            sugerencias.forEach((sugerencia, indice) => {
                const opcion = document.createElement("li");
                opcion.id = `${this.raiz.id}-opcion-${indice}`;
                opcion.className = "autocompletado-opcion";
                opcion.dataset.indice = indice;
                opcion.setAttribute("role", "option");
                opcion.setAttribute("aria-selected", "false");
                const principal = document.createElement("span");
                principal.className = "autocompletado-principal";
                principal.textContent = sugerencia.textoPrincipal;
                opcion.append(principal);
                if (sugerencia.textoSecundario) {
                    const secundario = document.createElement("span");
                    secundario.className = "autocompletado-secundario";
                    secundario.textContent = sugerencia.textoSecundario;
                    opcion.append(secundario);
                }
                this.lista.append(opcion);
            });
            this.lista.hidden = false;
            this.input.setAttribute("aria-expanded", "true");
            this.raiz.dataset.estado = "resultados";
            this.estado.textContent = this.configuracion.mensajeResultados.replace("{cantidad}", sugerencias.length);
        }

        activar(indice) {
            this.indiceActivo = indice;
            this.lista.hidden = false;
            this.input.setAttribute("aria-expanded", "true");
            [...this.lista.children].forEach((opcion, posicion) =>
                opcion.setAttribute("aria-selected", String(posicion === indice)));
            const opcion = this.lista.children[indice];
            this.input.setAttribute("aria-activedescendant", opcion.id);
            opcion.scrollIntoView({ block: "nearest" });
        }

        navegar(evento) {
            if (evento.isComposing || this.componiendo) return;
            if ((evento.key === "ArrowDown" || evento.key === "ArrowUp") && this.sugerencias.length) {
                evento.preventDefault();
                const cantidad = this.sugerencias.length;
                const siguiente = evento.key === "ArrowDown"
                    ? (this.indiceActivo + 1) % cantidad
                    : (this.indiceActivo < 0 ? cantidad - 1 : (this.indiceActivo - 1 + cantidad) % cantidad);
                this.activar(siguiente);
            } else if (evento.key === "Enter" && !this.lista.hidden && this.indiceActivo >= 0) {
                evento.preventDefault();
                this.seleccionar(this.indiceActivo);
            } else if (evento.key === "Escape" && (!this.lista.hidden || this.peticion || this.temporizador)) {
                evento.preventDefault();
                evento.stopPropagation();
                this.cancelar();
                this.cerrar();
                this.mostrarEstado("inicial");
            }
        }

        seleccionar(indice) {
            const sugerencia = this.sugerencias[indice];
            if (!sugerencia) return;
            this.cancelar();
            this.seleccion = sugerencia;
            this.input.value = sugerencia.textoPrincipal;
            if (this.campoId) this.campoId.value = String(sugerencia.id);
            this.cerrar();
            this.mostrarEstado("seleccionado", "mensajeSeleccionado");
            this.comunicar("seleccion", { sugerencia: { ...sugerencia } });
        }
    }

    const inicializar = (contenedor = document) => {
        const raices = [...contenedor.querySelectorAll("[data-autocompletado]")];
        if (contenedor.matches?.("[data-autocompletado]")) raices.unshift(contenedor);
        raices.forEach(raiz => {
            if (!instancias.has(raiz)) instancias.set(raiz, new BuscadorAutocompletado(raiz));
        });
    };

    window.AutoTallerAutocompletado = Object.freeze({ inicializar });
    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", () => inicializar(), { once: true });
    else inicializar();
})();
