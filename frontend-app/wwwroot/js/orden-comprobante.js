(() => {
    const claveReserva = "orden:comprobante-reservado";
    const duracionReserva = 120000;

    if (document.body.hasAttribute("data-comprobante-espera")) {
        const reserva = location.hash.slice(1);
        const limite = Date.now() + duracionReserva;
        const espera = window.setInterval(() => {
            try {
                if (!reserva || !window.opener || window.opener.closed || Date.now() >= limite) {
                    window.clearInterval(espera);
                    window.close();
                    return;
                }
                const origen = window.opener.document;
                if (origen.readyState !== "complete" ||
                    origen.getElementById("formulario-orden")?.dataset.comprobanteReserva === reserva) return;

                const enlace = origen.querySelector("[data-comprobante-creado]");
                window.clearInterval(espera);
                if (enlace && new URL(enlace.href).origin === location.origin) {
                    window.opener = null;
                    window.location.replace(enlace.href);
                } else window.close();
            } catch {
                window.clearInterval(espera);
                window.close();
            }
        }, 100);
        return;
    }

    let reservaAnterior = false;
    try {
        const reserva = JSON.parse(sessionStorage.getItem(claveReserva) || "null");
        reservaAnterior = reserva && Date.now() - reserva.fecha < duracionReserva;
        sessionStorage.removeItem(claveReserva);
    } catch { /* El enlace manual sigue disponible sin almacenamiento. */ }

    function reservarComprobante(formulario) {
        try {
            const id = crypto.randomUUID();
            sessionStorage.setItem(claveReserva, JSON.stringify({ fecha: Date.now() }));
            formulario.dataset.comprobanteReserva = id;
            const pestaña = window.open(`${formulario.dataset.comprobanteEsperaUrl}#${id}`, "_blank");
            if (pestaña) return;
        } catch { /* El navegador puede bloquear ventanas o almacenamiento. */ }
        try { sessionStorage.removeItem(claveReserva); } catch { /* Almacenamiento opcional. */ }
        delete formulario.dataset.comprobanteReserva;
    }

    const dialogoAnulacion = document.getElementById("anular-orden-dialog");
    const botonConfirmarAnulacion = dialogoAnulacion?.querySelector("[data-confirmar-anulacion]");
    const botonCancelarAnulacion = dialogoAnulacion?.querySelector("[data-cancelar-anulacion]");
    const descripcionAnulacion = dialogoAnulacion?.querySelector("#anular-orden-descripcion");
    let formularioAnulacion = null;
    let anulacionConfirmada = false;

    document.addEventListener("submit", evento => {
        const formulario = evento.target;
        if (!(formulario instanceof HTMLFormElement) || !formulario.matches("[data-confirmar-anulacion]")) return;
        if (anulacionConfirmada) {
            anulacionConfirmada = false;
            return;
        }

        evento.preventDefault();
        formularioAnulacion = formulario;
        if (descripcionAnulacion) {
            descripcionAnulacion.textContent = "¿Deseas anular la orden seleccionada?";
        }
        dialogoAnulacion?.showModal();
    });

    botonCancelarAnulacion?.addEventListener("click", () => dialogoAnulacion.close());
    botonConfirmarAnulacion?.addEventListener("click", () => {
        if (!formularioAnulacion) return;
        anulacionConfirmada = true;
        dialogoAnulacion.close();
        formularioAnulacion.requestSubmit();
        formularioAnulacion = null;
    });

    const formulario = document.getElementById("formulario-orden");
    if (formulario) {
        const dialogoConfirmacion = document.getElementById("confirmar-orden-dialog");
        const botonConfirmar = dialogoConfirmacion?.querySelector("[data-confirmar-orden]");
        const botonCancelar = dialogoConfirmacion?.querySelector("[data-cancelar-confirmacion]");
        const botonGuardar = document.querySelector('[type="submit"][form="formulario-orden"]');
        let confirmada = false;
        let enviando = false;

        formulario.addEventListener("submit", evento => {
            if (enviando) {
                evento.preventDefault();
                return;
            }
            if (evento.defaultPrevented) return;
            if (!confirmada) {
                evento.preventDefault();
                dialogoConfirmacion?.showModal();
                return;
            }

            confirmada = false;
            enviando = true;
            reservarComprobante(formulario);
            document.querySelectorAll('[type="submit"][form="formulario-orden"]').forEach(boton => {
                boton.disabled = true;
            });
        });

        botonCancelar?.addEventListener("click", () => dialogoConfirmacion.close());
        botonConfirmar?.addEventListener("click", () => {
            confirmada = true;
            dialogoConfirmacion.close();
            if (botonGuardar instanceof HTMLButtonElement) formulario.requestSubmit(botonGuardar);
            else formulario.requestSubmit();
        });

        window.addEventListener("pageshow", evento => {
            if (!evento.persisted) return;
            enviando = false;
            document.querySelectorAll('[type="submit"][form="formulario-orden"]').forEach(boton => {
                boton.disabled = false;
            });
        });
    }

    const dialogoComprobante = document.getElementById("comprobante-orden-dialog");
    if (!dialogoComprobante) return;

    const frame = dialogoComprobante.querySelector("[data-comprobante-frame]");
    const botonCerrar = dialogoComprobante.querySelector("[data-cerrar-comprobante]");

    const mostrarComprobante = url => {
        if (!url || !frame) return;
        frame.src = url;
        if (!dialogoComprobante.open) dialogoComprobante.showModal();
    };

    document.querySelectorAll("[data-comprobante-url]").forEach(enlace => {
        enlace.addEventListener("click", evento => {
            evento.preventDefault();
            mostrarComprobante(enlace.dataset.comprobanteUrl);
        });
    });

    botonCerrar?.addEventListener("click", () => dialogoComprobante.close());
    dialogoComprobante.addEventListener("close", () => {
        if (frame) frame.src = "about:blank";
    });

    const enlaceCreado = document.querySelector("[data-comprobante-creado]");
    if (enlaceCreado && !reservaAnterior) {
        try { window.open(enlaceCreado.href, "_blank", "noopener"); }
        catch { /* El enlace de respaldo permanece visible. */ }
    }
})();
