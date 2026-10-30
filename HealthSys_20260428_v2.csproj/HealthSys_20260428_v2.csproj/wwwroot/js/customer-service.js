(() => {
    'use strict';
    const dialog = document.querySelector('[data-customer-chat]');
    if (!dialog) return;
    const form = dialog.querySelector('[data-customer-chat-form]');
    const input = dialog.querySelector('textarea');
    const messages = dialog.querySelector('[data-customer-chat-messages]');
    const send = form.querySelector('[type=submit]');
    const reply = dialog.dataset.unavailableMessage;
    const welcome = dialog.querySelector('[data-customer-chat-welcome]').cloneNode(true);
    const clear = dialog.querySelector('[data-customer-chat-clear]');
    const fontSize = dialog.querySelector('[data-customer-chat-font]');
    let busy = false;
    let opener = null;
    document.querySelectorAll('[data-smart-service-button]').forEach(button => {
        button.addEventListener('click', () => {
            opener = button;
            if (!dialog.open) dialog.showModal();
            input.focus();
        });
    });
    dialog.querySelector('[data-customer-chat-close]').addEventListener('click', () => dialog.close());
    dialog.addEventListener('close', () => opener?.focus({ preventScroll: true }));
    clear.addEventListener('click', () => {
        if (busy) return;
        messages.replaceChildren(welcome.cloneNode(true));
        input.value = '';
        input.focus();
    });
    fontSize.addEventListener('click', () => {
        fontSize.setAttribute('aria-pressed', String(dialog.classList.toggle('large-text')));
    });
    function append(role, text) {
        dialog.querySelector('.customer-chat-welcome')?.remove();
        const message = document.createElement('div');
        message.className = 'message-row customer-chat-message ' + role;
        if (role !== 'user') {
            const avatar = document.createElement('span');
            avatar.className = 'avatar';
            avatar.textContent = '✦';
            message.append(avatar);
        }
        const body = document.createElement('div');
        body.className = 'message-body';
        const label = document.createElement('strong');
        label.className = 'message-label';
        label.textContent = role === 'user' ? '您' : '健檢智慧精靈';
        const content = document.createElement('p');
        content.className = 'bubble';
        content.textContent = text;
        body.append(label, content);
        message.append(body);
        messages.append(message);
        messages.scrollTop = messages.scrollHeight;
    }
    form.addEventListener('submit', async event => {
        event.preventDefault();
        const text = input.value.trim();
        if (!text || busy) return;
        append('user', text);
        input.value = '';
        busy = true;
        send.disabled = input.disabled = clear.disabled = true;
        messages.setAttribute('aria-busy', 'true');
        try {
            // No page context, command parsing, extension detection or external AI call.
            await fetch(dialog.dataset.chatUrl, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': form.querySelector('[name=__RequestVerificationToken]').value },
                body: JSON.stringify({ message: text }),
                signal: AbortSignal.timeout(5000)
            });
        } catch (_) {
            // Display the same reply when the server cannot be reached.
        } finally {
            append('assistant', reply);
            busy = false;
            send.disabled = input.disabled = clear.disabled = false;
            messages.setAttribute('aria-busy', 'false');
            if (dialog.open) input.focus();
        }
    });
    input.addEventListener('keydown', event => {
        if (event.key === 'Enter' && !event.shiftKey && !event.isComposing) {
            event.preventDefault();
            form.requestSubmit();
        }
    });
})();
