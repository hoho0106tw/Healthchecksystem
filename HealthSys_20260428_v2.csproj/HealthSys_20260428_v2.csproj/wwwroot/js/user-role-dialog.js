(function () {
    "use strict";
    let queue = Promise.resolve();

    // Shared result dialog for the settings page and its role editor.
    window.showUserRoleResult = function ({ title, message, success = true }) {
        const show = () => new Promise(resolve => {
            const previousFocus = document.activeElement;
            const dialog = document.createElement("dialog");
            dialog.className = "user-role-result";
            dialog.dataset.state = success ? "success" : "error";
            dialog.setAttribute("aria-labelledby", "user-role-result-title");
            dialog.setAttribute("aria-describedby", "user-role-result-message");
            dialog.innerHTML = '<div class="user-role-result-accent"></div>' +
                '<div class="user-role-result-content">' +
                '<span class="user-role-result-icon" aria-hidden="true"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"></svg></span>' +
                '<span class="user-role-result-label">角色與功能設定</span>' +
                '<h2 id="user-role-result-title"></h2><p id="user-role-result-message"></p>' +
                '<button type="button" class="primary-button user-role-result-confirm" autofocus>確定</button></div>';
            dialog.querySelector("h2").textContent = title;
            dialog.querySelector("p").textContent = message;
            dialog.querySelector("svg").innerHTML = success
                ? '<path d="m5 12 4 4L19 6" />'
                : '<path d="m7 7 10 10M17 7 7 17" />';
            dialog.querySelector("button").addEventListener("click", () => dialog.close());
            // Keep Escape local to this dialog so it cannot also close an underlying editor.
            dialog.addEventListener("keydown", event => event.stopPropagation());
            dialog.addEventListener("close", () => {
                dialog.remove();
                if (previousFocus?.isConnected) previousFocus.focus();
                resolve();
            }, { once: true });
            document.body.appendChild(dialog);
            dialog.showModal();
        });
        queue = queue.then(show, show);
        return queue;
    };
})();
