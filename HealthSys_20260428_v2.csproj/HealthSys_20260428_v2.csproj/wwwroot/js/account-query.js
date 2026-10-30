(() => {
    "use strict";

    const root = document.querySelector(".account-query");
    if (!root) return;
    const byId = id => document.getElementById(id);
    const amountTooltip = byId("account-amount-tooltip");
    let tooltipButton = null;
    let tooltipPinned = false;

    function positionAmountTooltip() {
        if (!tooltipButton || amountTooltip.hidden) return;
        const anchor = tooltipButton.getBoundingClientRect();
        const bounds = amountTooltip.getBoundingClientRect();
        amountTooltip.style.left = `${Math.max(8, Math.min(anchor.right - bounds.width, window.innerWidth - bounds.width - 8))}px`;
        amountTooltip.style.top = `${anchor.bottom + bounds.height + 8 <= window.innerHeight
            ? anchor.bottom + 8 : Math.max(8, anchor.top - bounds.height - 8)}px`;
    }

    function hideAmountTooltip() {
        amountTooltip.hidden = true;
        tooltipButton = null;
        tooltipPinned = false;
    }

    function showAmountTooltip(button) {
        if (tooltipButton !== button) tooltipPinned = false;
        tooltipButton = button;
        amountTooltip.textContent = button.dataset.help;
        amountTooltip.hidden = false;
        positionAmountTooltip();
    }

    root.querySelectorAll(".account-heading-help").forEach(button => {
        button.addEventListener("pointerenter", () => showAmountTooltip(button));
        button.addEventListener("focus", () => showAmountTooltip(button));
        button.addEventListener("pointerleave", () => {
            if (!tooltipPinned && document.activeElement !== button) hideAmountTooltip();
        });
        button.addEventListener("blur", hideAmountTooltip);
        button.addEventListener("click", () => {
            if (tooltipButton === button && tooltipPinned) hideAmountTooltip();
            else {
                showAmountTooltip(button);
                tooltipPinned = true;
            }
        });
    });
    document.addEventListener("click", event => {
        if (!event.target.closest(".account-heading-help")) hideAmountTooltip();
    });
    document.addEventListener("keydown", event => {
        if (event.key === "Escape") hideAmountTooltip();
    });
    window.addEventListener("resize", positionAmountTooltip);
    window.addEventListener("scroll", hideAmountTooltip, true);
    const escapeHtml = value => String(value ?? "").replace(/[&<>"']/g, char => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[char]);
    const show = value => escapeHtml(String(value ?? "").trim() || "—");
    const orderText = order => `${String(order.name ?? "").trim()}${String(order.code ?? "").trim() ? ` (${String(order.code).trim()})` : ""}`;
    const suiteOrders = (project, suite) => project.orders.filter(order => String(order.sequence).trim() === String(suite.seq).trim());
    const money = value => Number(value).toLocaleString("zh-TW");
    const keys = ["total", "receivable", "discount", "booked", "arrears"];
    const sum = (orders, key) => orders.reduce((total, order) => total + order[key], 0);
    const dateText = date => `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}`;
    const today = new Date();
    byId("account-start").value = dateText(new Date(today.getFullYear(), today.getMonth(), 1));
    byId("account-end").value = dateText(today);

    const recordId = row => [row.barcode, row.projectNo, row.chop1Date, row.chop1Time, row.chop1Room, row.intop1No].join(":");
    const toProject = row => ({ ...row, id: recordId(row), date: row.reserveDate, suites: [], suiteState: "loading", orders: [], ordersState: "idle" });
    let projects = [];
    let filteredProjects = [];
    let openSuites = new Set();
    let selectedSuite = null;
    let openProjectDetails = new Set();
    let currentPage = 1;
    let requestController = null;
    let listLoading = false;
    let exporting = false;
    const tableScroll = root.querySelector(".account-table-scroll");
    const pagination = root.querySelector(".account-pagination");
    let resizeFrame = 0;

    function fitGridToViewport() {
        resizeFrame = 0;
        const topAtPageStart = tableScroll.getBoundingClientRect().top + window.scrollY;
        const available = window.innerHeight - topAtPageStart - pagination.offsetHeight - 28;
        tableScroll.style.height = `${Math.max(160, Math.floor(available))}px`;
    }

    function scheduleGridResize() {
        if (resizeFrame) cancelAnimationFrame(resizeFrame);
        resizeFrame = requestAnimationFrame(fitGridToViewport);
    }

    window.addEventListener("resize", scheduleGridResize);
    window.visualViewport?.addEventListener("resize", scheduleGridResize);
    if (typeof ResizeObserver !== "undefined") {
        const observer = new ResizeObserver(scheduleGridResize);
        observer.observe(root.querySelector(".account-search"));
        observer.observe(root.querySelector(".account-card-head"));
        observer.observe(pagination);
    }

    function setLoading(message) {
        byId("account-loading").hidden = !message;
        if (message) byId("account-loading-text").textContent = message;
        scheduleGridResize();
    }

    async function loadSuites(project, signal) {
        if (!project.chop1Date || !project.chop1Time || !project.chop1Room || !project.intop1No) {
            project.suiteState = "missing";
            render();
            return;
        }
        try {
            const params = new URLSearchParams({ chop1Date: project.chop1Date, chop1Time: project.chop1Time,
                chop1Room: project.chop1Room, intop1No: project.intop1No });
            const response = await fetch(`${root.dataset.suitesUrl}?${params}`, { signal });
            const data = await response.json();
            if (signal.aborted) return;
            if (!response.ok || !Array.isArray(data)) throw new Error(data.message || "組套查詢失敗");
            project.suites = data.map((suite, index) => ({ ...suite, id: index }));
            project.suiteState = "ready";
        } catch (error) {
            if (error.name === "AbortError") return;
            project.suiteState = "error";
        }
        render();
    }

    async function loadAllSuites(items, signal) {
        let next = 0;
        await Promise.all(Array.from({ length: Math.min(4, items.length) }, async () => {
            while (next < items.length && !signal.aborted) {
                const project = items[next++];
                await loadSuites(project, signal);
            }
        }));
    }

    function loadOrders(project, signal) {
        if (project.ordersState === "ready") return Promise.resolve(true);
        if (project.orderPromise) return project.orderPromise;
        project.ordersState = "loading";
        render();
        project.orderPromise = (async () => {
            try {
                const params = new URLSearchParams({ chop1Date: project.chop1Date, chop1Time: project.chop1Time,
                    chop1Room: project.chop1Room, intop1No: project.intop1No });
                const response = await fetch(`${root.dataset.ordersUrl}?${params}`, { signal });
                const data = await response.json();
                if (signal?.aborted) return false;
                if (!response.ok || !Array.isArray(data)) throw new Error(data.message || "醫令查詢失敗");
                project.orders = data;
                project.ordersState = "ready";
                render();
                return true;
            } catch (error) {
                if (error.name === "AbortError") return false;
                project.ordersState = "error";
                render();
                return false;
            } finally {
                project.orderPromise = null;
            }
        })();
        return project.orderPromise;
    }

    async function loadAllOrders(items, signal) {
        let next = 0;
        const results = await Promise.all(Array.from({ length: Math.min(4, items.length) }, async () => {
            let success = true;
            while (next < items.length && !signal?.aborted) {
                success = await loadOrders(items[next++], signal) && success;
            }
            return success;
        }));
        return results.every(Boolean) && !signal?.aborted;
    }

    function amountCells(record, showZero = false, selectedKeys = keys) {
        return selectedKeys.map(key => {
            const value = record[key];
            const tone = value === 0 && !showZero ? "account-muted" : key === "total" ? "account-item-total" : key === "receivable" ? "account-receivable" : "";
            const chip = { discount: "account-discount-chip", booked: "account-booked-chip", arrears: "account-arrears-chip" }[key];
            const content = value === 0 ? (showZero ? "$0" : "—") : chip ? `<span class="${chip}">${value < 0 ? "−" : ""}$${money(Math.abs(value))}</span>` : `$${money(value)}`;
            return `<td class="amount ${tone} ${key === "total" ? "account-total-cell" : key === "receivable" ? "account-receivable-cell" : "account-deduction-cell"}">${content}</td>`;
        }).join("");
    }

    function projectCell(project, rowSpan) {
        const item = (label, value) => `<dt>${label}</dt><dd>${show(value)}</dd>`;
        const detailsExpanded = openProjectDetails.has(project.id);
        return `<td class="account-project-cell" rowspan="${rowSpan}">
            <div class="account-project-name">
                <span>姓名：${show(project.patientName)}</span>
                <span class="account-project-company">企業名稱：${show(project.customerName)}</span>
                <span class="account-project-company">專案名稱：${show(project.projectName)}</span>
            </div>
            <div class="account-project-meta">
                <div class="account-meta-toggle-row">
                    <dl class="account-meta-full">${item("預約時間", `${project.reserveDate || ""} ${project.reserveTime || ""}`)}</dl>
                    <button type="button" class="account-meta-toggle" data-project-detail="${escapeHtml(project.id)}" aria-expanded="${detailsExpanded}" aria-label="${detailsExpanded ? "收合" : "展開"} ${show(project.patientName)}的其他資訊"><span aria-hidden="true">${detailsExpanded ? "⌃" : "⌄"}</span>${detailsExpanded ? "收合資訊" : "展開資訊"}</button>
                </div>
                ${detailsExpanded ? `<dl class="account-meta-more">
                    <div class="account-meta-pair">
                        <div>${item("專案編號", project.projectNo)}</div>
                        <div>${item("條碼", project.barcode)}</div>
                    </div>
                    <div class="account-meta-pair">
                        <div>${item("狀態", project.status)}</div>
                        <div>${item("病歷號", project.patNo)}</div>
                    </div>
                </dl>` : ""}
            </div>
        </td>`;
    }

    function render() {
        const start = byId("account-start").value;
        const end = byId("account-end").value;
        filteredProjects = projects.filter(project => (!start || project.date >= start) && (!end || project.date <= end));
        const suites = filteredProjects.flatMap(project => project.suites);
        for (const key of keys) {
            const total = sum(suites, key);
            byId(`account-grid-${key}`).textContent = listLoading ? "—" : `${total < 0 ? "−" : ""}$${money(Math.abs(total))}`;
        }
        const incomplete = filteredProjects.some(project => project.suiteState !== "ready");
        byId("account-grand-total-label").textContent = incomplete ? "已載入組套小計" : "總計";
        byId("account-count").textContent = listLoading ? "正在查詢帳目…" : `${filteredProjects.length} 個專案 / ${suites.length} 個組套${incomplete ? "（部分組套尚未載入）" : ""}`;
        byId("account-export").disabled = exporting || suites.length === 0 || incomplete;
        const pageSize = Number(byId("account-page-size").value);
        const pageCount = Math.ceil(filteredProjects.length / pageSize);
        currentPage = pageCount ? Math.min(currentPage, pageCount) : 1;
        byId("account-page-status").textContent = listLoading ? "載入中…" : pageCount ? `第 ${currentPage} / ${pageCount} 頁` : "無資料";
        byId("account-prev-page").disabled = currentPage <= 1;
        byId("account-next-page").disabled = currentPage >= pageCount;
        const page = filteredProjects.slice((currentPage - 1) * pageSize, currentPage * pageSize);
        byId("account-rows").innerHTML = page.length ? page.map(project => {
            const projectGap = '<tr class="account-project-gap" aria-hidden="true"><td colspan="7"></td></tr>';
            const rowSpan = Math.max(1, project.suites.reduce((count, suite) => count + 1 + (openSuites.has(`${project.id}:${suite.id}`) ? Math.max(1, suiteOrders(project, suite).length) : 0), 0));
            const projectTotals = Object.fromEntries(keys.map(key => [key, sum(project.suites, key)]));
            const suiteRows = project.suites.map((suite, index) => {
                const suiteKey = `${project.id}:${suite.id}`;
                const expanded = openSuites.has(suiteKey);
                const suiteRow = `<tr class="account-suite-row${selectedSuite === suiteKey ? " is-selected" : ""}" data-account-row="${escapeHtml(suiteKey)}">${index === 0 ? projectCell(project, rowSpan) : ""}${amountCells(suite, false, ["total"])}
                    <td class="account-suite-name-cell"><button type="button" class="account-suite-button" data-suite="${escapeHtml(suiteKey)}" aria-expanded="${expanded}" aria-label="${expanded ? "收合" : "展開"}${show(suite.name)}醫令">
                    <span class="account-chevron" aria-hidden="true">${expanded ? "⌄" : "›"}</span><span>${show(suite.name)}</span></button>
                    </td>${amountCells(suite, false, ["receivable", "discount", "booked", "arrears"])}</tr>`;
                const orders = suiteOrders(project, suite);
                const orderRows = expanded ? orders.length
                    ? orders.map(order => `<tr class="account-order-row"><td class="amount">—</td><td><span class="account-order-name">${show(orderText(order))}</span></td><td class="amount">—</td><td class="amount">—</td><td class="amount">—</td><td class="amount">—</td></tr>`).join("")
                    : `<tr class="account-order-row"><td colspan="6" class="account-empty">${project.ordersState === "error" ? "醫令讀取失敗，請收合後重新展開。" : project.ordersState === "ready" ? "查無醫令。" : "醫令載入中…"}</td></tr>` : "";
                return suiteRow + orderRows;
            }).join("");
            if (!project.suites.length) {
                const message = { loading: "組套載入中…", missing: "缺少掛號資料，無法查詢組套。", error: "組套讀取失敗。", ready: "查無組套。" }[project.suiteState];
                return `<tr class="account-suite-row">${projectCell(project, 1)}<td colspan="6" class="account-empty">${message}</td></tr>${projectGap}`;
            }
            return `${suiteRows}<tr class="account-project-total-row"><th scope="row">總計 <span>${project.suites.length} 個組套</span></th>${amountCells(projectTotals, true, ["total"])}<td class="account-total-spacer"></td>${amountCells(projectTotals, true, ["receivable", "discount", "booked", "arrears"])}</tr>${projectGap}`;
        }).join("") : listLoading ? "" : '<tr><td colspan="7" class="account-empty">查無符合條件的專案。</td></tr>';
    }

    byId("account-rows").addEventListener("click", event => {
        const selectedRow = event.target.closest("[data-account-row]");
        if (selectedRow) {
            selectedSuite = selectedRow.dataset.accountRow;
            byId("account-rows").querySelectorAll("[data-account-row]").forEach(row => row.classList.toggle("is-selected", row === selectedRow));
        }
        const detailButton = event.target.closest("[data-project-detail]");
        if (detailButton) {
            const projectId = detailButton.dataset.projectDetail;
            if (openProjectDetails.has(projectId)) openProjectDetails.delete(projectId);
            else openProjectDetails.add(projectId);
            render();
            [...byId("account-rows").querySelectorAll("[data-project-detail]")].find(item => item.dataset.projectDetail === projectId)?.focus();
            return;
        }
        const button = event.target.closest("[data-suite]");
        if (!button) return;
        const suiteKey = button.dataset.suite;
        if (openSuites.has(suiteKey)) openSuites.delete(suiteKey);
        else {
            openSuites.add(suiteKey);
            const project = projects.find(item => item.suites.some(suite => `${item.id}:${suite.id}` === suiteKey));
            if (project && project.ordersState !== "ready") void loadOrders(project, requestController?.signal);
        }
        render();
        [...byId("account-rows").querySelectorAll("[data-suite]")].find(item => item.dataset.suite === suiteKey)?.focus();
    });

    byId("account-query-form").addEventListener("submit", async event => {
        event.preventDefault();
        const start = byId("account-start").value;
        const end = byId("account-end").value;
        const error = byId("account-query-error");
        if (!start || !end || start > end) {
            error.textContent = "請選擇有效的起始日期與結束日期。";
            error.hidden = false;
            return;
        }
        requestController?.abort();
        requestController = new AbortController();
        const currentRequest = requestController;
        const button = event.submitter;
        if (button) button.disabled = true;
        error.hidden = true;
        projects = [];
        listLoading = true;
        setLoading("正在查詢帳目資料…");
        render();
        try {
            const params = new URLSearchParams({ startDate: start, endDate: end, keyword: byId("account-keyword").value.trim() });
            const response = await fetch(`${root.dataset.listUrl}?${params}`, { signal: currentRequest.signal });
            const data = await response.json();
            if (!response.ok) throw new Error(data.message || "查詢失敗，請稍後再試。");
            if (!Array.isArray(data)) throw new Error("查詢結果格式不正確。");
            if (currentRequest.signal.aborted) return;
            projects = data.map(toProject);
            listLoading = false;
            currentPage = 1;
            openSuites = new Set();
            selectedSuite = null;
            openProjectDetails = new Set();
            setLoading(projects.length ? "正在載入組套資料…" : "");
            render();
            void loadAllSuites(projects, currentRequest.signal).then(
                () => { if (requestController === currentRequest) setLoading(""); },
                exception => {
                    if (requestController === currentRequest) setLoading("");
                    console.error(exception);
                });
        } catch (exception) {
            if (requestController === currentRequest) {
                listLoading = false;
                setLoading("");
                render();
            }
            if (exception.name !== "AbortError" && requestController === currentRequest) {
                error.textContent = exception.message || "查詢失敗，請稍後再試。";
                error.hidden = false;
            }
        } finally {
            if (button && requestController === currentRequest) button.disabled = false;
        }
    });

    byId("account-reset").addEventListener("click", () => {
        requestController?.abort();
        listLoading = false;
        setLoading("");
        byId("account-query-form").reset();
        byId("account-start").value = "";
        byId("account-end").value = "";
        byId("account-query-error").hidden = true;
        projects = [];
        openSuites = new Set();
        selectedSuite = null;
        openProjectDetails = new Set();
        currentPage = 1;
        render();
    });
    byId("account-page-size").addEventListener("change", () => { currentPage = 1; render(); });
    byId("account-prev-page").addEventListener("click", () => { if (currentPage > 1) { currentPage--; render(); } });
    byId("account-next-page").addEventListener("click", () => { if (currentPage * Number(byId("account-page-size").value) < filteredProjects.length) { currentPage++; render(); } });

    function excelColumn(index) {
        let label = "";
        for (index++; index; index = Math.floor((index - 1) / 26)) label = String.fromCharCode(65 + (index - 1) % 26) + label;
        return label;
    }
    async function createWorkbook() {
        const exportProjects = filteredProjects.filter(project => project.suites.length);
        if (!await loadAllOrders(exportProjects, requestController?.signal)) {
            throw new Error("醫令資料尚未載入完成。");
        }
        const headers = ["條碼", "專案編號", "專案名稱", "預約日期", "預約時間", "狀態", "病歷號", "姓名", "公司名稱", "組套名稱", "醫令名稱", "總金額", "應收金額", "優待金額", "記帳金額", "轉欠金額"];
        const rows = filteredProjects.flatMap(project => project.suites.map(suite => [
            project.barcode, project.projectNo, project.projectName, project.reserveDate, project.reserveTime, project.status,
            project.patNo, project.patientName, project.customerName, suite.name, suiteOrders(project, suite).map(orderText).join("、"),
            suite.total, suite.receivable, suite.discount, suite.booked, suite.arrears
        ]));
        const cell = (value, rowIndex, columnIndex) => {
            const ref = `${excelColumn(columnIndex)}${rowIndex + 1}`;
            if (rowIndex && columnIndex >= 11) return `<c r="${ref}" s="2"><v>${value}</v></c>`;
            return `<c r="${ref}"${rowIndex ? "" : ' s="1"'} t="inlineStr"><is><t>${escapeHtml(value)}</t></is></c>`;
        };
        const sheetRows = [headers, ...rows].map((row, index) => `<row r="${index + 1}">${row.map((value, column) => cell(value, index, column)).join("")}</row>`).join("");
        const sheet = `<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetViews><sheetView workbookViewId="0"><pane ySplit="1" topLeftCell="A2" activePane="bottomLeft" state="frozen"/></sheetView></sheetViews><sheetData>${sheetRows}</sheetData><autoFilter ref="A1:P${rows.length + 1}"/></worksheet>`;
        const styles = '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><fonts count="2"><font><sz val="11"/><name val="Calibri"/></font><font><b/><color rgb="FFFFFFFF"/><sz val="11"/><name val="Calibri"/></font></fonts><fills count="3"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill><fill><patternFill patternType="solid"><fgColor rgb="FF176AC8"/><bgColor indexed="64"/></patternFill></fill></fills><borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders><cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs><cellXfs count="3"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="0" fontId="1" fillId="2" borderId="0" xfId="0" applyFont="1" applyFill="1"/><xf numFmtId="3" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/></cellXfs><cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles></styleSheet>';
        const zip = new JSZip();
        zip.file("[Content_Types].xml", '<?xml version="1.0" encoding="UTF-8"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/></Types>');
        zip.file("_rels/.rels", '<?xml version="1.0" encoding="UTF-8"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>');
        zip.file("xl/workbook.xml", '<?xml version="1.0" encoding="UTF-8"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="組套帳目" sheetId="1" r:id="rId1"/></sheets></workbook>');
        zip.file("xl/_rels/workbook.xml.rels", '<?xml version="1.0" encoding="UTF-8"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>');
        zip.file("xl/worksheets/sheet1.xml", sheet);
        zip.file("xl/styles.xml", styles);
        return zip.generateAsync({ type: "blob", compression: "DEFLATE" });
    }
    byId("account-export").addEventListener("click", async () => {
        const button = byId("account-export");
        exporting = true;
        button.disabled = true;
        button.textContent = "匯出中…";
        try {
            const blob = await createWorkbook();
            const url = URL.createObjectURL(blob);
            const link = document.createElement("a");
            link.href = url;
            link.download = "帳目查詢_組套.xlsx";
            document.body.appendChild(link);
            link.click();
            link.remove();
            setTimeout(() => URL.revokeObjectURL(url), 60000);
        } catch (error) {
            console.error(error);
            window.alert("Excel 匯出失敗，請再試一次。");
        } finally {
            exporting = false;
            button.textContent = "匯出 Excel";
            button.disabled = filteredProjects.some(project => project.suiteState !== "ready") || filteredProjects.every(project => !project.suites.length);
        }
    });

    render();
    scheduleGridResize();
    byId("account-query-form").requestSubmit();
})();
