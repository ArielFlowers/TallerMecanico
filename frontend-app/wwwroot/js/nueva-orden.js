(() => {
    const formulario = document.getElementById("formulario-orden");
    if (!formulario) return;

    const selectorServicio = document.querySelector(".ordenes-servicios-selector");
    const campoServicioId = document.getElementById("orden-servicio-id");
    const campoServicioTexto = document.getElementById("orden-servicio-texto");
    const estadoServicio = document.getElementById("orden-servicio-costo");
    const mensajeServicio = document.getElementById("orden-servicio-mensaje");
    const contenedorServicios = document.getElementById("orden-servicios-seleccionados");
    const botonAgregarServicio = document.getElementById("orden-agregar-servicio");
    let servicioPendiente = null;

    const obtenerServicios = () => [...contenedorServicios.querySelectorAll("[data-servicio-seleccionado]")];

    function cantidadServicio(fila) {
        const campo = fila.querySelector("[data-cantidad-servicio]");
        if (!fila.dataset.unidadCantidad) return 1;
        const cantidad = Number(campo.value);
        return campo.value !== "" && Number.isInteger(cantidad) && cantidad >= 1 && cantidad <= 1000 ? cantidad : null;
    }

    function actualizarServiciosSeleccionados() {
        const seleccionados = obtenerServicios();
        const validas = seleccionados.every(fila => cantidadServicio(fila) !== null);
        const costo = seleccionados.reduce((suma, fila) => suma + Number(fila.dataset.costo) * (cantidadServicio(fila) ?? 0), 0);
        formulario.dataset.cantidadesServiciosValidas = String(validas);
        formulario.dataset.costoServicio = String(costo);
        for (const fila of seleccionados) {
            const cantidad = cantidadServicio(fila);
            const campo = fila.querySelector("[data-cantidad-servicio]");
            if (!fila.dataset.unidadCantidad) campo.value = "1";
            campo.setAttribute("aria-invalid", String(cantidad === null));
            fila.querySelector("[data-servicio-subtotal]").textContent = cantidad === null
                ? "Cantidad inválida"
                : `Subtotal: Bs. ${(Number(fila.dataset.costo) * cantidad).toFixed(2)}`;
        }
        document.getElementById("orden-servicios-cantidad").textContent = String(seleccionados.length);

        const resumen = document.getElementById("orden-servicios-resumen-lista");
        if (seleccionados.length === 0) {
            resumen?.remove();
            if (!document.getElementById("orden-servicios-vacio")) {
                const vacio = document.createElement("p");
                vacio.id = "orden-servicios-vacio";
                vacio.textContent = "Agrega uno o más servicios para completar la orden.";
                document.querySelector(".ordenes-servicios-resumen").append(vacio);
            }
        } else {
            document.getElementById("orden-servicios-vacio")?.remove();
            const lista = resumen ?? document.createElement("ul");
            lista.id = "orden-servicios-resumen-lista";
            lista.replaceChildren(...seleccionados.map(fila => {
                const item = document.createElement("li");
                item.dataset.servicioResumen = fila.dataset.servicioId;
                const nombre = document.createElement("span");
                const cantidad = cantidadServicio(fila);
                const unidad = fila.dataset.unidadCantidad;
                nombre.textContent = `${fila.querySelector("[data-servicio-nombre]").textContent}${unidad && cantidad !== null ? ` · ${cantidad} ${plural(unidad, cantidad)}` : ""}`;
                const precio = document.createElement("strong");
                precio.textContent = cantidad === null ? "Cantidad inválida" : `Bs. ${(Number(fila.dataset.costo) * cantidad).toFixed(2)}`;
                item.append(nombre, precio);
                return item;
            }));
            if (!resumen) document.querySelector(".ordenes-servicios-resumen").append(lista);
        }
        formulario.dispatchEvent(new CustomEvent("orden:servicio-actualizado"));
    }

    function plural(unidad, cantidad) {
        if (cantidad === 1) return unidad;
        return unidad === "llanta" ? "llantas" : unidad === "retrovisor" ? "retrovisores" : "unidades";
    }

    function crearFilaServicio(servicio) {
        const fila = document.createElement("article");
        fila.className = "ordenes-servicio-seleccionado";
        fila.dataset.servicioSeleccionado = "";
        fila.dataset.servicioId = String(servicio.id);
        fila.dataset.costo = String(servicio.costo);
        fila.dataset.unidadCantidad = servicio.unidadCantidad || "";

        const hidden = document.createElement("input");
        hidden.type = "hidden";
        hidden.name = "Formulario.ServicioIds";
        hidden.value = String(servicio.id);

        const cantidad = document.createElement("input");
        cantidad.type = servicio.unidadCantidad ? "number" : "hidden";
        cantidad.name = `Formulario.CantidadesServicios[${servicio.id}]`;
        cantidad.value = "1";
        cantidad.min = "1";
        cantidad.max = "1000";
        cantidad.step = "1";
        cantidad.dataset.cantidadServicio = "";
        if (servicio.unidadCantidad) {
            cantidad.required = true;
            const label = document.createElement("label");
            label.className = "ordenes-cantidad-servicio";
            label.append(document.createTextNode(`Cantidad de ${plural(servicio.unidadCantidad, 2)}`));
            cantidad.dataset.cantidadVisible = "";
            label.append(cantidad);
            fila.append(label);
        } else {
            fila.append(cantidad);
        }

        const datos = document.createElement("div");
        const nombre = document.createElement("strong");
        nombre.dataset.servicioNombre = "";
        nombre.textContent = servicio.nombre;
        const costoTiempo = document.createElement("span");
        costoTiempo.dataset.servicioPrecio = "";
        costoTiempo.textContent = `Precio unitario: Bs. ${Number(servicio.costo).toFixed(2)} · ${servicio.tiempoEstimadoHoras} h`;
        datos.append(nombre, costoTiempo);
        if (!servicio.unidadCantidad) {
            const fija = document.createElement("span");
            fija.textContent = "Cantidad fija: 1";
            datos.append(fija);
        }
        const subtotal = document.createElement("strong");
        subtotal.className = "ordenes-servicio-subtotal";
        subtotal.dataset.servicioSubtotal = "";
        datos.append(subtotal);

        const quitar = document.createElement("button");
        quitar.type = "button";
        quitar.className = "services-btn services-btn-secondary";
        quitar.dataset.quitarServicio = "";
        quitar.textContent = "Quitar";
        quitar.setAttribute("aria-label", `Quitar ${servicio.nombre}`);
        fila.append(hidden, datos, quitar);
        return fila;
    }

    function limpiarSelectorServicio() {
        servicioPendiente = null;
        campoServicioTexto.value = "";
        campoServicioTexto.dispatchEvent(new Event("input", { bubbles: true }));
    }

    selectorServicio.addEventListener("click", evento => {
        const botonQuitar = evento.target.closest("[data-quitar-servicio]");
        if (botonQuitar) {
            botonQuitar.closest("[data-servicio-seleccionado]")?.remove();
            actualizarServiciosSeleccionados();
            return;
        }

        if (!evento.target.closest("#orden-agregar-servicio")) return;
        const id = Number(campoServicioId.value);
        if (!servicioPendiente || servicioPendiente.id !== id) {
            mensajeServicio.textContent = "Busca y selecciona un servicio antes de agregarlo.";
            return;
        }
        if (obtenerServicios().some(fila => Number(fila.dataset.servicioId) === id)) {
            mensajeServicio.textContent = "Ese servicio ya está agregado a la orden.";
            return;
        }

        contenedorServicios.append(crearFilaServicio(servicioPendiente));
        mensajeServicio.textContent = `${servicioPendiente.nombre} se agregó a la orden.`;
        actualizarServiciosSeleccionados();
        limpiarSelectorServicio();
        botonAgregarServicio.disabled = true;
    });

    const inicial = Number(selectorServicio.dataset.servicioPendienteId);
    if (inicial > 0) {
        servicioPendiente = {
            id: inicial,
            nombre: selectorServicio.dataset.servicioPendienteNombre,
            costo: Number(selectorServicio.dataset.servicioPendienteCosto),
            tiempoEstimadoHoras: Number(selectorServicio.dataset.servicioPendienteTiempo),
            unidadCantidad: selectorServicio.dataset.servicioPendienteUnidad || null
        };
    }

    const formulariosConectados = new WeakSet();
    function conectarFormulario(destino) {
        if (formulariosConectados.has(destino)) return;
        formulariosConectados.add(destino);
        destino.addEventListener("submit", () => {
            const producto = document.getElementById("orden-producto-id");
            if (producto) {
                producto.value ||= "0";
                formulario.elements.namedItem("ProductoId").value = producto.value;
                formulario.elements.namedItem("BuscarProducto").value = document.getElementById("orden-producto-texto").value;
            }
            for (const nombre of ["Formulario.VehiculoId", "Formulario.MecanicoId", "Formulario.ServicioId", "Formulario.ClienteNuevoId", "BuscarVehiculo", "TextoServicio", "TextoMecanico"]) {
                const origen = formulario.elements.namedItem(nombre);
                const campo = destino.elements.namedItem(nombre);
                if (!origen || !campo) continue;
                const contenido = origen.value || (nombre === "Formulario.VehiculoId" || nombre === "Formulario.MecanicoId" ? "0" : "");
                campo.value = contenido;
            }
            if (destino !== formulario) {
                destino.querySelectorAll('[name="Formulario.ServicioIds"]').forEach(campo => campo.remove());
                destino.querySelectorAll('[name^="Formulario.CantidadesServicios"]').forEach(campo => campo.remove());
                for (const servicio of obtenerServicios()) {
                    const oculto = document.createElement("input");
                    oculto.type = "hidden";
                    oculto.name = "Formulario.ServicioIds";
                    oculto.value = servicio.dataset.servicioId;
                    destino.append(oculto);
                    const cantidad = document.createElement("input");
                    cantidad.type = "hidden";
                    cantidad.name = `Formulario.CantidadesServicios[${servicio.dataset.servicioId}]`;
                    cantidad.value = servicio.querySelector("[data-cantidad-servicio]")?.value || "1";
                    destino.append(cantidad);
                }
            }
            if (destino.matches("[data-preservar-scroll]")) {
                const scroll = Math.max(window.scrollY, document.documentElement.scrollTop);
                destino.querySelectorAll("[data-scroll-y]").forEach(campo => campo.value = String(scroll));
                const token = destino.elements.namedItem("Formulario.Token")?.value || "nuevo";
                try { sessionStorage.setItem(`orden-scroll:${token}`, String(scroll)); } catch { /* almacenamiento opcional */ }
            }
        });
    }
    document.querySelectorAll(".nueva-orden-page form").forEach(conectarFormulario);

    document.querySelectorAll("[data-cambiar-eleccion]").forEach(boton => {
        boton.addEventListener("click", () => {
            const raiz = document.getElementById(boton.dataset.cambiarEleccion);
            const campo = raiz?.querySelector("[data-autocompletado-texto]");
            if (!campo) return;
            campo.value = "";
            campo.dispatchEvent(new Event("input", { bubbles: true }));
            campo.focus();
        });
    });

    document.querySelectorAll("#orden-vehiculo, #orden-servicio, #orden-mecanico").forEach(raiz => {
        const tarjeta = document.getElementById(`${raiz.id}-elegido`);
        raiz.addEventListener("autocompletado:seleccion", evento => {
            const registro = evento.detail.sugerencia;
            tarjeta.hidden = false;
            tarjeta.querySelector("[data-elegido-principal]").textContent = registro.textoPrincipal;
            const secundario = tarjeta.querySelector("[data-elegido-secundario]");
            secundario.textContent = registro.textoSecundario || "";
        });
        raiz.addEventListener("autocompletado:invalidacion", () => { tarjeta.hidden = true; });
    });

    contenedorServicios.addEventListener("input", evento => {
        if (evento.target.matches("[data-cantidad-visible]")) actualizarServiciosSeleccionados();
    });

    const scrollQuery = Number(formulario.dataset.restaurarScroll || 0);
    const tokenFormulario = formulario.elements.namedItem("Formulario.Token")?.value || "nuevo";
    let scrollGuardado = 0;
    try {
        scrollGuardado = Number(sessionStorage.getItem(`orden-scroll:${tokenFormulario}`) || 0);
        sessionStorage.removeItem(`orden-scroll:${tokenFormulario}`);
    } catch { /* almacenamiento opcional */ }
    const scrollRestaurar = Math.max(scrollQuery, scrollGuardado);
    if (scrollRestaurar > 0) {
        history.scrollRestoration = "manual";
        const restaurar = () => window.scrollTo({ top: scrollRestaurar, left: 0, behavior: "instant" });
        requestAnimationFrame(() => requestAnimationFrame(restaurar));
        window.addEventListener("load", () => setTimeout(restaurar, 80), { once: true });
    }

    const avisoVehiculo = document.getElementById("orden-vehiculo-aviso");
    const peticiones = new Map();
    const dependencias = {
        "orden-vehiculo": {
            url: formulario.dataset.vehiculoUrl,
            limpiar() { avisoVehiculo.hidden = true; },
            mostrar(registro) {
                avisoVehiculo.textContent = "El vehículo requiere un cliente antes de guardar la orden.";
                avisoVehiculo.hidden = !registro.requiereCliente;
            },
            error() { avisoVehiculo.textContent = "No se pudo consultar el vehículo. Vuelve a seleccionarlo."; avisoVehiculo.hidden = false; }
        },
        "orden-servicio": {
            url: formulario.dataset.servicioUrl,
            limpiar() {
                servicioPendiente = null;
                estadoServicio.textContent = "Busca y selecciona un servicio para agregarlo.";
                botonAgregarServicio.disabled = true;
            },
            mostrar(registro) {
                servicioPendiente = {
                    id: registro.id,
                    nombre: registro.nombre,
                    costo: registro.costo,
                    tiempoEstimadoHoras: registro.tiempoEstimadoHoras,
                    unidadCantidad: registro.unidadCantidad
                };
                estadoServicio.textContent = `Bs. ${Number(registro.costo).toFixed(2)} por ${registro.unidadCantidad || "trabajo"} · ${registro.tiempoEstimadoHoras} h`;
                const tarjeta = document.getElementById("orden-servicio-elegido");
                tarjeta.hidden = false;
                tarjeta.querySelector("[data-elegido-principal]").textContent = registro.nombre;
                tarjeta.querySelector("[data-elegido-secundario]").textContent = `Bs. ${Number(registro.costo).toFixed(2)} · ${registro.tiempoEstimadoHoras} h`;
                botonAgregarServicio.disabled = false;
                mensajeServicio.textContent = "";
            },
            error() {
                servicioPendiente = null;
                estadoServicio.textContent = "No se pudo consultar el costo. Vuelve a seleccionar el servicio.";
                botonAgregarServicio.disabled = true;
            }
        }
    };
    dependencias["orden-producto"] = {
        limpiar() {
            const campo = document.querySelector("#orden-agregar-producto [name='Cantidad']");
            if (campo) { campo.removeAttribute("max"); campo.disabled = true; }
            document.getElementById("orden-boton-agregar-repuesto").disabled = true;
            const estado = document.getElementById("orden-producto-stock");
            estado.dataset.estadoStock = "inicial";
            estado.textContent = "El stock se mostrará aquí al seleccionar un producto.";
        },
        mostrar(registro) {
            const campo = document.querySelector("#orden-agregar-producto [name='Cantidad']");
            const stock = Number(registro.stock);
            campo.max = String(stock);
            campo.disabled = stock <= 0;
            campo.value = stock > 0 ? String(Math.min(Number(campo.value || 1), stock)) : "1";
            document.getElementById("orden-boton-agregar-repuesto").disabled = stock <= 0;
            const estado = document.getElementById("orden-producto-stock");
            estado.dataset.estadoStock = stock > 0 ? "disponible" : "agotado";
            estado.textContent = stock > 0
                ? `Disponibles: ${registro.stock} unidades.`
                : `No hay stock disponible de ${registro.nombre}.`;
        },
        error() {
            const estado = document.getElementById("orden-producto-stock");
            estado.dataset.estadoStock = "error";
            estado.textContent = "No se pudo cargar el stock. Vuelve a elegir el producto.";
        }
    };

    document.addEventListener("input", evento => {
        if (evento.target.matches("#orden-agregar-producto [name='Cantidad']")) {
            const cantidad = Number(evento.target.value);
            const maximo = Number(evento.target.max);
            document.getElementById("orden-boton-agregar-repuesto").disabled =
                !Number.isInteger(cantidad) || cantidad < 1 || !Number.isFinite(maximo) || cantidad > maximo;
        }
    });

    async function cargarDependencia(raiz, sugerencia) {
        const dependencia = dependencias[raiz.id];
        if (!dependencia) return;
        if (raiz.id === "orden-producto") {
            dependencia.limpiar();
            if (Number.isSafeInteger(sugerencia.stock)) {
                dependencia.mostrar({ ...sugerencia, nombre: sugerencia.textoPrincipal });
            } else {
                dependencia.error();
            }
            return;
        }
        peticiones.get(raiz.id)?.abort();
        const peticion = new AbortController();
        peticiones.set(raiz.id, peticion);
        dependencia.limpiar();
        const sigueSeleccionado = () => peticiones.get(raiz.id) === peticion &&
            !peticion.signal.aborted && raiz.querySelector("[data-autocompletado-id]").value === String(sugerencia.id);
        try {
            const url = new URL(dependencia.url, document.baseURI);
            url.searchParams.set("id", sugerencia.id);
            const respuesta = await fetch(url, {
                credentials: "same-origin", cache: "no-store", signal: peticion.signal,
                headers: { "Accept": "application/json", "X-Requested-With": "XMLHttpRequest" }
            });
            if (!respuesta.ok || respuesta.redirected || !respuesta.headers.get("Content-Type")?.includes("application/json")) throw new Error();
            const registro = await respuesta.json();
            if (sigueSeleccionado() && registro.id === sugerencia.id) dependencia.mostrar(registro);
        } catch {
            if (sigueSeleccionado()) dependencia.error();
        }
    }

    const selectoresConectados = new WeakSet();
    function conectarSelectores(contenedor = document) {
        const raices = [...contenedor.querySelectorAll(".nueva-orden-page [data-autocompletado]")];
        if (contenedor.matches?.(".nueva-orden-page [data-autocompletado]")) raices.unshift(contenedor);
        raices.forEach(raiz => {
            if (selectoresConectados.has(raiz)) return;
            selectoresConectados.add(raiz);
            raiz.addEventListener("autocompletado:seleccion", evento => cargarDependencia(raiz, evento.detail.sugerencia));
            raiz.addEventListener("autocompletado:invalidacion", () => {
                peticiones.get(raiz.id)?.abort();
                dependencias[raiz.id]?.limpiar();
            });
        });
    }
    conectarSelectores();

    document.addEventListener("change", evento => {
        const campo = evento.target.closest("[data-cantidad-original]");
        if (campo) actualizarBotonCantidad(campo);
    });

    document.addEventListener("input", evento => {
        const campo = evento.target.closest("[data-cantidad-original]");
        if (campo) actualizarBotonCantidad(campo);
    });

    function actualizarBotonCantidad(campo) {
        const boton = campo.form?.querySelector(".ordenes-guardar-cantidad");
        if (!boton) return;
        const cantidad = Number(campo.value);
        const maximo = Number(campo.max);
        const valida = Number.isInteger(cantidad) && cantidad >= 1 && (!Number.isFinite(maximo) || cantidad <= maximo);
        boton.disabled = !valida || String(cantidad) === campo.dataset.cantidadOriginal;
    }

    document.addEventListener("DOMContentLoaded", actualizarServiciosSeleccionados, { once: true });
})();
