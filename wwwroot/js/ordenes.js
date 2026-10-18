document.addEventListener("DOMContentLoaded", () => {
    const actualizarResumen = () => {
        let cantidadTotal = 0;
        let total = 0;
        const filas = [...document.querySelectorAll('[data-orden-linea]')];
        for (const fila of filas) {
            const cantidad = Math.max(0, Number(fila.querySelector('[data-cantidad]')?.value || 0));
            const precio = Number(fila.dataset.precio || 0);
            cantidadTotal += cantidad;
            total += cantidad * precio;
            const subtotal = fila.querySelector('[data-subtotal]');
            if (subtotal) subtotal.textContent = `Bs. ${(cantidad * precio).toFixed(2)}`;
        }
        document.getElementById('resumen-lineas')?.replaceChildren(String(filas.length));
        document.getElementById('resumen-cantidad')?.replaceChildren(String(cantidadTotal));
        const servicio = Number(document.getElementById('formulario-orden')?.dataset.costoServicio || 0);
        const totalEl = document.getElementById('resumen-total');
        if (totalEl) {
            totalEl.dataset.costoServicio = String(servicio);
            totalEl.textContent = `Bs. ${(total + servicio).toFixed(2)}`;
        }
        document.querySelectorAll('[data-total-estimado]').forEach(elemento => elemento.textContent = `Bs. ${(total + servicio).toFixed(2)}`);
    };
    document.querySelectorAll('[data-cantidad]').forEach(input => input.addEventListener('input', actualizarResumen));
    document.getElementById('formulario-orden')?.addEventListener('orden:servicio-actualizado', actualizarResumen);
});

document.addEventListener("DOMContentLoaded", () => {
    const dialog = document.getElementById("orden-detalle-dialog");
    const contenido = document.getElementById("orden-detalle-contenido");
    const estado = document.getElementById("orden-detalle-estado");
    const cerrar = document.querySelector("[data-cerrar-detalle]");

    if (!(dialog instanceof HTMLDialogElement) || !contenido || !estado || !cerrar) return;

    let consultaPendiente = null;
    let botonOrigen = null;

    const abrirDetalle = async boton => {
        consultaPendiente?.abort();
        const consultaActual = new AbortController();
        consultaPendiente = consultaActual;
        botonOrigen = boton;
        contenido.replaceChildren();
        contenido.setAttribute("aria-busy", "true");
        estado.textContent = "Cargando detalles de la orden…";
        estado.hidden = false;
        if (!dialog.open) dialog.showModal();

        try {
            const respuesta = await fetch(boton.dataset.detalleUrl, {
                method: "GET",
                credentials: "same-origin",
                headers: { "X-Requested-With": "XMLHttpRequest" },
                signal: consultaActual.signal
            });

            if (respuesta.redirected || respuesta.status === 401 || respuesta.status === 403) {
                throw new Error("Tu sesión venció o no tienes permiso para consultar órdenes.");
            }
            if (respuesta.status === 404) throw new Error("La orden seleccionada no existe.");
            if (respuesta.status === 400) throw new Error("Selecciona una orden válida.");
            if (!respuesta.ok || !respuesta.headers.get("Content-Type")?.includes("text/html")) {
                throw new Error("No se pudo consultar la orden. Intenta nuevamente.");
            }

            const detalleHtml = await respuesta.text();
            if (consultaPendiente !== consultaActual || !dialog.open) return;

            // La parcial del mismo servidor codifica los datos mediante Razor.
            contenido.innerHTML = detalleHtml;
            estado.textContent = "";
            estado.hidden = true;
        } catch (error) {
            if (consultaActual.signal.aborted || consultaPendiente !== consultaActual || !dialog.open) return;
            estado.textContent = error instanceof TypeError
                ? "No se pudo conectar. Intenta consultar la orden nuevamente."
                : error.message;
        } finally {
            if (consultaPendiente === consultaActual) contenido.setAttribute("aria-busy", "false");
        }
    };

    document.querySelectorAll("[data-ver-orden]").forEach(boton => {
        boton.addEventListener("click", () => abrirDetalle(boton));
    });

    cerrar.addEventListener("click", () => dialog.close());
    dialog.addEventListener("close", () => {
        consultaPendiente?.abort();
        consultaPendiente = null;
        contenido.replaceChildren();
        contenido.setAttribute("aria-busy", "false");
        estado.textContent = "";
        botonOrigen?.focus();
    });
});
