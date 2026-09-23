document.addEventListener("DOMContentLoaded", () => {
    const catalogo = JSON.parse(document.getElementById("catalogo-vehiculos").textContent);

    const cargarModelos = (formulario, seleccionado = "") => {
        const marca = formulario.querySelector("[data-marca-input]").value;
        const modelo = formulario.querySelector("[data-modelo-input]");
        const modelos = Object.hasOwn(catalogo, marca) ? catalogo[marca] : [];
        modelo.replaceChildren(new Option("Selecciona un modelo", ""));
        modelos.forEach(nombre => modelo.add(new Option(nombre, nombre)));
        modelo.value = modelos.includes(seleccionado) ? seleccionado : "";
        modelo.disabled = modelos.length === 0;
    };

    document.querySelectorAll("[data-marca-input]").forEach(marca => {
        const formulario = marca.form;
        const seleccionado = formulario.querySelector("[data-modelo-input]").value;
        cargarModelos(formulario, seleccionado);
        marca.addEventListener("change", () => cargarModelos(formulario));
    });

    document.querySelectorAll('[data-modal-abrir="modal-editar"]').forEach(boton => {
        boton.addEventListener("click", () => {
            const formulario = document.querySelector("#modal-editar form");
            ["id", "placa", "marca", "kilometraje", "observaciones"].forEach(campo => {
                document.getElementById(`editar-${campo}`).value = boton.dataset[campo] ?? "";
            });
            cargarModelos(formulario, boton.dataset.modelo);
            const anterior = document.getElementById("editar-modelo-anterior");
            anterior.hidden = Boolean(boton.dataset.marca);
            anterior.querySelector("span").textContent = boton.dataset.modelo;
            limpiarErrores(formulario);
        });
    });

    const limpiarErrores = formulario => {
        formulario.querySelectorAll(".service-validation, [data-placa-aviso]").forEach(aviso => {
            aviso.textContent = "";
            aviso.classList.remove("is-visible");
        });
        formulario.querySelectorAll("input, select, textarea").forEach(campo => {
            campo.classList.remove("input-error", "input-validation-error");
            campo.setCustomValidity("");
        });
    };

    document.querySelector('[data-modal-abrir="modal-crear"]').addEventListener("click", () => {
        const formulario = document.querySelector("#modal-crear form");
        formulario.querySelectorAll("input:not([type=hidden]), textarea, select").forEach(campo => {
            campo.value = "";
        });
        cargarModelos(formulario);
        limpiarErrores(formulario);
    });

    document.querySelectorAll("[data-placa-input]").forEach(input => {
        const aviso = input.parentElement.querySelector("[data-placa-aviso]");
        const formato = new RegExp(input.pattern);
        input.addEventListener("input", () => {
            input.value = input.value.replace(/\s/g, "").toUpperCase();
            const invalido = input.value.length > 0 && !formato.test(input.value);
            const mensaje = invalido ? input.dataset.placaMensaje : "";
            input.setCustomValidity(mensaje);
            aviso.textContent = mensaje;
            aviso.classList.toggle("is-visible", invalido);
            input.classList.toggle("input-error", invalido);
        });
    });
});
