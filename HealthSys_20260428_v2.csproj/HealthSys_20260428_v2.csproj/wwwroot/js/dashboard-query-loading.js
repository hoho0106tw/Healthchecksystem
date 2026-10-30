(function () {
    const form = document.querySelector(".dashboard-list-filters");
    const overlay = document.querySelector("[data-dashboard-query-loading]");
    const button = form ? form.querySelector('[type="submit"]') : null;
    if (!form || !overlay) return;
    let querying = false;

    function reset() {
        querying = false;
        overlay.hidden = true;
        form.removeAttribute("aria-busy");
        if (button) button.disabled = false;
    }

    form.addEventListener("submit", function (event) {
        if (event.defaultPrevented) return;
        if (querying) {
            event.preventDefault();
            return;
        }
        querying = true;
        overlay.hidden = false;
        form.setAttribute("aria-busy", "true");
        if (button) button.disabled = true;
    });
    window.addEventListener("pageshow", reset);
    window.addEventListener("pagehide", reset);
    reset();
})();
