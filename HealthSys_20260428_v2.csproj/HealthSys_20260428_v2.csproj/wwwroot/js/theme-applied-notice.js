(() => {
    const notice = document.querySelector('[data-theme-applied-notice]');
    if (!notice) return;
    const announcement = document.querySelector('[data-theme-applied-announcement]');
    let timer;
    const hide = () => {
        clearTimeout(timer);
        const restoreFocus = notice.contains(document.activeElement);
        notice.hidden = true;
        if (restoreFocus) document.querySelector('[data-theme-apply]')?.focus({ preventScroll: true });
    };
    window.healthSysShowThemeApplied = () => {
        hide();
        // Restart the entrance and marquee when settings are applied again.
        void notice.offsetWidth;
        notice.hidden = false;
        announcement.textContent = '';
        requestAnimationFrame(() => { announcement.textContent = '佈景主題已套用，您的外觀設定已儲存。'; });
        timer = setTimeout(hide, 1500);
    };
    document.addEventListener('keydown', event => {
        if (event.key === 'Escape' && !notice.hidden) {
            const restoreFocus = notice.contains(document.activeElement);
            hide();
            if (restoreFocus) document.querySelector('[data-theme-apply]')?.focus({ preventScroll: true });
        }
    });
})();
