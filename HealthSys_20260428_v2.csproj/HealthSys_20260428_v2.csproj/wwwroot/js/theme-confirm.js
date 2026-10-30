(() => {
    const dialog = document.querySelector('[data-theme-confirm]');
    if (!dialog) return;
    let timer, restore, seconds;
    const finish = revert => {
        clearInterval(timer);
        const callback = restore;
        restore = null;
        dialog.close();
        if (revert) callback?.();
        document.querySelector('[data-theme-apply]')?.focus({ preventScroll: true });
    };
    window.healthSysConfirmThemeChange = callback => {
        clearInterval(timer);
        restore = callback;
        seconds = 15;
        const counter = dialog.querySelector('[data-theme-confirm-seconds]');
        counter.textContent = seconds;
        document.body.classList.remove('theme-switching');
        document.body.classList.add('theme-switching');
        setTimeout(() => document.body.classList.remove('theme-switching'), 280);
        if (!dialog.open) dialog.showModal();
        dialog.querySelector('[data-theme-confirm-keep]').focus();
        timer = setInterval(() => {
            counter.textContent = --seconds;
            if (seconds <= 0) finish(true);
        }, 1000);
    };
    dialog.querySelector('[data-theme-confirm-keep]').addEventListener('click', () => finish(false));
    dialog.querySelector('[data-theme-confirm-revert]').addEventListener('click', () => finish(true));
    dialog.addEventListener('cancel', event => { event.preventDefault(); finish(true); });
})();
