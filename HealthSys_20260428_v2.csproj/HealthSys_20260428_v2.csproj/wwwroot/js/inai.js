(() => {
    const panel = document.querySelector('[data-inai-panel]');
    if (!panel) return;
    const opener = document.querySelector('[data-inai-open]');
    const handle = document.querySelector('[data-inai-handle]');
    const input = panel.querySelector('[data-inai-input]');
    const form = panel.querySelector('[data-inai-form]');
    const send = panel.querySelector('[data-inai-send]');
    const clear = panel.querySelector('[data-inai-clear]');
    const messages = panel.querySelector('[data-inai-messages]');
    const status = panel.querySelector('[data-inai-status]');
    const welcome = panel.querySelector('[data-inai-empty]').cloneNode(true);
    let history = [], busy = false;
    const show = () => {
        panel.hidden = false;
        handle.hidden = true;
        opener.setAttribute('aria-expanded', 'true');
        handle.setAttribute('aria-expanded', 'true');
        input.focus({ preventScroll: true });
    };
    const hide = () => {
        panel.hidden = true;
        handle.hidden = false;
        opener.setAttribute('aria-expanded', 'false');
        handle.setAttribute('aria-expanded', 'false');
        handle.focus({ preventScroll: true });
    };
    opener.addEventListener('click', show);
    handle.addEventListener('click', show);
    panel.querySelector('[data-inai-close]').addEventListener('click', hide);
    panel.addEventListener('keydown', event => { if (event.key === 'Escape') hide(); });
    const append = (role, content) => {
        panel.querySelector('[data-inai-empty]')?.remove();
        const item = document.createElement('div');
        item.className = `inai-message inai-message-${role}`;
        const label = document.createElement('strong');
        label.textContent = role === 'user' ? '您' : '智能查詢';
        const text = document.createElement('p');
        text.textContent = content;
        item.append(label, text);
        messages.append(item);
        messages.scrollTop = messages.scrollHeight;
        return item;
    };
    clear.addEventListener('click', () => {
        if (busy) return;
        history = [];
        messages.replaceChildren();
        messages.append(welcome.cloneNode(true));
        status.textContent = '';
        input.value = '';
        input.focus({ preventScroll: true });
    });
    form.addEventListener('submit', async event => {
        event.preventDefault();
        const question = input.value.trim();
        if (busy || !question) return;
        busy = true;
        panel.dataset.busy = 'true';
        messages.setAttribute('aria-busy', 'true');
        send.disabled = clear.disabled = true;
        input.disabled = true;
        const userItem = append('user', question);
        input.value = '';
        status.textContent = '查詢中…';
        try {
            const response = await fetch(panel.dataset.chatUrl, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': form.querySelector('[name="__RequestVerificationToken"]').value },
                body: JSON.stringify({ messages: [...history.slice(-20), { role: 'user', content: question }] })
            });
            const data = await response.json().catch(() => ({}));
            if (!response.ok || !data.reply) throw new Error(data.error || `查詢失敗（HTTP ${response.status}），請稍後重試。`);
            append('assistant', data.reply);
            history.push({ role: 'user', content: question }, { role: 'assistant', content: data.reply });
            history = history.slice(-20);
            status.textContent = '';
        } catch (error) {
            userItem.remove();
            if (!messages.children.length) messages.append(welcome.cloneNode(true));
            input.value = question;
            status.textContent = error.message || '連線失敗，請稍後重試。';
        } finally {
            busy = false;
            delete panel.dataset.busy;
            messages.setAttribute('aria-busy', 'false');
            send.disabled = clear.disabled = input.disabled = false;
            if (!panel.hidden) input.focus({ preventScroll: true });
        }
    });
    input.addEventListener('keydown', event => {
        if (event.key === 'Enter' && !event.shiftKey && !event.isComposing) {
            event.preventDefault();
            form.requestSubmit();
        }
    });
})();

