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

        const rows = Array.from(page.querySelectorAll("[data-appointment-row]"));
        const tableWrap = page.querySelector("[data-appointment-table-wrap]");
        const horizontalScrollbar = page.querySelector("[data-appointment-table-scrollbar]");
        const horizontalScrollbarContent = page.querySelector("[data-appointment-table-scrollbar-content]");
        let isSyncingHorizontalScroll = false;

        const getFixedColumnsWidth = () => {
            const styles = getComputedStyle(page);
            const readPixelValue = (name) => Number.parseFloat(styles.getPropertyValue(name)) || 0;

            return readPixelValue("--today-fixed-select-width")
                + readPixelValue("--today-fixed-name-width")
                + readPixelValue("--today-fixed-date-width")
                + readPixelValue("--today-fixed-time-width");
        };

        const syncHorizontalScrollbarSize = () => {
            if (!tableWrap || !horizontalScrollbar || !horizontalScrollbarContent) {
                return;
            }

            const fixedColumnsWidth = getFixedColumnsWidth();
            const visibleScrollWidth = tableWrap.clientWidth - fixedColumnsWidth;
            const scrollableContentWidth = tableWrap.scrollWidth - fixedColumnsWidth;
            const hasHorizontalOverflow = tableWrap.scrollWidth > tableWrap.clientWidth + 1 && visibleScrollWidth > 0;

            horizontalScrollbar.hidden = !hasHorizontalOverflow;
            horizontalScrollbarContent.style.width = `${Math.max(scrollableContentWidth, visibleScrollWidth)}px`;

            if (hasHorizontalOverflow) {
                horizontalScrollbar.scrollLeft = tableWrap.scrollLeft;
            }
        };

        const syncFromTableScroll = () => {
            if (!horizontalScrollbar || isSyncingHorizontalScroll) {
                return;
            }

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

            if ("ResizeObserver" in window) {
                const resizeObserver = new ResizeObserver(syncHorizontalScrollbarSize);
                resizeObserver.observe(tableWrap);
            }
        }

        const setSelectedRow = (row) => {
            rows.forEach((currentRow) => {
                const isSelected = currentRow === row;
                currentRow.classList.toggle("is-selected", isSelected);
                currentRow.setAttribute("aria-selected", isSelected ? "true" : "false");
            });

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

        rows.forEach((row) => {
            row.addEventListener("click", (event) => {
                if (event.target.closest("a, button, input, select, textarea")) {
                    return;
                }

                setSelectedRow(row);
            });

            row.addEventListener("keydown", (event) => {
                if (event.key !== "Enter" && event.key !== " ") {
                    return;
                }

                event.preventDefault();
                setSelectedRow(row);
            });
        });

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
            const visiblePages = [];
            const addPage = (pageNumber) => {
                if (!visiblePages.includes(pageNumber)) {
                    visiblePages.push(pageNumber);
                }
            };

            if (pageCount <= 7) {
                for (let pageNumber = 1; pageNumber <= pageCount; pageNumber += 1) {
                    addPage(pageNumber);
                }

                return visiblePages;
            }

            addPage(1);

            const startPage = Math.max(2, activePage - 2);
            const endPage = Math.min(pageCount - 1, activePage + 2);

            if (startPage > 2) {
                visiblePages.push("ellipsis-start");
            }

            for (let pageNumber = startPage; pageNumber <= endPage; pageNumber += 1) {
                addPage(pageNumber);
            }

            if (endPage < pageCount - 1) {
                visiblePages.push("ellipsis-end");
            }

            addPage(pageCount);

            return visiblePages;
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
                return button;
            }

            button.addEventListener("click", () => setActivePage(pageNumber));

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

            rows.forEach((row, index) => {
                row.hidden = index < startIndex || index >= endIndex;
            });

            const visibleRows = rows.slice(startIndex, endIndex);
            const selectedRow = rows.find((row) => row.classList.contains("is-selected"));

            if (visibleRows.length && !visibleRows.includes(selectedRow)) {
                setSelectedRow(visibleRows[0]);
            }

            updatePageSummary(startIndex, visibleRows.length);
            renderPagination();
            syncHorizontalScrollbarSize();
        };

        pageSizeSelect?.addEventListener("change", () => setActivePage(1));
        setActivePage(activePage);
    })();
