(() => {
    let pendingRequest = null;

    async function loadOrders(url, initial, updateHistory = true) {
        const page = document.querySelector('.hospital-orders-page');
        if (!page) return;

        pendingRequest?.abort();
        const request = new AbortController();
        pendingRequest = request;
        const buttons = [...page.querySelectorAll('.hospital-orders-index-buttons button')];
        const previousInitial = page.dataset.selectedInitial ?? '';
        if (initial !== undefined) {
            buttons.forEach(button => {
                const selected = button.value === initial;
                button.classList.toggle('active', selected);
                button.setAttribute('aria-pressed', String(selected));
            });
        }
        page.setAttribute('aria-busy', 'true');
        const aiResult = page.querySelector('[data-ai-page="hospitalOrders"]');
        if (aiResult) aiResult.dataset.aiQueryError = '';
        const status = page.querySelector('.hospital-orders-query-status');
        if (status) status.textContent = '查詢中…';

        try {
            const response = await fetch(url, {
                headers: { 'X-Requested-With': 'XMLHttpRequest' },
                signal: request.signal
            });
            if (!response.ok) throw new Error('Unable to load hospital orders');
            const html = await response.text();
            if (request !== pendingRequest) return;
            const nextPage = new DOMParser().parseFromString(html, 'text/html')
                .querySelector('.hospital-orders-page');
            if (!nextPage) throw new Error('Hospital orders missing from response');
            const nextAiResult = nextPage.querySelector('[data-ai-page="hospitalOrders"]');
            if (nextAiResult) nextAiResult.dataset.aiRevision = String(Number(aiResult?.dataset.aiRevision || 0) + 1);
            const focusedElement = document.activeElement;
            const focusId = focusedElement?.id;
            const focusInitial = focusedElement?.matches('.hospital-orders-index-buttons button')
                ? focusedElement.value : undefined;
            page.replaceWith(nextPage);
            if (updateHistory) history.pushState(null, '', url);
            if (focusId) document.getElementById(focusId)?.focus({ preventScroll: true });
            else if (focusInitial !== undefined) {
                [...nextPage.querySelectorAll('.hospital-orders-index-buttons button')]
                    .find(button => button.value === focusInitial)?.focus({ preventScroll: true });
            }
        } catch (error) {
            if (request !== pendingRequest || error.name === 'AbortError') return;
            buttons.forEach(button => {
                const selected = button.value === previousInitial;
                button.classList.toggle('active', selected);
                button.setAttribute('aria-pressed', String(selected));
            });
            if (status) status.textContent = '查詢失敗，請再試一次。';
            if (aiResult) {
                aiResult.dataset.aiQueryError = '院內醫令查詢失敗，請再試一次。';
                aiResult.dataset.aiRevision = String(Number(aiResult.dataset.aiRevision || 0) + 1);
            }
        } finally {
            if (request === pendingRequest) {
                page.removeAttribute('aria-busy');
                pendingRequest = null;
            }
        }
    }

    document.addEventListener('submit', event => {
        const form = event.target;
        if (!form.matches('.hospital-orders-filter, .hospital-orders-page-size')) return;
        event.preventDefault();
        const data = new FormData(form);
        if (event.submitter?.name) data.set(event.submitter.name, event.submitter.value);
        const url = new URL(form.action, location.href);
        url.search = new URLSearchParams(data).toString();
        loadOrders(url.href, data.get('initial') ?? '');
    });

    document.addEventListener('click', event => {
        if (event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
        const link = event.target.closest('.hospital-orders-results nav a, .hospital-orders-actions a');
        if (!link || link.target || link.hasAttribute('download')) return;
        event.preventDefault();
        const url = new URL(link.href, location.href);
        loadOrders(url.href, url.searchParams.get('initial') ?? '');
    });

    window.addEventListener('popstate', () => {
        if (document.querySelector('.hospital-orders-page')) {
            loadOrders(location.href, new URL(location.href).searchParams.get('initial') ?? '', false);
        }
    });
})();
