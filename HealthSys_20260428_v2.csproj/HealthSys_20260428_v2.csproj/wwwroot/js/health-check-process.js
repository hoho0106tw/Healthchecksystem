(() => {
    'use strict';
    const page = document.querySelector('.station-page');
    if (!page) return;
    const form = page.querySelector('#sp-filter');
    const fields = form.elements;
    const feedback = page.querySelector('#sp-feedback');
    const types = { colon: ['大腸鏡', 'pink'], stomach: ['胃鏡', 'orange'], ct: ['CT影像', 'teal'], mr: ['MR影像', 'purple'], xray: ['一般X光影像', 'blue'] };
    const statuses = { arrived: '報到', checking: '檢查中', waiting: '等待中', completed: '已完成' };
    // Reference-image fixtures. No clinical records or annual quotas are persisted.
    const rows = [
        ['08:00', 'colon', '內視鏡室1', '陳○○', 'A1234*****', '林醫師', 'arrived', 'HC0001'],
        ['08:20', 'stomach', '內視鏡室2', '王○○', 'B9876*****', '張醫師', 'checking', 'HC0002'],
        ['08:30', 'ct', 'CT室1', '李○○', 'C1357*****', '陳醫師', 'waiting', 'HC0003'],
        ['08:40', 'mr', 'MR室1', '林○○', 'D2468*****', '黃醫師', 'completed', 'HC0004'],
        ['09:00', 'xray', 'X光室1', '張○○', 'E1357*****', '吳醫師', 'waiting', 'HC0005'],
        ['09:10', 'colon', '內視鏡室1', '劉○○', 'F2468*****', '林醫師', 'waiting', 'HC0006'],
        ['09:20', 'ct', 'CT室2', '黃○○', 'G3579*****', '陳醫師', 'arrived', 'HC0007'],
        ['09:40', 'stomach', '內視鏡室2', '趙○○', 'H2468*****', '張醫師', 'waiting', 'HC0008']
    ].map((r, index) => ({ id: index + 1, date: '2024-06-17', time: r[0], type: r[1], room: r[2], name: r[3], identity: r[4], doctor: r[5], status: r[6], record: r[7] }));
    const modes = {
        'quota-management': { title: '總額查詢', types: ['colon', 'stomach', 'ct', 'mr', 'xray'] },
        'scope-management': { title: '鏡檢管理', types: ['colon', 'stomach'] },
        'imaging-management': { title: '影像排程', types: ['ct', 'mr', 'xray'] }
    };
    let mode = 'quota-management';
    let filtered = rows.slice();
    const settingsButton = page.querySelector('[data-quota-settings]');
    const quotaRows = [...page.querySelectorAll('[data-quota]')];
    const navigation = [...document.querySelectorAll('[data-process-nav]')];
    function modeRows() { return rows.filter(row => modes[mode].types.includes(row.type)); }
    function categoryMatches(row, category) { return !category || row.type === category || (category === 'scope' && ['colon', 'stomach'].includes(row.type)); }
    function fillSelect(select, entries, placeholder) {
        select.replaceChildren(new Option(placeholder, ''), ...entries.map(([value, label]) => new Option(label, value)));
    }
    function applyMode() {
        const allowed = modes[mode].types;
        page.dataset.mode = mode;
        page.setAttribute('aria-label', modes[mode].title);
        settingsButton.hidden = mode !== 'scope-management';
        const categories = allowed.map(type => [type, types[type][0]]);
        if (mode === 'quota-management') categories.unshift(['scope', '鏡檢']);
        fillSelect(fields.category, categories, '全部檢查類別');
        fillSelect(fields.room, [...new Set(modeRows().map(row => row.room))].map(value => [value, value]), '全部檢查站');
        fillSelect(fields.doctor, [...new Set(modeRows().map(row => row.doctor))].map(value => [value, value]), '全部醫師');
        fields.keyword.value = '';
        page.querySelectorAll('[data-category]').forEach(button => {
            const category = button.dataset.category;
            button.hidden = category === 'scope' ? mode === 'imaging-management' : category !== '' && !allowed.includes(category);
        });
        quotaRows.forEach(row => { row.hidden = !allowed.includes(row.dataset.quota); });
        page.querySelector('.sp-summary').style.setProperty('--sp-summary-columns', mode === 'quota-management' ? '6' : '3');
        navigation.forEach(link => {
            const active = link.dataset.processNav === mode;
            link.classList.toggle('active', active);
            if (active) link.setAttribute('aria-current', 'page');
            else link.removeAttribute('aria-current');
        });
    }
    function updateStatistics() {
        page.querySelectorAll('[data-category]').forEach(button => {
            const count = filtered.filter(row => categoryMatches(row, button.dataset.category)).length;
            const number = button.querySelector('b');
            if (number) number.textContent = count;
        });
        const counts = ['completed', 'checking', 'waiting', 'arrived'].map(status => filtered.filter(row => row.status === status).length);
        const total = filtered.length;
        const legend = [...page.querySelectorAll('.sp-status-legend li')];
        const labels = ['已完成', '檢查中', '等待中', '已報到'];
        const colors = ['#43b77c', '#398cf5', '#ffbc50', '#a9b5c7'];
        let previous = 0;
        const segments = counts.map((count, i) => {
            const end = previous + (total ? count * 100 / total : 0);
            const segment = `${colors[i]} ${previous}% ${end}%`;
            previous = end;
            legend[i].querySelector('span').textContent = labels[i];
            legend[i].querySelector('b').textContent = count;
            legend[i].querySelector('small').textContent = `${total ? Math.round(count * 100 / total) : 0}%`;
            return segment;
        });
        const donut = page.querySelector('.sp-donut');
        donut.querySelector('b').textContent = total;
        donut.style.background = total ? `conic-gradient(${segments.join(', ')})` : 'var(--app-border)';
        donut.setAttribute('aria-label', `共${total}人次：${counts.map((count, i) => labels[i] + count).join('、')}`);
    }
    function announce(message) { feedback.hidden = false; feedback.textContent = message; }
    function cell(value) { const td = document.createElement('td'); td.textContent = value; return td; }
    function render() {
        const body = page.querySelector('#sp-schedule-rows');
        body.replaceChildren();
        filtered.forEach(row => {
            const tr = document.createElement('tr');
            tr.append(cell(row.id), cell(row.time));
            const typeCell = cell('');
            const badge = document.createElement('span');
            badge.className = `sp-badge sp-${types[row.type][1]}`;
            badge.textContent = types[row.type][0];
            typeCell.append(badge);
            tr.append(typeCell, cell(row.room), cell(row.name), cell(row.identity), cell(row.doctor));
            const statusCell = cell('');
            const status = document.createElement('span');
            status.className = `sp-badge sp-status-${row.status}`;
            status.textContent = statuses[row.status];
            statusCell.append(status);
            const actionCell = cell('');
            const button = document.createElement('button');
            button.type = 'button'; button.className = 'sp-row-detail'; button.textContent = '⋯';
            button.setAttribute('aria-label', `查看${row.name}的檢查明細`);
            button.addEventListener('click', () => openDetail(row));
            actionCell.append(button); tr.append(statusCell, actionCell); body.append(tr);
        });
        if (!filtered.length) {
            const tr = document.createElement('tr'); const td = cell('查無符合條件的排程'); td.colSpan = 9; tr.append(td); body.append(tr);
        }
        page.querySelectorAll('[data-category]').forEach(button => button.setAttribute('aria-pressed', String(button.dataset.category === fields.category.value)));
        updateStatistics();
    }
    function query() {
        fields.end.setCustomValidity(fields.end.value < fields.start.value ? '結束日期不能早於開始日期。' : '');
        if (!form.reportValidity()) { filtered = []; render(); return; }
        const keyword = fields.keyword.value.trim().toLowerCase();
        filtered = modeRows().filter(row => row.date >= fields.start.value && row.date <= fields.end.value
            && categoryMatches(row, fields.category.value)
            && (!fields.room.value || row.room === fields.room.value)
            && (!fields.doctor.value || row.doctor === fields.doctor.value)
            && (!keyword || [row.name, row.identity, row.record].some(value => value.toLowerCase().includes(keyword))));
        render();
        const start = fields.start.value.replaceAll('-', '/'); const end = fields.end.value.replaceAll('-', '/');
        page.querySelector('#sp-schedule-date').textContent = `(${start}${start === end ? '' : ' ～ ' + end})`;
        announce(`${modes[mode].title}：共 ${filtered.length} 筆示範排程。`);
    }
    form.addEventListener('submit', event => { event.preventDefault(); query(); });
    [fields.start, fields.end].forEach(input => input.addEventListener('input', () => fields.end.setCustomValidity('')));
    form.addEventListener('reset', () => {
        fields.end.setCustomValidity('');
        queueMicrotask(() => { applyMode(); query(); announce(`已重設查詢條件，保留${modes[mode].title}的資料範圍。`); });
    });
    page.querySelectorAll('[data-category]').forEach(button => button.addEventListener('click', () => { fields.category.value = button.dataset.category; query(); }));
    function openDetail(row) {
        const content = page.querySelector('#sp-detail-content'); content.replaceChildren();
        const values = { '檢查日期': row.date, '預約時間': row.time, '檢查類別': types[row.type][0], '檢查室': row.room, '受檢者姓名': row.name, '身分證號': row.identity, '病歷號': row.record, '醫師': row.doctor, '狀態': statuses[row.status] };
        Object.entries(values).forEach(([label, value]) => { const dt = document.createElement('dt'); dt.textContent = label; const dd = document.createElement('dd'); dd.textContent = value; content.append(dt, dd); });
        page.querySelector('#sp-detail-dialog').showModal();
    }
    page.querySelectorAll('[data-close-dialog]').forEach(button => button.addEventListener('click', () => button.closest('dialog').close()));
    page.querySelector('[data-export]').addEventListener('click', () => {
        const csvCell = value => `"${String(value).replaceAll('"', '""')}"`;
        const csv = [['序號', '檢查日期', '預約時間', '檢查類別', '檢查室', '姓名', '身分證號', '病歷號', '醫師', '狀態'], ...filtered.map(row => [row.id, row.date, row.time, types[row.type][0], row.room, row.name, row.identity, row.record, row.doctor, statuses[row.status]])].map(row => row.map(csvCell).join(',')).join('\r\n');
        const url = URL.createObjectURL(new Blob(['\uFEFF' + csv], { type: 'text/csv;charset=utf-8' }));
        const link = document.createElement('a'); link.href = url; link.download = '檢查排程_示範資料.csv'; document.body.append(link); link.click(); link.remove(); setTimeout(() => URL.revokeObjectURL(url), 1000);
        announce(`已匯出 ${filtered.length} 筆示範排程。`);
    });
    settingsButton.addEventListener('click', () => {
        if (mode !== 'scope-management') return;
        const container = page.querySelector('#sp-quota-fields');
        container.replaceChildren();
        quotaRows.filter(row => modes['scope-management'].types.includes(row.dataset.quota)).forEach(row => {
            const label = document.createElement('label');
            label.textContent = `${row.dataset.name} 年度額度（人次）`;
            const input = document.createElement('input');
            input.type = 'number'; input.name = row.dataset.quota; input.min = 1; input.max = 100000000; input.step = 1; input.required = true; input.value = row.dataset.limit;
            label.append(input); container.append(label);
        });
        page.querySelector('#sp-quota-dialog').showModal();
    });
    page.querySelector('#sp-quota-form').addEventListener('submit', event => {
        event.preventDefault();
        if (mode !== 'scope-management' || !event.target.reportValidity()) return;
        quotaRows.filter(row => modes['scope-management'].types.includes(row.dataset.quota)).forEach(row => {
            const limit = Number(event.target.elements[row.dataset.quota].value);
            const used = Number(row.dataset.used);
            row.dataset.limit = String(limit);
            row.querySelector('progress').max = limit;
            row.querySelector('.sp-quota-value').textContent = `${used.toLocaleString('zh-TW')} / ${limit.toLocaleString('zh-TW')} 人次`;
            row.querySelector('.sp-percent').textContent = `${Math.round(used * 100 / limit)}%`;
        });
        page.querySelector('#sp-quota-dialog').close();
        announce('胃鏡與大腸鏡示範額度已更新，重新整理後還原。');
    });
    function navigate(scroll = true) {
        const target = window.location.hash.slice(1);
        const legacyCategory = Object.hasOwn(types, target) ? target : '';
        mode = Object.hasOwn(modes, target) ? target : legacyCategory ? (['colon', 'stomach'].includes(target) ? 'scope-management' : 'imaging-management') : 'quota-management';
        page.querySelector('#sp-quota-dialog').close();
        page.querySelector('#sp-detail-dialog').close();
        applyMode();
        if (legacyCategory) fields.category.value = legacyCategory;
        query();
        if (scroll) page.scrollIntoView({ block: 'start' });
    }
    navigation.forEach(link => link.addEventListener('click', event => {
        event.preventDefault();
        const target = link.dataset.processNav;
        if (window.location.hash.slice(1) === target) navigate();
        else window.location.hash = target;
    }));
    window.addEventListener('hashchange', () => navigate());
    navigate(false);
})();
