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

});