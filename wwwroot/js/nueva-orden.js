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

    function actualizarServiciosSeleccionados() {
        const seleccionados = obtenerServicios();
        const costo = seleccionados.reduce((suma, fila) => suma + Number(fila.dataset.costo || 0), 0);
        formulario.dataset.costoServicio = String(costo);
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
                nombre.textContent = fila.querySelector("[data-servicio-nombre]").textContent;
                const precio = document.createElement("strong");
                precio.textContent = `Bs. ${Number(fila.dataset.costo).toFixed(2)}`;
                item.append(nombre, precio);
                return item;
            }));
            if (!resumen) document.querySelector(".ordenes-servicios-resumen").append(lista);
        }
        formulario.dispatchEvent(new CustomEvent("orden:servicio-actualizado"));
    }

    function crearFilaServicio(servicio) {
        const fila = document.createElement("article");
        fila.className = "ordenes-servicio-seleccionado";
        fila.dataset.servicioSeleccionado = "";
        fila.dataset.servicioId = String(servicio.id);
        fila.dataset.costo = String(servicio.costo);

        const hidden = document.createElement("input");
        hidden.type = "hidden";
        hidden.name = "Formulario.ServicioIds";
        hidden.value = String(servicio.id);

        const datos = document.createElement("div");
        const nombre = document.createElement("strong");
        nombre.dataset.servicioNombre = "";
        nombre.textContent = servicio.nombre;
        const costoTiempo = document.createElement("span");
        costoTiempo.textContent = `Bs. ${Number(servicio.costo).toFixed(2)} · ${servicio.tiempoEstimadoHoras} h`;
        datos.append(nombre, costoTiempo);

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
            tiempoEstimadoHoras: Number(selectorServicio.dataset.servicioPendienteTiempo)
        };
    }

    document.querySelectorAll(".nueva-orden-page form").forEach(destino => {
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
                for (const servicio of obtenerServicios()) {
                    const oculto = document.createElement("input");
                    oculto.type = "hidden";
                    oculto.name = "Formulario.ServicioIds";
                    oculto.value = servicio.dataset.servicioId;
                    destino.append(oculto);
                }
            }
        });
    });

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
                    tiempoEstimadoHoras: registro.tiempoEstimadoHoras
                };
                estadoServicio.textContent = `Bs. ${Number(registro.costo).toFixed(2)} · ${registro.tiempoEstimadoHoras} h`;
                botonAgregarServicio.disabled = false;
                mensajeServicio.textContent = "Servicio seleccionado. Pulsa Agregar servicio para incluirlo.";
            },
            error() {
                servicioPendiente = null;
                estadoServicio.textContent = "No se pudo consultar el costo. Vuelve a seleccionar el servicio.";
                botonAgregarServicio.disabled = true;
            }
        }
    };

    async function cargarDependencia(raiz, sugerencia) {
        const dependencia = dependencias[raiz.id];
        if (!dependencia) return;
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

    document.querySelectorAll(".nueva-orden-page [data-autocompletado]").forEach(raiz => {
        raiz.addEventListener("autocompletado:seleccion", evento => cargarDependencia(raiz, evento.detail.sugerencia));
        raiz.addEventListener("autocompletado:invalidacion", () => {
            peticiones.get(raiz.id)?.abort();
            dependencias[raiz.id]?.limpiar();
        });
    });
})();
