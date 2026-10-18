(() => {
    const claveReserva = "autotaller:comprobante-pendiente";

    function consumirReserva() {
        try {
            const reserva = JSON.parse(sessionStorage.getItem(claveReserva));
            sessionStorage.removeItem(claveReserva);
            return reserva && /^[a-f0-9]{32}$/.test(reserva.token) ? reserva : null;
        } catch {
            return null;
        }
    }

    const nombrePestana = reserva => `comprobante-orden-${reserva.token}`;
    const enlace = document.querySelector("[data-comprobante-creado]");
    if (enlace) {
        const reserva = consumirReserva();
        if (reserva?.reservada && reserva.token === enlace.dataset.token) {
            try {
                const pestana = window.open(enlace.href, nombrePestana(reserva));
                if (pestana) pestana.opener = null;
            } catch {
                // El enlace permanece disponible aunque el navegador impida abrir el PDF.
            }
        }
        return;
    }

    const formulario = document.getElementById("formulario-orden");
    if (!formulario) return;

    const anterior = consumirReserva();
    if (anterior?.reservada) {
        try { window.open("", nombrePestana(anterior))?.close(); } catch { }
    }

    let enviando = false;
    formulario.addEventListener("submit", evento => {
        if (enviando) { evento.preventDefault(); return; }
        if (evento.defaultPrevented) return;
        if (!window.confirm("¿Guardar orden?")) { evento.preventDefault(); return; }
        enviando = true;
        document.querySelectorAll('[type="submit"][form="formulario-orden"]').forEach(boton => { boton.disabled = true; });

        const seleccionCompleta = ["VehiculoId", "MecanicoId", "ServicioId"]
            .every(campo => Number(formulario.elements.namedItem(`Formulario.${campo}`)?.value) > 0);
        if (!seleccionCompleta) return;

        const reserva = {
            token: formulario.elements.namedItem("Formulario.Token").value.replaceAll("-", ""),
            reservada: false
        };
        if (!/^[a-f0-9]{32}$/.test(reserva.token) || /^0+$/.test(reserva.token)) return;
        let pestana;
        try {
            sessionStorage.setItem(claveReserva, JSON.stringify(reserva));
            pestana = window.open("about:blank", nombrePestana(reserva));
            if (pestana) {
                pestana.document.title = "Comprobante de la orden";
                pestana.document.body.textContent = "Guardando la orden. El comprobante se mostrará únicamente si se confirma el registro.";
                reserva.reservada = true;
                sessionStorage.setItem(claveReserva, JSON.stringify(reserva));
            }
        } catch {
            pestana?.close();
        }
    });

    window.addEventListener("pageshow", evento => {
        if (!evento.persisted) return;
        enviando = false;
        document.querySelectorAll('[type="submit"][form="formulario-orden"]').forEach(boton => { boton.disabled = false; });
    });
})();
