(() => {
    'use strict';
    const button = document.querySelector('[data-smart-service-button]');
    const dialog = document.querySelector('[data-ai-agent-detection]');
    if (!button || !dialog) return;
    const title = dialog.querySelector('[data-ai-agent-detection-title]');
    const message = dialog.querySelector('[data-ai-agent-detection-message]');
    const progress = dialog.querySelector('[data-ai-agent-detection-progress]');
    const action = dialog.querySelector('[data-ai-agent-detection-action]');
    let cancelDetection = null;
    const dismiss = () => { if (dialog.open) dialog.close(); };
    dialog.querySelectorAll('[data-ai-agent-dismiss]').forEach(item => item.addEventListener('click', dismiss));
    dialog.addEventListener('click', event => { if (event.target === dialog) dismiss(); });
    dialog.addEventListener('close', () => { cancelDetection?.(); });
    button.addEventListener('click', async () => {
        if (button.disabled) return;
        button.disabled = true;
        dialog.dataset.state = 'loading';
        title.textContent = '正在連接智慧精靈';
        message.textContent = '正在偵測 AI AGENT，請稍候…';
        action.textContent = '取消';
        progress.hidden = false;
        try {
            if (!dialog.open) dialog.showModal();
            const id = Array.from(crypto.getRandomValues(new Uint8Array(16)), byte => byte.toString(16).padStart(2, '0')).join('');
            await new Promise((resolve, reject) => {
                let timer;
                const finish = error => {
                    window.removeEventListener('message', receive);
                    clearTimeout(timer);
                    cancelDetection = null;
                    error ? reject(error) : resolve();
                };
                const receive = event => {
                    if (event.source !== window || event.origin !== location.origin || event.data?.source !== 'healthsys-agent-extension'
                        || event.data.id !== id) return;
                    finish(event.data.ok ? null : new Error('智慧精靈暫時無法開啟，請找系統負責人處理。'));
                };
                cancelDetection = () => { const error = new Error('已取消偵測'); error.name = 'AbortError'; finish(error); };
                window.addEventListener('message', receive);
                timer = setTimeout(() => finish(new Error('你系統未安裝AI AGENT，請找系統負責人處理。')), 5000);
                window.postMessage({ source: 'healthsys-agent-page', type: 'OPEN', id }, location.origin);
            });
            dismiss();
        } catch (error) {
            if (error.name === 'AbortError') return;
            dialog.dataset.state = 'error';
            title.textContent = error.message === '你系統未安裝AI AGENT，請找系統負責人處理。' ? '尚未安裝 AI AGENT' : '暫時無法連接智慧精靈';
            message.textContent = error.message === '你系統未安裝AI AGENT，請找系統負責人處理。'
                ? error.message : '智慧精靈暫時無法開啟，請找系統負責人處理。';
            progress.hidden = true;
            action.textContent = '我知道了';
            action.focus({ preventScroll: true });
        } finally { button.disabled = false; }
    });
})();
