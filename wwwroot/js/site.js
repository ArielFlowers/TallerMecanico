document.addEventListener("DOMContentLoaded", () => {

    const menuToggles = document.querySelectorAll(".menu-toggle");

    menuToggles.forEach(toggle => {

        toggle.addEventListener("click", () => {

            const menuId = toggle.getAttribute("aria-controls");
            const submenu = document.getElementById(menuId);

            if (!submenu) {
                return;
            }

            const isOpen =
                toggle.getAttribute("aria-expanded") === "true";

            menuToggles.forEach(otherToggle => {

                if (otherToggle !== toggle) {

                    const otherMenuId =
                        otherToggle.getAttribute("aria-controls");

                    const otherSubmenu =
                        document.getElementById(otherMenuId);

                    otherToggle.setAttribute("aria-expanded", "false");

                    if (otherSubmenu) {
                        otherSubmenu.classList.remove("open");
                    }
                }

            });

            toggle.setAttribute(
                "aria-expanded",
                (!isOpen).toString()
            );

            if (!isOpen) {
                submenu.classList.add("open");
            } else {
                submenu.classList.remove("open");
            }

        });

    });


    const activeSubmenuItem =
        document.querySelector(".submenu-item.active");

    if (activeSubmenuItem) {

        const submenu =
            activeSubmenuItem.closest(".submenu");

        if (submenu) {

            submenu.classList.add("open");

            const toggle =
                document.querySelector(
                    `[aria-controls="${submenu.id}"]`
                );

            if (toggle) {
                toggle.setAttribute("aria-expanded", "true");
            }

        }

    }


    // ===== MODALES =====

    const abrirModal = modal => {
        modal.classList.add("is-open");
        document.body.classList.add("modal-open");
    };

    const cerrarModales = () => {

        document.querySelectorAll(".modal-overlay.is-open")
            .forEach(modal => modal.classList.remove("is-open"));

        document.body.classList.remove("modal-open");
    };

    const asignarValor = (id, valor) => {

        const campo = document.getElementById(id);

        if (campo) {
            campo.value = valor;
        }

    };

    document.querySelectorAll("[data-modal-abrir]").forEach(boton => {

        boton.addEventListener("click", () => {

            const modal =
                document.getElementById(boton.dataset.modalAbrir);

            if (!modal) {
                return;
            }

            if (modal.id === "modal-editar") {
                asignarValor("editar-id", boton.dataset.id);
                asignarValor("editar-placa", boton.dataset.placa);
                asignarValor("editar-modelo", boton.dataset.modelo);
                asignarValor("editar-kilometraje", boton.dataset.kilometraje);
                asignarValor("editar-observaciones", boton.dataset.observaciones);
            }

            if (modal.id === "modal-eliminar") {

                asignarValor("eliminar-id", boton.dataset.id);

                const placa = document.getElementById("eliminar-placa");

                if (placa) {
                    placa.textContent = boton.dataset.placa;
                }

            }

            abrirModal(modal);

        });

    });

    document.querySelectorAll("[data-modal-cerrar]").forEach(boton => {
        boton.addEventListener("click", cerrarModales);
    });

    document.querySelectorAll(".modal-overlay").forEach(modal => {

        modal.addEventListener("click", event => {

            if (event.target === modal) {
                cerrarModales();
            }

        });

    });

    document.addEventListener("keydown", event => {

        if (event.key === "Escape") {
            cerrarModales();
        }

    });

    if (document.querySelector(".modal-overlay.is-open")) {
        document.body.classList.add("modal-open");
    }


    // ===== VALIDACION EN VIVO DE LA PLACA =====

    const formatoPlaca = /^[A-Za-z0-9]{6,8}$/;

    document.querySelectorAll("[data-placa-input]").forEach(input => {

        const aviso =
            input.parentElement.querySelector("[data-placa-aviso]");

        if (!aviso) {
            return;
        }

        input.addEventListener("input", () => {

            input.value = input.value.toUpperCase();

            const invalido =
                input.value.length > 0 && !formatoPlaca.test(input.value);

            aviso.textContent =
                invalido ? "Formato alfanumérico requerido." : "";

            aviso.classList.toggle("is-visible", invalido);
            input.classList.toggle("input-warning", invalido);

        });

    });

});
