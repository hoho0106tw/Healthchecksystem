(function () {
    "use strict";

    // 播放器內是真實系統頁面，子頁不可再次啟動導覽。
    if (window.self !== window.top) return;

    const config = document.querySelector("[data-system-guide-config]");
    const restartButton = document.querySelector("[data-system-guide-restart]");
    const supportRestartButton = document.querySelector("[data-system-guide-support]");
    if (!config) return;

    const deviceGuidStorageKey = "healthsys:device-guid:v1";
    const firstLoginStoragePrefix = "healthsys:system-guide:first-login:v1:";
    let firstLoginStorageKey = "";

    const urls = {
        customer: config.dataset.customerUrl || "/CustomerMaintenance/CustomerMaintenance",
        project: config.dataset.projectUrl || "/ProjectPackageSettings/HealthCheckProjectManagement",
        appointment: config.dataset.appointmentUrl || "/Appointment/AppointmentManagement",
        checkin: config.dataset.checkinUrl || "/Appointment/CheckInOperation"
    };
    urls.projectCreate = urls.project.replace(/HealthCheckProjectManagement(?:\?.*)?$/i, "CreateHealthCheckProject");
    urls.appointmentCreate = urls.appointment.replace(/AppointmentManagement(?:\?.*)?$/i, "CreateAppointment");
    urls.projectOrders = urls.projectCreate.replace(/CreateHealthCheckProject$/i, "CreateHealthCheckProjectOrders");
    urls.projectSpecials = urls.projectCreate.replace(/CreateHealthCheckProject$/i, "CreateHealthCheckProjectSpecialOperations");
    urls.projectConfirm = urls.projectCreate.replace(/CreateHealthCheckProject$/i, "ConfirmHealthCheckProject");
    urls.projectComplete = urls.projectCreate.replace(/CreateHealthCheckProject$/i, "CompleteHealthCheckProject");

    const currentPageUrl = new URL(window.location.href);
    currentPageUrl.searchParams.delete("systemGuideDemo");
    urls.current = currentPageUrl.pathname + currentPageUrl.search + currentPageUrl.hash;

    const scenes = [
        {
            url: urls.current, title: "日後不懂時，從聯絡客服重播教學",
            description: "先開啟左下角「聯絡客服」，再按「教學」。首次提示不再出現後，仍可隨時從這裡重新觀看。",
            prepare: "support",
            targets: [
                { selector: "[data-smart-assist-open]", label: "點選聯絡客服", click: true },
                { selector: "[data-system-guide-support]", fallback: ".smart-assist-window", label: "點選教學即可重新播放" }
            ]
        },
        {
            url: urls.customer, title: "建立企業",
            description: "進入「公司維護」，按下新增公司，填妥企業基本資料後儲存。",
            targets: [
                { selector: "[data-open-customer-create-frame]", label: "按下新增公司", click: true },
                { selector: "[data-customer-create-frame-modal]:not([hidden])", label: "填寫企業資料並儲存" }
            ]
        },
        {
            url: urls.project, title: "建立專案",
            description: "企業建立完成後，前往「專案維護」並建立新的健檢專案。",
            targets: [{ selector: ".health-project-inline-create-button", label: "按下建立專案" }]
        },
        {
            url: urls.projectCreate, title: "專案步驟 1：基本資料",
            description: "填寫專案名稱、企業、體檢年度與日期，再設定流程及預設執行區域。",
            targets: [
                { selector: "[data-project-field='projectName']", label: "輸入專案名稱" },
                { selector: "[data-open-customer-lookup]", label: "選擇剛建立的企業" },
                { selector: "[data-project-field='checkStartDate']", label: "設定體檢日期" },
                { selector: "[data-project-next-link]", label: "下一步：設定醫令" }
            ]
        },
        {
            url: urls.projectOrders, title: "專案步驟 2：匯入院內組套醫令",
            description: "按下匯入院內組套，查詢醫院既有組套，選擇後匯入專案並確認價格。",
            targets: [
                { selector: "[data-suite-import-internal]", label: "按下匯入院內組套", click: true },
                { selector: "[data-suite-internal-import-modal]:not([hidden])", label: "開啟醫院組套查詢視窗" },
                { selector: "[data-suite-internal-filter='name']", fallback: "[data-suite-internal-import-modal]", label: "輸入醫院組套名稱" },
                { selector: "[data-suite-internal-search]", fallback: "[data-suite-internal-import-modal]", label: "查詢醫院組套", click: true },
                { selector: "[data-suite-internal-body]", fallback: "[data-suite-internal-import-modal]", label: "勾選要匯入的醫院組套" },
                { selector: "[data-suite-internal-confirm]", fallback: "[data-suite-internal-import-modal]", label: "確認匯入（僅示範）" },
                { selector: "[data-suite-selected-body]", fallback: "[data-suite-page]", label: "確認已匯入的組套" },
                { selector: "[data-suite-next-link]", label: "下一步：問卷項目" }
            ]
        },
        {
            url: urls.projectSpecials, title: "專案步驟 3：問卷與特殊項目",
            description: "選擇專案需要的問卷或特殊作業，並調整項目的顯示順序。",
            targets: [
                { selector: "[data-special-available-body]", fallback: "[data-special-page]", label: "選擇問卷項目" },
                { selector: "[data-special-add]", label: "加入所選項目" },
                { selector: "[data-special-selected-body]", label: "確認已選項目" },
                { selector: "[data-special-carry-link].primary-button", label: "下一步：確認發布" }
            ]
        },
        {
            url: urls.projectConfirm, title: "專案步驟 4：確認發布",
            description: "逐項確認基本資料、組套醫令、問卷與通知設定，再完成發布。",
            targets: [
                { selector: ".project-confirm-basic", fallback: "[data-project-confirm-page]", label: "確認專案基本資料" },
                { selector: ".project-confirm-orders", label: "確認醫令與特殊項目" },
                { selector: "[data-project-confirm-page] .project-wizard-footer .primary-button", label: "完成建立並發布" }
            ]
        },
        {
            url: urls.projectComplete, title: "專案步驟 5：建立完成",
            description: "看到專案建立成功後，接著才能使用這個專案建立預約。",
            targets: [{ selector: ".project-complete-card", fallback: ".project-wizard-page", label: "專案建立成功" }]
        },
        {
            url: urls.appointmentCreate, title: "預約步驟 1：查詢醫院資料",
            description: "先按下查詢院內資料，從醫院病人資料中找到受檢者並帶入基本資訊。",
            targets: [
                { selector: "[data-open-patient-lookup]", label: "按下查詢院內資料", click: true },
                { selector: "[data-patient-lookup-modal]:not([hidden])", label: "醫院受檢者查詢視窗" },
                { selector: "[data-patient-lookup-frame-host]", fallback: "[data-patient-lookup-modal]", label: "輸入條件並選擇醫院資料" }
            ]
        },
        {
            url: urls.appointmentCreate, title: "預約步驟 2：確認帶入資料",
            description: "醫院資料帶入後，確認姓名、身分證字號、生日及聯絡手機是否正確。",
            targets: [
                { selector: "[data-patient-field='name']", label: "確認受檢者姓名" },
                { selector: "[data-patient-field='idNo']", label: "確認身分證字號" },
                { selector: "[data-patient-field='birthDate']", label: "確認出生日期" },
                { selector: "[data-patient-field='mobile']", label: "確認聯絡手機" }
            ]
        },
        {
            url: urls.appointmentCreate, title: "預約步驟 3：預約資訊",
            description: "切換到專案／預約頁籤，設定預約區域、日期、時間與看診醫師。",
            targets: [
                { selector: "[data-appointment-tab='project']", label: "開啟專案／預約資訊", click: true },
                { selector: "select[name='ReservationArea']", label: "選擇預約區域" },
                { selector: "input[name='ReservationDate']", label: "選擇預約日期" },
                { selector: "input[name='ReservationTime']", label: "設定預約時間" }
            ]
        },
        {
            url: urls.appointmentCreate, title: "預約步驟 4：選擇專案與套組",
            description: "選擇剛建立的健檢專案，再確認套餐、加做項目及問卷。",
            targets: [
                { selector: "[data-appointment-tab='project']", label: "開啟專案／預約資訊", click: true },
                { selector: "[data-project-select]", label: "選擇健檢專案" },
                { selector: "[data-package-options-body]", fallback: ".package-data-grid-shell", label: "選擇套餐／套組" },
                { selector: "[data-questionnaire-search-input]", fallback: "#questionnaire-title", label: "確認問卷項目" }
            ]
        },
        {
            url: urls.appointmentCreate, title: "預約步驟 5：費用與建立",
            description: "確認繳款日期、方式與備註，最後按下建立預約。動畫不會真的送出。",
            targets: [
                { selector: "[data-appointment-tab='project']", label: "開啟費用資訊", click: true },
                { selector: "[data-billing-payment-method]", fallback: "#billing-title", label: "確認繳款方式" },
                { selector: "[data-create-appointment-submit][data-reserve-status='1']", label: "建立預約（僅示範）" }
            ]
        },
        {
            url: urls.appointment, title: "預約完成：狀態為已預約",
            description: "建立預約完成後，清單狀態應顯示「已預約」，不會自動變成「已掛號」。",
            prepare: "reserved",
            targets: [
                { selector: "[data-appointment-row][data-reserve-status='1']", fallback: ".appointment-table-panel", label: "確認預約狀態為已預約" }
            ]
        },
        {
            url: urls.appointment, title: "另行執行掛號",
            description: "需要掛號時，再從預約清單選取一筆「已預約」資料並按下批次掛號。",
            prepare: "reserved",
            targets: [
                { selector: "[data-appointment-row][data-reserve-status='1']", fallback: ".appointment-table-panel", label: "選取已預約資料", click: true },
                { selector: "[data-batch-register]", label: "執行掛號（動畫模擬）", simulateRegister: true }
            ]
        },
        {
            url: urls.appointment, title: "插曲：取消掛號後再掛號",
            description: "已掛號資料可先取消掛號，狀態會恢復為「已預約」；選取後可再次掛號，再進行報到。",
            prepare: "registered",
            targets: [
                { selector: "[data-appointment-row][data-reserve-status='4'] .appointment-action-toggle", fallback: ".appointment-action-toggle", label: "開啟已掛號資料選單", click: true },
                { selector: "[data-cancel-registration]:not([hidden])", fallback: ".appointment-table-panel", label: "選擇取消掛號", click: true },
                { selector: "[data-appointment-confirm-submit]", fallback: ".appointment-table-panel", label: "確認取消（動畫模擬）", simulateCancel: true },
                { selector: "[data-appointment-row][data-reserve-status='1'] [data-appointment-status-cell]", fallback: ".appointment-table-panel", label: "狀態恢復為已預約" },
                { selector: "[data-appointment-row][data-reserve-status='1']", fallback: ".appointment-table-panel", label: "重新選取預約", click: true },
                { selector: "[data-batch-register]", label: "再次掛號（動畫模擬）", simulateRegister: true }
            ]
        },
        {
            url: urls.checkin, title: "執行報到",
            description: "掛號完成後進入報到作業，找到受檢者並確認報到。",
            prepare: "checkin",
            targets: [
                { selector: "[data-check-in-row][data-reserve-status='4'] [data-check-in-button]", fallback: ".appointment-table-panel", label: "按下報到", click: true },
                { selector: "[data-check-in-modal-confirm]", fallback: ".appointment-table-panel", label: "確認報到資料" }
            ]
        },
        {
            url: urls.appointment, title: "補下醫令",
            description: "報到完成後開啟補下醫令視窗，確認原組套、選擇新增組套與醫令，最後按新增。",
            prepare: "checkedin",
            targets: [
                { selector: "[data-appointment-row][data-reserve-status='5'] .appointment-action-toggle", fallback: ".appointment-action-toggle", label: "開啟操作選單", click: true },
                { selector: "[data-supplement-order]:not([hidden])", fallback: ".appointment-table-panel", label: "選擇補下醫令", click: true },
                { selector: "[data-supplement-order-modal]:not([hidden])", fallback: ".appointment-table-panel", label: "補下醫令視窗" },
                { selector: "[data-supplement-package-body]", fallback: "[data-supplement-order-modal]", label: "查看原本的套餐／組套" },
                { selector: "[data-supplement-package-picker-body]", fallback: "[data-supplement-order-modal]", label: "選擇要新增的組套" },
                { selector: "[data-supplement-new-order-body]", fallback: "[data-supplement-order-modal]", label: "確認準備新增的醫令" },
                { selector: "[data-supplement-order-submit]", fallback: "[data-supplement-order-modal]", label: "新增醫令（僅示範）" }
            ]
        }
    ];

    let player = null;
    let iframe = null;
    let cursor = null;
    let sceneIndex = 0;
    let targetIndex = 0;
    let timer = 0;
    let paused = false;
    let runToken = 0;

    function createDeviceGuid() {
        if (window.crypto?.randomUUID) return window.crypto.randomUUID();
        return "xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx".replace(/[xy]/g, function (character) {
            const random = Math.random() * 16 | 0;
            const value = character === "x" ? random : (random & 3 | 8);
            return value.toString(16);
        });
    }

    function getDeviceGuid() {
        const suppliedGuid = String(config.dataset.deviceGuid || "").trim();
        if (suppliedGuid) return suppliedGuid;

        try {
            const savedGuid = localStorage.getItem(deviceGuidStorageKey);
            if (savedGuid) return savedGuid;
            const generatedGuid = createDeviceGuid();
            localStorage.setItem(deviceGuidStorageKey, generatedGuid);
            return generatedGuid;
        } catch (_) {
            return [navigator.platform || "unknown", screen.width, screen.height].join("-");
        }
    }

    async function getDeviceModel() {
        const suppliedModel = String(config.dataset.deviceModel || "").trim();
        if (suppliedModel) return suppliedModel;

        try {
            if (navigator.userAgentData?.getHighEntropyValues) {
                const values = await navigator.userAgentData.getHighEntropyValues(["model", "platform"]);
                return String(values.model || values.platform || navigator.platform || "unknown").trim();
            }
        } catch (_) { }

        return String(navigator.platform || "unknown").trim();
    }

    function stableHash(value) {
        let hash = 2166136261;
        for (let index = 0; index < value.length; index += 1) {
            hash ^= value.charCodeAt(index);
            hash = Math.imul(hash, 16777619);
        }
        return (hash >>> 0).toString(36);
    }

    async function initializeFirstLoginKey() {
        const loginId = String(config.dataset.loginId || "anonymous").trim().toLowerCase() || "anonymous";
        const deviceGuid = getDeviceGuid();
        const deviceModel = await getDeviceModel();
        firstLoginStorageKey = firstLoginStoragePrefix + stableHash([loginId, deviceGuid, deviceModel].join("|"));
    }

    function hasAnsweredFirstLoginQuestion() {
        if (!firstLoginStorageKey) return false;
        try { return localStorage.getItem(firstLoginStorageKey) === "answered"; } catch (_) { return false; }
    }

    function rememberFirstLoginAnswer() {
        if (!firstLoginStorageKey) return;
        try { localStorage.setItem(firstLoginStorageKey, "answered"); } catch (_) { }
    }

    function removeQuestion() {
        document.querySelectorAll("[data-system-guide-dialog], [data-system-guide-overlay]").forEach(function (element) { element.remove(); });
    }

    function showQuestion() {
        closePlayer();
        removeQuestion();
        const overlay = document.createElement("div");
        overlay.className = "system-guide-overlay";
        overlay.dataset.systemGuideOverlay = "";
        const dialog = document.createElement("section");
        dialog.className = "system-guide-dialog";
        dialog.dataset.systemGuideDialog = "";
        dialog.setAttribute("role", "dialog");
        dialog.setAttribute("aria-modal", "true");
        dialog.setAttribute("aria-labelledby", "system-guide-question-title");
        dialog.innerHTML = [
            '<span class="system-guide-dialog-badge">QUICK TOUR</span>',
            '<h2 id="system-guide-question-title">第一次登入，是否觀看系統操作導覽？</h2>',
            '<p>這是一段不會寫入資料的自動示範。日後若不熟悉操作，可從「聯絡客服」按下「教學」重新觀看。</p>',
            '<div class="system-guide-actions">',
            '  <button type="button" data-guide-decline>略過教學</button>',
            '  <button class="primary" type="button" data-guide-accept>觀看導覽</button>',
            '</div>'
        ].join("");
        document.body.append(overlay, dialog);
        dialog.querySelector("[data-guide-decline]").addEventListener("click", function () {
            rememberFirstLoginAnswer();
            removeQuestion();
        });
        dialog.querySelector("[data-guide-accept]").addEventListener("click", function () {
            rememberFirstLoginAnswer();
            removeQuestion(); startPlayer();
        });
        dialog.querySelector("[data-guide-accept]").focus();
    }

    function startPlayer() {
        closePlayer();
        sceneIndex = 0; targetIndex = 0; paused = false; runToken += 1;
        player = document.createElement("section");
        player.className = "system-guide-player";
        player.dataset.systemGuidePlayer = "";
        player.setAttribute("role", "dialog");
        player.setAttribute("aria-modal", "true");
        player.setAttribute("aria-label", "系統操作自動導覽");
        player.innerHTML = [
            '<header class="system-guide-player-header">',
            ' <div><span class="system-guide-dialog-badge">SYSTEM TOUR</span><strong data-guide-player-title>系統快速導覽</strong></div>',
            ' <div class="system-guide-player-controls"><button type="button" data-guide-toggle>暫停</button><button class="system-guide-cancel-button" type="button" data-guide-cancel>取消教學，返回系統</button><button type="button" data-guide-close aria-label="關閉教學並返回系統" title="關閉教學並返回系統">×</button></div>',
            '</header>',
            '<div class="system-guide-player-stage">',
            ' <iframe title="系統實際操作畫面" data-guide-frame></iframe>',
            ' <div class="system-guide-demo-cursor" data-guide-cursor><span></span></div>',
            ' <div class="system-guide-loading" data-guide-loading><span></span>正在開啟系統畫面…</div>',
            '</div>',
            '<footer class="system-guide-player-footer">',
            ' <div class="system-guide-scene-copy"><strong data-guide-scene-title></strong><span data-guide-scene-description></span></div>',
            ' <div class="system-guide-player-progress"><span data-guide-progress-bar></span></div><b data-guide-step-count></b>',
            '</footer>'
        ].join("");
        document.body.appendChild(player);
        document.documentElement.classList.add("system-guide-demo-open");
        iframe = player.querySelector("[data-guide-frame]");
        cursor = player.querySelector("[data-guide-cursor]");
        player.querySelector("[data-guide-cancel]").addEventListener("click", closePlayer);
        player.querySelector("[data-guide-close]").addEventListener("click", closePlayer);
        player.querySelector("[data-guide-toggle]").addEventListener("click", togglePause);
        playScene(runToken);
    }

    function closePlayer() {
        window.clearTimeout(timer); runToken += 1; player?.remove();
        player = null; iframe = null; cursor = null;
        document.documentElement.classList.remove("system-guide-demo-open");
    }

    function togglePause() {
        if (!player) return;
        paused = !paused;
        player.querySelector("[data-guide-toggle]").textContent = paused ? "繼續" : "暫停";
        if (!paused) playTarget(runToken);
    }

    function updatePlayerCopy(scene) {
        player.querySelector("[data-guide-scene-title]").textContent = scene.title;
        player.querySelector("[data-guide-scene-description]").textContent = scene.description;
        player.querySelector("[data-guide-step-count]").textContent = (sceneIndex + 1) + " / " + scenes.length;
        player.querySelector("[data-guide-progress-bar]").style.width = ((sceneIndex + 1) / scenes.length * 100) + "%";
    }

    function playScene(token) {
        if (!player || token !== runToken || paused) return;
        if (sceneIndex >= scenes.length) { showFinished(); return; }
        const scene = scenes[sceneIndex];
        targetIndex = 0; updatePlayerCopy(scene);
        cursor.classList.remove("is-visible", "is-clicking");
        player.querySelector("[data-guide-loading]").hidden = false;
        iframe.onload = function () {
            if (!player || token !== runToken) return;
            player.querySelector("[data-guide-loading]").hidden = true;
            prepareScene(scene);
            timer = window.setTimeout(function () { playTarget(token); }, 700);
        };
        iframe.src = appendDemoQuery(scene.url);
    }

    function appendDemoQuery(url) {
        const nextUrl = new URL(url, window.location.origin);
        nextUrl.searchParams.set("systemGuideDemo", "1");
        return nextUrl.toString();
    }

    function frameDocument() {
        try { return iframe?.contentDocument || null; } catch (_) { return null; }
    }

    function setAppointmentDemoStatus(row, reserveStatus, statusText) {
        if (!row) return;
        row.dataset.reserveStatus = reserveStatus;
        row.dataset.status = statusText;
        const status = row.querySelector("[data-appointment-status-cell]");
        if (status) {
            status.textContent = statusText;
            status.className = "appointment-status " + (reserveStatus === "1" ? "confirmed" : reserveStatus === "4" ? "registered" : "arrived");
        }
        const hasRegistration = reserveStatus === "4" || reserveStatus === "5";
        row.querySelectorAll("[data-standard-appointment-action]").forEach(function (action) { action.hidden = hasRegistration; });
        const cancel = row.querySelector("[data-cancel-registration]");
        const supplement = row.querySelector("[data-supplement-order]");
        const readOnly = row.querySelector("[data-readonly-appointment-link]");
        if (cancel) cancel.hidden = reserveStatus !== "4";
        if (supplement) supplement.hidden = !hasRegistration;
        if (readOnly) readOnly.hidden = !hasRegistration;
    }

    function prepareScene(scene) {
        const doc = frameDocument();
        if (!doc || !scene.prepare) return;
        if (scene.prepare === "support") {
            const widget = doc.querySelector("[data-smart-assist]");
            const launcher = widget?.querySelector("[data-smart-assist-open]");
            const panel = widget?.querySelector(".smart-assist-window");
            widget?.classList.remove("is-open", "is-hidden");
            widget?.classList.add("is-minimized");
            launcher?.setAttribute("aria-expanded", "false");
            panel?.setAttribute("aria-hidden", "true");
        }
        if (scene.prepare === "reserved") {
            const row = doc.querySelector("[data-appointment-row][data-reserve-status='1']") || doc.querySelector("[data-appointment-row]");
            setAppointmentDemoStatus(row, "1", "已預約");
        }
        if (scene.prepare === "registered" || scene.prepare === "checkedin") {
            const row = doc.querySelector("[data-appointment-row]");
            setAppointmentDemoStatus(row, scene.prepare === "registered" ? "4" : "5", scene.prepare === "registered" ? "已掛號" : "已報到");
        }
        if (scene.prepare === "checkin") {
            const row = doc.querySelector("[data-check-in-row]");
            if (!row) return;
            row.dataset.reserveStatus = "4";
            const status = row.querySelector("[data-check-in-status]");
            const button = row.querySelector("[data-check-in-button]");
            if (status) { status.textContent = "已掛號"; status.className = "appointment-status registered"; }
            if (button) {
                button.disabled = false;
                button.classList.remove("is-cancel-check-in");
                const label = button.querySelector("[data-check-in-button-label]");
                if (label) label.textContent = "已報到";
            }
        }
    }

    function simulateCancelRegistration() {
        const doc = frameDocument();
        if (!doc) return;
        const modal = doc.querySelector("[data-appointment-confirm-modal]");
        if (modal) modal.hidden = true;
        const row = doc.querySelector("[data-appointment-row][data-reserve-status='4']") || doc.querySelector("[data-appointment-row]");
        setAppointmentDemoStatus(row, "1", "已預約");
        if (row) {
            row.classList.remove("is-selected");
            row.setAttribute("aria-selected", "false");
        }
    }

    function simulateRegisterAgain() {
        const doc = frameDocument();
        if (!doc) return;
        const row = doc.querySelector("[data-appointment-row][data-reserve-status='1']") || doc.querySelector("[data-appointment-row]");
        setAppointmentDemoStatus(row, "4", "已掛號");
        const selectedCount = doc.querySelector("[data-batch-selected-count]");
        if (selectedCount) selectedCount.textContent = "掛號完成，準備前往報到";
    }

    function clearHighlight() {
        frameDocument()?.querySelectorAll(".system-guide-demo-highlight").forEach(function (element) { element.classList.remove("system-guide-demo-highlight"); });
    }

    function findTarget(definition) {
        const doc = frameDocument();
        if (!doc) return null;
        const primary = Array.from(doc.querySelectorAll(definition.selector)).find(function (element) {
            return !element.hidden && element.getClientRects().length;
        });
        return primary || (definition.fallback ? doc.querySelector(definition.fallback) : null);
    }

    function moveCursorTo(element, label) {
        clearHighlight(); element.classList.add("system-guide-demo-highlight");
        element.scrollIntoView({ behavior: "smooth", block: "center", inline: "center" });
        window.setTimeout(function () {
            if (!player || !element.isConnected) return;
            const frameRect = iframe.getBoundingClientRect();
            const rect = element.getBoundingClientRect();
            const x = frameRect.left + Math.min(rect.right - 12, rect.left + rect.width * .68);
            const y = frameRect.top + Math.min(rect.bottom - 10, rect.top + rect.height * .62);
            cursor.style.transform = "translate3d(" + Math.round(x) + "px," + Math.round(y) + "px,0)";
            cursor.dataset.label = label || "";
            cursor.classList.add("is-visible");
        }, 380);
    }

    function playTarget(token) {
        if (!player || token !== runToken || paused) return;
        const scene = scenes[sceneIndex];
        if (targetIndex >= scene.targets.length) {
            clearHighlight(); sceneIndex += 1;
            timer = window.setTimeout(function () { playScene(token); }, 650); return;
        }
        const definition = scene.targets[targetIndex];
        const element = findTarget(definition);
        if (!element) { targetIndex += 1; playTarget(token); return; }
        moveCursorTo(element, definition.label);
        timer = window.setTimeout(function () {
            if (!player || token !== runToken || paused) return;
            cursor.classList.add("is-clicking");
            if (definition.simulateCancel) simulateCancelRegistration();
            else if (definition.simulateRegister) simulateRegisterAgain();
            else if (definition.click && !element.disabled) element.click();
            timer = window.setTimeout(function () {
                cursor.classList.remove("is-clicking"); targetIndex += 1; playTarget(token);
            }, (definition.click || definition.simulateCancel || definition.simulateRegister) ? 1200 : 900);
        }, 1450);
    }

    function showFinished() {
        if (!player) return;
        clearHighlight(); cursor.classList.remove("is-visible", "is-clicking");
        const finished = document.createElement("div");
        finished.className = "system-guide-finished";
        finished.innerHTML = [
            '<span class="system-guide-finished-icon">✓</span><h2>導覽播放完成</h2>',
            '<p>以上僅為畫面示範，沒有新增、修改或送出任何系統資料。</p>',
            '<div class="system-guide-actions"><button type="button" data-guide-finish-close>返回系統操作</button><button class="primary" type="button" data-guide-replay>重新播放</button></div>'
        ].join("");
        player.querySelector(".system-guide-player-stage").appendChild(finished);
        finished.querySelector("[data-guide-finish-close]").addEventListener("click", closePlayer);
        finished.querySelector("[data-guide-replay]").addEventListener("click", startPlayer);
    }

    restartButton?.addEventListener("click", startPlayer);
    supportRestartButton?.addEventListener("click", startPlayer);

    // 同一位登入者在同一裝置上只詢問一次；日後可由「聯絡客服 > 教學」重播。
    initializeFirstLoginKey().then(function () {
        if (!hasAnsweredFirstLoginQuestion()) showQuestion();
    }).catch(showQuestion);
})();
