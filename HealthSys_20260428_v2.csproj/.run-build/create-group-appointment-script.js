(function () {
        const form = document.querySelector("[data-group-import-form]");
        const input = document.querySelector("[data-group-roster-input]");
        const dropzone = document.querySelector("[data-group-dropzone]");
        const clearButton = document.querySelector("[data-roster-clear]");
        const rosterReserveButtons = Array.from(document.querySelectorAll("[data-roster-reserve]"));
        const rosterRows = Array.from(document.querySelectorAll("[data-roster-row]"));
        const rosterFloatingRows = Array.from(document.querySelectorAll("[data-roster-floating-row]"));
        const pageInfo = document.querySelector("[data-roster-page-info]");
        const pageSizeSelect = document.querySelector("[data-roster-page-size]");
        const pageButtonsHost = document.querySelector("[data-roster-page-buttons]");
        const prevButton = document.querySelector("[data-roster-prev]");
        const nextButton = document.querySelector("[data-roster-next]");
        const selectAllCheckbox = document.querySelector("[data-roster-select-all]");
        const rosterCheckboxes = Array.from(document.querySelectorAll("[data-roster-select-row]"));
        const tableWrap = document.querySelector("[data-roster-table-wrap]");
        const fixedPane = document.querySelector("[data-roster-fixed-pane]");
        const horizontalScrollbar = document.querySelector("[data-roster-table-scrollbar]");
        const horizontalScrollbarContent = document.querySelector("[data-roster-table-scrollbar-content]");
        const page = document.querySelector(".group-create-page");
        const createAppointmentUrl = page ? page.dataset.createAppointmentUrl || "" : "";
        const rosterKey = page ? page.dataset.rosterKey || "" : "";
        const fixedTable = document.querySelector(".roster-fixed-table");
        const scrollTable = document.querySelector(".roster-scroll-table");
        const rosterColumnResizeGroups = [
            {
                table: fixedTable,
                columns: [
                    { variableName: "--roster-fixed-select-width", minWidth: 36, fallbackWidth: 40 },
                    { variableName: "--roster-fixed-edit-width", minWidth: 48, fallbackWidth: 58 },
                    { variableName: "--roster-fixed-row-width", minWidth: 40, fallbackWidth: 44 },
                    { variableName: "--roster-fixed-id-width", minWidth: 80, fallbackWidth: 104 },
                    { variableName: "--roster-fixed-chart-width", minWidth: 88, fallbackWidth: 104 },
                    { variableName: "--roster-fixed-name-width", minWidth: 64, fallbackWidth: 74 },
                    { variableName: "--roster-fixed-sex-width", minWidth: 40, fallbackWidth: 40 },
                    { variableName: "--roster-fixed-birthday-width", minWidth: 76, fallbackWidth: 84 },
                    { variableName: "--roster-fixed-phone-width", minWidth: 100, fallbackWidth: 116 },
                    { variableName: "--roster-fixed-mobile-width", minWidth: 104, fallbackWidth: 120 },
                    { variableName: "--roster-fixed-date-width", minWidth: 96, fallbackWidth: 112 }
                ]
            },
            {
                table: scrollTable,
                columns: [
                    { variableName: "--roster-scroll-address-width", minWidth: 140, fallbackWidth: 220 },
                    { variableName: "--roster-scroll-email-width", minWidth: 160, fallbackWidth: 240 },
                    { variableName: "--roster-scroll-dependent-width", minWidth: 72, fallbackWidth: 92 },
                    { variableName: "--roster-scroll-dependent-note-width", minWidth: 96, fallbackWidth: 132 },
                    { variableName: "--roster-scroll-note-width", minWidth: 110, fallbackWidth: 168 },
                    { variableName: "--roster-scroll-package-width", minWidth: 140, fallbackWidth: 208 },
                    { variableName: "--roster-scroll-addon-width", minWidth: 120, fallbackWidth: 168 },
                    { variableName: "--roster-scroll-company-width", minWidth: 120, fallbackWidth: 158 },
                    { variableName: "--roster-scroll-job-width", minWidth: 90, fallbackWidth: 118 },
                    { variableName: "--roster-scroll-status-width", minWidth: 90, fallbackWidth: 112 }
                ]
            }
        ];
        let currentRosterPage = 1;
        let rosterSelectionAnchorIndex = null;
        let isSyncingRosterHorizontalScroll = false;

        if (!form || !input) {
            return;
        }

        function getRosterPageSize() {
            const pageSize = Number.parseInt(pageSizeSelect ? pageSizeSelect.value : "20", 10);
            return Number.isFinite(pageSize) && pageSize > 0 ? pageSize : 20;
        }

        function getRosterPageCount() {
            return Math.max(1, Math.ceil(rosterRows.length / getRosterPageSize()));
        }

        function appendRosterPageButton(pageNumber) {
            if (!pageButtonsHost) {
                return;
            }

            const button = document.createElement("button");
            button.type = "button";
            button.textContent = String(pageNumber);
            button.classList.toggle("active", pageNumber === currentRosterPage);

            if (pageNumber === currentRosterPage) {
                button.setAttribute("aria-current", "page");
            }

            button.addEventListener("click", function () {
                currentRosterPage = pageNumber;
                renderRosterPage();
            });

            pageButtonsHost.appendChild(button);
        }

        function appendRosterPageEllipsis() {
            if (!pageButtonsHost) {
                return;
            }

            const ellipsis = document.createElement("span");
            ellipsis.className = "pagination-ellipsis";
            ellipsis.textContent = "...";
            pageButtonsHost.appendChild(ellipsis);
        }

        function renderRosterPageButtons(pageCount) {
            if (!pageButtonsHost) {
                return;
            }

            pageButtonsHost.innerHTML = "";

            if (pageCount <= 7) {
                for (let pageNumber = 1; pageNumber <= pageCount; pageNumber += 1) {
                    appendRosterPageButton(pageNumber);
                }
                return;
            }

            const visiblePages = [1, currentRosterPage - 1, currentRosterPage, currentRosterPage + 1, pageCount]
                .filter(function (pageNumber, index, pages) {
                    return pageNumber >= 1 && pageNumber <= pageCount && pages.indexOf(pageNumber) === index;
                })
                .sort(function (left, right) { return left - right; });

            visiblePages.forEach(function (pageNumber, index) {
                if (index > 0 && pageNumber - visiblePages[index - 1] > 1) {
                    appendRosterPageEllipsis();
                }

                appendRosterPageButton(pageNumber);
            });
        }

        function getRosterFixedColumnsWidth() {
            const fixedPaneWidth = fixedPane ? fixedPane.getBoundingClientRect().width : 0;

            if (fixedPaneWidth > 0) {
                return fixedPaneWidth;
            }

            const page = document.querySelector(".group-create-page") || document.documentElement;
            const styles = getComputedStyle(page);
            const fixedWidthNames = [
                "--roster-fixed-select-width",
                "--roster-fixed-edit-width",
                "--roster-fixed-row-width",
                "--roster-fixed-id-width",
                "--roster-fixed-chart-width",
                "--roster-fixed-name-width",
                "--roster-fixed-sex-width",
                "--roster-fixed-birthday-width",
                "--roster-fixed-phone-width",
                "--roster-fixed-mobile-width",
                "--roster-fixed-date-width"
            ];

            return fixedWidthNames.reduce(function (total, name) {
                return total + (Number.parseFloat(styles.getPropertyValue(name)) || 0);
            }, 0);
        }

        function syncRosterHorizontalScrollbarSize() {
            if (!tableWrap || !horizontalScrollbar || !horizontalScrollbarContent) {
                return;
            }

            const fixedColumnsWidth = getRosterFixedColumnsWidth();
            const visibleScrollWidth = tableWrap.clientWidth;
            const scrollableContentWidth = tableWrap.scrollWidth;
            const hasHorizontalOverflow = tableWrap.scrollWidth > tableWrap.clientWidth + 1 && visibleScrollWidth > 0;

            horizontalScrollbar.hidden = !hasHorizontalOverflow;
            horizontalScrollbar.style.left = fixedColumnsWidth + "px";
            horizontalScrollbarContent.style.width = Math.max(scrollableContentWidth, visibleScrollWidth) + "px";

            if (hasHorizontalOverflow) {
                horizontalScrollbar.scrollLeft = tableWrap.scrollLeft;
            }
        }

        function syncFromRosterTableScroll() {
            if (!horizontalScrollbar || isSyncingRosterHorizontalScroll) {
                return;
            }

            isSyncingRosterHorizontalScroll = true;
            horizontalScrollbar.scrollLeft = tableWrap ? tableWrap.scrollLeft : 0;
            isSyncingRosterHorizontalScroll = false;
        }

        function syncFromRosterHorizontalScrollbar() {
            if (!tableWrap || isSyncingRosterHorizontalScroll) {
                return;
            }

            isSyncingRosterHorizontalScroll = true;
            tableWrap.scrollLeft = horizontalScrollbar ? horizontalScrollbar.scrollLeft : 0;
            isSyncingRosterHorizontalScroll = false;
        }

        function bindRosterHorizontalScrollbar() {
            if (!tableWrap || !horizontalScrollbar || !horizontalScrollbarContent) {
                return;
            }

            tableWrap.addEventListener("scroll", syncFromRosterTableScroll);
            horizontalScrollbar.addEventListener("scroll", syncFromRosterHorizontalScrollbar);
            tableWrap.addEventListener("wheel", function (event) {
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

            window.addEventListener("resize", syncRosterHorizontalScrollbarSize);

            if ("ResizeObserver" in window) {
                const resizeObserver = new ResizeObserver(syncRosterHorizontalScrollbarSize);
                resizeObserver.observe(tableWrap);
            }
        }

        function getRosterCssPixelValue(variableName, fallbackWidth) {
            if (!page) {
                return fallbackWidth;
            }

            const value = Number.parseFloat(getComputedStyle(page).getPropertyValue(variableName));
            return Number.isFinite(value) && value > 0 ? value : fallbackWidth;
        }

        function setRosterCssPixelValue(variableName, value) {
            if (!page) {
                return;
            }

            page.style.setProperty(variableName, Math.round(value) + "px");
        }

        function bindRosterColumnResize() {
            if (!page) {
                return;
            }

            rosterColumnResizeGroups.forEach(function (group) {
                if (!group.table) {
                    return;
                }

                const headers = Array.from(group.table.querySelectorAll("thead th"));

                group.columns.forEach(function (column, index) {
                    const header = headers[index];

                    if (!header) {
                        return;
                    }

                    const handle = document.createElement("span");
                    handle.className = "roster-column-resize-handle";
                    handle.setAttribute("aria-hidden", "true");
                    header.appendChild(handle);

                    handle.addEventListener("pointerdown", function (event) {
                        if (event.button !== undefined && event.button !== 0) {
                            return;
                        }

                        event.preventDefault();
                        event.stopPropagation();

                        const startX = event.clientX;
                        const startWidth = getRosterCssPixelValue(column.variableName, column.fallbackWidth);
                        const minWidth = column.minWidth || 56;

                        header.classList.add("is-resizing");
                        document.body.classList.add("is-roster-column-resizing");

                        if (handle.setPointerCapture) {
                            handle.setPointerCapture(event.pointerId);
                        }

                        function updateColumnWidth(moveEvent) {
                            const nextWidth = Math.max(minWidth, startWidth + moveEvent.clientX - startX);
                            setRosterCssPixelValue(column.variableName, nextWidth);
                            syncRosterHorizontalScrollbarSize();
                        }

                        function finishResize(endEvent) {
                            handle.removeEventListener("pointermove", updateColumnWidth);
                            handle.removeEventListener("pointerup", finishResize);
                            handle.removeEventListener("pointercancel", finishResize);

                            if (handle.releasePointerCapture && endEvent && handle.hasPointerCapture && handle.hasPointerCapture(endEvent.pointerId)) {
                                handle.releasePointerCapture(endEvent.pointerId);
                            }

                            header.classList.remove("is-resizing");
                            document.body.classList.remove("is-roster-column-resizing");
                            syncRosterHorizontalScrollbarSize();
                        }

                        handle.addEventListener("pointermove", updateColumnWidth);
                        handle.addEventListener("pointerup", finishResize);
                        handle.addEventListener("pointercancel", finishResize);
                    });
                });
            });
        }

        function setRosterRowPairClass(index, className, isActive) {
            const fixedRow = rosterRows[index];
            const floatingRow = rosterFloatingRows[index];

            if (fixedRow) {
                fixedRow.classList.toggle(className, isActive);
            }

            if (floatingRow) {
                floatingRow.classList.toggle(className, isActive);
            }
        }

        function shouldIgnoreRosterRowClick(event) {
            return !!(event.target && event.target.closest && event.target.closest("button, a, input, select, textarea, label"));
        }

        function getRosterRowCheckbox(index) {
            const fixedRow = rosterRows[index];
            return fixedRow ? fixedRow.querySelector("[data-roster-select-row]") : null;
        }

        function clearRosterSelection() {
            rosterCheckboxes.forEach(function (checkbox) {
                checkbox.checked = false;
            });
        }

        function setRosterRangeSelection(startIndex, endIndex, isChecked, preserveExisting) {
            const lowerIndex = Math.min(startIndex, endIndex);
            const upperIndex = Math.max(startIndex, endIndex);

            if (!preserveExisting) {
                clearRosterSelection();
            }

            rosterRows.forEach(function (row, index) {
                if (index < lowerIndex || index > upperIndex) {
                    return;
                }

                const checkbox = getRosterRowCheckbox(index);

                if (checkbox && !checkbox.disabled) {
                    checkbox.checked = isChecked;
                }
            });

            rosterSelectionAnchorIndex = endIndex;
            updateRosterSelectionState();
        }

        function setRosterSingleSelection(index, isChecked) {
            const targetCheckbox = getRosterRowCheckbox(index);

            rosterCheckboxes.forEach(function (checkbox) {
                checkbox.checked = checkbox === targetCheckbox && isChecked;
            });

            rosterSelectionAnchorIndex = isChecked ? index : null;
            updateRosterSelectionState();
        }

        function applyRosterSelection(index, event, options) {
            const checkbox = getRosterRowCheckbox(index);
            const isCheckboxAction = !!(options && options.checkboxMode);

            if (!checkbox || checkbox.disabled) {
                return;
            }

            if (event && event.shiftKey && rosterSelectionAnchorIndex !== null) {
                const nextChecked = isCheckboxAction ? checkbox.checked : true;
                const preserveExisting = isCheckboxAction || !!(event.ctrlKey || event.metaKey);
                setRosterRangeSelection(rosterSelectionAnchorIndex, index, nextChecked, preserveExisting);
                return;
            }

            if (isCheckboxAction) {
                rosterSelectionAnchorIndex = index;
                updateRosterSelectionState();
                return;
            }

            if (event && (event.ctrlKey || event.metaKey)) {
                checkbox.checked = !checkbox.checked;
                rosterSelectionAnchorIndex = index;
                updateRosterSelectionState();
                return;
            }

            setRosterSingleSelection(index, true);
        }

        function toggleRosterRowSelection(index, event) {
            applyRosterSelection(index, event, { checkboxMode: false });
        }

        function getGroupAppointmentReturnUrl() {
            const returnUrl = new URL(window.location.href);

            if (rosterKey) {
                returnUrl.searchParams.set("rosterKey", rosterKey);
            }

            return returnUrl.pathname + returnUrl.search + returnUrl.hash;
        }

        function setRosterAppointmentParam(url, name, value) {
            const normalizedValue = String(value || "").trim();

            if (normalizedValue) {
                url.searchParams.set(name, normalizedValue);
            }
        }

        function openRosterAppointment(button) {
            if (!createAppointmentUrl || !button) {
                return;
            }

            const url = new URL(createAppointmentUrl, window.location.href);
            setRosterAppointmentParam(url, "source", "groupRoster");
            setRosterAppointmentParam(url, "returnUrl", getGroupAppointmentReturnUrl());
            setRosterAppointmentParam(url, "name", button.dataset.rosterName);
            setRosterAppointmentParam(url, "idNo", button.dataset.rosterIdNo);
            setRosterAppointmentParam(url, "chartNo", button.dataset.rosterChartNo);
            setRosterAppointmentParam(url, "sex", button.dataset.rosterSex);
            setRosterAppointmentParam(url, "birthDate", button.dataset.rosterBirthday);
            setRosterAppointmentParam(url, "phone", button.dataset.rosterPhone);
            setRosterAppointmentParam(url, "mobile", button.dataset.rosterMobile);
            setRosterAppointmentParam(url, "email", button.dataset.rosterEmail);
            setRosterAppointmentParam(url, "checkDate", button.dataset.rosterCheckDate);
            setRosterAppointmentParam(url, "workArea", button.dataset.rosterWorkArea);
            setRosterAppointmentParam(url, "address", button.dataset.rosterAddress);
            setRosterAppointmentParam(url, "company", button.dataset.rosterCompany);
            setRosterAppointmentParam(url, "note", button.dataset.rosterNote);
            setRosterAppointmentParam(url, "packageName", button.dataset.rosterPackageName);
            setRosterAppointmentParam(url, "addOns", button.dataset.rosterAddOns);
            window.location.assign(url.toString());
        }
        function bindRosterRowSync() {
            function bindRow(row, index) {
                if (!row) {
                    return;
                }

                row.addEventListener("mouseenter", function () {
                    setRosterRowPairClass(index, "is-row-hovered", true);
                });

                row.addEventListener("mouseleave", function () {
                    setRosterRowPairClass(index, "is-row-hovered", false);
                });

                row.addEventListener("click", function (event) {
                    if (shouldIgnoreRosterRowClick(event)) {
                        return;
                    }

                    toggleRosterRowSelection(index, event);
                });
            }

            rosterRows.forEach(bindRow);
            rosterFloatingRows.forEach(bindRow);
        }

        function updateRosterSelectionState() {
            const selectedCount = rosterCheckboxes.filter(function (checkbox) { return checkbox.checked; }).length;

            rosterRows.forEach(function (row, index) {
                const checkbox = getRosterRowCheckbox(index);
                const isSelected = !!checkbox && checkbox.checked;
                row.classList.toggle("is-selected", isSelected);

                if (rosterFloatingRows[index]) {
                    rosterFloatingRows[index].classList.toggle("is-selected", isSelected);
                }
            });

            if (selectAllCheckbox) {
                selectAllCheckbox.checked = rosterCheckboxes.length > 0 && selectedCount === rosterCheckboxes.length;
                selectAllCheckbox.indeterminate = selectedCount > 0 && selectedCount < rosterCheckboxes.length;
                selectAllCheckbox.disabled = rosterCheckboxes.length === 0;
            }
        }

        function renderRosterPage() {
            const pageSize = getRosterPageSize();
            const totalRows = rosterRows.length;
            const pageCount = getRosterPageCount();

            currentRosterPage = Math.min(Math.max(currentRosterPage, 1), pageCount);

            const startIndex = (currentRosterPage - 1) * pageSize;
            const endIndex = Math.min(startIndex + pageSize, totalRows);

            rosterRows.forEach(function (row, index) {
                const isHidden = index < startIndex || index >= endIndex;
                row.hidden = isHidden;

                if (rosterFloatingRows[index]) {
                    rosterFloatingRows[index].hidden = isHidden;
                }
            });

            if (pageInfo) {
                pageInfo.textContent = totalRows > 0
                    ? "顯示第 " + (startIndex + 1) + " - " + endIndex + " 筆，共 " + totalRows + " 筆"
                    : "顯示 0 筆，共 0 筆";
            }

            if (prevButton) {
                prevButton.disabled = currentRosterPage <= 1 || totalRows === 0;
            }

            if (nextButton) {
                nextButton.disabled = currentRosterPage >= pageCount || totalRows === 0;
            }

            renderRosterPageButtons(pageCount);
            updateRosterSelectionState();
            syncRosterHorizontalScrollbarSize();
        }

        bindRosterHorizontalScrollbar();
        bindRosterColumnResize();
        bindRosterRowSync();

        if (selectAllCheckbox) {
            selectAllCheckbox.addEventListener("click", function (event) {
                event.stopPropagation();
                const shouldSelectAll = selectAllCheckbox.checked;

                rosterCheckboxes.forEach(function (checkbox) {
                    checkbox.checked = shouldSelectAll;
                });

                rosterSelectionAnchorIndex = shouldSelectAll && rosterCheckboxes.length > 0 ? rosterCheckboxes.length - 1 : null;
                updateRosterSelectionState();
            });
        }
        rosterCheckboxes.forEach(function (checkbox, index) {
            checkbox.addEventListener("click", function (event) {
                event.stopPropagation();
                applyRosterSelection(index, event, { checkboxMode: true });
            });
        });

        input.addEventListener("change", function () {
            if (input.files && input.files.length > 0) {
                form.submit();
            }
        });

        rosterReserveButtons.forEach(function (button) {
            button.addEventListener("click", function (event) {
                event.preventDefault();
                event.stopPropagation();
                openRosterAppointment(button);
            });
        });

        if (clearButton) {
            clearButton.addEventListener("click", function () {
                const clearUrl = clearButton.dataset.rosterClearUrl;

                if (clearUrl) {
                    window.location.assign(clearUrl);
                }
            });
        }

        if (dropzone) {
            dropzone.addEventListener("dragover", function (event) {
                event.preventDefault();
                dropzone.classList.add("is-dragover");
            });

            dropzone.addEventListener("dragleave", function () {
                dropzone.classList.remove("is-dragover");
            });

            dropzone.addEventListener("drop", function (event) {
                event.preventDefault();
                dropzone.classList.remove("is-dragover");

                if (event.dataTransfer && event.dataTransfer.files.length > 0) {
                    input.files = event.dataTransfer.files;
                    form.submit();
                }
            });
        }

        if (pageSizeSelect) {
            pageSizeSelect.addEventListener("change", function () {
                currentRosterPage = 1;
                renderRosterPage();
            });
        }

        if (prevButton) {
            prevButton.addEventListener("click", function () {
                currentRosterPage -= 1;
                renderRosterPage();
            });
        }

        if (nextButton) {
            nextButton.addEventListener("click", function () {
                currentRosterPage += 1;
                renderRosterPage();
            });
        }

        renderRosterPage();
    })();
