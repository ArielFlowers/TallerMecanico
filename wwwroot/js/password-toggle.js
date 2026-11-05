(() => {
    "use strict";

    document.querySelectorAll("[data-password-toggle]").forEach(button => {
        const input = document.getElementById(button.getAttribute("aria-controls"));

        if (!(input instanceof HTMLInputElement) || input.type !== "password") {
            return;
        }

        const label = button.dataset.passwordLabel || "contraseña";

        const updateButton = () => {
            const visible = input.type === "text";
            const action = `${visible ? "Ocultar" : "Mostrar"} ${label}`;

            button.setAttribute("aria-pressed", String(visible));
            button.setAttribute("aria-label", action);
            button.title = action;
        };

        updateButton();
        button.hidden = false;

        button.addEventListener("click", () => {
            input.type = input.type === "password" ? "text" : "password";
            updateButton();
        });
    });
})();
