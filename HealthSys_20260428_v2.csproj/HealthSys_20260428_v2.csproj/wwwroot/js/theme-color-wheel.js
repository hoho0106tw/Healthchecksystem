(function (root) {
    "use strict";

    function hsvToHex(hue, saturation, value) {
        const h = ((hue % 360) + 360) % 360 / 60;
        const chroma = value * saturation;
        const x = chroma * (1 - Math.abs(h % 2 - 1));
        const segments = [[chroma, x, 0], [x, chroma, 0], [0, chroma, x], [0, x, chroma], [x, 0, chroma], [chroma, 0, x]];
        return "#" + segments[Math.floor(h)].map(channel =>
            Math.round((channel + value - chroma) * 255).toString(16).padStart(2, "0")).join("");
    }

    function hexToHsv(hex) {
        const rgb = [1, 3, 5].map(index => parseInt(hex.slice(index, index + 2), 16) / 255);
        const [red, green, blue] = rgb;
        const max = Math.max(...rgb), min = Math.min(...rgb), delta = max - min;
        let hue = 0;
        if (delta) {
            hue = max === red ? (green - blue) / delta : max === green ? (blue - red) / delta + 2 : (red - green) / delta + 4;
            hue = ((hue * 60) + 360) % 360;
        }
        return { hue, saturation: max ? delta / max : 0, value: max };
    }

    function createColorPicker(page, onChange) {
        const wheel = page.querySelector("[data-theme-color-wheel]");
        const marker = page.querySelector("[data-theme-color-marker]");
        const colorInput = page.querySelector("[data-theme-custom-color]");
        const hexInput = page.querySelector("[data-theme-color-hex]");
        const brightness = page.querySelector("[data-theme-color-brightness]");
        const output = page.querySelector("[data-theme-color-brightness-output]");
        if (!wheel || !marker || !colorInput || !hexInput || !brightness) return null;

        let hsv = hexToHsv(colorInput.value);
        let activePointer = null;
        let pointerFrame = 0;
        let pendingPointer = null;

        function render(color) {
            const angle = hsv.hue * Math.PI / 180;
            marker.style.left = `${50 + Math.sin(angle) * hsv.saturation * 50}%`;
            marker.style.top = `${50 - Math.cos(angle) * hsv.saturation * 50}%`;
            marker.style.backgroundColor = color;
            wheel.style.setProperty("--wheel-darkness", String(1 - hsv.value));
            wheel.setAttribute("aria-valuenow", String(Math.round(hsv.hue)));
            wheel.setAttribute("aria-valuetext", `${color}，色相 ${Math.round(hsv.hue)} 度，飽和度 ${Math.round(hsv.saturation * 100)}%`);
            colorInput.value = color;
            hexInput.value = color;
            hexInput.setCustomValidity("");
            brightness.value = String(Math.round(hsv.value * 100));
            if (output) output.textContent = `${brightness.value}%`;
        }

        function emit() {
            const color = hsvToHex(hsv.hue, hsv.saturation, hsv.value);
            render(color);
            onChange(color);
        }

        function chooseAtPointer(event) {
            const rect = wheel.getBoundingClientRect();
            const x = (event.clientX - rect.left - rect.width / 2) / (rect.width / 2);
            const y = (event.clientY - rect.top - rect.height / 2) / (rect.height / 2);
            hsv.hue = (Math.atan2(x, -y) * 180 / Math.PI + 360) % 360;
            hsv.saturation = Math.min(1, Math.hypot(x, y));
            emit();
        }

        wheel.addEventListener("pointerdown", event => {
            if (activePointer !== null || (event.button != null && event.button !== 0)) return;
            event.preventDefault();
            activePointer = event.pointerId;
            wheel.setPointerCapture(event.pointerId);
            wheel.focus();
            chooseAtPointer(event);
        });
        wheel.addEventListener("pointermove", event => {
            if (event.pointerId !== activePointer) return;
            if (!root.requestAnimationFrame) { chooseAtPointer(event); return; }
            pendingPointer = { clientX: event.clientX, clientY: event.clientY };
            if (pointerFrame) return;
            pointerFrame = root.requestAnimationFrame(() => {
                pointerFrame = 0;
                const pointer = pendingPointer;
                pendingPointer = null;
                if (pointer && activePointer !== null) chooseAtPointer(pointer);
            });
        });
        function endPointer(event) {
            if (event.pointerId !== activePointer) return;
            if (pointerFrame) root.cancelAnimationFrame(pointerFrame);
            pointerFrame = 0;
            if (pendingPointer && event.type === "pointerup") chooseAtPointer(pendingPointer);
            pendingPointer = null;
            activePointer = null;
        }
        wheel.addEventListener("pointerup", endPointer);
        wheel.addEventListener("pointercancel", endPointer);
        wheel.addEventListener("lostpointercapture", endPointer);
        wheel.addEventListener("keydown", event => {
            const step = event.shiftKey ? 10 : 2;
            if (event.key === "ArrowRight") hsv.hue = (hsv.hue + step) % 360;
            else if (event.key === "ArrowLeft") hsv.hue = (hsv.hue - step + 360) % 360;
            else if (event.key === "ArrowUp") hsv.saturation = Math.min(1, hsv.saturation + step / 100);
            else if (event.key === "ArrowDown") hsv.saturation = Math.max(0, hsv.saturation - step / 100);
            else return;
            event.preventDefault();
            emit();
        });
        brightness.addEventListener("input", () => {
            hsv.value = Number(brightness.value) / 100;
            emit();
        });
        function chooseHex(color) {
            if (!/^#[0-9a-f]{6}$/i.test(color)) return false;
            hsv = hexToHsv(color);
            render(color.toLowerCase());
            onChange(color.toLowerCase());
            return true;
        }
        colorInput.addEventListener("input", () => chooseHex(colorInput.value));
        hexInput.addEventListener("input", () => {
            if (!chooseHex(hexInput.value.trim())) hexInput.setCustomValidity("請輸入六位色碼，例如 #1677e8。");
        });
        render(colorInput.value);
        return {
            setColor(color) {
                if (!/^#[0-9a-f]{6}$/i.test(color)) return;
                // Preserve hue/saturation while brightness is zero, or the color
                // rounds to gray. Changing brightness should recover that hue.
                if (color.toLowerCase() !== hsvToHex(hsv.hue, hsv.saturation, hsv.value)) hsv = hexToHsv(color);
                render(color.toLowerCase());
            }
        };
    }

    root.healthSysCreateColorPicker = createColorPicker;
    if (typeof module !== "undefined" && module.exports) module.exports = { hsvToHex, hexToHsv, createColorPicker };
})(typeof window !== "undefined" ? window : globalThis);
