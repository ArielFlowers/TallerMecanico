(() => {
    'use strict';

    const form = document.getElementById('reporte-mecanicos-filtros');
    const results = document.getElementById('reporte-mecanicos-resultados');
    const status = document.getElementById('reporte-mecanicos-estado');
    if (!form || !results || !status) return;

    const search = form.querySelector('input[type="search"]');
    let timer;
    let controller;
    let version = 0;

    function setBusy(busy) {
        results.setAttribute('aria-busy', String(busy));
        results.querySelectorAll('[data-exportar-reporte]').forEach(button => {
            button.disabled = busy;
        });
    }

    async function filter(requestVersion) {
        controller = new AbortController();
        const url = new URL(form.action, window.location.href);
        url.search = new URLSearchParams(new FormData(form)).toString();
        const pageUrl = new URL(url);
        url.searchParams.set('handler', 'Resultados');

        try {
            const response = await fetch(url, {
                signal: controller.signal,
                headers: { 'X-Requested-With': 'XMLHttpRequest' },
                cache: 'no-store'
            });
            if (requestVersion !== version) return;
            if (response.redirected || response.status === 401 || response.status === 403) {
                throw new Error('La sesión expiró o no tienes permiso. Inicia sesión nuevamente.');
            }
            if (!response.ok) throw new Error('No se pudo actualizar el reporte. Intenta buscar nuevamente.');
            const html = await response.text();
            if (requestVersion !== version) return;
            results.innerHTML = html;
            window.history.replaceState(null, '', pageUrl);
            const total = results.querySelector('[data-total-mecanicos]')?.dataset.totalMecanicos;
            status.textContent = total === undefined ? '' : `${total} mecánico(s) encontrados.`;
            setBusy(false);
        } catch (error) {
            if (requestVersion !== version || error.name === 'AbortError') return;
            results.setAttribute('aria-busy', 'false');
            status.textContent = error.message;
        }
    }

    function schedule(delay) {
        clearTimeout(timer);
        controller?.abort();
        const requestVersion = ++version;
        setBusy(true);
        status.textContent = 'Buscando mecánicos…';
        timer = setTimeout(() => filter(requestVersion), delay);
    }

    search.addEventListener('compositionstart', () => {
        clearTimeout(timer);
        controller?.abort();
        version++;
        setBusy(true);
        status.textContent = 'Escribiendo nombre…';
    });
    search.addEventListener('input', event => {
        if (!event.isComposing) schedule(200);
    });
    search.addEventListener('compositionend', () => schedule(200));
    form.addEventListener('submit', event => {
        event.preventDefault();
        schedule(0);
    });
})();
