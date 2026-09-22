document.addEventListener("DOMContentLoaded", () => {
    const formDialog = document.getElementById("mecanico-form-dialog");
    const deleteDialog = document.getElementById("mecanico-delete-dialog");

    if (!(formDialog instanceof HTMLDialogElement) ||
        !(deleteDialog instanceof HTMLDialogElement)) {
        return;
    }

    const form = document.getElementById("mecanico-form");
    const mecanicoId = document.getElementById("MecanicoId");
    const ci = document.getElementById("MecanicoInput_Ci");
    const nombres = document.getElementById("MecanicoInput_Nombres");
    const apellidos = document.getElementById("MecanicoInput_Apellidos");
    const genero = document.getElementById("MecanicoInput_Genero");
    const especialidad = document.getElementById("MecanicoInput_Especialidad");
    const celular = document.getElementById("MecanicoInput_Celular");
    const formTitle = document.getElementById("mecanico-form-title");
    const formDescription = document.getElementById("mecanico-form-description");
    const createSubmit = document.getElementById("guardar-nuevo-mecanico");
    const editSubmit = document.getElementById("guardar-edicion-mecanico");
    const deleteId = document.getElementById("mecanico-eliminar-id");
    const deleteName = document.getElementById("mecanico-eliminar-nombre");

    const formElementsExist =
        form instanceof HTMLFormElement &&
        mecanicoId instanceof HTMLInputElement &&
        ci instanceof HTMLInputElement &&
        nombres instanceof HTMLInputElement &&
        apellidos instanceof HTMLInputElement &&
        genero instanceof HTMLSelectElement &&
        especialidad instanceof HTMLInputElement &&
        celular instanceof HTMLInputElement &&
        formTitle &&
        formDescription &&
        createSubmit instanceof HTMLButtonElement &&
        editSubmit instanceof HTMLButtonElement;

    if (!formElementsExist ||
        !(deleteId instanceof HTMLInputElement) ||
        !deleteName) {
        return;
    }

    const configurarFormulario = (modo, datos = null, conservarValores = false) => {
        const esEdicion = modo === "editar";

        if (!conservarValores) {
            form.reset();
            mecanicoId.value = esEdicion && datos ? datos.id : "0";
            ci.value = esEdicion && datos ? datos.ci : "";
            nombres.value = esEdicion && datos ? datos.nombres : "";
            apellidos.value = esEdicion && datos ? datos.apellidos : "";
            genero.value = esEdicion && datos ? datos.genero : "";
            especialidad.value = esEdicion && datos ? datos.especialidad : "";
            celular.value = esEdicion && datos ? datos.celular : "";
        }

        formTitle.textContent = esEdicion ? "Editar Mecánico" : "Nuevo Mecánico";
        formDescription.textContent = esEdicion
            ? "Actualiza la información del mecánico seleccionado."
            : "Completa los datos para registrar un mecánico.";
        createSubmit.hidden = esEdicion;
        editSubmit.hidden = !esEdicion;
    };

    document.getElementById("abrir-crear-mecanico")?.addEventListener("click", () => {
        configurarFormulario("crear");
        formDialog.showModal();
    });

    document.querySelectorAll("[data-editar-mecanico]").forEach(button => {
        button.addEventListener("click", () => {
            configurarFormulario("editar", {
                id: button.dataset.id ?? "0",
                ci: button.dataset.ci ?? "",
                nombres: button.dataset.nombres ?? "",
                apellidos: button.dataset.apellidos ?? "",
                genero: button.dataset.genero ?? "",
                especialidad: button.dataset.especialidad ?? "",
                celular: button.dataset.celular ?? ""
            });
            formDialog.showModal();
        });
    });

    document.querySelectorAll("[data-eliminar-mecanico]").forEach(button => {
        button.addEventListener("click", () => {
            deleteId.value = button.dataset.id ?? "0";
            const nombreMecanico = [button.dataset.nombres, button.dataset.apellidos]
                .filter(Boolean)
                .join(" ");

            deleteName.textContent = nombreMecanico || "este mecánico";
            deleteDialog.showModal();
        });
    });

    document.querySelectorAll("[data-close-dialog]").forEach(button => {
        button.addEventListener("click", () => {
            const dialog = button.closest("dialog");
            if (dialog instanceof HTMLDialogElement) {
                dialog.close();
            }
        });
    });

    const modoInicial = formDialog.dataset.openMode;
    if (modoInicial === "crear" || modoInicial === "editar") {
        configurarFormulario(modoInicial, null, true);
        formDialog.showModal();
    }
});
