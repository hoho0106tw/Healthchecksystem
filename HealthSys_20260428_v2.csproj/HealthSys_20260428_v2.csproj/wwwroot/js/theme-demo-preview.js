(() => {
    const preview = document.querySelector('[data-theme-preview]');
    if (!preview) return;
    const chartButton = preview.querySelector('[data-theme-demo-chart-toggle]');
    const chartDetails = preview.querySelector('[data-theme-demo-chart-details]');
    const setChartOpen = open => {
        if (!chartButton || !chartDetails) return;
        chartButton.setAttribute('aria-expanded', String(open));
        chartDetails.hidden = !open;
    };
    chartButton?.addEventListener('click', () => setChartOpen(chartDetails.hidden));
    preview.querySelector('[data-theme-demo-chart-close]')?.addEventListener('click', () => {
        setChartOpen(false);
        chartButton.focus({ preventScroll: true });
    });
    document.addEventListener('keydown', event => {
        if (event.key === 'Escape' && chartDetails && !chartDetails.hidden) {
            setChartOpen(false);
            chartButton.focus({ preventScroll: true });
        }
    });
    document.addEventListener('click', event => {
        if (chartDetails && !chartDetails.hidden && !chartDetails.contains(event.target) && !chartButton.contains(event.target)) setChartOpen(false);
    });

    const accountRows = Array.from(preview.querySelectorAll('.theme-demo-account-table tbody tr'));
    const selectAccountRow = row => accountRows.forEach(item => item.classList.toggle('is-selected', item === row));
    accountRows.forEach((row, index) => {
        row.tabIndex = 0;
        row.addEventListener('click', () => selectAccountRow(row));
        row.addEventListener('keydown', event => {
            let next;
            if (event.key === 'ArrowDown') next = (index + 1) % accountRows.length;
            if (event.key === 'ArrowUp') next = (index + accountRows.length - 1) % accountRows.length;
            if (event.key === 'Home') next = 0;
            if (event.key === 'End') next = accountRows.length - 1;
            if (event.key === 'Enter' || event.key === ' ') next = index;
            if (next === undefined) return;
            event.preventDefault();
            selectAccountRow(accountRows[next]);
            accountRows[next].focus({ preventScroll: true });
        });
    });
    const tabs = Array.from(preview.querySelectorAll('[data-theme-demo-tab]'));
    const selectTab = tab => {
        tabs.forEach(item => {
            const active = item === tab;
            item.setAttribute('aria-selected', String(active));
            item.tabIndex = active ? 0 : -1;
        });
        preview.querySelectorAll('[data-theme-demo-tab-panel]').forEach(panel => {
            panel.hidden = panel.dataset.themeDemoTabPanel !== tab.dataset.themeDemoTab;
        });
    };
    tabs.forEach((tab, index) => {
        tab.addEventListener('click', () => selectTab(tab));
        tab.addEventListener('keydown', event => {
            let next;
            if (event.key === 'ArrowRight') next = (index + 1) % tabs.length;
            if (event.key === 'ArrowLeft') next = (index + tabs.length - 1) % tabs.length;
            if (event.key === 'Home') next = 0;
            if (event.key === 'End') next = tabs.length - 1;
            if (next === undefined) return;
            event.preventDefault();
            selectTab(tabs[next]);
            tabs[next].focus({ preventScroll: true });
        });
    });
    const table = preview.querySelector('[data-theme-demo-datagrid]');
    const size = preview.querySelector('[data-theme-demo-page-size]');
    const pages = preview.querySelector('[data-theme-demo-pages]');
    const summary = preview.querySelector('[data-theme-demo-page-summary]');
    if (!table || !size || !pages || !summary) return;
    const companies = ['A', 'B', 'C', 'D', 'E', 'F'];
    let currentPage = 1;
    const selected = new Set([0]);
    const render = () => {
        const pageSize = Number(size.value);
        const count = Math.ceil(companies.length / pageSize);
        currentPage = Math.max(1, Math.min(currentPage, count));
        const start = (currentPage - 1) * pageSize;
        const end = Math.min(start + pageSize, companies.length);
        const rows = [];
        for (let index = start; index < end; index++) {
            const row = document.createElement('tr');
            row.classList.toggle('is-selected', selected.has(index));
            const selection = document.createElement('td');
            const checkbox = document.createElement('input');
            checkbox.type = 'checkbox';
            checkbox.checked = selected.has(index);
            checkbox.setAttribute('aria-label', `選取範例公司 ${companies[index]}`);
            checkbox.addEventListener('change', () => {
                if (checkbox.checked) selected.add(index); else selected.delete(index);
                row.classList.toggle('is-selected', checkbox.checked);
            });
            row.tabIndex = 0;
            row.addEventListener('click', event => {
                if (event.target.closest('input')) return;
                checkbox.checked = !checkbox.checked;
                checkbox.dispatchEvent(new Event('change'));
            });
            row.addEventListener('keydown', event => {
                if (event.target !== row || !['Enter', ' '].includes(event.key)) return;
                event.preventDefault();
                checkbox.checked = !checkbox.checked;
                checkbox.dispatchEvent(new Event('change'));
            });
            selection.append(checkbox);
            row.append(selection);
            for (const value of [`範例公司 ${companies[index]}`, `範例 ${String(index + 1).padStart(2, '0')}`]) {
                const cell = document.createElement('td');
                cell.textContent = value;
                row.append(cell);
            }
            const status = document.createElement('td');
            const badge = document.createElement('span');
            badge.className = 'theme-status complete';
            badge.textContent = '啟用';
            status.append(badge);
            row.append(status);
            rows.push(row);
        }
        table.tBodies[0].replaceChildren(...rows);
        summary.textContent = `第 ${start + 1}–${end} 筆，共 ${companies.length} 筆`;
        const buttons = [];
        const button = (text, target, label, disabled = false) => {
            const item = document.createElement('button');
            item.type = 'button';
            item.textContent = text;
            item.setAttribute('aria-label', label);
            item.disabled = disabled;
            if (Number(text) === currentPage) item.setAttribute('aria-current', 'page');
            item.addEventListener('click', () => {
                currentPage = target;
                render();
                pages.querySelector('[aria-current="page"]')?.focus({ preventScroll: true });
            });
            buttons.push(item);
        };
        button('‹', currentPage - 1, '範例上一頁', currentPage === 1);
        for (let page = 1; page <= count; page++) button(String(page), page, `範例第 ${page} 頁`);
        button('›', currentPage + 1, '範例下一頁', currentPage === count);
        pages.replaceChildren(...buttons);
    };
    size.addEventListener('change', () => { currentPage = 1; render(); });
    preview.querySelector('[data-theme-preview-refresh]')?.addEventListener('click', () => {
        setChartOpen(false);
        selectAccountRow(null);
        currentPage = 1;
        size.value = '2';
        selected.clear();
        selected.add(0);
        if (tabs.length) selectTab(tabs[0]);
        render();
    });
    render();
})();
