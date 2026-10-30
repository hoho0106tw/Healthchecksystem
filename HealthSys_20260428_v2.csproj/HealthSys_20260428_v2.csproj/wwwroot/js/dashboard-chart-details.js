(function () {
    const button = document.querySelector("[data-chart-details-toggle]");
    const tooltip = document.querySelector("[data-chart-details]");
    if (!button || !tooltip) return;
    function positionDetails() {
        if (tooltip.hidden) return;
        const rect = button.getBoundingClientRect();
        tooltip.style.left = Math.max(8, Math.min(rect.right - tooltip.offsetWidth, window.innerWidth - tooltip.offsetWidth - 8)) + "px";
        tooltip.style.top = Math.max(8, Math.min(rect.bottom + 8, window.innerHeight - tooltip.offsetHeight - 8)) + "px";
    }
    function close() {
        tooltip.hidden = true;
        button.setAttribute("aria-expanded", "false");
        button.focus();
    }
    button.addEventListener("click", function () {
        tooltip.hidden = false;
        button.setAttribute("aria-expanded", "true");
        positionDetails();
    });
    tooltip.querySelector("[data-chart-details-close]").addEventListener("click", close);
    document.addEventListener("keydown", function (event) {
        if (event.key === "Escape" && !tooltip.hidden) close();
    });
    button.addEventListener("transitionend", positionDetails);
    function reposition() { positionDetails(); }
    window.addEventListener("resize", reposition);
    window.addEventListener("scroll", reposition, true);
})();
