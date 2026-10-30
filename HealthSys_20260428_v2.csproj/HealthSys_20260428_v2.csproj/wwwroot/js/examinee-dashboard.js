(function () {
    const table = document.querySelector(".examinee-table");
    const tableBody = table ? table.querySelector("tbody") : null;
    const detailsPanel = document.querySelector(".details-panel");
    const progressPanel = document.querySelector(".progress-panel");
    const flowPanel = document.querySelector(".flow-panel");
    const alertPanel = document.querySelector(".alert-panel");
    const pagination = document.querySelector(".pagination");
    const pageSelect = document.querySelector("[data-dashboard-page-size]");
    const pageSummary = document.querySelector("[data-page-summary]");
    const myHealthModal = document.querySelector("[data-my-health-modal]");
    const openMyHealthButton = document.querySelector("[data-open-my-health]");
    const closeMyHealthButtons = Array.from(document.querySelectorAll("[data-close-my-health]"));
    const myHealthToggleSizeButton = document.querySelector("[data-my-health-toggle-size]");
    const myHealthFrame = document.querySelector("[data-my-health-frame]");

    if (!table || !tableBody || !detailsPanel) {
        return;
    }

    const initialRows = Array.from(tableBody.querySelectorAll("tr[data-booking-time]"));

    const statusClasses = ["slot", "cancelled", "reserved", "registered", "checking", "done", "arrived", "unfinished"];
    const statusText = {
        checking: "檢查中",
        done: "已完成",
        arrived: "已報到",
        unfinished: "未完成"
    };
    let pageSize = readPositiveNumber(pageSelect ? pageSelect.value : "", 20);
    const declaredTotalItems = readPositiveNumber(
        pagination ? pagination.getAttribute("data-total-items") : "",
        initialRows.length
    );
    const seedRecords = initialRows.map(readRecord);
    const records = seedRecords;
    const rowRecords = new WeakMap();
    let visibleRows = [];
    let currentPage = 1;
    let previousMyHealthFocus = null;
    let selectedPatientNumber = "";

    const detailTargets = {
        name: detailsPanel.querySelector('[data-detail="name"]'),
        status: detailsPanel.querySelector('[data-detail="status"]'),
        serial: detailsPanel.querySelector('[data-detail="serial"]'),
        examNumber: detailsPanel.querySelector('[data-detail="examNumber"]'),
        patientNumber: detailsPanel.querySelector('[data-detail="patientNumber"]'),
        idNumber: detailsPanel.querySelector('[data-detail="idNumber"]'),
        genderAge: detailsPanel.querySelector('[data-detail="genderAge"]'),
        station: detailsPanel.querySelector('[data-detail="station"]'),
        checkInTime: detailsPanel.querySelector('[data-detail="checkInTime"]')
    };
    const profileTargets = flowPanel
        ? {
            name: flowPanel.querySelector('[data-profile="name"]'),
            genderAge: flowPanel.querySelector('[data-profile="genderAge"]'),
            examNumber: flowPanel.querySelector('[data-profile="examNumber"]'),
            idNumber: flowPanel.querySelector('[data-profile="idNumber"]'),
            station: flowPanel.querySelector('[data-profile="station"]'),
            checkInTime: flowPanel.querySelector('[data-profile="checkInTime"]')
        }
        : {};
    const flowTargets = flowPanel
        ? {
            name: flowPanel.querySelector('[data-flow="name"]'),
            currentStation: flowPanel.querySelector('[data-flow="currentStation"]'),
            waitText: flowPanel.querySelector('[data-flow="waitText"]'),
            currentTime: flowPanel.querySelector('[data-flow="currentTime"]'),
            stationCard: flowPanel.querySelector('[data-flow="stationCard"]'),
            stationIcon: flowPanel.querySelector('[data-flow="stationCard"] .alert-icon'),
            steps: {
                booking: flowPanel.querySelector('[data-flow-step="booking"]'),
                checkin: flowPanel.querySelector('[data-flow-step="checkin"]'),
                inspection: flowPanel.querySelector('[data-flow-step="inspection"]'),
                registration: flowPanel.querySelector('[data-flow-step="registration"]'),
                finish: flowPanel.querySelector('[data-flow-step="finish"]')
            },
            stepTimes: {
                booking: flowPanel.querySelector('[data-flow-step-time="booking"]'),
                checkin: flowPanel.querySelector('[data-flow-step-time="checkin"]'),
                inspection: flowPanel.querySelector('[data-flow-step-time="inspection"]'),
                registration: flowPanel.querySelector('[data-flow-step-time="registration"]'),
                finish: flowPanel.querySelector('[data-flow-step-time="finish"]')
            }
        }
        : {};
    const metricTargets = {
        values: {
            total: document.querySelector('[data-metric-value="total"]'),
            done: document.querySelector('[data-metric-value="done"]'),
            checking: document.querySelector('[data-metric-value="checking"]'),
            unfinished: document.querySelector('[data-metric-value="unfinished"]')
        },
        percents: {
            done: document.querySelector('[data-metric-percent="done"]'),
            checking: document.querySelector('[data-metric-percent="checking"]'),
            unfinished: document.querySelector('[data-metric-percent="unfinished"]')
        },
        cards: {
            total: document.querySelector('[data-metric-card="total"]'),
            done: document.querySelector('[data-metric-card="done"]'),
            checking: document.querySelector('[data-metric-card="checking"]'),
            unfinished: document.querySelector('[data-metric-card="unfinished"]')
        }
    };
    const alertTargets = alertPanel
        ? {
            list: alertPanel.querySelector("[data-alert-list]"),
            total: alertPanel.querySelector("[data-alert-total]")
        }
        : {};

    function readPositiveNumber(value, fallback) {
        const parsedValue = Number.parseInt(value, 10);

        return Number.isFinite(parsedValue) && parsedValue > 0 ? parsedValue : fallback;
    }

    function getCellText(row, index) {
        const cell = row.cells[index];

        return cell ? cell.textContent.trim() : "";
    }

    function getStatus(row) {
        const badge = row.cells[8] ? row.cells[8].querySelector(".status") : null;
        const className = badge
            ? statusClasses.find(function (name) {
                return badge.classList.contains(name);
            })
            : "";

        return {
            text: badge ? badge.textContent.trim() : getCellText(row, 8),
            className: className || "unfinished"
        };
    }

    function getProgressNumber(row, name, fallback) {
        const value = Number.parseInt(row.getAttribute("data-progress-" + name), 10);

        return Number.isFinite(value) ? value : fallback;
    }

    function readRecord(row) {
        const status = getStatus(row);
        const fallback = { completed: 0, checking: 0, pending: 0, total: 0 };
        const examNumber = getCellText(row, 2);

        return {
            serial: getCellText(row, 1),
            examNumber: examNumber,
            patientNumber: getCellText(row, 3),
            name: getCellText(row, 4),
            idNumber: getCellText(row, 5),
            gender: getCellText(row, 6),
            age: getCellText(row, 7),
            status: status,
            station: getCellText(row, 9),
            reservationDate: getCellText(row, 10),
            reservationTime: getCellText(row, 11),
            checkInDate: getCellText(row, 12),
            checkInTime: getCellText(row, 13),
            bookingTime: (row.getAttribute("data-booking-time") || "").trim(),
            progress: {
                completed: getProgressNumber(row, "completed", fallback.completed),
                checking: getProgressNumber(row, "checking", fallback.checking),
                pending: getProgressNumber(row, "pending", fallback.pending),
                total: getProgressNumber(row, "total", fallback.total)
            }
        };
    }

    function getCheckInDateTime(record) {
        if (!record.checkInTime || record.checkInTime === "-") {
            return "尚未報到";
        }

        return [record.checkInDate, record.checkInTime].filter(Boolean).join(" ");
    }

    function setPlainText(target, value) {
        if (target) {
            target.textContent = value;
        }
    }

    function setDetailText(name, value) {
        setPlainText(detailTargets[name], value);
    }

    function setStatus(status) {
        const badge = detailTargets.status;

        if (!badge) {
            return;
        }

        badge.textContent = status.text;
        badge.classList.remove.apply(badge.classList, statusClasses);
        badge.classList.add(status.className);
    }

    function updateProfile(record) {
        setPlainText(profileTargets.name, record.name);
        setPlainText(profileTargets.genderAge, record.gender + " / " + record.age + " 歲");
        setPlainText(profileTargets.examNumber, record.examNumber);
        setPlainText(profileTargets.idNumber, record.idNumber);
        setPlainText(profileTargets.station, record.station);
        setPlainText(profileTargets.checkInTime, getCheckInDateTime(record));
    }

    function updateFlow(record) {
        if (!flowPanel) return;
        const stages = ["已預約", "已掛號", "已報到", "檢查中", "完成報告"];
        const keys = ["booking", "registration", "checkin", "inspection", "finish"];
        const current = stages.indexOf(record.status.text);
        const hasCheckIn = Boolean(record.checkInDate || (record.checkInTime && record.checkInTime !== "-"));
        // Only display timestamps supplied by this record; never infer report completion.
        const times = [record.bookingTime || "—", "—", hasCheckIn ? getCheckInDateTime(record) : "—", "—", "—"];
        setPlainText(flowTargets.name, record.name);
        keys.forEach(function (key, index) {
            const state = index < current || (index === current && key === "finish")
                ? "complete" : index === current ? "active" : "pending";
            setFlowStep(key, state, current >= index ? times[index] : "—");
        });
    }

    function updateMyHealthAction(record) {
        if (!openMyHealthButton) {
            return;
        }

        selectedPatientNumber = (record.patientNumber || "").trim();
        openMyHealthButton.disabled = !selectedPatientNumber;
        openMyHealthButton.setAttribute("aria-label", "在內嵌視窗顯示 " + record.name + " My Health+ WEB LINK");
    }

    function setFlowStep(name, state, time) {
        const step = flowTargets.steps ? flowTargets.steps[name] : null;

        if (step) {
            step.classList.remove("is-complete", "is-active", "is-pending");
            step.classList.add("is-" + state);

            if (state === "active") {
                step.setAttribute("aria-current", "step");
            } else {
                step.removeAttribute("aria-current");
            }
        }

        setPlainText(flowTargets.stepTimes ? flowTargets.stepTimes[name] : null, time);
        const caption = step ? step.querySelector(".flow-step-caption") : null;
        if (caption) {
            const label = caption.querySelector("strong");
            caption.title = (label ? label.textContent : "") + "（" + time + "）";
        }
    }

    function renderAlerts() {
        if (alertTargets.list) alertTargets.list.textContent = "尚無即時訊息";
        setPlainText(alertTargets.total, "查看全部 (0)");
    }

    function clearSelection() {
        selectedPatientNumber = "";
        document.querySelectorAll("[data-detail], [data-profile], [data-flow-step-time]").forEach(function (target) {
            target.textContent = "—";
        });
        setPlainText(flowTargets.name, records.length ? "請選取資料列" : "尚無資料");
        Object.keys(flowTargets.steps || {}).forEach(function (name) { setFlowStep(name, "pending", "—"); });
        if (flowTargets.stationCard) flowTargets.stationCard.hidden = true;
        if (detailTargets.status) detailTargets.status.classList.remove.apply(detailTargets.status.classList, statusClasses);
        if (openMyHealthButton) openMyHealthButton.disabled = true;
        renderAlerts();
    }

    function pulsePanel(panel) {
        if (!panel) {
            return;
        }

        panel.classList.remove("is-updating");
        window.requestAnimationFrame(function () {
            panel.classList.add("is-updating");
        });
    }

    function openMyHealthFrame() {
        const baseUrl = openMyHealthButton ? openMyHealthButton.getAttribute("data-my-health-url") : "";

        if (!baseUrl || !selectedPatientNumber) {
            return;
        }

        const targetUrl = new URL(baseUrl, window.location.href);
        targetUrl.searchParams.set("patientId", selectedPatientNumber);

        if (!myHealthModal || !myHealthFrame) {
            return;
        }

        previousMyHealthFocus = document.activeElement;
        setMyHealthSize(myHealthModal.getAttribute("data-my-health-size") || "normal");
        myHealthFrame.src = targetUrl.toString();
        myHealthModal.hidden = false;
        document.body.classList.add("my-health-open");
    }

    function setMyHealthSize(size) {
        if (!myHealthModal) {
            return;
        }

        const nextSize = size === "large" ? "large" : "normal";

        myHealthModal.setAttribute("data-my-health-size", nextSize);

        if (myHealthToggleSizeButton) {
            const isLarge = nextSize === "large";

            myHealthToggleSizeButton.textContent = isLarge ? "▣" : "□";
            myHealthToggleSizeButton.title = isLarge ? "還原" : "放大";
            myHealthToggleSizeButton.setAttribute("aria-label", (isLarge ? "還原" : "放大") + " My Health+ 視窗");
            myHealthToggleSizeButton.setAttribute("aria-pressed", String(isLarge));
        }
    }

    function closeMyHealthFrame() {
        if (!myHealthModal) {
            return;
        }

        myHealthModal.hidden = true;
        document.body.classList.remove("my-health-open");

        if (myHealthFrame) {
            myHealthFrame.removeAttribute("src");
        }

        if (previousMyHealthFocus && typeof previousMyHealthFocus.focus === "function") {
            previousMyHealthFocus.focus();
        }
    }

    function refreshDashboard(record) {
        setDetailText("serial", record.displaySerial || record.serial);
        setDetailText("examNumber", record.examNumber);
        setDetailText("patientNumber", record.patientNumber);
        setDetailText("name", record.name);
        setDetailText("idNumber", record.idNumber);
        setDetailText("genderAge", record.gender + " / " + record.age + " 歲");
        setDetailText("station", record.station);
        setDetailText("checkInTime", getCheckInDateTime(record));
        setStatus(record.status);
        updateProfile(record);
        updateFlow(record);
        updateMyHealthAction(record);
        renderAlerts(record);

        pulsePanel(detailsPanel);
        pulsePanel(flowPanel);
        pulsePanel(alertPanel);
    }

    function selectRow(row) {
        if (!row) {
            return;
        }

        visibleRows.forEach(function (item) {
            const selected = item === row;

            item.classList.toggle("is-selected", selected);
            item.setAttribute("aria-selected", String(selected));
        });

        refreshDashboard(rowRecords.get(row) || readRecord(row));
    }

    function createTextCell(value) {
        const cell = document.createElement("td");

        cell.textContent = value;

        return cell;
    }

    function createCheckboxCell() {
        const cell = document.createElement("td");
        const checkbox = document.createElement("span");

        checkbox.className = "checkbox";
        checkbox.setAttribute("aria-hidden", "true");
        cell.appendChild(checkbox);

        return cell;
    }

    function createStatusCell(status) {
        const cell = document.createElement("td");
        const badge = document.createElement("span");

        badge.className = "status " + status.className;
        badge.textContent = status.text;
        cell.appendChild(badge);

        return cell;
    }

    function renderRow(record, displaySerial) {
        const row = document.createElement("tr");

        row.setAttribute("tabindex", "0");
        row.setAttribute("aria-selected", "false");
        row.setAttribute("data-booking-time", record.bookingTime);
        row.setAttribute("data-progress-completed", String(record.progress.completed));
        row.setAttribute("data-progress-checking", String(record.progress.checking));
        row.setAttribute("data-progress-pending", String(record.progress.pending));
        row.setAttribute("data-progress-total", String(record.progress.total));

        row.appendChild(createCheckboxCell());
        row.appendChild(createTextCell(String(displaySerial)));
        row.appendChild(createTextCell(record.examNumber));
        row.appendChild(createTextCell(record.patientNumber));
        row.appendChild(createTextCell(record.name));
        row.appendChild(createTextCell(record.idNumber));
        row.appendChild(createTextCell(record.gender));
        row.appendChild(createTextCell(record.age));
        row.appendChild(createStatusCell(record.status));
        row.appendChild(createTextCell(record.station));
        row.appendChild(createTextCell(record.reservationDate));
        row.appendChild(createTextCell(record.reservationTime));
        row.appendChild(createTextCell(record.checkInDate));
        row.appendChild(createTextCell(record.checkInTime));

        rowRecords.set(row, { ...record, displaySerial: String(displaySerial) });
        row.addEventListener("click", function () {
            selectRow(row);
        });

        row.addEventListener("keydown", function (event) {
            if (event.key !== "Enter" && event.key !== " ") {
                return;
            }

            event.preventDefault();
            selectRow(row);
        });

        return row;
    }

    // Parse components explicitly, avoiding locale-dependent Date.parse behavior.
    function getSortValue(value, key) {
        const text = String(key === "status" ? (value ? value.text : "") : (value ?? "")).trim();
        if (key === "age") return /^\d+$/.test(text) ? Number(text) : null;
        if (key === "station") return text && text !== "-" && text !== "—" ? text : null;
        if (key === "status") {
            const order = ["已預約", "已掛號", "已報到", "檢查中", "佔名額", "已取消", "完成報告"];
            const index = order.indexOf(text);
            return index >= 0 ? index : null;
        }
        if (key.endsWith("Date")) {
            const parts = /^(\d{4})[-/](\d{1,2})[-/](\d{1,2})$/.exec(text);
            if (!parts) return null;
            const year = Number(parts[1]), month = Number(parts[2]), day = Number(parts[3]);
            const date = new Date(Date.UTC(year, month - 1, day));
            return date.getUTCFullYear() === year && date.getUTCMonth() === month - 1 && date.getUTCDate() === day
                ? date.getTime() : null;
        }
        const parts = /^(\d{1,2}):(\d{2})(?::(\d{2}))?$/.exec(text)
            || /^(\d{2})(\d{2})(\d{2})?$/.exec(text);
        if (!parts) return null;
        const hour = Number(parts[1]), minute = Number(parts[2]), second = Number(parts[3] || 0);
        return hour < 24 && minute < 60 && second < 60 ? hour * 3600 + minute * 60 + second : null;
    }

    function compareSortRecords(left, right, key, direction) {
        const a = getSortValue(left[key], key), b = getSortValue(right[key], key);
        // Missing timestamps stay at the bottom in both directions.
        if (a === null && b !== null) return 1;
        if (b === null && a !== null) return -1;
        if (a !== b) {
            const compared = typeof a === "string" ? a.localeCompare(b, "zh-Hant-TW", { numeric: true }) : a - b;
            if (compared) return compared * (direction === "asc" ? 1 : -1);
        }
        return Number(left.serial) - Number(right.serial);
    }

    let sortKey = "";
    let sortDirection = "asc";
    const sortButtons = Array.from(table.querySelectorAll("[data-sort-key]"));
    sortButtons.forEach(function (button) {
        button.addEventListener("click", function () {
            const key = button.getAttribute("data-sort-key");
            sortDirection = sortKey === key && sortDirection === "asc" ? "desc" : "asc";
            sortKey = key;
            records.sort(function (a, b) { return compareSortRecords(a, b, sortKey, sortDirection); });
            sortButtons.forEach(function (item) {
                const active = item === button;
                item.closest("th").setAttribute("aria-sort", active ? (sortDirection === "asc" ? "ascending" : "descending") : "none");
                item.querySelectorAll("[data-sort-direction]").forEach(function (arrow) {
                    arrow.classList.toggle("is-active", active && arrow.getAttribute("data-sort-direction") === sortDirection);
                });
                const label = item.firstChild.textContent.trim();
                item.setAttribute("aria-label", label + (active ? (sortDirection === "asc" ? "：目前遞增，點擊遞減排序" : "：目前遞減，點擊遞增排序") : "：點擊遞增排序"));
            });
            if (records.length) renderPage(1);
        });
    });

    function getPageCount() {
        return Math.max(1, Math.ceil(records.length / pageSize));
    }

    function clampPage(page) {
        return Math.min(Math.max(page, 1), getPageCount());
    }

    function updatePageSummary(startIndex, itemCount) {
        if (!pageSummary) {
            return;
        }

        const firstItem = records.length ? startIndex + 1 : 0;
        const lastItem = startIndex + itemCount;

        pageSummary.textContent = "顯示 " + firstItem + "-" + lastItem + " 筆，共 " + records.length + " 筆";
    }

    function getVisiblePages() {
        const pageCount = getPageCount();
        const pages = [];

        function addPage(page) {
            if (!pages.includes(page)) {
                pages.push(page);
            }
        }

        if (pageCount <= 7) {
            for (let page = 1; page <= pageCount; page += 1) {
                addPage(page);
            }

            return pages;
        }

        addPage(1);

        const start = Math.max(2, currentPage - 2);
        const end = Math.min(pageCount - 1, currentPage + 2);

        if (start > 2) {
            pages.push("ellipsis-left");
        }

        for (let page = start; page <= end; page += 1) {
            addPage(page);
        }

        if (end < pageCount - 1) {
            pages.push("ellipsis-right");
        }

        addPage(pageCount);

        return pages;
    }

    function createPageButton(label, page, options) {
        const button = document.createElement("button");
        const settings = options || {};

        button.type = "button";
        button.textContent = label;

        if (settings.ariaLabel) {
            button.setAttribute("aria-label", settings.ariaLabel);
        }

        if (settings.active) {
            button.classList.add("active");
            button.setAttribute("aria-current", "page");
        }

        if (settings.disabled) {
            button.disabled = true;
        } else {
            button.addEventListener("click", function () {
                renderPage(page);
            });
        }

        return button;
    }

    function renderPagination() {
        if (!pagination) {
            return;
        }

        const pageCount = getPageCount();
        const items = [
            createPageButton("<", currentPage - 1, {
                ariaLabel: "上一頁",
                disabled: currentPage === 1
            })
        ];

        getVisiblePages().forEach(function (page) {
            if (typeof page !== "number") {
                items.push(createPageButton("...", currentPage, {
                    ariaLabel: "省略頁碼",
                    disabled: true
                }));
                return;
            }

            items.push(createPageButton(String(page), page, {
                active: page === currentPage,
                ariaLabel: "第 " + page + " 頁"
            }));
        });

        items.push(createPageButton(">", currentPage + 1, {
            ariaLabel: "下一頁",
            disabled: currentPage === pageCount
        }));

        pagination.replaceChildren.apply(pagination, items);
    }

    function renderPage(page) {
        const nextPage = clampPage(page);
        const startIndex = (nextPage - 1) * pageSize;
        const pageRecords = records.slice(startIndex, startIndex + pageSize);
        const rows = pageRecords.map(function (record, index) {
            return renderRow(record, startIndex + index + 1);
        });

        currentPage = nextPage;
        tableBody.replaceChildren.apply(tableBody, rows);
        visibleRows = Array.from(tableBody.querySelectorAll("tr"));

        renderPagination();
        updatePageSummary(startIndex, pageRecords.length);
        clearSelection();
    }

    if (pageSelect) {
        pageSelect.addEventListener("change", function () {
            pageSize = readPositiveNumber(pageSelect.value, 20);
            if (records.length) renderPage(1);
            else {
                renderPagination();
                updatePageSummary(0, 0);
            }
        });
    }

    if (openMyHealthButton) {
        openMyHealthButton.addEventListener("click", openMyHealthFrame);
    }

    closeMyHealthButtons.forEach(function (button) {
        button.addEventListener("click", closeMyHealthFrame);
    });

    if (myHealthToggleSizeButton) {
        myHealthToggleSizeButton.addEventListener("click", function () {
            setMyHealthSize(myHealthModal && myHealthModal.getAttribute("data-my-health-size") === "large" ? "normal" : "large");
        });
    }

    window.addEventListener("keydown", function (event) {
        if (event.key === "Escape" && myHealthModal && !myHealthModal.hidden) {
            closeMyHealthFrame();
        }
    });

    setMyHealthSize(myHealthModal ? myHealthModal.getAttribute("data-my-health-size") : "normal");
    clearSelection();
    if (records.length) renderPage(1);
})();
