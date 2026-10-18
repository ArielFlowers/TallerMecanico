(() => {
    const formulario = document.getElementById("formulario-orden");
    if (!formulario) return;

    document.querySelectorAll(".nueva-orden-page form").forEach(destino => {
        destino.addEventListener("submit", () => {
            const producto = document.getElementById("orden-producto-id");
            if (producto) {
                producto.value ||= "0";
                formulario.elements.namedItem("ProductoId").value = producto.value;
                formulario.elements.namedItem("BuscarProducto").value = document.getElementById("orden-producto-texto").value;
            }
            for (const nombre of ["Formulario.VehiculoId", "Formulario.MecanicoId", "Formulario.ServicioId", "Formulario.Fecha", "Formulario.ClienteNuevoId", "BuscarVehiculo", "TextoServicio", "TextoMecanico"]) {
                const origen = formulario.elements.namedItem(nombre);
                const campo = destino.elements.namedItem(nombre);
                if (!origen || !campo) continue;
                const contenido = origen.value || (nombre === "Formulario.VehiculoId" || nombre === "Formulario.MecanicoId" ? "0" : "");
                campo.value = contenido;
            }
        });
    });

    const avisoVehiculo = document.getElementById("orden-vehiculo-aviso");
    const costoServicio = document.getElementById("orden-servicio-costo");
    const actualizarCosto = costo => {
        formulario.dataset.costoServicio = String(costo);
        formulario.dispatchEvent(new CustomEvent("orden:servicio-actualizado"));
    };
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
                actualizarCosto(0);
                costoServicio.textContent = "Selecciona un servicio para consultar su costo.";
                document.getElementById("orden-servicio-detalle").hidden = true;
            },
            mostrar(registro) {
                actualizarCosto(registro.costo);
                costoServicio.textContent = `Bs. ${Number(registro.costo).toFixed(2)} · ${registro.tiempoEstimadoHoras} h`;
                const detalle = document.getElementById("orden-servicio-detalle");
                detalle.querySelector("strong").textContent = document.getElementById("orden-servicio-texto").value;
                detalle.hidden = false;
            },
            error() { costoServicio.textContent = "No se pudo consultar el costo. Se verificará nuevamente al guardar."; }
        }
    };
    const peticiones = new Map();

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
