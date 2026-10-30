(function () {
    const storageKey = "healthsys:sidebar";
    const collapsedClass = "sidebar-collapsed";
    const autoCollapsedClass = "sidebar-auto-collapsed";
    const root = document.documentElement;
    const sidebarToggle = document.querySelector(".sidebar-toggle-button");
    const menuToggle = document.querySelector(".menu-toggle");
    const toggles = [sidebarToggle, menuToggle].filter(Boolean);
    let preferredState = "expanded";

    if (!toggles.length) {
        return;
    }

    try {
        preferredState = localStorage.getItem(storageKey) || "expanded";
    } catch (error) {
    }

    function setToggleState(collapsed) {
        const sidebarLabel = collapsed ? "展開側邊選單" : "收合側邊選單";
        const menuLabel = collapsed ? "展開主選單" : "收合主選單";

        root.classList.toggle(collapsedClass, collapsed);
        root.classList.remove(autoCollapsedClass);

        if (sidebarToggle) {
            sidebarToggle.setAttribute("aria-expanded", String(!collapsed));
            sidebarToggle.setAttribute("aria-label", sidebarLabel);
            sidebarToggle.setAttribute("title", sidebarLabel);
        }

        if (menuToggle) {
            menuToggle.setAttribute("aria-expanded", String(!collapsed));
            menuToggle.setAttribute("aria-label", menuLabel);
            menuToggle.setAttribute("title", menuLabel);
        }
    }

    setToggleState(preferredState === "collapsed");

    function toggleSidebar() {
        const collapsed = !root.classList.contains(collapsedClass);

        preferredState = collapsed ? "collapsed" : "expanded";
        setToggleState(collapsed);

        try {
            localStorage.setItem(storageKey, preferredState);
        } catch (error) {
        }
    }

    toggles.forEach(function (toggle) {
        toggle.addEventListener("click", toggleSidebar);
    });

    document.querySelectorAll("[data-submenu]").forEach(function (group) {
        const submenuToggle = group.querySelector(".menu-submenu-toggle");

        if (!submenuToggle) {
            return;
        }

        function setSubmenuState(open) {
            const label = open
                ? submenuToggle.dataset.labelClose || "隱藏子選單"
                : submenuToggle.dataset.labelOpen || "開啟子選單";

            group.classList.toggle("is-open", open);
            submenuToggle.setAttribute("aria-expanded", String(open));
            submenuToggle.setAttribute("aria-label", label);
            submenuToggle.setAttribute("title", label);
        }

        setSubmenuState(group.classList.contains("is-open"));

        submenuToggle.addEventListener("click", function (event) {
            event.preventDefault();
            event.stopPropagation();
            setSubmenuState(!group.classList.contains("is-open"));
        });
    });

    const assistStorageKey = "healthsys:smartAssist";
    const assistPositionStorageKey = "healthsys:smartAssistPosition:v4";
    const smartAssist = document.querySelector("[data-smart-assist]");

    if (smartAssist) {
        const launcher = smartAssist.querySelector("[data-smart-assist-open]");
        const panel = smartAssist.querySelector(".smart-assist-window");
        const minimizeButton = smartAssist.querySelector("[data-smart-assist-minimize]");
        const dragMargin = 12;
        let assistState = "minimized";
        let hasCustomAssistPosition = false;
        let minimizedAssistPosition = null;
        let dragState = null;
        let suppressLauncherClick = false;

        try {
            const savedAssistState = localStorage.getItem(assistStorageKey);
            assistState = savedAssistState === "open" ? "open" : "minimized";
        } catch (error) {
        }

        function setAssistPosition(left, top) {
            const pageZoom = Number.parseFloat(root.style.getPropertyValue("--app-page-zoom")) || 1;
            smartAssist.style.right = "auto";
            smartAssist.style.bottom = "auto";
            smartAssist.style.left = Math.round(left / pageZoom) + "px";
            smartAssist.style.top = Math.round(top / pageZoom) + "px";
        }

        function getClampedAssistPosition(left, top) {
            const rect = smartAssist.getBoundingClientRect();
            const width = rect.width || smartAssist.offsetWidth || 48;
            const height = rect.height || smartAssist.offsetHeight || 48;
            const maxLeft = Math.max(dragMargin, window.innerWidth - width - dragMargin);
            const maxTop = Math.max(dragMargin, window.innerHeight - height - dragMargin);

            return {
                left: Math.min(Math.max(left, dragMargin), maxLeft),
                top: Math.min(Math.max(top, dragMargin), maxTop)
            };
        }

        function getAssistPosition() {
            const rect = smartAssist.getBoundingClientRect();

            return {
                left: Math.round(rect.left),
                top: Math.round(rect.top)
            };
        }

        function saveAssistPosition() {
            if (!hasCustomAssistPosition || !smartAssist.classList.contains("is-minimized")) {
                return;
            }

            const position = getAssistPosition();
            minimizedAssistPosition = position;

            try {
                localStorage.setItem(assistPositionStorageKey, JSON.stringify({
                    left: position.left,
                    top: position.top
                }));
            } catch (error) {
            }
        }

        function constrainAssistPosition() {
            const rect = smartAssist.getBoundingClientRect();
            const nextPosition = getClampedAssistPosition(rect.left, rect.top);
            if (!hasCustomAssistPosition && nextPosition.left === rect.left && nextPosition.top === rect.top) {
                return;
            }

            hasCustomAssistPosition = true;
            setAssistPosition(nextPosition.left, nextPosition.top);
            saveAssistPosition();
        }

        function applySavedAssistPosition() {
            let savedPosition = null;

            try {
                savedPosition = JSON.parse(localStorage.getItem(assistPositionStorageKey) || "null");
            } catch (error) {
            }

            if (!savedPosition || !Number.isFinite(savedPosition.left) || !Number.isFinite(savedPosition.top)) {
                return;
            }

            hasCustomAssistPosition = true;
            minimizedAssistPosition = {
                left: savedPosition.left,
                top: savedPosition.top
            };
            setAssistPosition(savedPosition.left, savedPosition.top);
            requestAnimationFrame(constrainAssistPosition);
        }

        function setAssistState(nextState) {
            const state = nextState === "minimized" ? nextState : "open";
            const expanded = state === "open";
            const wasMinimized = smartAssist.classList.contains("is-minimized");

            if (state === "open" && wasMinimized && hasCustomAssistPosition) {
                minimizedAssistPosition = getAssistPosition();
            }

            smartAssist.classList.toggle("is-minimized", state === "minimized");
            smartAssist.classList.remove("is-hidden");

            if (state === "minimized" && !wasMinimized && hasCustomAssistPosition && minimizedAssistPosition) {
                setAssistPosition(minimizedAssistPosition.left, minimizedAssistPosition.top);
            }

            if (launcher) {
                launcher.setAttribute("aria-expanded", String(expanded));
            }

            if (panel) {
                panel.setAttribute("aria-hidden", String(!expanded));
            }

            try {
                localStorage.setItem(assistStorageKey, state);
            } catch (error) {
            }

            requestAnimationFrame(constrainAssistPosition);
        }

        setAssistState(assistState);
        applySavedAssistPosition();

        if (launcher) {
            launcher.addEventListener("click", function (event) {
                if (suppressLauncherClick) {
                    event.preventDefault();
                    suppressLauncherClick = false;
                    return;
                }

                setAssistState("open");
            });

        }

        const panelDragHandle = smartAssist.querySelector("[data-smart-assist-drag]");
        [launcher, panelDragHandle].filter(Boolean).forEach(function (handle) {
            handle.addEventListener("pointerdown", function (event) {
                if (event.button !== 0 || !event.isPrimary || dragState) {
                    return;
                }

                const rect = smartAssist.getBoundingClientRect();

                dragState = {
                    pointerId: event.pointerId,
                    startX: event.clientX,
                    startY: event.clientY,
                    offsetX: event.clientX - rect.left,
                    offsetY: event.clientY - rect.top,
                    moved: false
                };

                smartAssist.classList.add("is-dragging");
                handle.setPointerCapture(event.pointerId);
            });

            handle.addEventListener("pointermove", function (event) {
                if (!dragState || dragState.pointerId !== event.pointerId) {
                    return;
                }

                if (Math.hypot(event.clientX - dragState.startX, event.clientY - dragState.startY) > 4) {
                    dragState.moved = true;
                }

                if (!dragState.moved) {
                    return;
                }

                const nextPosition = getClampedAssistPosition(
                    event.clientX - dragState.offsetX,
                    event.clientY - dragState.offsetY
                );

                hasCustomAssistPosition = true;
                setAssistPosition(nextPosition.left, nextPosition.top);
                event.preventDefault();
            });

            function finishAssistDrag(event) {
                if (!dragState || dragState.pointerId !== event.pointerId) {
                    return;
                }

                suppressLauncherClick = handle === launcher && dragState.moved;
                dragState = null;
                smartAssist.classList.remove("is-dragging");
                saveAssistPosition();

                if (handle.hasPointerCapture(event.pointerId)) {
                    handle.releasePointerCapture(event.pointerId);
                }
            }

            handle.addEventListener("pointerup", finishAssistDrag);
            handle.addEventListener("pointercancel", finishAssistDrag);
            handle.addEventListener("lostpointercapture", finishAssistDrag);
        });

        if (minimizeButton) {
            minimizeButton.addEventListener("click", function () {
                setAssistState("minimized");
            });
        }

        let assistResizeFrame = 0;
        function scheduleAssistConstraints() {
            if (assistResizeFrame) return;
            assistResizeFrame = requestAnimationFrame(function () {
                assistResizeFrame = 0;
                constrainAssistPosition();
            });
        }
        window.addEventListener("resize", scheduleAssistConstraints);
        window.addEventListener("healthsys-theme-applied", scheduleAssistConstraints);
    }
})();
