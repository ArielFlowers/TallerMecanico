document.addEventListener("DOMContentLoaded", () => {
    const catalogo = JSON.parse(document.getElementById("catalogo-vehiculos").textContent);

    const validarPlaca = formulario => {
        const input = formulario.querySelector("[data-placa-input]");
        const extranjera = formulario.querySelector("[data-placa-extranjera]").checked;
        const aviso = formulario.querySelector("[data-placa-aviso]");
        input.pattern = extranjera ? input.dataset.patronExtranjero : input.dataset.patronNacional;
        input.maxLength = extranjera ? 15 : 7;
        input.placeholder = extranjera ? "Ej. AB123CD" : "Ej. 123ABC o 1234ABC";
        input.value = input.value.replace(/\s/g, "").toUpperCase();
        if (extranjera) input.value = input.value.replace(/-/g, "");
        const ayuda = extranjera ? input.dataset.mensajeExtranjero : input.dataset.mensajeNacional;
        const invalido = input.value.length > 0 && !new RegExp(input.pattern).test(input.value);
        const mensaje = invalido ? ayuda : "";
        formulario.querySelector("[data-placa-ayuda]").textContent = ayuda;
        input.setCustomValidity(mensaje);
        aviso.textContent = mensaje;
        aviso.classList.toggle("is-visible", invalido);
        input.classList.toggle("input-error", invalido);
    };

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
            document.getElementById("editar-cliente").value = boton.dataset.clienteId ?? "";
            document.getElementById("editar-extranjera").checked = boton.dataset.extranjera === "true";
            formulario.querySelector('[name="BorradorId"]').value = "";
            cargarModelos(formulario, boton.dataset.modelo);
            const anterior = document.getElementById("editar-modelo-anterior");
            anterior.hidden = Boolean(boton.dataset.marca);
            anterior.querySelector("span").textContent = boton.dataset.modelo;
            limpiarErrores(formulario);
            validarPlaca(formulario);
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
            if (campo.type === "checkbox") campo.checked = false;
            else campo.value = "";
        });
        formulario.querySelector('[name="Formulario.Id"]').value = "0";
        formulario.querySelector('[name="BorradorId"]').value = "";
        cargarModelos(formulario);
        limpiarErrores(formulario);
        validarPlaca(formulario);
    });

    document.querySelectorAll("[data-placa-input]").forEach(input => {
        input.addEventListener("input", () => validarPlaca(input.form));
        input.form.querySelector("[data-placa-extranjera]")
            .addEventListener("change", () => validarPlaca(input.form));
    });
});
