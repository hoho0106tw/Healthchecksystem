(function () {
    const toggle = document.querySelector("[data-dashboard-auto-refresh]");
    const queryForm = document.querySelector(".dashboard-list-filters");
    if (!toggle || !queryForm) return;

    // Refresh the conditions that produced this page, including its antiforgery token.
    // Capture before the user edits the form so refresh cannot submit an unfinished query.
    const refreshData = new FormData(queryForm);
    const refreshAction = queryForm.action;

    function refreshDashboard() {
        const form = document.createElement("form");
        form.method = "post";
        form.action = refreshAction;
        form.hidden = true;
        refreshData.forEach(function (value, name) {
            const input = document.createElement("input");
            input.type = "hidden";
            input.name = name;
            input.value = value;
            form.appendChild(input);
        });
        document.body.appendChild(form);
        form.submit();
    }

    const storageKey = "healthsys:dashboard:autoRefresh:optIn";
    let timer = null;
    toggle.checked = false;
    try {
        toggle.checked = localStorage.getItem(storageKey) === "true";
    } catch (error) {
    }

    function updateTimer() {
        window.clearInterval(timer);
        timer = null;
        if (toggle.checked) {
            timer = window.setInterval(function () {
                refreshDashboard();
            }, 60000);
        }
    }

    toggle.addEventListener("change", function () {
        try {
            localStorage.setItem(storageKey, String(toggle.checked));
        } catch (error) {
        }
        updateTimer();
    });

    window.addEventListener("pagehide", function () {
        window.clearInterval(timer);
    });
    window.addEventListener("pageshow", updateTimer);
    updateTimer();
})();
