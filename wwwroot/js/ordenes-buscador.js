(() => {
    const buscador = document.getElementById("ordenes-autocompletado");
    const formulario = document.getElementById("ordenes-busqueda");
    if (!buscador || !formulario) return;

    buscador.addEventListener("autocompletado:seleccion", () => formulario.requestSubmit());

    const actualizarFiltrosSugerencias = () => {
        const url = new URL(buscador.dataset.url, document.baseURI);
        for (const nombre of ["Estado", "Fecha"]) {
            const filtro = formulario.elements.namedItem(nombre);
            if (filtro.value) url.searchParams.set(nombre, filtro.value);
            else url.searchParams.delete(nombre);
        }
        buscador.dataset.url = url.href;
    };

    formulario.elements.namedItem("Estado").addEventListener("change", actualizarFiltrosSugerencias);
    formulario.elements.namedItem("Fecha").addEventListener("change", actualizarFiltrosSugerencias);
})();
