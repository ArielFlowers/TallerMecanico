(() => {
    function filtrar(inputId, selectId) {
        const input = document.getElementById(inputId);
        const select = document.getElementById(selectId);
        if (!input || !select) return;
        input.addEventListener('input', () => {
            const termino = input.value.trim().toLocaleLowerCase('es');
            for (const opcion of select.options) {
                if (!opcion.value) continue;
                opcion.hidden = !opcion.dataset.search?.toLocaleLowerCase('es').includes(termino);
            }
        });
    }
    filtrar('filtro-vehiculos', 'vehiculo-select');
    filtrar('filtro-productos', 'producto-select');

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
            if (subtotal) subtotal.textContent = `Bs. ${ (cantidad * precio).toFixed(2) }`;
        }
        document.getElementById('resumen-lineas')?.replaceChildren(String(filas.length));
        document.getElementById('resumen-cantidad')?.replaceChildren(String(cantidadTotal));
        const totalEl = document.getElementById('resumen-total');
        if (totalEl) totalEl.textContent = `Bs. ${total.toFixed(2)}`;
    };
    document.querySelectorAll('[data-cantidad]').forEach(input => input.addEventListener('input', actualizarResumen));
})();
