(() => {
    const pagina = document.querySelector('.hu3-usuarios');
    const formulario = document.getElementById('hu3AutoFilterForm');
    const busqueda = document.getElementById('hu3Busqueda');
    const resultados = document.getElementById('usuarios-resultados');
    const estado = document.getElementById('usuarios-busqueda-estado');
    if (!pagina || !formulario || !busqueda || !resultados || !estado) return;

    let espera;
    let peticion;
    let version = 0;
    let componiendo = false;

    function cancelarBusqueda() {
        clearTimeout(espera);
        peticion?.abort();
        return ++version;
    }

    async function cargarResultados(numeroPagina, versionActual, enfocarPaginacion = false) {
        const consulta = new AbortController();
        peticion = consulta;
        const url = new URL(formulario.dataset.resultadosUrl, document.baseURI);
        const parametros = new URLSearchParams(new FormData(formulario));
        parametros.set('Pagina', String(numeroPagina));
        for (const [nombre, valor] of parametros) url.searchParams.set(nombre, valor);

        resultados.setAttribute('aria-busy', 'true');
        delete estado.dataset.error;
        estado.textContent = 'Buscando usuarios…';
        try {
            const respuesta = await fetch(url, {
                credentials: 'same-origin',
                cache: 'no-store',
                signal: consulta.signal,
                headers: { 'X-Requested-With': 'XMLHttpRequest', 'Accept': 'text/html' }
            });
            if (respuesta.redirected || respuesta.status === 401 || respuesta.status === 403) {
                throw new Error('Tu sesión venció o no tienes permiso para consultar usuarios.');
            }
            if (!respuesta.ok || !respuesta.headers.get('Content-Type')?.includes('text/html')) {
                throw new Error('No se pudo actualizar la lista. Vuelve a intentarlo.');
            }

            const contenido = await respuesta.text();
            if (consulta.signal.aborted || versionActual !== version) return;
            const plantilla = document.createElement('template');
            // La parcial del mismo servidor codifica los datos mediante Razor.
            plantilla.innerHTML = contenido;
            const bloque = plantilla.content.querySelector('[data-usuarios-resultados]');
            if (!bloque) throw new Error('No se pudo actualizar la lista. Vuelve a intentarlo.');
            resultados.replaceChildren(plantilla.content);
            estado.textContent = `${bloque.dataset.total} usuario(s) encontrado(s).`;

            if (enfocarPaginacion) {
                const enlaceActual = resultados.querySelector('.page-item.active .page-link');
                enlaceActual?.focus({ preventScroll: true });
            }

            const direccion = new URL(formulario.action, document.baseURI);
            parametros.set('Pagina', bloque.dataset.pagina);
            direccion.search = parametros.toString();
            window.history.replaceState(null, '', direccion);
        } catch (error) {
            if (consulta.signal.aborted || versionActual !== version) return;
            estado.dataset.error = '';
            estado.textContent = error instanceof TypeError
                ? 'No se pudo conectar. Revisa tu conexión y vuelve a intentarlo.'
                : error.message;
        } finally {
            if (versionActual === version) resultados.setAttribute('aria-busy', 'false');
        }
    }

    function programarBusqueda() {
        const versionActual = cancelarBusqueda();
        espera = setTimeout(() => cargarResultados(1, versionActual), 250);
    }

    busqueda.addEventListener('input', evento => {
        if (!componiendo && !evento.isComposing) programarBusqueda();
    });
    busqueda.addEventListener('compositionstart', () => {
        componiendo = true;
        cancelarBusqueda();
    });
    busqueda.addEventListener('compositionend', () => {
        componiendo = false;
        programarBusqueda();
    });

    formulario.addEventListener('submit', evento => {
        evento.preventDefault();
        cargarResultados(1, cancelarBusqueda());
    });

    formulario.querySelectorAll('select').forEach(filtro => filtro.addEventListener('change', () => {
        cargarResultados(1, cancelarBusqueda());
    }));

    formulario.querySelector('[data-limpiar-usuarios]')?.addEventListener('click', evento => {
        if (evento.button !== 0 || evento.ctrlKey || evento.metaKey || evento.shiftKey || evento.altKey) return;
        evento.preventDefault();
        busqueda.value = '';
        formulario.querySelectorAll('select').forEach(filtro => { filtro.value = ''; });
        cargarResultados(1, cancelarBusqueda());
        busqueda.focus();
    });

    resultados.addEventListener('click', evento => {
        const enlace = evento.target.closest('a[data-usuarios-pagina]');
        if (!enlace || evento.button !== 0 || evento.ctrlKey || evento.metaKey || evento.shiftKey || evento.altKey) return;
        evento.preventDefault();
        const url = new URL(enlace.href);
        const numeroPagina = Number(url.searchParams.get('pagina') || url.searchParams.get('Pagina') || 1);
        cargarResultados(numeroPagina, cancelarBusqueda(), true);
    });

    const dialogo = document.getElementById('usuario-estado-dialog');
    if (!(dialogo instanceof HTMLDialogElement)) return;
    const titulo = document.getElementById('usuario-estado-titulo');
    const descripcion = document.getElementById('usuario-estado-descripcion');
    const nombre = dialogo.querySelector('[data-usuario-confirmacion]');
    const confirmar = dialogo.querySelector('[data-confirmar-usuario]');
    let formularioPendiente;
    let botonOrigen;
    let formularioConfirmado;

    pagina.addEventListener('submit', evento => {
        const accion = evento.target;
        if (!(accion instanceof HTMLFormElement) || !accion.matches('form[data-confirmar-usuario]')) return;
        if (formularioConfirmado === accion) {
            formularioConfirmado = null;
            return;
        }

        evento.preventDefault();
        formularioPendiente = accion;
        botonOrigen = evento.submitter;
        const reactivar = accion.dataset.confirmarUsuario === 'reactivar';
        dialogo.dataset.accion = reactivar ? 'reactivar' : 'desactivar';
        titulo.textContent = reactivar ? 'Reactivar usuario' : 'Desactivar usuario';
        nombre.textContent = accion.dataset.usuario;
        descripcion.textContent = reactivar
            ? 'Este usuario volverá a tener acceso al sistema. ¿Deseas reactivar su cuenta?'
            : 'Este usuario perderá el acceso al sistema inmediatamente. ¿Deseas desactivar su cuenta?';
        confirmar.textContent = reactivar ? 'Reactivar usuario' : 'Desactivar usuario';
        dialogo.showModal();
    });

    dialogo.querySelector('[data-cancelar-usuario]').addEventListener('click', () => dialogo.close());
    confirmar.addEventListener('click', () => {
        if (!formularioPendiente || !resultados.contains(formularioPendiente)) {
            dialogo.close();
            return;
        }
        const accion = formularioPendiente;
        const boton = botonOrigen;
        formularioConfirmado = accion;
        dialogo.close();
        accion.requestSubmit(boton instanceof HTMLButtonElement ? boton : undefined);
    });
    dialogo.addEventListener('close', () => {
        // Un cierre pendiente no debe borrar una confirmación que ya se volvió a abrir.
        if (dialogo.open) return;
        formularioPendiente = null;
        if (botonOrigen?.isConnected) botonOrigen.focus({ preventScroll: true });
        botonOrigen = null;
    });
})();
