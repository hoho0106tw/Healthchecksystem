(() => {
        const page = document.querySelector(".today-appointment-page");
        if (!page) {
            return;
        }

        const selectedFields = {
            name: page.querySelector("[data-selected-field='name']"),
            status: page.querySelector("[data-selected-field='status']"),
            projectNumber: page.querySelector("[data-selected-field='projectNumber']"),
            dateTime: page.querySelector("[data-selected-field='dateTime']"),
            plan: page.querySelector("[data-selected-field='plan']"),
            phone: page.querySelector("[data-selected-field='phone']"),
            note: page.querySelector("[data-selected-field='note']")
        };

        let allRows = Array.from(page.querySelectorAll("[data-appointment-row]"));
        let rows = allRows.slice();
        let dateFilterTouched = false;
        const startDateInput = page.querySelector("[data-appointment-start-date]");
        const endDateInput = page.querySelector("[data-appointment-end-date]");
        const reservationAreaSelect = page.querySelector("[data-reservation-area-filter]");
        const appointmentStatusSelect = page.querySelector("[data-appointment-status-filter]");
        const keywordInput = page.querySelector("[data-appointment-keyword-filter]");
        const searchButton = page.querySelector("[data-appointment-search-button]");
        const searchButtonLabel = page.querySelector("[data-appointment-search-label]");
        const queryStatus = page.querySelector("[data-appointment-query-status]");
        const dashboardCountTargets = {
            total: Array.from(page.querySelectorAll("[data-dashboard-count='total']")),
            group: Array.from(page.querySelectorAll("[data-dashboard-count='group']")),
            individual: Array.from(page.querySelectorAll("[data-dashboard-count='individual']"))
        };
        const tableWrap = page.querySelector("[data-appointment-table-wrap]");
        const horizontalScrollbar = page.querySelector("[data-appointment-table-scrollbar]");
        const horizontalScrollbarContent = page.querySelector("[data-appointment-table-scrollbar-content]");
        const confirmModal = page.querySelector("[data-appointment-confirm-modal]");
        const confirmTitle = page.querySelector("[data-appointment-confirm-title]");
        const confirmMessage = page.querySelector("[data-appointment-confirm-message]");
        const confirmWarning = page.querySelector("[data-appointment-confirm-warning]");
        const confirmSubmit = page.querySelector("[data-appointment-confirm-submit]");
        const confirmCloseButtons = Array.from(page.querySelectorAll("[data-appointment-confirm-close]"));
        const batchRegisterButton = page.querySelector("[data-batch-register]");
        const batchClearButton = page.querySelector("[data-batch-clear]");
        const batchSelectedCount = page.querySelector("[data-batch-selected-count]");
        const batchSelectionHint = page.querySelector("[data-batch-selection-hint]");
        const batchResultModal = page.querySelector("[data-appointment-batch-result-modal]");
        const batchResultCloseButtons = Array.from(page.querySelectorAll("[data-batch-result-close]"));
        const batchResultDownloadButton = page.querySelector("[data-batch-result-download]");
        const batchResultList = page.querySelector("[data-batch-result-list]");
        const batchResultTotal = page.querySelector("[data-batch-result-total]");
        const batchResultSuccess = page.querySelector("[data-batch-result-success]");
        const batchResultFailed = page.querySelector("[data-batch-result-failed]");
        const tablePanel = page.querySelector(".appointment-table-panel");
        const selectedAppointmentButton = page.querySelector("[data-open-selected-appointment]");
        const createAppointmentUrl = page.dataset.createAppointmentUrl || "";
        const appointmentDeleteUrl = page.dataset.appointmentDeleteUrl || "";
        const appointmentCancelUrl = page.dataset.appointmentCancelUrl || "";
        const antiForgeryToken = page.querySelector("input[name='__RequestVerificationToken']")?.value || "";
        const appointmentStatusByReserveStatus = {
            "1": { status: "已預約", tone: "confirmed" },
            "0": { status: "預約取消", tone: "cancelled" },
            "2": { status: "佔名額", tone: "reserved-slot" }
        };
        const reserveStatusByAppointmentStatus = {
            "已預約": "1",
            "預約取消": "0",
            "佔名額": "2"
        };
        const statusSummaryKeyByReserveStatus = {
            "1": "confirmed",
            "2": "reserved",
            "0": "cancelled"
        };
        let isSyncingHorizontalScroll = false;
        let pendingConfirmAction = null;
        let batchSelectionAnchor = null;
        let lastBatchResults = [];
        let queryStateTimer = 0;

        const setDashboardCount = (key, value) => {
            dashboardCountTargets[key]?.forEach((target) => {
                target.textContent = String(value);
            });
        };

        const getAppointmentType = (row) => (row.dataset.appointmentType || "").trim();
        const getAppointmentWorkArea = (row) => (row.dataset.workArea || "").trim();

        const getAppointmentReserveStatus = (row) => {
            const reserveStatus = String(row.dataset.reserveStatus || "").trim();

            if (reserveStatus === "0" || reserveStatus === "1" || reserveStatus === "2") {
                return reserveStatus;
            }

            return reserveStatusByAppointmentStatus[String(row.dataset.status || "").trim()] || "";
        };

        const normalizeFilterValue = (value) => String(value || "").trim().toUpperCase();

        const normalizeDateValue = (value) => {
            const text = String(value || "").trim();

            if (!text) {
                return "";
            }

            const digitsOnly = text.replace(/\D/g, "");
            let year = "";
            let month = "";
            let day = "";

            if (/^\d{8}$/.test(digitsOnly)) {
                year = digitsOnly.slice(0, 4);
                month = digitsOnly.slice(4, 6);
                day = digitsOnly.slice(6, 8);
            } else if (/^\d{7}$/.test(digitsOnly)) {
                year = String(Number.parseInt(digitsOnly.slice(0, 3), 10) + 1911);
                month = digitsOnly.slice(3, 5);
                day = digitsOnly.slice(5, 7);
            } else {
                const match = text.match(/^(\d{2,4})\D+(\d{1,2})\D+(\d{1,2})$/);

                if (!match) {
                    return "";
                }

                year = match[1];
                month = match[2];
                day = match[3];
            }

            const parsedYear = Number.parseInt(year, 10);
            const parsedMonth = Number.parseInt(month, 10);
            const parsedDay = Number.parseInt(day, 10);

            if (!Number.isFinite(parsedYear)
                || !Number.isFinite(parsedMonth)
                || !Number.isFinite(parsedDay)
                || parsedMonth < 1
                || parsedMonth > 12
                || parsedDay < 1
                || parsedDay > 31) {
                return "";
            }

            const normalizedYear = year.length <= 3 && parsedYear > 0 && parsedYear < 1911
                ? String(parsedYear + 1911)
                : String(parsedYear).padStart(4, "0");

            return `${normalizedYear}${String(parsedMonth).padStart(2, "0")}${String(parsedDay).padStart(2, "0")}`;
        };

        const isAppointmentDateInRange = (row, startDate, endDate) => {
            if (!startDate && !endDate) {
                return true;
            }

            const appointmentDate = normalizeDateValue(row.dataset.appointmentDate);

            return Boolean(appointmentDate)
                && (!startDate || appointmentDate >= startDate)
                && (!endDate || appointmentDate <= endDate);
        };

        const getAppointmentKeywordText = (row) => normalizeFilterValue([
            row.dataset.name,
            row.dataset.idNumber,
            row.dataset.projectNumber,
            row.dataset.barcode,
            row.dataset.company,
            row.dataset.patNo
        ].join(" "));

        const matchesAppointmentKeyword = (row, keyword) =>
            !keyword || getAppointmentKeywordText(row).includes(keyword);

        const isDefaultTodayDateRange = () => {
            const today = getTodayInputDate();

            return Boolean(today)
                && String(startDateInput?.value || "").trim() === today
                && String(endDateInput?.value || "").trim() === today;
        };

        const createEmptyStatusSummaryCounts = () => ({
            total: { total: 0, group: 0, individual: 0 },
            confirmed: { total: 0, group: 0, individual: 0 },
            reserved: { total: 0, group: 0, individual: 0 },
            cancelled: { total: 0, group: 0, individual: 0 }
        });

        const incrementStatusSummaryCount = (summary, key, appointmentType) => {
            if (!summary[key]) {
                return;
            }

            summary[key].total += 1;

            if (appointmentType === "團檢") {
                summary[key].group += 1;
            } else if (appointmentType === "個人") {
                summary[key].individual += 1;
            }
        };

        const setStatusSummaryCount = (key, bucket, value) => {
            page.querySelectorAll(`[data-status-summary-count='${key}-${bucket}']`).forEach((target) => {
                target.textContent = String(value);
            });
        };

        const syncStatusSummaryCounts = () => {
            const summaryCounts = createEmptyStatusSummaryCounts();

            rows.forEach((row) => {
                const appointmentType = getAppointmentType(row);
                const reserveStatus = getAppointmentReserveStatus(row);
                const statusKey = statusSummaryKeyByReserveStatus[reserveStatus];

                incrementStatusSummaryCount(summaryCounts, "total", appointmentType);

                if (statusKey) {
                    incrementStatusSummaryCount(summaryCounts, statusKey, appointmentType);
                }
            });

            Object.entries(summaryCounts).forEach(([key, counts]) => {
                setStatusSummaryCount(key, "total", counts.total);
                setStatusSummaryCount(key, "group", counts.group);
                setStatusSummaryCount(key, "individual", counts.individual);
            });
        };

        const syncDashboardCounts = () => {
            setDashboardCount("total", rows.length);
            setDashboardCount("group", rows.filter((row) => getAppointmentType(row) === "團檢").length);
            setDashboardCount("individual", rows.filter((row) => getAppointmentType(row) === "個人").length);
            syncStatusSummaryCounts();
        };

        const closeActionMenus = (exceptActions = null) => {
            page.querySelectorAll(".appointment-row-actions").forEach((actions) => {
                if (actions === exceptActions) {
                    return;
                }

                actions.classList.remove("is-open");
                actions.closest("[data-appointment-row]")?.classList.remove("has-open-actions");
                const menu = actions.querySelector(".row-action-menu");

                if (menu) {
                    menu.style.left = "";
                    menu.style.top = "";
                }

                actions.querySelector(".appointment-action-toggle")?.setAttribute("aria-expanded", "false");
            });
        };

        const positionActionMenu = (actions) => {
            const toggle = actions?.querySelector(".appointment-action-toggle");
            const menu = actions?.querySelector(".row-action-menu");

            if (!toggle || !menu) {
                return;
            }

            const toggleRect = toggle.getBoundingClientRect();
            const spacing = 6;
            const menuWidth = menu.offsetWidth || 128;
            const menuHeight = menu.offsetHeight || 78;
            const viewportPadding = 8;
            const left = Math.min(
                Math.max(toggleRect.left - menuWidth - spacing, viewportPadding),
                window.innerWidth - menuWidth - viewportPadding
            );
            const top = Math.min(
                Math.max(toggleRect.top, viewportPadding),
                window.innerHeight - menuHeight - viewportPadding
            );

            menu.style.left = `${left}px`;
            menu.style.top = `${top}px`;
        };

        const openActionMenu = (actions) => {
            if (!actions) {
                return;
            }

            closeActionMenus(actions);
            actions.classList.add("is-open");
            const row = actions.closest("[data-appointment-row]");

            row?.classList.add("has-open-actions");
            syncAppointmentStatusActions(row);
            actions.querySelector(".appointment-action-toggle")?.setAttribute("aria-expanded", "true");
            positionActionMenu(actions);
        };

        const closeConfirmDialog = () => {
            if (!confirmModal) {
                return;
            }

            confirmModal.hidden = true;
            confirmModal.classList.remove("danger");
            pendingConfirmAction = null;
        };

        const openConfirmDialog = ({ title, message, warning = "", confirmText = "確認", danger = false, onConfirm }) => {
            if (!confirmModal || !confirmTitle || !confirmMessage || !confirmWarning || !confirmSubmit) {
                onConfirm?.();
                return;
            }

            confirmTitle.textContent = title;
            confirmMessage.textContent = message;
            confirmSubmit.textContent = confirmText;
            confirmModal.classList.toggle("danger", danger);

            if (warning) {
                confirmWarning.textContent = warning;
                confirmWarning.hidden = false;
            } else {
                confirmWarning.textContent = "";
                confirmWarning.hidden = true;
            }

            pendingConfirmAction = onConfirm;
            confirmModal.hidden = false;
            confirmSubmit.focus();
        };

        const updateAppointmentStatus = (row, status, statusTone, reserveStatus) => {
            const nextReserveStatus = String(reserveStatus || "").trim();
            const fallbackStatus = appointmentStatusByReserveStatus[nextReserveStatus] || appointmentStatusByReserveStatus["0"];
            const nextStatus = String(status || fallbackStatus.status).trim() || fallbackStatus.status;
            const nextStatusTone = String(statusTone || fallbackStatus.tone).trim() || fallbackStatus.tone;

            row.dataset.status = nextStatus;
            row.dataset.statusTone = nextStatusTone;

            if (nextReserveStatus) {
                row.dataset.reserveStatus = nextReserveStatus;
            }

            const statusCell = row.querySelector("[data-appointment-status-cell]");

            if (statusCell) {
                statusCell.textContent = nextStatus;
                statusCell.className = `appointment-status ${nextStatusTone}`.trim();
            }

            if (row.classList.contains("is-selected")) {
                setSelectedRow(row);
            }

            syncAppointmentStatusActions(row);
            applyAppointmentFilters(false);
        };

        const syncAppointmentStatusActions = (row) => {
            const currentStatus = String(row?.dataset.status || "").trim();
            const reserveStatusValue = String(row?.dataset.reserveStatus || "").trim();
            const currentReserveStatus = ["0", "1", "2"].includes(reserveStatusValue)
                ? reserveStatusValue
                : String(reserveStatusByAppointmentStatus[currentStatus] || "").trim();

            row?.querySelectorAll("[data-target-reserve-status]").forEach((button) => {
                const targetReserveStatus = String(button.dataset.targetReserveStatus || "").trim();
                const isCurrentStatus = Boolean(currentReserveStatus && targetReserveStatus === currentReserveStatus);
                const isBlockedReservedSlotAction = currentReserveStatus === "1" && targetReserveStatus === "2";

                button.hidden = isCurrentStatus || isBlockedReservedSlotAction;

                if (isCurrentStatus) {
                    button.disabled = false;
                }
            });
        };

        const createCancelFormData = (row, reserveStatus) => {
            const formData = new FormData();
            const currentReserveStatus = String(row.dataset.reserveStatus || "").trim();
            const nextReserveStatus = String(reserveStatus || currentReserveStatus).trim();

            if (antiForgeryToken) {
                formData.append("__RequestVerificationToken", antiForgeryToken);
            }

            formData.append("ProjectNumber", row.dataset.projectNumber || "");
            formData.append("Barcode", row.dataset.barcode || "");
            formData.append("PatNo", row.dataset.patNo || "");
            formData.append("Name", row.dataset.name || "");
            formData.append("CurrentReserveStatus", currentReserveStatus);
            formData.append("ReserveStatus", nextReserveStatus);

            return formData;
        };

        const showCancelErrorMessage = (message) => {
            window.alert(String(message || "").trim() || "更新預約狀態失敗，請確認預約資料後再試一次。");
        };

        const cancelAppointmentRow = async (row, reserveStatus = "0", triggerButton = null) => {
            if (!row || !appointmentCancelUrl) {
                return;
            }

            if (!String(row.dataset.barcode || "").trim()) {
                showCancelErrorMessage("缺少 Barcode 編號，無法更新預約狀態。");
                return;
            }

            if (!["0", "1", "2"].includes(String(reserveStatus || "").trim())) {
                showCancelErrorMessage("預約狀態只能是預約取消、已預約、佔名額。");
                return;
            }

            const cancelButton = triggerButton || row.querySelector(`[data-target-reserve-status='${reserveStatus}']`);

            if (cancelButton) {
                cancelButton.disabled = true;
            }

            try {
                const response = await fetch(appointmentCancelUrl, {
                    method: "POST",
                    body: createCancelFormData(row, reserveStatus),
                    headers: {
                        "X-Requested-With": "XMLHttpRequest"
                    }
                });
                let result = null;

                try {
                    result = await response.json();
                } catch {
                    result = null;
                }

                if (!response.ok) {
                    showCancelErrorMessage(result?.message);
                    return;
                }

                const updatedCount = Number.parseInt(result?.updatedCount || "0", 10);

                if (result?.updated === true || result?.canceled === true || updatedCount >= 1) {
                    updateAppointmentStatus(row, result?.status, result?.statusTone, result?.reserveStatus || reserveStatus);
                } else {
                    showCancelErrorMessage(result?.message || "更新預約狀態失敗，未更新任何預約資料。");
                }
            } catch {
                showCancelErrorMessage("更新預約狀態時發生錯誤，請稍後再試。");
            } finally {
                if (cancelButton && row.isConnected) {
                    cancelButton.disabled = false;
                }
            }
        };

        const removeAppointmentRow = (row) => {
            row.remove();
            allRows = Array.from(page.querySelectorAll("[data-appointment-row]"));
            applyAppointmentFilters(false);
        };

        const createDeleteFormData = (row) => {
            const formData = new FormData();

            if (antiForgeryToken) {
                formData.append("__RequestVerificationToken", antiForgeryToken);
            }

            formData.append("ProjectNumber", row.dataset.projectNumber || "");
            formData.append("Barcode", row.dataset.barcode || "");
            formData.append("PatNo", row.dataset.patNo || "");
            formData.append("Name", row.dataset.name || "");

            return formData;
        };

        const showDeleteErrorMessage = (message) => {
            window.alert(String(message || "").trim() || "刪除失敗，請確認預約資料後再試一次。");
        };

        const deleteAppointmentRow = async (row) => {
            if (!row || !appointmentDeleteUrl) {
                return;
            }

            if (!String(row.dataset.barcode || "").trim()) {
                showDeleteErrorMessage("缺少 Barcode 編號，無法刪除預約。");
                return;
            }

            const deleteButton = row.querySelector("[data-delete-appointment]");

            if (deleteButton) {
                deleteButton.disabled = true;
            }

            try {
                const response = await fetch(appointmentDeleteUrl, {
                    method: "POST",
                    body: createDeleteFormData(row),
                    headers: {
                        "X-Requested-With": "XMLHttpRequest"
                    }
                });
                let result = null;

                try {
                    result = await response.json();
                } catch {
                    result = null;
                }

                if (!response.ok) {
                    showDeleteErrorMessage(result?.message);
                    return;
                }

                const deletedCount = Number.parseInt(result?.deletedCount || "0", 10);

                if (result?.deleted === true || deletedCount >= 1) {
                    removeAppointmentRow(row);
                } else {
                    showDeleteErrorMessage(result?.message || "刪除失敗，未刪除任何預約資料。");
                }
            } catch {
                showDeleteErrorMessage("刪除預約時發生錯誤，請稍後再試。");
            } finally {
                if (deleteButton && row.isConnected) {
                    deleteButton.disabled = false;
                }
            }
        };

        const getFixedColumnsWidth = () => {
            const fixedHeaderCells = tableWrap?.querySelectorAll(".today-appointment-table thead th:nth-child(-n + 6)");

            if (fixedHeaderCells?.length) {
                return Array.from(fixedHeaderCells).reduce((width, cell) => width + cell.getBoundingClientRect().width, 0);
            }

            const styles = getComputedStyle(page);
            const readPixelValue = (name) => Number.parseFloat(styles.getPropertyValue(name)) || 0;

            return readPixelValue("--today-fixed-select-width")
                + readPixelValue("--today-fixed-action-width")
                + readPixelValue("--today-fixed-name-width")
                + readPixelValue("--today-fixed-date-width")
                + readPixelValue("--today-fixed-time-width")
                + readPixelValue("--today-fixed-workarea-width");
        };

        const syncHorizontalScrollbarSize = () => {
            if (!tableWrap || !horizontalScrollbar || !horizontalScrollbarContent) {
                return;
            }

            const fixedColumnsWidth = getFixedColumnsWidth();
            const visibleScrollWidth = tableWrap.clientWidth - fixedColumnsWidth;
            const scrollableContentWidth = tableWrap.scrollWidth - fixedColumnsWidth;
            const hasHorizontalOverflow = tableWrap.scrollWidth > tableWrap.clientWidth + 1 && visibleScrollWidth > 0;
            const scrollbarLeft = Math.max(0, Math.min(fixedColumnsWidth, tableWrap.clientWidth));

            horizontalScrollbar.hidden = !hasHorizontalOverflow;
            page.style.setProperty("--today-fixed-columns-width", `${scrollbarLeft}px`);
            horizontalScrollbar.style.left = `${scrollbarLeft}px`;
            horizontalScrollbarContent.style.width = `${Math.max(scrollableContentWidth, visibleScrollWidth)}px`;

            if (hasHorizontalOverflow) {
                horizontalScrollbar.scrollLeft = tableWrap.scrollLeft;
            }
        };

        const syncFromTableScroll = () => {
            if (!horizontalScrollbar || isSyncingHorizontalScroll) {
                return;
            }

            closeActionMenus();
            isSyncingHorizontalScroll = true;
            horizontalScrollbar.scrollLeft = tableWrap?.scrollLeft || 0;
            isSyncingHorizontalScroll = false;
        };

        const syncFromHorizontalScrollbar = () => {
            if (!tableWrap || isSyncingHorizontalScroll) {
                return;
            }

            isSyncingHorizontalScroll = true;
            tableWrap.scrollLeft = horizontalScrollbar?.scrollLeft || 0;
            isSyncingHorizontalScroll = false;
        };

        if (tableWrap && horizontalScrollbar && horizontalScrollbarContent) {
            tableWrap.addEventListener("scroll", syncFromTableScroll);
            horizontalScrollbar.addEventListener("scroll", syncFromHorizontalScrollbar);
            tableWrap.addEventListener("wheel", (event) => {
                const horizontalDelta = event.shiftKey ? event.deltaY : event.deltaX;

                if (!horizontalDelta || horizontalScrollbar.hidden) {
                    return;
                }

                const maxScrollLeft = tableWrap.scrollWidth - tableWrap.clientWidth;
                const nextScrollLeft = Math.min(Math.max(tableWrap.scrollLeft + horizontalDelta, 0), maxScrollLeft);

                if (nextScrollLeft === tableWrap.scrollLeft) {
                    return;
                }

                tableWrap.scrollLeft = nextScrollLeft;
                event.preventDefault();
            }, { passive: false });

            window.addEventListener("resize", syncHorizontalScrollbarSize);
            window.addEventListener("resize", closeActionMenus);

            if ("ResizeObserver" in window) {
                const resizeObserver = new ResizeObserver(syncHorizontalScrollbarSize);
                resizeObserver.observe(tableWrap);
            }
        }

        page.addEventListener("click", (event) => {
            const toggle = event.target.closest(".appointment-action-toggle");

            if (toggle && page.contains(toggle)) {
                const actions = toggle.closest(".appointment-row-actions");

                if (actions?.classList.contains("is-open")) {
                    closeActionMenus();
                } else {
                    openActionMenu(actions);
                }

                event.stopPropagation();
                return;
            }

            const editAppointmentLink = event.target.closest("[data-edit-appointment-link]");

            if (editAppointmentLink && page.contains(editAppointmentLink)) {
                const row = editAppointmentLink.closest("[data-appointment-row]");

                if (row) {
                    setSelectedRow(row);
                }

                editAppointmentLink.href = appendAppointmentReturnUrl(editAppointmentLink.href);
                closeActionMenus();
                return;
            }

            const statusButton = event.target.closest("[data-update-appointment-status], [data-cancel-appointment]");

            if (statusButton && page.contains(statusButton)) {
                const row = statusButton.closest("[data-appointment-row]");
                const name = statusButton.dataset.name || row?.dataset.name || "這筆預約";
                const reserveStatus = statusButton.dataset.targetReserveStatus || "0";
                const targetStatusName = statusButton.dataset.targetStatusName
                    || appointmentStatusByReserveStatus[reserveStatus]?.status
                    || "預約狀態";
                const isCancelStatus = reserveStatus === "0";

                closeActionMenus();
                event.stopPropagation();

                if (!row) {
                    return;
                }

                openConfirmDialog({
                    title: isCancelStatus ? "取消預約" : "更新預約狀態",
                    message: `是否要將 ${name} 的預約狀態改為「${targetStatusName}」？`,
                    confirmText: isCancelStatus ? "確認取消" : "確認更新",
                    onConfirm: () => cancelAppointmentRow(row, reserveStatus, statusButton)
                });

                return;
            }

            const deleteButton = event.target.closest("[data-delete-appointment]");

            if (deleteButton && page.contains(deleteButton)) {
                const row = deleteButton.closest("[data-appointment-row]");
                const name = deleteButton.dataset.name || row?.dataset.name || "這筆預約";

                closeActionMenus();
                event.stopPropagation();

                if (!row) {
                    return;
                }

                openConfirmDialog({
                    title: "刪除預約",
                    message: `是否真的要刪除 ${name} 嗎？`,
                    warning: "刪除後，此筆資料就會消失。",
                    confirmText: "確認刪除",
                    danger: true,
                    onConfirm: () => deleteAppointmentRow(row)
                });
                return;
            }

            if (!event.target.closest(".appointment-row-actions")) {
                closeActionMenus();
            }
        });

        page.addEventListener("keydown", (event) => {
            const key = String(event.key || "").toLowerCase();

            if ((event.ctrlKey || event.metaKey) && key === "a" && !event.target.closest("input, textarea, select, [contenteditable='true']")) {
                event.preventDefault();
                selectAllBatchRows();
                return;
            }

            if (event.key === "Escape") {
                closeActionMenus();
                closeConfirmDialog();
                closeBatchResultDialog();
            }
        });

        confirmCloseButtons.forEach((button) => {
            button.addEventListener("click", closeConfirmDialog);
        });

        confirmSubmit?.addEventListener("click", () => {
            const action = pendingConfirmAction;

            closeConfirmDialog();
            action?.();
        });

        const getSelectedAppointmentRow = () => rows.find((row) => row.classList.contains("is-selected"))
            || allRows.find((row) => row.classList.contains("is-selected"))
            || null;

        const getAppointmentReturnUrl = () => `${window.location.pathname}${window.location.search}${window.location.hash}`;

        const appendAppointmentReturnUrl = (target) => {
            const url = new URL(target, window.location.href);

            url.searchParams.set("returnUrl", getAppointmentReturnUrl());

            return url.toString();
        };

        const buildSelectedAppointmentUrl = (row) => {
            const projectNumber = String(row?.dataset.projectNumber || "").trim();
            const barcode = String(row?.dataset.barcode || "").trim();
            const reserveStatus = String(row?.dataset.reserveStatus || "").trim();

            if (!createAppointmentUrl || !projectNumber || !barcode) {
                return "";
            }

            const url = new URL(createAppointmentUrl, window.location.href);
            url.searchParams.set("projectNumber", projectNumber);
            url.searchParams.set("barcode", barcode);

            if (reserveStatus) {
                url.searchParams.set("reserveStatus", reserveStatus);
            }

            return appendAppointmentReturnUrl(url.toString());
        };

        const syncSelectedAppointmentButton = (row = getSelectedAppointmentRow()) => {
            if (!selectedAppointmentButton) {
                return;
            }

            const canOpen = Boolean(buildSelectedAppointmentUrl(row));
            selectedAppointmentButton.disabled = !canOpen;
            selectedAppointmentButton.setAttribute("aria-disabled", canOpen ? "false" : "true");
        };

        const openSelectedAppointment = () => {
            const targetUrl = buildSelectedAppointmentUrl(getSelectedAppointmentRow());

            if (!targetUrl) {
                window.alert("缺少專案編號或 Barcode，無法帶入個人預約。");
                syncSelectedAppointmentButton();
                return;
            }

            window.location.assign(targetUrl);
        };

        const clearSelectedFields = () => {
            Object.entries(selectedFields).forEach(([fieldName, field]) => {
                if (!field) {
                    return;
                }

                field.textContent = "";

                if (fieldName === "status") {
                    field.className = "appointment-status";
                }
            });

            syncSelectedAppointmentButton(null);
        };

        const setSelectedRow = (row) => {
            rows.forEach((currentRow) => {
                const isSelected = currentRow === row;
                currentRow.classList.toggle("is-selected", isSelected);
                currentRow.setAttribute("aria-selected", isSelected ? "true" : "false");
            });

            syncSelectedAppointmentButton(row);

            if (!selectedFields.name) {
                return;
            }

            selectedFields.name.textContent = row.dataset.name || "";
            selectedFields.projectNumber.textContent = row.dataset.projectNumber || "";
            selectedFields.dateTime.textContent = `${row.dataset.appointmentDate || ""} ${row.dataset.time || ""}`.trim();
            selectedFields.plan.textContent = row.dataset.plan || "";
            selectedFields.phone.textContent = row.dataset.phone || "";
            selectedFields.note.textContent = row.dataset.note || "";

            if (selectedFields.status) {
                selectedFields.status.textContent = row.dataset.status || "";
                selectedFields.status.className = `appointment-status ${row.dataset.statusTone || ""}`.trim();
            }
        };

        const getCurrentPageRows = () => rows.filter((row) => !row.hidden);

        const getBatchSelectedRows = () => allRows.filter((row) => row.classList.contains("is-batch-selected"));

        const setBatchRowSelected = (row, selected) => {
            if (!row) {
                return;
            }

            row.classList.toggle("is-batch-selected", selected);
        };

        const clearBatchSelection = () => {
            allRows.forEach((row) => setBatchRowSelected(row, false));
            batchSelectionAnchor = null;
            syncBatchSelectionState();
        };


        const toggleBatchRowSelection = (row) => {
            setBatchRowSelected(row, !row.classList.contains("is-batch-selected"));
            batchSelectionAnchor = row;
            syncBatchSelectionState();
        };

        const selectBatchRange = (fromRow, toRow) => {
            if (!fromRow || !toRow) {
                setBatchRowSelected(toRow, true);
                batchSelectionAnchor = toRow;
                syncBatchSelectionState();
                return;
            }

            const fromIndex = rows.indexOf(fromRow);
            const toIndex = rows.indexOf(toRow);

            if (fromIndex < 0 || toIndex < 0) {
                setBatchRowSelected(toRow, true);
                batchSelectionAnchor = toRow;
                syncBatchSelectionState();
                return;
            }

            const startIndex = Math.min(fromIndex, toIndex);
            const endIndex = Math.max(fromIndex, toIndex);

            rows.slice(startIndex, endIndex + 1).forEach((row) => setBatchRowSelected(row, true));
            batchSelectionAnchor = toRow;
            syncBatchSelectionState();
        };

        const syncBatchSelectionState = () => {
            const selectedRows = getBatchSelectedRows();
            const selectedCount = selectedRows.length;

            if (batchSelectedCount) {
                batchSelectedCount.textContent = `已選取 ${selectedCount} 筆預約`;
            }

            if (batchSelectionHint) {
                batchSelectionHint.textContent = selectedCount > 0
                    ? "點選只移動光棒，Ctrl 可增減單筆，Shift 可延伸範圍，Ctrl+A 全選"
                    : "點選移動光棒，Ctrl 多選，Shift 範圍，Ctrl+A 全選";
            }

            if (batchRegisterButton) {
                batchRegisterButton.disabled = selectedCount === 0;
            }

            if (batchClearButton) {
                batchClearButton.disabled = selectedCount === 0;
            }
        };

        const selectAllBatchRows = () => {
            const targetRows = rows.slice();

            allRows.forEach((row) => setBatchRowSelected(row, false));
            targetRows.forEach((row) => setBatchRowSelected(row, true));
            batchSelectionAnchor = targetRows.length ? targetRows[targetRows.length - 1] : null;
            syncBatchSelectionState();
        };

        const closeBatchResultDialog = () => {
            if (batchResultModal) {
                batchResultModal.hidden = true;
            }
        };

        const createBatchResultRow = (result) => {
            const item = document.createElement("article");
            const badge = document.createElement("span");
            const content = document.createElement("div");
            const title = document.createElement("strong");
            const message = document.createElement("p");
            const time = document.createElement("time");

            item.className = `appointment-batch-result-item ${result.success ? "success" : "failed"}`;
            badge.className = "appointment-batch-result-badge";
            badge.textContent = result.success ? "成功" : "失敗";
            title.textContent = `${result.name || "未命名"}｜專案編號 ${result.projectNumber || "--"}`;
            message.textContent = result.message;
            time.textContent = result.processedAt;
            content.append(title, message);
            item.append(badge, content, time);

            return item;
        };

        const openBatchResultDialog = (results) => {
            if (!batchResultModal) {
                window.alert("批次掛號假資料已產生。");
                return;
            }

            const successCount = results.filter((result) => result.success).length;
            const failedCount = results.length - successCount;

            if (batchResultTotal) {
                batchResultTotal.textContent = String(results.length);
            }

            if (batchResultSuccess) {
                batchResultSuccess.textContent = String(successCount);
            }

            if (batchResultFailed) {
                batchResultFailed.textContent = String(failedCount);
            }

            if (batchResultList) {
                batchResultList.replaceChildren(...results.map(createBatchResultRow));
            }

            batchResultModal.dataset.state = failedCount > 0 ? (successCount > 0 ? "partial" : "failed") : "success";
            batchResultModal.hidden = false;
            batchResultModal.querySelector("[data-batch-result-close]")?.focus();
        };

        const createFakeBatchResult = (row, index) => {
            const processedAt = new Date().toLocaleString("zh-TW", { hour12: false });
            const failedMessages = [
                "此時段名額已滿",
                "醫令代碼 00063 已存在",
                "缺少必要掛號資料",
                "院內掛號服務暫時無回應"
            ];
            const success = index % 2 === 0;

            return {
                row,
                success,
                name: row.dataset.name || "",
                projectNumber: row.dataset.projectNumber || "",
                barcode: row.dataset.barcode || "",
                message: success ? "掛號成功，醫令新增成功" : failedMessages[index % failedMessages.length],
                processedAt
            };
        };

        const runBatchRegister = () => {
            const selectedRows = getBatchSelectedRows();

            if (selectedRows.length === 0) {
                syncBatchSelectionState();
                return;
            }

            closeActionMenus();
            const results = selectedRows.map(createFakeBatchResult);

            results
                .filter((result) => result.success)
                .forEach((result) => updateAppointmentStatus(result.row, "已預約", "confirmed", "1"));

            lastBatchResults = results;
            openBatchResultDialog(results);
            syncBatchSelectionState();
        };

        const escapeCsvValue = (value) => `"${String(value ?? "").replace(/"/g, '""')}"`;

        const downloadBatchResults = () => {
            if (lastBatchResults.length === 0) {
                return;
            }

            const header = ["處理結果", "受檢者", "專案編號", "Barcode", "訊息", "處理時間"];
            const rowsForCsv = lastBatchResults.map((result) => [
                result.success ? "成功" : "失敗",
                result.name,
                result.projectNumber,
                result.barcode,
                result.message,
                result.processedAt
            ]);
            const csv = [header, ...rowsForCsv]
                .map((line) => line.map(escapeCsvValue).join(","))
                .join("\r\n");
            const blob = new Blob(["\ufeff", csv], { type: "text/csv;charset=utf-8" });
            const url = URL.createObjectURL(blob);
            const link = document.createElement("a");

            link.href = url;
            link.download = "批次掛號結果.csv";
            document.body.appendChild(link);
            link.click();
            link.remove();
            URL.revokeObjectURL(url);
        };

        const bindAppointmentRow = (row) => {
            row.addEventListener("click", (event) => {
                if (event.target.closest("a, button, input, select, textarea")) {
                    return;
                }

                setSelectedRow(row);

                if (event.shiftKey) {
                    selectBatchRange(batchSelectionAnchor, row);
                } else if (event.ctrlKey || event.metaKey) {
                    toggleBatchRowSelection(row);
                } else {
                    batchSelectionAnchor = row;
                    syncBatchSelectionState();
                }
            });

            row.addEventListener("keydown", (event) => {
                if (event.key === "Enter") {
                    event.preventDefault();
                    setSelectedRow(row);
                    batchSelectionAnchor = row;
                    syncBatchSelectionState();
                    return;
                }

                if (event.key !== " ") {
                    return;
                }

                event.preventDefault();
                setSelectedRow(row);

                if (event.shiftKey) {
                    selectBatchRange(batchSelectionAnchor, row);
                } else {
                    toggleBatchRowSelection(row);
                }
            });
        };
        allRows.forEach(bindAppointmentRow);
        const pagination = page.querySelector(".table-footer .pagination");
        const pageSizeSelect = page.querySelector(".table-footer .page-select");
        const pageSummary = page.querySelector(".table-footer > span");
        let activePage = Number.parseInt(page.querySelector("[data-page].active")?.dataset.page || "1", 10);

        if (!Number.isFinite(activePage) || activePage < 1) {
            activePage = 1;
        }

        const getPageSize = () => {
            const parsedSize = Number.parseInt(pageSizeSelect?.value || "", 10);

            return Number.isFinite(parsedSize) && parsedSize > 0 ? parsedSize : 10;
        };

        const getPageCount = () => Math.max(1, Math.ceil(rows.length / getPageSize()));

        const clampPage = (pageNumber) => Math.min(Math.max(pageNumber, 1), getPageCount());

        const updatePageSummary = (startIndex, visibleCount) => {
            if (!pageSummary) {
                return;
            }

            if (!rows.length) {
                pageSummary.textContent = "顯示 0 筆，共 0 筆";
                return;
            }

            pageSummary.textContent = `顯示 ${startIndex + 1} - ${startIndex + visibleCount} 筆，共 ${rows.length} 筆`;
        };

        const getVisiblePageNumbers = () => {
            const pageCount = getPageCount();
            const maxVisiblePages = 7;

            if (pageCount <= maxVisiblePages) {
                const visiblePages = [];

                for (let pageNumber = 1; pageNumber <= pageCount; pageNumber += 1) {
                    visiblePages.push(pageNumber);
                }

                while (visiblePages.length < maxVisiblePages) {
                    visiblePages.push(`placeholder-${visiblePages.length}`);
                }

                return visiblePages;
            }

            if (activePage <= 4) {
                return [1, 2, 3, 4, 5, "ellipsis-end", pageCount];
            }

            if (activePage >= pageCount - 3) {
                return [1, "ellipsis-start", pageCount - 4, pageCount - 3, pageCount - 2, pageCount - 1, pageCount];
            }

            return [1, "ellipsis-start", activePage - 1, activePage, activePage + 1, "ellipsis-end", pageCount];
        };

        const createPageButton = (label, pageNumber, options = {}) => {
            const button = document.createElement("button");

            button.type = "button";
            button.textContent = label;

            if (typeof pageNumber === "number") {
                button.dataset.page = String(pageNumber);
            }

            if (options.ariaLabel) {
                button.setAttribute("aria-label", options.ariaLabel);
            }

            if (options.active) {
                button.classList.add("active");
                button.setAttribute("aria-current", "page");
            }

            if (options.disabled) {
                button.disabled = true;
            }

            if (options.placeholder) {
                button.classList.add("pagination-placeholder");
                button.disabled = true;
                button.setAttribute("aria-hidden", "true");
                button.tabIndex = -1;
                return button;
            }

            if (!options.disabled) {
                button.addEventListener("click", () => setActivePage(pageNumber));
            }

            return button;
        };

        const renderPagination = () => {
            if (!pagination) {
                return;
            }

            const pageCount = getPageCount();
            const buttons = [
                createPageButton("‹", activePage - 1, {
                    ariaLabel: "上一頁",
                    disabled: activePage === 1
                })
            ];

            getVisiblePageNumbers().forEach((pageNumber) => {
                if (typeof pageNumber === "string" && pageNumber.startsWith("placeholder-")) {
                    buttons.push(createPageButton("", null, {
                        placeholder: true
                    }));
                    return;
                }

                if (typeof pageNumber !== "number") {
                    buttons.push(createPageButton("...", null, {
                        ariaLabel: "省略頁碼",
                        disabled: true
                    }));
                    return;
                }

                buttons.push(createPageButton(String(pageNumber), pageNumber, {
                    active: pageNumber === activePage,
                    ariaLabel: `第 ${pageNumber} 頁`
                }));
            });

            buttons.push(createPageButton("›", activePage + 1, {
                ariaLabel: "下一頁",
                disabled: activePage === pageCount
            }));

            pagination.replaceChildren(...buttons);
        };

        const setActivePage = (pageNumber) => {
            activePage = clampPage(pageNumber);

            const pageSize = getPageSize();
            const startIndex = (activePage - 1) * pageSize;
            const endIndex = Math.min(startIndex + pageSize, rows.length);
            const visibleRowSet = new Set(rows.slice(startIndex, endIndex));

            allRows.forEach((row) => {
                row.hidden = !visibleRowSet.has(row);
            });

            const visibleRows = rows.slice(startIndex, endIndex);
            const selectedRow = rows.find((row) => row.classList.contains("is-selected"));

            if (visibleRows.length && !visibleRows.includes(selectedRow)) {
                setSelectedRow(visibleRows[0]);
            } else if (!rows.length) {
                clearSelectedFields();
            } else {
                syncSelectedAppointmentButton(selectedRow);
            }

            updatePageSummary(startIndex, visibleRows.length);
            renderPagination();
            syncHorizontalScrollbarSize();
            syncBatchSelectionState();
        };

        const applyAppointmentFilters = (resetPage = true) => {
            const startDate = normalizeDateValue(startDateInput?.value);
            const endDate = normalizeDateValue(endDateInput?.value);
            const selectedArea = (reservationAreaSelect?.value || "").trim();
            const selectedStatus = (appointmentStatusSelect?.value || "").trim();
            const keyword = normalizeFilterValue(keywordInput?.value);
            const searchAllDatesForDefaultKeyword = Boolean(keyword)
                && !dateFilterTouched
                && isDefaultTodayDateRange();

            rows = allRows.filter((row) => {
                const matchesKeyword = matchesAppointmentKeyword(row, keyword);
                const matchesDate = isAppointmentDateInRange(row, startDate, endDate)
                    || (searchAllDatesForDefaultKeyword && matchesKeyword);

                return matchesDate
                    && (!selectedArea || getAppointmentWorkArea(row) === selectedArea)
                    && (!selectedStatus || getAppointmentReserveStatus(row) === selectedStatus)
                    && matchesKeyword;
            });

            syncDashboardCounts();
            setActivePage(resetPage ? 1 : activePage);
        };

        const setAppointmentQueryState = (state, message = "") => {
            page.dataset.queryState = state;
            tablePanel?.classList.toggle("is-querying", state === "loading");

            if (searchButton) {
                searchButton.disabled = state === "loading";
                searchButton.setAttribute("aria-busy", String(state === "loading"));
            }

            if (searchButtonLabel) {
                searchButtonLabel.textContent = state === "loading" ? "查詢中" : "搜尋";
            }

            if (queryStatus) {
                queryStatus.dataset.state = state;
                queryStatus.textContent = message;
            }
        };

        const buildAppointmentSearchUrl = () => {
            const url = new URL(window.location.href);
            const keyword = String(keywordInput?.value || "").trim();
            const startDate = String(startDateInput?.value || "").trim();
            const endDate = String(endDateInput?.value || "").trim();
            const selectedArea = String(reservationAreaSelect?.value || "").trim();
            const selectedStatus = String(appointmentStatusSelect?.value || "").trim();
            const shouldSearchAllDatesForKeyword = Boolean(keyword)
                && !dateFilterTouched
                && isDefaultTodayDateRange();

            url.search = "";

            if (startDate && !shouldSearchAllDatesForKeyword) {
                url.searchParams.set("checkDateStart", startDate);
            }

            if (endDate && !shouldSearchAllDatesForKeyword) {
                url.searchParams.set("checkDateEnd", endDate);
            }

            if (selectedArea) {
                url.searchParams.set("reservationArea", selectedArea);
            }

            if (selectedStatus) {
                url.searchParams.set("reserveStatus", selectedStatus);
            }

            if (keyword) {
                url.searchParams.set("keyword", keyword);
            }

            return url.toString();
        };

        const runAppointmentSearch = () => {
            window.clearTimeout(queryStateTimer);
            closeActionMenus();
            setAppointmentQueryState("loading", "查詢中...");
            window.location.assign(buildAppointmentSearchUrl());
        };

        const getTodayInputDate = () => {
            const today = new Date();
            return `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, "0")}-${String(today.getDate()).padStart(2, "0")}`;
        };

        const toDateInputValue = (value) => {
            const normalized = normalizeDateValue(value);

            return normalized
                ? `${normalized.slice(0, 4)}-${normalized.slice(4, 6)}-${normalized.slice(6, 8)}`
                : "";
        };

        const applyAppointmentQueryFromUrl = () => {
            const query = new URLSearchParams(window.location.search);
            const keyword = String(query.get("keyword") || query.get("barcode") || query.get("healthCode") || "").trim();
            const projectNo = String(query.get("projectNo") || query.get("projectNumber") || "").trim();
            const customerName = String(query.get("customerName") || query.get("company") || "").trim();
            const startDateParam = query.get("checkDateStart") || query.get("startDate");
            const endDateParam = query.get("checkDateEnd") || query.get("endDate");
            const hasDateQuery = query.has("checkDateStart")
                || query.has("checkDateEnd")
                || query.has("startDate")
                || query.has("endDate");
            const hasAppointmentQuery = keyword || projectNo || customerName || hasDateQuery;

            if (!hasAppointmentQuery) {
                return;
            }

            const today = getTodayInputDate();
            const shouldUseTodayFallback = !keyword && (projectNo || customerName);
            const fallbackDate = shouldUseTodayFallback ? today : "";
            const startDate = toDateInputValue(startDateParam) || fallbackDate;
            const endDate = toDateInputValue(endDateParam) || startDate || fallbackDate;

            if (keywordInput) {
                keywordInput.value = keyword || projectNo || customerName;
            }

            if (startDateInput) {
                startDateInput.value = startDate;
            }

            if (endDateInput) {
                endDateInput.value = endDate;
            }

            dateFilterTouched = Boolean(startDate || endDate);
        };

        applyAppointmentQueryFromUrl();
        [startDateInput, endDateInput].forEach((input) => {
            ["input", "change"].forEach((eventName) => {
                input?.addEventListener(eventName, () => {
                    dateFilterTouched = true;
                });
            });
        });
        [startDateInput, endDateInput, keywordInput].forEach((input) => {
            input?.addEventListener("keydown", (event) => {
                if (event.key !== "Enter") {
                    return;
                }

                event.preventDefault();
                runAppointmentSearch();
            });
        });

        searchButton?.addEventListener("click", runAppointmentSearch);
        selectedAppointmentButton?.addEventListener("click", openSelectedAppointment);
        pageSizeSelect?.addEventListener("change", () => setActivePage(1));
        batchClearButton?.addEventListener("click", clearBatchSelection);
        batchRegisterButton?.addEventListener("click", runBatchRegister);
        batchResultCloseButtons.forEach((button) => {
            button.addEventListener("click", closeBatchResultDialog);
        });
        batchResultDownloadButton?.addEventListener("click", downloadBatchResults);
        applyAppointmentFilters(false);
    })();
