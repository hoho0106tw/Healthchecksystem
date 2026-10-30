(() => {
    const root = document.documentElement;
    const sync = () => {
        try {
            if (window.parent !== window && window.parent.healthSysApplyTheme) {
                const source = window.parent.document.documentElement;
                for (const [key, value] of Object.entries(source.dataset)) {
                    if (key.startsWith('theme')) root.dataset[key] = value;
                }
                for (let index = 0; index < source.style.length; index++) {
                    const key = source.style[index];
                    if (key.startsWith('--app-') || key.startsWith('--tech-black-') || key.startsWith('--gray-mode-') || ['--blue', '--blue-dark', '--blue-soft', '--bg', '--panel', '--panel-bg', '--text', '--text-muted', '--muted', '--line', '--soft-line', '--shadow'].includes(key)) {
                        root.style.setProperty(key, source.style.getPropertyValue(key));
                    }
                }
                // The parent already applies page zoom; avoid scaling embedded content twice.
                root.style.setProperty('--app-page-zoom', '1');
                return;
            }
            const saved = JSON.parse(localStorage.getItem('healthsys:theme-settings') || 'null');
            if (!saved) return;
            root.dataset.themePreference = saved.mode || 'light';
            root.dataset.themeMode = saved.mode === 'simple' ? 'light' : saved.mode === 'system' ? (matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light') : saved.mode || 'light';
            root.dataset.themeDensity = saved.density || 'standard';
            root.dataset.themeFont = saved.font || 'medium';
            root.dataset.themeRadius = 'true';
            root.dataset.themeRowHeight = 'true';
            root.dataset.themeMotion = String(saved.motion !== false);
            root.dataset.themeCardShadow = String(saved.cardShadow !== false);
            root.dataset.themeTextColor = /^#[0-9a-f]{6}$/i.test(saved.textColor || '') ? 'custom' : 'auto';
            const dark = root.dataset.themeMode === 'dark';
            const text = root.dataset.themeTextColor === 'custom' ? saved.textColor : dark ? '#dfebf8' : '#263850';
            root.style.setProperty('--app-text-color', text);
            root.style.setProperty('--app-radius', `${Math.max(4, Math.min(18, Number(saved.radius) || 8))}px`);
            root.style.setProperty('--app-row-height', `${Math.max(32, Math.min(52, Number(saved.rowHeight) || 40))}px`);
            root.style.setProperty('--app-font-scale', saved.font === 'small' ? '.9' : saved.font === 'large' ? '1.15' : '1');
            const accent = /^#[0-9a-f]{6}$/i.test(saved.color || '') ? saved.color : '#1677e8';
            for (const key of ['--app-accent', '--blue', '--blue-dark']) root.style.setProperty(key, accent);
            const mix = (color, base, amount) => '#' + [1, 3, 5].map(i => Math.round(parseInt(color.slice(i, i + 2), 16) * (1 - amount) + parseInt(base.slice(i, i + 2), 16) * amount).toString(16).padStart(2, '0')).join('');
            const surface = mix(accent, dark ? '#172033' : '#ffffff', dark ? .88 : .97);
            const soft = mix(accent, dark ? '#101a2b' : '#ffffff', dark ? .9 : .92);
            const strong = mix(accent, dark ? '#273449' : '#ffffff', dark ? .88 : .86);
            const background = mix(accent, dark ? '#0b1220' : '#ffffff', dark ? .94 : .95);
            const muted = mix(text, surface, .34), border = mix(accent, surface, dark ? .7 : .79);
            for (const [key, value] of Object.entries({ '--app-surface': surface, '--app-surface-soft': soft, '--app-surface-strong': strong, '--app-dark-surface': dark ? strong : mix(accent, '#152033', .88), '--app-bg': background, '--app-muted': muted, '--app-border': border, '--bg': background, '--panel': surface, '--panel-bg': surface, '--text': text, '--muted': muted, '--line': border, '--soft-line': border })) root.style.setProperty(key, value);
            root.style.setProperty('--app-text', text);
            root.style.setProperty('--app-text-muted', muted);
            root.style.setProperty('--app-surface-muted', soft);
            root.style.setProperty('--text-muted', muted);
            for (const prefix of ['--tech-black-', '--gray-mode-']) {
                for (const [key, value] of Object.entries({ bg: background, shell: background, panel: surface, 'panel-soft': soft, 'panel-strong': strong, text, muted, subtle: muted, border })) root.style.setProperty(prefix + key, value);
            }
            const zoom = parseInt(saved.zoom, 10);
            root.style.setProperty('--app-page-zoom', [90, 100, 110, 125].includes(zoom) ? String(zoom / 100) : '1');
        } catch { /* Cross-origin frames and unavailable local storage keep their default appearance. */ }
    };
    sync();
    try { if (window.parent !== window) window.parent.addEventListener('healthsys-theme-applied', sync); } catch {}
    window.addEventListener('storage', sync);
})();
