(function () {
    const lookupModal = document.querySelector("[data-patient-lookup-modal]");
    const lookupHost = document.querySelector("[data-patient-lookup-frame-host]");
    const openLookupButtons = Array.from(document.querySelectorAll("[data-open-patient-lookup]"));
    const closeLookupButtons = Array.from(document.querySelectorAll("[data-close-patient-lookup]"));
    const projectLookupModal = document.querySelector("[data-project-lookup-modal]");
    const openProjectLookupButton = document.querySelector("[data-open-project-lookup]");
    const closeProjectLookupButtons = Array.from(document.querySelectorAll("[data-close-project-lookup]"));
    const projectSelect = document.querySelector("[data-project-select]");
    const createAppointmentForm = document.querySelector("[data-create-appointment-form]");
    const isReadOnlyMode = createAppointmentForm?.dataset.readOnlyMode === "true";
    const createAppointmentSubmitButtons = Array.from(document.querySelectorAll("[data-create-appointment-submit]"));
    const createAppointmentRegisterButton = document.querySelector("[data-create-appointment-register]");
    const consultingDoctorSelect = document.querySelector("[data-consulting-doctor]");
    const consultingDoctorNameInput = document.querySelector("[data-consulting-doctor-name]");
    const isConfirmedEditMode = createAppointmentForm
        ? createAppointmentForm.dataset.editMode === "true"
            && createAppointmentForm.dataset.currentReserveStatus === "1"
        : false;
    const createAppointmentRegisterProgress = document.querySelector("[data-create-appointment-register-progress]");
    const createAppointmentResult = document.querySelector("[data-create-appointment-result]");
    const createAppointmentResultTitle = document.querySelector("[data-create-appointment-result-title]");
    const createAppointmentResultMessage = document.querySelector("[data-create-appointment-result-message]");
    const createAppointmentResultClose = document.querySelector("[data-create-appointment-result-close]");
    const appointmentPage = document.querySelector(".appointment-create-page");
    const appointmentTabs = Array.from(document.querySelectorAll("[data-appointment-tab]"));
    const appointmentTabPanels = Array.from(document.querySelectorAll("[data-appointment-tab-panel]"));
    const packageUrl = appointmentPage ? appointmentPage.dataset.packageUrl : "";
    const projectLookupUrl = appointmentPage ? appointmentPage.dataset.projectLookupUrl : "";
    const healthCodeUrl = appointmentPage ? appointmentPage.dataset.healthCodeUrl : "";
    const otherSuiteLookupUrl = appointmentPage ? appointmentPage.dataset.otherSuiteLookupUrl : "";
    const packageTable = document.querySelector("[data-grid='package-options']");
    const packageBody = document.querySelector("[data-package-options-body]");
    const packageSelectAllCheckbox = document.querySelector("[data-package-select-all]");
    const packagePageInfo = document.querySelector("[data-package-page-info]");
    const packagePagination = document.querySelector("[data-package-pagination]");
    const packagePageSizeSelect = document.querySelector("[data-package-page-size]");
    const packageSearchInput = document.querySelector("[data-package-search-input]");
    const packageCategoryTabs = document.querySelector("[data-package-category-tabs]");
    const tallPackageCategoryQuery = window.matchMedia("(min-height: 901px)");
    let packageCategoryLayoutFrame = null;
    const initialPackageDataElement = document.querySelector("[data-initial-packages]");
    const otherSuiteAddButton = document.querySelector("[data-add-other-suite]");
    const otherSuiteBody = document.querySelector("[data-other-suite-body]");
    const otherSuitePageInfo = document.querySelector("[data-other-suite-page-info]");
    const otherSuitePagination = document.querySelector("[data-other-suite-pagination]");
    const otherSuitePageSizeSelect = document.querySelector("[data-other-suite-page-size]");
    const otherSuiteLookupModal = document.querySelector("[data-other-suite-lookup-modal]");
    const otherSuiteLookupHost = document.querySelector("[data-other-suite-lookup-frame-host]");
    const closeOtherSuiteLookupButtons = Array.from(document.querySelectorAll("[data-close-other-suite-lookup]"));
    const initialOtherSuiteDataElement = document.querySelector("[data-initial-other-suites]");
    const questionnaireSection = document.querySelector("[data-questionnaire-section]");
    const questionnaireDataElement = document.querySelector("[data-questionnaire-options]");
    const questionnaireBody = document.querySelector("[data-questionnaire-body]");
    const questionnairePageInfo = document.querySelector("[data-questionnaire-page-info]");
    const questionnairePagination = document.querySelector("[data-questionnaire-pagination]");
    const questionnairePageSizeSelect = document.querySelector("[data-questionnaire-page-size]");
    const questionnaireSearchInput = document.querySelector("[data-questionnaire-search-input]");
    const questionnaireCategoryTabs = document.querySelector("[data-questionnaire-category-tabs]");
    const billingValueElements = {
        package: document.querySelector("[data-billing-value='package']"),
        addon: document.querySelector("[data-billing-value='addon']"),
        actual: document.querySelector("[data-billing-value='actual']")
    };
    const billingAdjustmentInputs = Array.from(document.querySelectorAll("[data-billing-adjustment]"));
    const billingPaymentDateInput = document.querySelector("[data-billing-payment-date]");
    const billingPaymentMethodSelect = document.querySelector("[data-billing-payment-method]");
    const billingNoteInput = document.querySelector("[data-billing-note-input]");
    const projectFilterInputs = Array.from(document.querySelectorAll("[data-project-filter]"));
    const projectDateFilterInputs = Array.from(document.querySelectorAll("[data-project-date-filter]"));
    const projectSearchButton = document.querySelector("[data-project-search]");
    let projectRows = Array.from(document.querySelectorAll("[data-project-row]"));
    const projectEmptyRow = document.querySelector("[data-project-empty]");
    const projectResultsBody = document.querySelector("[data-project-results-body]");
    const projectCustomerNoInput = document.querySelector("[data-project-customer-no]");
    const selfPayAccountingModal = document.querySelector("[data-self-pay-accounting-modal]");
    const closeSelfPayAccountingButtons = Array.from(document.querySelectorAll("[data-close-self-pay-accounting-lookup]"));
    const selfPayAccountingFilters = Array.from(document.querySelectorAll("[data-self-pay-accounting-filter]"));
    const selfPayAccountingRows = Array.from(document.querySelectorAll("[data-self-pay-accounting-row]"));
    const selfPayAccountingEmpty = document.querySelector("[data-self-pay-accounting-empty]");
    const selfPayAccountingTabs = document.querySelector("[data-self-pay-accounting-tabs]");
    const clearSelfPayAccountingFiltersButton = document.querySelector("[data-clear-self-pay-accounting-filters]");
    const packageAccountingAllValue = document.querySelector("[data-package-accounting-all-value]");
    const packageAccountingAllQueryButton = document.querySelector("[data-package-accounting-all-query]");
    const packageAccountingAllClearButton = document.querySelector("[data-package-accounting-all-clear]");
    const packageAccountingAllReminder = document.querySelector("[data-package-accounting-all-reminder]");
    const patientPanel = document.querySelector(".examinee-card-panel");
    const healthCodeInput = document.querySelector("[data-health-code-input]");
    const healthCodeShell = healthCodeInput ? healthCodeInput.closest(".field-control-shell") : null;
    const defaultHealthCode = healthCodeInput ? healthCodeInput.value : "";
    const mailingAddressInput = document.querySelector("[data-mailing-address]");
    const addressInput = document.querySelector("[data-address-field]");
    const sameAddressCheckbox = document.querySelector("[data-same-address]");
    const employeeNoInput = document.querySelector("[data-employee-no-input]");
    let previousFocus = null;
    let previousSelfPayAccountingFocus = null;
    let activeSelfPayAccountingPrefix = "";
    let activePackageAccountingId = "";
    let activeOtherSuiteAccountingId = "";
    let packageOptions = [];
    let expandedPackageIds = new Set();
    let otherSuiteOptions = [];
    let questionnaireOptions = [];
    let initialPackageOptions = null;
    let initialQuestionnaireOptions = null;
    let shouldApplyInitialPackageSelections = true;
    let shouldApplyInitialQuestionnaireSelections = true;
    let currentPackagePage = 1;
    let currentOtherSuitePage = 1;
    let currentQuestionnairePage = 1;
    let packageEmptyMessage = "請先選擇健檢專案 / 方案";
    let packageLoadController = null;
    let packageLoadPromise = Promise.resolve();
    let packageLoadError = "";
    let projectLookupController = null;
    let healthCodeLoadController = null;
    let healthCodeLoadPromise = null;
    let activeAppointmentTab = "patient";
    const narrowAppointmentTabsQuery = window.matchMedia("(max-width: 1440px)");
    let shouldPreserveInitialBilling = Object.values(billingValueElements).some(function (element) {
        return element && parseCurrency(element.dataset.initialBillingValue || element.textContent) > 0;
    });
    let shouldUseInitialPackageAmount = shouldPreserveInitialBilling;
    let shouldUseInitialExtraAmount = shouldPreserveInitialBilling;

    if (!lookupModal || !lookupHost || !openLookupButtons.length) {
        return;
    }

    function getVisiblePaginationItems(currentPage, pageCount) {
        const totalPages = Math.max(1, pageCount);
        const activePage = Math.min(Math.max(currentPage, 1), totalPages);
        const maxVisiblePages = 7;

        if (totalPages <= maxVisiblePages) {
            return Array.from({ length: totalPages }, function (_, index) {
                return index + 1;
            });
        }

        if (activePage <= 4) {
            return [1, 2, 3, 4, 5, "ellipsis-end", totalPages];
        }

        if (activePage >= totalPages - 3) {
            return [1, "ellipsis-start", totalPages - 4, totalPages - 3, totalPages - 2, totalPages - 1, totalPages];
        }

        return [1, "ellipsis-start", activePage - 1, activePage, activePage + 1, "ellipsis-end", totalPages];
    }

    function updateAppointmentTabs() {
        const useTabs = narrowAppointmentTabsQuery.matches;

        appointmentTabs.forEach(function (tab) {
            const isActive = tab.dataset.appointmentTab === activeAppointmentTab;
            tab.classList.toggle("is-active", isActive);
            tab.setAttribute("aria-selected", isActive ? "true" : "false");
            tab.tabIndex = !useTabs || isActive ? 0 : -1;
        });

        appointmentTabPanels.forEach(function (panel) {
            const isActive = panel.dataset.appointmentTabPanel === activeAppointmentTab;
            panel.hidden = useTabs && !isActive;
        });
    }

    function setFormValue(field, value) {
        document.querySelectorAll("[data-patient-field='" + field + "']").forEach(function (element) {
            const normalizedValue = field === "age"
                ? normalizeAgeValue(value)
                : (element.type === "date" ? normalizeDateValue(value) : value);

            if (element.tagName === "SELECT") {
                const options = Array.from(element.options);
                if (normalizedValue && !options.some(function (option) { return option.value === normalizedValue || option.text === normalizedValue; })) {
                    element.add(new Option(normalizedValue, normalizedValue));
                }
                element.value = normalizedValue;
                element.dispatchEvent(new Event("change", { bubbles: true }));
                return;
            }

            element.value = normalizedValue;
            element.dispatchEvent(new Event("input", { bubbles: true }));
        });
    }

    function normalizeDateValue(value) {
        const text = String(value || "").trim();

        if (/^\d{8}$/.test(text)) {
            return text.slice(0, 4) + "-" + text.slice(4, 6) + "-" + text.slice(6, 8);
        }

        if (/^\d{4}\/\d{2}\/\d{2}$/.test(text)) {
            return text.replace(/\//g, "-");
        }

        return text;
    }

    function normalizeAgeValue(value) {
        const text = String(value || "").trim();
        const ageText = text.replace(/\s*歲$/, "");
        const parsedAge = Number.parseInt(ageText, 10);

        if (!ageText || !Number.isFinite(parsedAge) || parsedAge <= 0) {
            return "";
        }

        return String(parsedAge);
    }

    function setTextValue(field, value) {
        document.querySelectorAll("[data-patient-text='" + field + "']").forEach(function (element) {
            element.textContent = value || "--";
        });
    }

    function loadHealthCode() {
        if (!healthCodeInput || !healthCodeUrl) {
            return Promise.resolve("");
        }

        if (healthCodeLoadPromise) {
            return healthCodeLoadPromise;
        }

        const currentValue = String(healthCodeInput.value || "").trim();
        const previousValue = currentValue === "產生中..." ? "" : currentValue;

        if (healthCodeLoadController) {
            healthCodeLoadController.abort();
        }

        healthCodeLoadController = new AbortController();
        healthCodeInput.value = "產生中...";
        healthCodeInput.setAttribute("aria-busy", "true");

        healthCodeLoadPromise = fetch(new URL(healthCodeUrl, window.location.href).toString(), {
            headers: {
                "Accept": "application/json"
            },
            signal: healthCodeLoadController.signal
        })
            .then(function (response) {
                if (!response.ok) {
                    throw new Error("Health code request failed");
                }

                return response.json();
            })
            .then(function (payload) {
                const healthCode = String(payload.healthCode || "").trim();

                if (!healthCode) {
                    throw new Error("Empty health code");
                }

                setFormValue("healthCode", healthCode);
                setTextValue("healthCode", healthCode);
                return healthCode;
            })
            .catch(function (error) {
                if (error.name !== "AbortError") {
                    healthCodeInput.value = previousValue;
                }

                return "";
            })
            .finally(function () {
                healthCodeInput.removeAttribute("aria-busy");
                healthCodeLoadPromise = null;
            });

        return healthCodeLoadPromise;
    }

    function openLookup(sourceButton) {
        const baseUrl = sourceButton.dataset.lookupUrl;
        if (!baseUrl) {
            return;
        }

        const lookupUrl = new URL(baseUrl, window.location.href);

        previousFocus = document.activeElement;
        lookupHost.innerHTML = "";

        const iframe = document.createElement("iframe");
        iframe.title = "查詢受檢者";
        iframe.src = lookupUrl.toString();
        iframe.loading = "eager";
        iframe.setAttribute("data-patient-lookup-frame", "");
        lookupHost.appendChild(iframe);

        lookupModal.hidden = false;
        document.body.classList.add("patient-lookup-open");
    }

    function closeLookup() {
        lookupModal.hidden = true;
        lookupHost.innerHTML = "";
        document.body.classList.remove("patient-lookup-open");

        if (previousFocus && typeof previousFocus.focus === "function") {
            previousFocus.focus();
        }
    }

    function openProjectLookup() {
        if (!projectLookupModal) {
            return;
        }

        previousFocus = document.activeElement;
        projectLookupModal.hidden = false;
        document.body.classList.add("project-lookup-open");
    }

    function closeProjectLookup() {
        if (!projectLookupModal) {
            return;
        }

        projectLookupModal.hidden = true;
        document.body.classList.remove("project-lookup-open");

        if (previousFocus && typeof previousFocus.focus === "function") {
            previousFocus.focus();
        }
    }

    function openSelfPayAccountingLookup(itemId, itemType) {
        const isOtherSuite = itemType === "otherSuite";
        const appliesToAllSelectedPackages = !isOtherSuite && itemId === "*";
        const itemExists = appliesToAllSelectedPackages
            ? packageOptions.some(function (packageOption) { return packageOption.selected; })
            : isOtherSuite
            ? otherSuiteOptions.some(function (otherSuite) { return otherSuite.id === itemId; })
            : packageOptions.some(function (packageOption) { return packageOption.id === itemId; });

        if (!selfPayAccountingModal || !itemExists) {
            return;
        }

        activePackageAccountingId = isOtherSuite ? "" : itemId;
        activeOtherSuiteAccountingId = isOtherSuite ? itemId : "";
        previousSelfPayAccountingFocus = document.activeElement;
        activeSelfPayAccountingPrefix = "";
        selfPayAccountingFilters.forEach(function (input) { input.value = ""; });
        renderSelfPayAccountingTabs();
        renderSelfPayAccountingRows();
        selfPayAccountingModal.hidden = false;
        document.body.classList.add("project-lookup-open");
        selfPayAccountingFilters[0]?.focus();
    }

    function closeSelfPayAccountingLookup() {
        if (!selfPayAccountingModal) {
            return;
        }

        selfPayAccountingModal.hidden = true;
        document.body.classList.remove("project-lookup-open");
        previousSelfPayAccountingFocus?.focus();
        activePackageAccountingId = "";
        activeOtherSuiteAccountingId = "";
    }

    function openOtherSuiteLookup() {
        if (!otherSuiteLookupModal || !otherSuiteLookupHost || !otherSuiteLookupUrl) {
            return;
        }

        previousFocus = document.activeElement;
        otherSuiteLookupHost.innerHTML = "";

        const iframe = document.createElement("iframe");
        iframe.title = "新增組套";
        iframe.src = new URL(otherSuiteLookupUrl, window.location.href).toString();
        iframe.loading = "eager";
        iframe.setAttribute("data-other-suite-lookup-frame", "");
        otherSuiteLookupHost.appendChild(iframe);

        otherSuiteLookupModal.hidden = false;
        document.body.classList.add("other-suite-lookup-open");
    }

    function closeOtherSuiteLookup() {
        if (!otherSuiteLookupModal || !otherSuiteLookupHost) {
            return;
        }

        otherSuiteLookupModal.hidden = true;
        otherSuiteLookupHost.innerHTML = "";
        document.body.classList.remove("other-suite-lookup-open");

        if (previousFocus && typeof previousFocus.focus === "function") {
            previousFocus.focus();
        }
    }

    function buildProjectOptionText(projectNo, projectName) {
        return String(projectName || projectNo || "").trim();
    }

    function getProjectCustomerNo(projectNo) {
        const normalizedProjectNo = String(projectNo || "").trim();
        const row = projectRows.find(function (candidate) {
            return String(candidate.dataset.projectNo || "").trim() === normalizedProjectNo;
        });

        return row ? String(row.dataset.customerNo || "").trim() : "";
    }

    function setSelectedProjectCustomerNo(projectNo, customerNo) {
        if (!projectCustomerNoInput) {
            return;
        }

        projectCustomerNoInput.value = String(customerNo || getProjectCustomerNo(projectNo) || projectCustomerNoInput.value || "").trim();
    }

    function setProject(projectNo, projectName, customerNo) {
        const normalizedProjectNo = String(projectNo || "").trim();
        const normalizedProjectName = String(projectName || "").trim();

        if (!projectSelect || !normalizedProjectNo) {
            return;
        }

        const options = Array.from(projectSelect.options);
        let option = options.find(function (candidate) {
            return candidate.value === normalizedProjectNo;
        });

        if (!option) {
            option = new Option(buildProjectOptionText(normalizedProjectNo, normalizedProjectName), normalizedProjectNo);
            projectSelect.add(option);
        } else if (normalizedProjectName) {
            option.text = buildProjectOptionText(normalizedProjectNo, normalizedProjectName);
        }

        projectSelect.value = normalizedProjectNo;
        setSelectedProjectCustomerNo(normalizedProjectNo, customerNo);
        projectSelect.dispatchEvent(new Event("change", { bubbles: true }));
    }

    function selfPayAccountingPrefix(row) {
        const text = String(row && (row.dataset.accountingName || row.dataset.accountingCode) || "").trim();

        return Array.from(text).slice(0, 2).join("");
    }

    function setPackageSelfPayAccountingUnit(packageId, code, displayText) {
        const packageOption = packageOptions.find(function (candidate) {
            return candidate.id === packageId;
        });
        const normalizedCode = String(code || "").trim();
        const normalizedDisplayText = String(displayText || "").trim();
        const row = selfPayAccountingRows.find(function (candidate) {
            return String(candidate.dataset.accountingCode || "").trim() === normalizedCode;
        });
        const name = String(row?.dataset.accountingName || (normalizedCode === "00" ? "無優待" : "")).trim();

        if (!packageOption) {
            return;
        }

        packageOption.selfPayAccountingUnit = normalizedCode;
        packageOption.selfPayAccountingUnitDisplay = normalizedCode
            ? (normalizedDisplayText || (name ? "[" + normalizedCode + "] " + name : normalizedCode))
            : "";
        renderPackagePage();
    }

    function setOtherSuiteSelfPayAccountingUnit(otherSuiteId, code, displayText) {
        const otherSuite = otherSuiteOptions.find(function (candidate) {
            return candidate.id === otherSuiteId;
        });
        const normalizedCode = String(code || "00").trim() || "00";
        const normalizedDisplayText = String(displayText || "").trim();
        const row = selfPayAccountingRows.find(function (candidate) {
            return String(candidate.dataset.accountingCode || "").trim() === normalizedCode;
        });
        const name = String(row?.dataset.accountingName || (normalizedCode === "00" ? "無優待" : "")).trim();

        if (!otherSuite) {
            return;
        }

        otherSuite.selfPayAccountingUnit = normalizedCode;
        otherSuite.selfPayAccountingUnitDisplay = normalizedDisplayText
            || (name ? "[" + normalizedCode + "] " + name : normalizedCode);
        renderOtherSuitePage();
    }

    function setActiveSelfPayAccountingUnit(code, displayText) {
        if (activeOtherSuiteAccountingId) {
            setOtherSuiteSelfPayAccountingUnit(activeOtherSuiteAccountingId, code, displayText);
            return;
        }

        if (activePackageAccountingId === "*") {
            const normalizedCode = String(code || "00").trim() || "00";
            const normalizedDisplayText = String(displayText || "").trim() || (normalizedCode === "00" ? "[00] 無優待" : normalizedCode);

            packageOptions.forEach(function (packageOption) {
                if (packageOption.selected) {
                    packageOption.selfPayAccountingUnit = normalizedCode;
                    packageOption.selfPayAccountingUnitDisplay = normalizedDisplayText;
                }
            });
            renderPackagePage();
            return;
        }

        setPackageSelfPayAccountingUnit(activePackageAccountingId, code, displayText);
    }

    function updatePackageAccountingBulkState() {
        const selectedPackages = packageOptions.filter(function (packageOption) { return packageOption.selected; });
        const firstPackage = selectedPackages[0];
        const firstCode = String(firstPackage?.selfPayAccountingUnit || "").trim();
        const firstDisplay = String(firstPackage?.selfPayAccountingUnitDisplay || "").trim();
        const allMatch = selectedPackages.length > 0 && selectedPackages.every(function (packageOption) {
            return String(packageOption.selfPayAccountingUnit || "").trim() === firstCode
                && String(packageOption.selfPayAccountingUnitDisplay || "").trim() === firstDisplay;
        });
        const isDefault = selectedPackages.length > 0 && selectedPackages.every(function (packageOption) {
            return (String(packageOption.selfPayAccountingUnit || "00").trim() || "00") === "00";
        });

        if (packageAccountingAllValue) {
            packageAccountingAllValue.textContent = selectedPackages.length === 0
                ? "尚未選擇套組"
                : (allMatch ? (firstDisplay || "未選擇") : "各套組設定不同");
            packageAccountingAllValue.title = packageAccountingAllValue.textContent;
        }
        if (packageAccountingAllQueryButton) packageAccountingAllQueryButton.disabled = selectedPackages.length === 0;
        if (packageAccountingAllClearButton) packageAccountingAllClearButton.disabled = selectedPackages.length === 0 || isDefault;
        if (packageAccountingAllReminder) packageAccountingAllReminder.hidden = selectedPackages.length > 0;
    }

    function renderSelfPayAccountingRows() {
        const codeKeyword = normalizeLookupValue(selfPayAccountingFilters.find(function (input) {
            return input.dataset.selfPayAccountingFilter === "code";
        })?.value);
        const nameKeyword = normalizeLookupValue(selfPayAccountingFilters.find(function (input) {
            return input.dataset.selfPayAccountingFilter === "name";
        })?.value);
        const matchRank = function (value, keyword) {
            if (!keyword || value === keyword) {
                return 0;
            }

            return value.startsWith(keyword) ? 1 : 2;
        };
        let visibleCount = 0;
        const rankedRows = selfPayAccountingRows.map(function (row, originalIndex) {
            const accountingCode = normalizeLookupValue(row.dataset.accountingCode);
            const accountingName = normalizeLookupValue(row.dataset.accountingName);
            const matchesPrefix = !activeSelfPayAccountingPrefix
                || selfPayAccountingPrefix(row) === activeSelfPayAccountingPrefix;
            const matchesCode = !codeKeyword || accountingCode.includes(codeKeyword);
            const matchesName = !nameKeyword || accountingName.includes(nameKeyword);
            const visible = matchesPrefix && matchesCode && matchesName;
            const rank = codeKeyword
                ? matchRank(accountingCode, codeKeyword) * 10 + matchRank(accountingName, nameKeyword)
                : matchRank(accountingName, nameKeyword);

            row.hidden = !visible;
            visibleCount += visible ? 1 : 0;

            return { row: row, visible: visible, rank: rank, originalIndex: originalIndex };
        });

        rankedRows.sort(function (left, right) {
            if (left.visible !== right.visible) {
                return left.visible ? -1 : 1;
            }

            return left.rank - right.rank || left.originalIndex - right.originalIndex;
        });

        const resultsBody = selfPayAccountingRows[0]?.parentElement;
        rankedRows.forEach(function (item) {
            resultsBody?.appendChild(item.row);
        });

        if (selfPayAccountingEmpty) {
            selfPayAccountingEmpty.hidden = visibleCount > 0;
            resultsBody?.appendChild(selfPayAccountingEmpty);
        }
    }
    function renderSelfPayAccountingTabs() {
        if (!selfPayAccountingTabs) {
            return;
        }

        const prefixes = Array.from(new Set(selfPayAccountingRows.map(selfPayAccountingPrefix).filter(Boolean)));
        const tabs = [{ value: "", text: "全部" }].concat(prefixes.map(function (prefix) {
            return { value: prefix, text: prefix };
        }));

        selfPayAccountingTabs.replaceChildren();
        tabs.forEach(function (tab, index) {
            const button = document.createElement("button");
            const icon = document.createElement("span");
            const label = document.createElement("span");
            const tones = ["blue", "red", "green", "amber", "purple", "teal", "pink", "brown"];

            button.type = "button";
            button.classList.add(tones[index % tones.length]);
            button.classList.toggle("active", tab.value === activeSelfPayAccountingPrefix);
            button.setAttribute("aria-pressed", String(tab.value === activeSelfPayAccountingPrefix));
            icon.className = "package-category-icon " + (tab.value ? "sample" : "stethoscope");
            icon.setAttribute("aria-hidden", "true");
            label.textContent = tab.text;
            button.append(icon, label);
            button.addEventListener("click", function () {
                activeSelfPayAccountingPrefix = tab.value;
                selfPayAccountingFilters.forEach(function (input) { input.value = ""; });
                renderSelfPayAccountingTabs();
                renderSelfPayAccountingRows();
            });
            selfPayAccountingTabs.appendChild(button);
        });
    }

    function isBlockedQuotaSubmitButton(button) {
        return isConfirmedEditMode
            && button
            && String(button.dataset.reserveStatus || "").trim() === "2";
    }

    function syncCreateAppointmentSubmitAvailability() {
        createAppointmentSubmitButtons.forEach(function (button) {
            if (!isBlockedQuotaSubmitButton(button)) {
                return;
            }

            button.hidden = true;
            button.disabled = true;
            button.tabIndex = -1;
            button.setAttribute("aria-hidden", "true");
            button.removeAttribute("aria-busy");
        });
    }

    function setCreateAppointmentBusy(isBusy) {
        createAppointmentSubmitButtons.forEach(function (button) {
            if (isBlockedQuotaSubmitButton(button)) {
                button.hidden = true;
                button.disabled = true;
                button.removeAttribute("aria-busy");
                return;
            }

            button.disabled = isBusy;
            button.setAttribute("aria-busy", String(isBusy));
        });
    }

    function showCreateAppointmentRegisterProgress() {
        if (createAppointmentRegisterProgress) {
            createAppointmentRegisterProgress.hidden = false;
        }


    }

    async function hideCreateAppointmentRegisterProgress() {
        if (createAppointmentRegisterProgress) {
            createAppointmentRegisterProgress.hidden = true;
        }
    }
    function showCreateAppointmentResult(state, title, message) {
        if (!createAppointmentResult) {
            return;
        }

        createAppointmentResult.dataset.state = state || "success";
        if (createAppointmentResultTitle) {
            createAppointmentResultTitle.textContent = title || "";
        }

        if (createAppointmentResultMessage) {
            createAppointmentResultMessage.textContent = message || "";
        }

        createAppointmentResult.hidden = false;

        if (createAppointmentResultClose && state === "error") {
            createAppointmentResultClose.focus();
        }
    }

    function hideCreateAppointmentResult() {
        if (createAppointmentResult) {
            createAppointmentResult.hidden = true;
        }
    }

    function appendSelectedAppointmentItems(formData) {
        formData.delete("SelectedPackageIds");
        formData.delete("SelectedPackageNames");
        formData.delete("SelectedPackageAmounts");
        formData.delete("SelectedPackageSelfPayAccountingUnits");
        formData.delete("SelfPayAccountingUnit");
        const selectedPackages = packageOptions.filter(function (packageOption) { return packageOption.selected; });
        selectedPackages
            .forEach(function (packageOption) {
                const amount = Math.max(0, Number(packageOption.price) || 0);
                formData.append("SelectedPackageIds", packageOption.id);
                formData.append("SelectedPackageNames", packageOption.name);
                formData.append("SelectedPackageAmounts", String(amount));
                formData.append("SelectedPackageSelfPayAccountingUnits", packageOption.selfPayAccountingUnit || "");
            });

        if (selectedPackages.length > 0) {
            formData.append("SelfPayAccountingUnit", selectedPackages[0].selfPayAccountingUnit || "");
        }

        formData.delete("SelectedOtherSuiteIds");
        formData.delete("SelectedOtherSuiteSelfPayAccountingUnits");
        otherSuiteOptions.forEach(function (otherSuite) {
            formData.append("SelectedOtherSuiteIds", otherSuite.id);
            formData.append("SelectedOtherSuiteSelfPayAccountingUnits", otherSuite.selfPayAccountingUnit || "00");
        });

        formData.delete("SelectedQuestionnaireIds");
        formData.delete("SelectedQuestionnaireNames");
        formData.delete("SelectedQuestionnaireAmounts");

        if (!shouldShowQuestionnaireSection()) {
            return;
        }

        questionnaireOptions
            .filter(function (questionnaire) { return questionnaire.selected; })
            .forEach(function (questionnaire) {
                const amount = Math.max(0, Number(questionnaire.price) || 0);
                formData.append("SelectedQuestionnaireIds", questionnaire.id);
                formData.append("SelectedQuestionnaireNames", questionnaire.name);
                formData.append("SelectedQuestionnaireAmounts", String(amount));
            });
    }

    function appendBillingInfo(formData) {
        const packageAmount = currentPackageAmount();
        const extraAmount = currentExtraAmount();
        const remitAmount = readBillingAdjustment("remittance");
        const discountAmount = readBillingAdjustment("discount");
        const accountAmount = readBillingAdjustment("account");
        const receivedAmount = currentReceivedAmount(packageAmount, extraAmount, remitAmount, discountAmount, accountAmount);

        formData.set("PackageAmount", String(packageAmount));
        formData.set("ExtraAmount", String(extraAmount));
        formData.set("RemitAmount", String(remitAmount));
        formData.set("DiscountAmount", String(discountAmount));
        formData.set("AccountAmount", String(accountAmount));
        formData.set("ReceivedAmount", String(receivedAmount));
        formData.set("PaymentDate", billingPaymentDateInput ? billingPaymentDateInput.value : "");
        formData.set("PaymentMethod", billingPaymentMethodSelect ? billingPaymentMethodSelect.value : "");
        formData.set("PaymentNote", billingNoteInput ? billingNoteInput.value : "");
    }

    async function showResponseErrorPage(response) {
        const contentType = String(response.headers.get("content-type") || "").toLocaleLowerCase();

        if (!contentType.includes("text/html")) {
            return false;
        }

        if (response.redirected && response.url) {
            window.location.assign(response.url);
            return true;
        }

        const html = await response.text();

        if (!html) {
            return false;
        }

        const errorPageUrl = URL.createObjectURL(new Blob([html], {
            type: "text/html;charset=utf-8"
        }));

        window.location.assign(errorPageUrl);

        return true;
    }

    function buildAppointmentListUrl(payload, formData) {
        const listUrl = createAppointmentForm.dataset.listUrl || createAppointmentForm.action;
        const url = new URL(listUrl, window.location.href);
        const barcode = String(payload?.barcode || formData.get("PatientHealthCode") || "").trim();
        const projectNumber = String(payload?.projectNo || formData.get("ProjectNo") || "").trim();
        const reservationDate = String(formData.get("ReservationDate") || "").trim();

        if (payload?.updated !== true) {
            url.search = "";
        }

        if (barcode) {
            url.searchParams.set("focusBarcode", barcode);
        }

        if (projectNumber) {
            url.searchParams.set("focusProjectNumber", projectNumber);
        }

        if (reservationDate) {
            url.searchParams.set("checkDateStart", reservationDate);
            url.searchParams.set("checkDateEnd", reservationDate);
        }

        return url.toString();
    }

    function syncConsultingDoctorName() {
        if (!consultingDoctorSelect || !consultingDoctorNameInput) {
            return;
        }

        const selectedOption = consultingDoctorSelect.options[consultingDoctorSelect.selectedIndex];
        const selectedText = String(selectedOption?.textContent || "").trim();
        const doctorMatch = selectedText.match(/^\s*\([^)]*\)\s*(.*)$/);

        if (doctorMatch) {
            consultingDoctorNameInput.value = String(doctorMatch[1] || "").trim();
        } else if (!String(consultingDoctorSelect.value || "").trim()) {
            consultingDoctorNameInput.value = "";
        }
    }

    async function submitCreateAppointment(button, options = {}) {
        if (!createAppointmentForm) {
            return null;
        }

        const reserveStatus = String(button.dataset.reserveStatus || "").trim();
        const requiresCompleteAppointment = reserveStatus !== "2";
        const shouldRedirect = options.redirect !== false;
        const shouldShowSuccess = options.showSuccess !== false;

        if (isBlockedQuotaSubmitButton(button)) {
            return null;
        }

        if (sameAddressCheckbox && sameAddressCheckbox.checked) {
            syncAddressFromMailing();
        }

        if (projectSelect) {
            setSelectedProjectCustomerNo(projectSelect.value);
        }

        setCreateAppointmentBusy(true);

        try {
            // A loading table is temporarily empty; wait before validating or
            // building FormData so an existing selection is not lost.
            let pendingPackages;
            do {
                pendingPackages = packageLoadPromise;
                await pendingPackages;
            } while (pendingPackages !== packageLoadPromise);

            if (packageLoadError) {
                showCreateAppointmentResult("error", "無法確認套組資料", packageLoadError);
                return null;
            }

            let healthCode = String(healthCodeInput?.value || "").trim();

            if (!healthCode || healthCode === "產生中...") {
                healthCode = await loadHealthCode();
            }

            if (!healthCode) {
                showCreateAppointmentResult("error", "無法建立預約", "健檢碼尚未產生，請稍後再試。");
                return;
            }

            syncConsultingDoctorName();
            const formData = new FormData(createAppointmentForm);
            formData.set("PatientHealthCode", healthCode);
            formData.set("ReserveStatus", reserveStatus);
            appendSelectedAppointmentItems(formData);
            appendBillingInfo(formData);

            if (projectSelect) {
                formData.set("ProjectNo", projectSelect.value || "");
            }

            if (projectCustomerNoInput) {
                formData.set("CustomerNo", projectCustomerNoInput.value || "");
            }

            if (sameAddressCheckbox) {
                formData.set("IsSameAddress", sameAddressCheckbox.checked ? "true" : "false");
            }

            const requiredFields = [
                ["PatientChartNo", "病歷號"],
                ["PatientName", "姓名"],
                ["PatientIdNo", "身分證 / 居留證號"],
                ["PatientSex", "性別"],
                ["PatientMobile", "手機"],
                ["ReservationArea", "預約區域"],
                ["ReservationDate", "預約日期"],
                ["ReservationTime", "預約時間"],
                ["ConsultingDoctorId", "看診醫師"],
                ["ClinicRoom", "診間號"],
                ["ProjectNo", "健檢專案"]
            ];

            requiredFields.forEach(function (field) {
                createAppointmentForm.elements.namedItem(field[0])?.removeAttribute("aria-invalid");
            });

            const missingFields = requiredFields.filter(function (field) {
                return !String(formData.get(field[0]) || "").trim();
            });

            if (requiresCompleteAppointment && missingFields.length > 0) {
                showCreateAppointmentResult(
                    "error",
                    "資料尚未填寫完整",
                    "請填寫：" + missingFields.map(function (field) { return field[1]; }).join("、") + "。");

                const firstMissingControl = createAppointmentForm.elements.namedItem(missingFields[0][0]);
                const missingPanel = firstMissingControl?.closest("[data-appointment-tab-panel]");
                const focusTarget = firstMissingControl?.readOnly
                    ? document.querySelector("[data-open-patient-lookup]")
                    : firstMissingControl;

                if (missingPanel) {
                    activeAppointmentTab = missingPanel.dataset.appointmentTabPanel || "patient";
                    updateAppointmentTabs();
                }

                firstMissingControl?.setAttribute("aria-invalid", "true");
                focusTarget?.focus();
                return;
            }

            const hasSelectedPackage = packageOptions.some(function (packageOption) {
                return packageOption.selected;
            });
            const hasSelectedOtherSuite = otherSuiteOptions.length > 0;

            if (requiresCompleteAppointment && !hasSelectedPackage && !hasSelectedOtherSuite) {
                activeAppointmentTab = "project";
                updateAppointmentTabs();
                packageTable?.setAttribute("aria-invalid", "true");
                showCreateAppointmentResult(
                    "error",
                    "\u5c1a\u672a\u9078\u64c7\u5957\u9910 / \u5957\u7d44",
                    "\u8acb\u81f3\u5c11\u9078\u64c7\u4e00\u500b\u5957\u9910 / \u5957\u7d44\u5f8c\u518d\u9001\u51fa\u3002");

                window.requestAnimationFrame(function () {
                    const firstPackageCheckbox = packageBody?.querySelector(
                        "input[type='checkbox'][name='SelectedPackageIds']");
                    packageTable?.scrollIntoView({ behavior: "smooth", block: "center" });
                    (firstPackageCheckbox || packageTable)?.focus();
                });
                return;
            }

            const packageWithoutAccountingUnit = packageOptions.find(function (packageOption) {
                return packageOption.selected && !String(packageOption.selfPayAccountingUnit || "").trim();
            });

            if (requiresCompleteAppointment && packageWithoutAccountingUnit) {
                activeAppointmentTab = "project";
                updateAppointmentTabs();
                packageTable?.setAttribute("aria-invalid", "true");
                showCreateAppointmentResult(
                    "error",
                    "套組尚未選擇優待身分",
                    "請替「" + packageWithoutAccountingUnit.name + "」選擇優待身分後再送出。");
                openSelfPayAccountingLookup(packageWithoutAccountingUnit.id);
                return;
            }

            packageTable?.removeAttribute("aria-invalid");

            const response = await fetch(createAppointmentForm.action, {
                method: "POST",
                body: formData,
                headers: {
                    "X-Requested-With": "XMLHttpRequest"
                }
            });

            if (!response.ok && await showResponseErrorPage(response)) {
                return;
            }

            const payload = await response.json().catch(function () { return {}; });

            if (!response.ok || payload.saved !== true) {
                throw new Error(payload.message || "預約資料寫入失敗。");
            }

            if (shouldShowSuccess) {
                const isConfirmedAppointment = reserveStatus === "1";
                const successTitle = isConfirmedAppointment
                    ? (payload.updated === true ? "預約已更新" : "預約已建立")
                    : (payload.updated === true ? "資料已更新" : "資料已建立");
                const successMessage = isConfirmedAppointment
                    ? (shouldRedirect ? "狀態：已預約，正在返回預約清單..." : "預約狀態已更新為已預約。")
                    : (shouldRedirect ? "正在返回預約清單..." : "預約資料已更新。");
                showCreateAppointmentResult(
                    "success",
                    successTitle,
                    successMessage);
            }

            if (shouldRedirect) {
                window.setTimeout(function () {
                    window.location.href = buildAppointmentListUrl(payload, formData);
                }, 950);
            }

            return { payload, formData };
        } catch (error) {
            showCreateAppointmentResult("error", "寫入失敗", error.message || "預約資料寫入失敗。");
            return null;
        } finally {
            setCreateAppointmentBusy(false);
        }
    }

    async function registerCurrentAppointment() {
        if (!createAppointmentForm || !createAppointmentRegisterButton) {
            return;
        }

        createAppointmentRegisterButton.disabled = true;
        createAppointmentRegisterButton.setAttribute("aria-busy", "true");
        showCreateAppointmentRegisterProgress();
        let registered = false;

        try {
            const confirmAppointmentButton = createAppointmentSubmitButtons.find(function (button) {
                return String(button.dataset.reserveStatus || "").trim() === "1";
            });

            if (!confirmAppointmentButton) {
                throw new Error("找不到確認預約按鈕，無法先更新預約資料。");
            }

            const savedAppointment = await submitCreateAppointment(confirmAppointmentButton, {
                redirect: false,
                showSuccess: false
            });

            if (!savedAppointment) {
                await hideCreateAppointmentRegisterProgress();
                return;
            }

            setCreateAppointmentBusy(true);
            syncConsultingDoctorName();
            const registerUrl = String(createAppointmentForm.dataset.registerUrl || "").trim();
            const formData = new FormData(createAppointmentForm);
            const selectedPackages = packageOptions.filter(function (packageOption) {
                return packageOption.selected;
            });
            const registrationPackages = selectedPackages.map(function (packageOption) {
                return {
                    orderNumber: packageOption.id,
                    orderTotal: Math.max(0, Number.parseInt(packageOption.orderTotal, 10) || 0),
                    selfPayAccountingUnit: String(packageOption.selfPayAccountingUnit || "").trim(),
                    type: "package"
                };
            }).concat(otherSuiteOptions.map(function (otherSuite) {
                return {
                    orderNumber: String(otherSuite.code || "").trim(),
                    orderTotal: 1,
                    selfPayAccountingUnit: String(otherSuite.selfPayAccountingUnit || "00").trim() || "00",
                    type: "other-suite"
                };
            })).filter(function (packageOrder) {
                return packageOrder.orderNumber;
            });
            const row = {
                projectNumber: String(formData.get("ProjectNo") || "").trim(),
                barcode: String(formData.get("PatientHealthCode") || "").trim(),
                patNo: String(formData.get("PatientChartNo") || "").trim(),
                name: String(formData.get("PatientName") || "").trim(),
                appointmentDate: String(formData.get("ReservationDate") || "").trim(),
                time: String(formData.get("ReservationTime") || "").trim(),
                doctorName: String(formData.get("ConsultingDoctorName") || "").trim(),
                doctorId: String(formData.get("ConsultingDoctorId") || "").trim(),
                roomNo: String(formData.get("ClinicRoom") || "").trim(),
                selfPayAccountingUnit: String(registrationPackages[0]?.selfPayAccountingUnit || "").trim(),
                selectedPackageIds: registrationPackages.map(function (packageOrder) {
                    return packageOrder.orderNumber;
                }),
                selectedPackageOrderTotals: registrationPackages.map(function (packageOrder) {
                    return String(packageOrder.orderTotal);
                }),
                selectedPackageSelfPayAccountingUnits: registrationPackages.map(function (packageOrder) {
                    return packageOrder.selfPayAccountingUnit;
                }),
                selectedPackageTypes: registrationPackages.map(function (packageOrder) {
                    return packageOrder.type;
                })
            };

            if (!registerUrl || !row.projectNumber || !row.barcode || !row.patNo || !row.appointmentDate || !row.time) {
                throw new Error("缺少專案編號、Barcode、病歷號、預約日期或預約時間。");
            }

            const antiForgeryToken = String(formData.get("__RequestVerificationToken") || "");
            const response = await fetch(registerUrl, {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                    "RequestVerificationToken": antiForgeryToken
                },
                body: JSON.stringify({ rows: [row] })
            });
            const responseText = await response.text();
            let payload = {};

            if (responseText.trim()) {
                try {
                    payload = JSON.parse(responseText);
                } catch {
                    throw new Error(response.ok
                        ? "掛號服務回傳格式不正確。"
                        : `掛號失敗（HTTP ${response.status}）。`);
                }
            }

            if (!response.ok) {
                throw new Error(payload?.message || `掛號失敗（HTTP ${response.status}）。`);
            }

            const result = Array.isArray(payload.results) ? payload.results[0] : null;
            registered = result?.success === true;

            await hideCreateAppointmentRegisterProgress();

            showCreateAppointmentResult(
                registered ? "success" : "error",
                registered ? "掛號成功" : "掛號失敗",
                String(result?.message || payload.message || "掛號失敗，請稍後再試。"));

            if (registered) {
                createAppointmentForm.dataset.currentReserveStatus = "4";
                window.setTimeout(function () {
                    window.location.href = String(createAppointmentForm.dataset.listUrl || "").trim()
                        || window.location.href;
                }, 950);
            }
        } catch (error) {
            await hideCreateAppointmentRegisterProgress();
            showCreateAppointmentResult("error", "掛號失敗", error.message || "掛號失敗，請稍後再試。");
        } finally {
            setCreateAppointmentBusy(registered);
            createAppointmentRegisterButton.disabled = registered;
            createAppointmentRegisterButton.removeAttribute("aria-busy");
        }
    }
    function formatCurrency(value) {
        const number = Number(value);

        if (!Number.isFinite(number)) {
            return "$0";
        }

        return "$" + number.toLocaleString("en-US", {
            maximumFractionDigits: 0
        });
    }

    function parseCurrency(value) {
        const number = Number(String(value || "").replace(/[^\d.-]/g, ""));

        return Number.isFinite(number) ? number : 0;
    }

    function formatEditableAmount(value) {
        const number = Number(value);

        if (!Number.isFinite(number)) {
            return "0";
        }

        return number.toLocaleString("en-US", {
            maximumFractionDigits: 0
        });
    }

    function readBillingAmount(key) {
        const element = billingValueElements[key];

        return element ? parseCurrency(element.value || element.textContent) : 0;
    }

    function readInitialBillingAmount(key) {
        const element = billingValueElements[key];

        return element ? parseCurrency(element.dataset.initialBillingValue || element.textContent) : 0;
    }

    function setBillingAmount(key, value) {
        const element = billingValueElements[key];

        if (!element) {
            return;
        }

        const formattedValue = formatEditableAmount(value);

        if ("value" in element) {
            element.value = formattedValue;
        } else {
            element.textContent = formattedValue;
        }
    }

    function readBillingAdjustment(name) {
        const input = billingAdjustmentInputs.find(function (candidate) {
            return candidate.dataset.billingAdjustment === name;
        });

        return input ? parseCurrency(input.value) : 0;
    }

    function formatBillingInput(input) {
        input.value = formatEditableAmount(parseCurrency(input.value));
    }

    function selectedPackageTotal() {
        return packageOptions.reduce(function (total, packageOption) {
            return packageOption.selected ? total + packageOption.price : total;
        }, 0);
    }

    function selectedOtherSuiteTotal() {
        return otherSuiteOptions.reduce(function (total, otherSuite) {
            return total + otherSuite.price;
        }, 0);
    }

    function currentPackageAmount() {
        return shouldUseInitialPackageAmount ? readInitialBillingAmount("package") : selectedPackageTotal();
    }

    function currentExtraAmount() {
        return shouldUseInitialExtraAmount ? readInitialBillingAmount("addon") : selectedOtherSuiteTotal();
    }

    function currentReceivedAmount(packageAmount, extraAmount, remittanceAmount, discountAmount, accountAmount) {
        if (shouldPreserveInitialBilling) {
            return readInitialBillingAmount("actual");
        }

        return Math.max(0, packageAmount + extraAmount - remittanceAmount - discountAmount - accountAmount);
    }

    function useCalculatedPackageAmount() {
        shouldUseInitialPackageAmount = false;
        shouldPreserveInitialBilling = false;
        updateBillingSummary();
    }

    function useCalculatedExtraAmount() {
        shouldUseInitialExtraAmount = false;
        shouldPreserveInitialBilling = false;
        updateBillingSummary();
    }

    function updateBillingSummary() {
        const packageTotal = currentPackageAmount();
        const addOnTotal = currentExtraAmount();
        const remittanceAmount = readBillingAdjustment("remittance");
        const discountAmount = readBillingAdjustment("discount");
        const accountAmount = readBillingAdjustment("account");
        const actualTotal = currentReceivedAmount(packageTotal, addOnTotal, remittanceAmount, discountAmount, accountAmount);

        setBillingAmount("package", packageTotal);
        setBillingAmount("addon", addOnTotal);
        setBillingAmount("actual", actualTotal);
    }

    function getPackagePageSize() {
        const pageSize = Number.parseInt(packagePageSizeSelect ? packagePageSizeSelect.value : "5", 10);
        return Number.isFinite(pageSize) && pageSize > 0 ? pageSize : 5;
    }

    function normalizePackageSearchValue(value) {
        return String(value || "")
            .normalize("NFKC")
            .replace(/\s+/g, "")
            .trim()
            .toLocaleLowerCase();
    }

    function getPackageSearchKeyword() {
        const keyword = normalizePackageSearchValue(packageSearchInput ? packageSearchInput.value : "");
        return keyword === normalizePackageSearchValue("全部") ? "" : keyword;
    }

    function packageCategorySearchValue(value) {
        const text = String(value || "").trim();
        const categoryText = text.startsWith("全部") ? text.slice(2).trim() : text;

        return Array.from(categoryText).slice(0, 2).join("");
    }

    function packageCategoryKey(packageOption) {
        return packageCategorySearchValue(packageOption.name || packageOption.id);
    }

    function packageCategoryTone(index) {
        return ["blue", "teal", "green", "purple", "pink", "orange"][index % 6];
    }

    function createPackageCategoryButton(value, label, index) {
        const button = document.createElement("button");
        const icon = document.createElement("span");
        const text = document.createElement("span");

        button.type = "button";
        button.className = packageCategoryTone(index);
        button.dataset.packageCategory = value;
        button.setAttribute("aria-pressed", "false");

        icon.className = "package-category-icon " + (value ? "sample" : "stethoscope");
        icon.setAttribute("aria-hidden", "true");
        text.textContent = label;

        button.append(icon, text);
        return button;
    }

    function updatePackageCategoryLayout() {
        if (!packageCategoryTabs) {
            return;
        }

        // Measure the single row before deciding whether a second row is needed.
        packageCategoryTabs.classList.remove("is-two-row");
        if (tallPackageCategoryQuery.matches
            && packageCategoryTabs.clientWidth > 0
            && packageCategoryTabs.scrollWidth > packageCategoryTabs.clientWidth + 1) {
            packageCategoryTabs.classList.add("is-two-row");
        }
    }

    function schedulePackageCategoryLayout() {
        if (packageCategoryLayoutFrame !== null) {
            return;
        }

        packageCategoryLayoutFrame = window.requestAnimationFrame(function () {
            packageCategoryLayoutFrame = null;
            updatePackageCategoryLayout();
        });
    }

    function initializePackageCategoryLayout() {
        window.addEventListener("resize", schedulePackageCategoryLayout);
        tallPackageCategoryQuery.addEventListener("change", schedulePackageCategoryLayout);
        if ("ResizeObserver" in window) {
            const observer = new ResizeObserver(schedulePackageCategoryLayout);
            observer.observe(packageCategoryTabs);
        }
        if (document.fonts) {
            document.fonts.ready.then(schedulePackageCategoryLayout);
        }
        schedulePackageCategoryLayout();
    }

    function renderPackageCategoryTabs() {
        if (!packageCategoryTabs) {
            return;
        }

        const categories = Array.from(new Set(packageOptions
            .map(packageCategoryKey)
            .filter(function (value) { return value; })))
            .sort(function (left, right) { return left.localeCompare(right, "zh-Hant"); });

        packageCategoryTabs.innerHTML = "";
        packageCategoryTabs.appendChild(createPackageCategoryButton("", "全部", 0));

        categories.forEach(function (category, index) {
            packageCategoryTabs.appendChild(createPackageCategoryButton(category, category, index + 1));
        });

        syncPackageCategoryButtons();
        schedulePackageCategoryLayout();
    }

    function syncPackageCategoryButtons() {
        if (!packageCategoryTabs) {
            return;
        }

        const keyword = getPackageSearchKeyword();

        Array.from(packageCategoryTabs.querySelectorAll("[data-package-category]")).forEach(function (button) {
            const category = normalizePackageSearchValue(button.dataset.packageCategory || "");
            const active = category ? category === keyword : !keyword;

            button.classList.toggle("active", active);
            button.setAttribute("aria-pressed", String(active));
        });
    }

    function getFilteredPackageOptions() {
        const keyword = getPackageSearchKeyword();

        if (!keyword) {
            return packageOptions;
        }

        return packageOptions.filter(function (packageOption) {
            return normalizePackageSearchValue(packageOption.name).includes(keyword)
                || normalizePackageSearchValue(packageOption.id).includes(keyword);
        });
    }

    function getPackagePageCount() {
        return Math.max(1, Math.ceil(getFilteredPackageOptions().length / getPackagePageSize()));
    }

    function createPackageMessageRow(message, colSpan) {
        const row = document.createElement("tr");
        const cell = document.createElement("td");

        row.className = "empty-row";
        cell.colSpan = colSpan || 4;
        cell.textContent = message;
        row.appendChild(cell);

        return row;
    }

    function createPackageRow(packageOption) {
        const row = document.createElement("tr");
        const choiceCell = document.createElement("td");
        const checkbox = document.createElement("input");
        const nameCell = document.createElement("td");
        const nameWrapper = document.createElement("div");
        const expandButton = document.createElement("button");
        const chevron = document.createElement("span");
        const name = document.createElement("span");
        const accountingCell = document.createElement("td");
        const accountingValue = document.createElement("span");
        const accountingButton = document.createElement("button");
        const moneyCell = document.createElement("td");
        const isExpanded = expandedPackageIds.has(packageOption.id);
        const orderCount = Array.isArray(packageOption.orders) ? packageOption.orders.length : 0;

        row.dataset.rowId = packageOption.id;
        row.dataset.price = String(packageOption.price);
        row.classList.toggle("is-selected", packageOption.selected);
        row.classList.toggle("is-expanded", isExpanded);

        choiceCell.className = "choice-cell";
        checkbox.type = "checkbox";
        checkbox.name = "SelectedPackageIds";
        checkbox.value = packageOption.id;
        checkbox.dataset.orderTotal = String(packageOption.orderTotal);
        checkbox.checked = packageOption.selected;
        checkbox.setAttribute("aria-label", "選擇 " + packageOption.name);
        choiceCell.appendChild(checkbox);

        nameWrapper.className = "package-name-with-expand";
        expandButton.type = "button";
        expandButton.className = "package-row-expand";
        expandButton.value = packageOption.id;
        expandButton.dataset.packageExpand = "true";
        expandButton.setAttribute("aria-label", (isExpanded ? "收合 " : "展開 ") + packageOption.name + " 醫令細項");
        expandButton.setAttribute("aria-expanded", isExpanded ? "true" : "false");
        expandButton.title = orderCount > 0 ? "查看 " + orderCount + " 筆醫令細項" : "查看醫令細項";
        chevron.className = "suite-chevron";
        expandButton.appendChild(chevron);

        name.className = "item-name";
        name.textContent = packageOption.name;
        nameWrapper.append(expandButton, name);
        nameCell.appendChild(nameWrapper);

        accountingCell.className = "package-accounting-cell";
        accountingValue.className = "package-accounting-value";
        accountingValue.textContent = packageOption.selfPayAccountingUnitDisplay || "未選擇";
        accountingButton.type = "button";
        accountingButton.className = "package-accounting-change";
        accountingButton.value = packageOption.id;
        accountingButton.dataset.packageAccounting = "true";
        accountingButton.textContent = packageOption.selfPayAccountingUnit ? "更改" : "選擇";
        accountingButton.setAttribute("aria-label", (packageOption.selfPayAccountingUnit ? "更改 " : "選擇 ") + packageOption.name + " 優待身分");
        accountingCell.append(accountingValue, accountingButton);

        moneyCell.className = "money-cell";
        moneyCell.textContent = packageOption.priceText || formatCurrency(packageOption.price);

        row.append(choiceCell, nameCell, accountingCell, moneyCell);

        return row;
    }

    function createPackageDetailRow(packageOption) {
        const orders = Array.isArray(packageOption.orders) ? packageOption.orders : [];
        const row = document.createElement("tr");
        const cell = document.createElement("td");
        const box = document.createElement("div");
        const note = document.createElement("p");
        const info = document.createElement("span");
        const table = document.createElement("table");
        const thead = document.createElement("thead");
        const headRow = document.createElement("tr");
        const tbody = document.createElement("tbody");

        row.className = "suite-detail-row appointment-package-detail-row";
        cell.colSpan = 4;
        box.className = "suite-detail-box";

        info.className = "suite-info-dot";
        info.textContent = "i";
        note.append(info, "套組醫令僅供檢視；預約收費以套組金額為準。");

        ["順序", "醫令代碼", "醫令名稱", "分類"].forEach(function (label) {
            const headCell = document.createElement("th");
            headCell.textContent = label;
            headRow.appendChild(headCell);
        });
        thead.appendChild(headRow);

        if (orders.length === 0) {
            const emptyRow = document.createElement("tr");
            const emptyCell = document.createElement("td");
            emptyCell.colSpan = 4;
            emptyCell.textContent = "此套組尚無醫令細項";
            emptyRow.appendChild(emptyCell);
            tbody.appendChild(emptyRow);
        } else {
            orders.forEach(function (order, index) {
                const detailRow = document.createElement("tr");
                const indexCell = document.createElement("td");
                const codeCell = document.createElement("td");
                const nameCell = document.createElement("td");
                const categoryCell = document.createElement("td");

                indexCell.textContent = String(index + 1);
                codeCell.textContent = order.code || "--";
                nameCell.textContent = order.name || order.code || "--";
                categoryCell.textContent = order.category || "--";
                detailRow.append(indexCell, codeCell, nameCell, categoryCell);
                tbody.appendChild(detailRow);
            });
        }

        table.append(thead, tbody);
        box.append(note, table);
        cell.appendChild(box);
        row.appendChild(cell);

        return row;
    }

    function normalizePackageOrder(order) {
        return {
            code: String(order && (order.code || order.Code || order.checkNo || order.CheckNo) ? order.code || order.Code || order.checkNo || order.CheckNo : "").trim(),
            name: String(order && (order.name || order.Name || order.reportName || order.ReportName) ? order.name || order.Name || order.reportName || order.ReportName : "").trim(),
            category: String(order && (order.category || order.Category || order.itemCategory || order.ItemCategory) ? order.category || order.Category || order.itemCategory || order.ItemCategory : "").trim()
        };
    }
    function normalizePackageOption(packageOption, index) {
        const id = String(packageOption && (packageOption.id || packageOption.Id || packageOption.lisItemName || packageOption.LisItemName || packageOption.schemeNo || packageOption.SchemeNo) ? packageOption.id || packageOption.Id || packageOption.lisItemName || packageOption.LisItemName || packageOption.schemeNo || packageOption.SchemeNo : index + 1).trim();
        const name = String(packageOption && (packageOption.name || packageOption.Name) ? packageOption.name || packageOption.Name : id).trim();
        const rawPrice = packageOption && packageOption.price !== undefined ? packageOption.price : packageOption && packageOption.Price;
        const rawPriceText = packageOption && (packageOption.priceText || packageOption.PriceText) ? packageOption.priceText || packageOption.PriceText : "";
        const price = rawPrice !== undefined ? parseCurrency(rawPrice) : parseCurrency(rawPriceText);
        const rawOrders = packageOption && (packageOption.orders || packageOption.Orders);
        const rawOrderTotal = packageOption && packageOption.orderTotal !== undefined
            ? packageOption.orderTotal
            : packageOption && packageOption.OrderTotal;
        const selfPayAccountingUnit = String(packageOption && (packageOption.selfPayAccountingUnit || packageOption.SelfPayAccountingUnit) ? packageOption.selfPayAccountingUnit || packageOption.SelfPayAccountingUnit : "00").trim() || "00";
        const selfPayAccountingUnitDisplay = String(packageOption && (packageOption.selfPayAccountingUnitDisplay || packageOption.SelfPayAccountingUnitDisplay) ? packageOption.selfPayAccountingUnitDisplay || packageOption.SelfPayAccountingUnitDisplay : "").trim();

        return {
            id: id,
            orderTotal: Math.max(0, Number.parseInt(rawOrderTotal, 10) || 0),
            level: String(packageOption && (packageOption.level || packageOption.Level) ? packageOption.level || packageOption.Level : "").trim(),
            name: name,
            selfPayAccountingUnit: selfPayAccountingUnit,
            selfPayAccountingUnitDisplay: selfPayAccountingUnitDisplay || (selfPayAccountingUnit === "00" ? "[00] 無優待" : selfPayAccountingUnit),
            price: Number.isFinite(price) ? price : 0,
            priceText: rawPriceText ? String(rawPriceText) : formatCurrency(price),
            selected: Boolean(packageOption && (packageOption.selected || packageOption.isSelected || packageOption.IsSelected)),
            orders: Array.isArray(rawOrders)
                ? rawOrders.map(normalizePackageOrder).filter(function (order) {
                    return order.code || order.name;
                })
                : []
        };
    }

    function readInitialPackageOptions() {
        if (initialPackageOptions !== null) {
            return initialPackageOptions;
        }

        if (!initialPackageDataElement) {
            initialPackageOptions = [];
            return initialPackageOptions;
        }

        try {
            const payload = JSON.parse(initialPackageDataElement.textContent || "[]");

            initialPackageOptions = Array.isArray(payload)
                ? payload.map(function (packageOption, index) {
                    const normalizedPackage = normalizePackageOption(packageOption, index);
                    normalizedPackage.initialOrder = index;
                    return normalizedPackage;
                }).filter(function (packageOption) {
                    return packageOption.id && packageOption.name;
                })
                : [];
        } catch (error) {
            initialPackageOptions = [];
        }

        return initialPackageOptions;
    }

    function normalizePackageMatchValue(value) {
        return String(value || "")
            .normalize("NFKC")
            .replace(/\s+/g, "")
            .trim()
            .toLocaleLowerCase();
    }

    function isSamePackageOption(packageOption, initialPackage) {
        const packageId = String(packageOption.id || "").trim();
        const initialId = String(initialPackage.id || "").trim();
        const packageName = normalizePackageMatchValue(packageOption.name);
        const initialName = normalizePackageMatchValue(initialPackage.name);
        const hasSameName = packageName && packageName === initialName;
        const hasSameAmount = Number(packageOption.price) === Number(initialPackage.price);

        return (packageId && initialId && packageId === initialId)
            || (hasSameName && hasSameAmount)
            || hasSameName;
    }

    function applyInitialPackageSelections(loadedPackages) {
        const initialPackages = readInitialPackageOptions();

        if (!shouldApplyInitialPackageSelections || initialPackages.length === 0) {
            loadedPackages.forEach(function (packageOption) {
                packageOption.selected = false;
            });

            return loadedPackages;
        }

        const remainingInitialPackages = initialPackages.slice();
        const selectedPackages = [];
        const unselectedPackages = [];

        loadedPackages.forEach(function (packageOption) {
            const matchIndex = remainingInitialPackages.findIndex(function (initialPackage) {
                return isSamePackageOption(packageOption, initialPackage);
            });

            if (matchIndex >= 0) {
                const initialPackage = remainingInitialPackages[matchIndex];
                packageOption.selected = true;
                packageOption.initialOrder = initialPackage.initialOrder;
                packageOption.selfPayAccountingUnit = initialPackage.selfPayAccountingUnit;
                packageOption.selfPayAccountingUnitDisplay = initialPackage.selfPayAccountingUnitDisplay;
                packageOption.orderTotal = initialPackage.orderTotal;
                selectedPackages.push(packageOption);
                remainingInitialPackages.splice(matchIndex, 1);
                return;
            }

            packageOption.selected = false;
            unselectedPackages.push(packageOption);
        });

        remainingInitialPackages.forEach(function (initialPackage) {
            initialPackage.selected = true;
            selectedPackages.push(initialPackage);
        });

        selectedPackages.sort(function (left, right) {
            return (left.initialOrder || 0) - (right.initialOrder || 0);
        });

        return selectedPackages.concat(unselectedPackages);
    }

    function appendPackagePageButton(pageNumber) {
        if (!packagePagination) {
            return;
        }

        const button = document.createElement("button");
        button.type = "button";
        button.textContent = String(pageNumber);
        button.classList.toggle("active", pageNumber === currentPackagePage);

        if (pageNumber === currentPackagePage) {
            button.setAttribute("aria-current", "page");
        }

        button.addEventListener("click", function () {
            currentPackagePage = pageNumber;
            renderPackagePage();
        });

        packagePagination.appendChild(button);
    }

    function appendPackagePageEllipsis() {
        if (!packagePagination) {
            return;
        }

        const ellipsis = document.createElement("span");
        ellipsis.className = "pagination-ellipsis";
        ellipsis.textContent = "...";
        packagePagination.appendChild(ellipsis);
    }

    function renderPackagePagination(pageCount) {
        if (!packagePagination) {
            return;
        }

        const totalRows = getFilteredPackageOptions().length;
        packagePagination.innerHTML = "";

        const previousButton = document.createElement("button");
        previousButton.type = "button";
        previousButton.textContent = "‹";
        previousButton.setAttribute("aria-label", "上一頁");
        previousButton.disabled = currentPackagePage <= 1 || totalRows === 0;
        previousButton.addEventListener("click", function () {
            currentPackagePage -= 1;
            renderPackagePage();
        });
        packagePagination.appendChild(previousButton);

        getVisiblePaginationItems(currentPackagePage, pageCount).forEach(function (pageItem) {
            if (typeof pageItem === "number") {
                appendPackagePageButton(pageItem);
                return;
            }

            appendPackagePageEllipsis();
        });

        const nextButton = document.createElement("button");
        nextButton.type = "button";
        nextButton.textContent = "›";
        nextButton.setAttribute("aria-label", "下一頁");
        nextButton.disabled = currentPackagePage >= pageCount || totalRows === 0;
        nextButton.addEventListener("click", function () {
            currentPackagePage += 1;
            renderPackagePage();
        });
        packagePagination.appendChild(nextButton);
    }

    function renderPackagePage(emptyMessage) {
        if (!packageBody) {
            return;
        }

        const pageSize = getPackagePageSize();
        const filteredPackageOptions = getFilteredPackageOptions();
        const totalRows = filteredPackageOptions.length;
        const pageCount = getPackagePageCount();

        currentPackagePage = Math.min(Math.max(currentPackagePage, 1), pageCount);
        packageBody.innerHTML = "";

        if (totalRows === 0) {
            const keyword = getPackageSearchKeyword();
            const message = keyword && packageOptions.length > 0
                ? "找不到符合「" + keyword + "」的套組"
                : emptyMessage || packageEmptyMessage || "尚無可選套餐";

            packageBody.appendChild(createPackageMessageRow(message));

            if (packagePageInfo) {
                packagePageInfo.textContent = "顯示 0 筆，共 0 筆";
            }

            renderPackagePagination(pageCount);
            syncPackageCategoryButtons();
            syncPackageSelectAllState();
            updatePackageAccountingBulkState();
            updateBillingSummary();
            return;
        }

        const startIndex = (currentPackagePage - 1) * pageSize;
        const endIndex = Math.min(startIndex + pageSize, totalRows);

        filteredPackageOptions.slice(startIndex, endIndex).forEach(function (packageOption) {
            packageBody.appendChild(createPackageRow(packageOption));

            if (expandedPackageIds.has(packageOption.id)) {
                packageBody.appendChild(createPackageDetailRow(packageOption));
            }
        });

        if (packagePageInfo) {
            packagePageInfo.textContent = "顯示第 " + (startIndex + 1) + " - " + endIndex + " 筆，共 " + totalRows + " 筆";
        }

        renderPackagePagination(pageCount);
        syncPackageCategoryButtons();
        syncPackageSelectAllState();
        updatePackageAccountingBulkState();
        updateBillingSummary();
    }

    function syncPackageSelectAllState() {
        if (!packageSelectAllCheckbox) {
            return;
        }

        const filteredPackages = getFilteredPackageOptions();
        const selectedCount = filteredPackages.filter(function (packageOption) {
            return packageOption.selected;
        }).length;

        packageSelectAllCheckbox.disabled = filteredPackages.length === 0;
        packageSelectAllCheckbox.checked = filteredPackages.length > 0
            && selectedCount === filteredPackages.length;
        packageSelectAllCheckbox.indeterminate = selectedCount > 0
            && selectedCount < filteredPackages.length;
    }
    function setPackageMessage(message) {
        packageOptions = [];
        expandedPackageIds.clear();
        packageEmptyMessage = message || "尚無可選套餐";
        currentPackagePage = 1;
        renderPackageCategoryTabs();
        renderPackagePage(message);
    }

    function loadProjectPackages(projectNo) {
        const normalizedProjectNo = String(projectNo || "").trim();

        packageLoadController?.abort();
        packageLoadController = null;
        packageLoadPromise = Promise.resolve();
        packageLoadError = "";

        if (!packageBody) {
            return;
        }

        if (!normalizedProjectNo) {
            setPackageMessage("請先選擇健檢專案 / 方案");
            setQuestionnaireMessage("請先選擇健檢專案 / 方案");
            return;
        }

        if (!packageUrl) {
            packageLoadError = "缺少套組查詢網址，無法確認已選套組。";
            setPackageMessage("缺少套組查詢網址");
            return;
        }

        const requestController = new AbortController();
        packageLoadController = requestController;
        setPackageMessage("套組資料載入中...");
        setQuestionnaireMessage("問卷資料載入中...");

        const requestUrl = new URL(packageUrl, window.location.href);
        requestUrl.searchParams.set("projectNo", normalizedProjectNo);

        packageLoadPromise = fetch(requestUrl.toString(), {
            headers: {
                "Accept": "application/json"
            },
            signal: requestController.signal
        })
            .then(function (response) {
                if (!response.ok) {
                    throw new Error("Package request failed");
                }

                return response.json();
            })
            .then(function (payload) {
                if (requestController.signal.aborted) return;
                if (!Array.isArray(payload?.packages) || !Array.isArray(payload?.questionnaires)) {
                    throw new Error("Invalid package response");
                }
                const loadedPackages = Array.isArray(payload.packages)
                    ? payload.packages.map(normalizePackageOption).filter(function (packageOption) {
                        return packageOption.id && packageOption.name;
                    })
                    : [];
                const loadedQuestionnaires = Array.isArray(payload.questionnaires)
                    ? payload.questionnaires.map(normalizeQuestionnaireOption).filter(function (questionnaire) {
                        return questionnaire.id && questionnaire.name;
                    })
                    : [];
                packageOptions = applyInitialPackageSelections(loadedPackages);
                expandedPackageIds.clear();
                questionnaireOptions = applyInitialQuestionnaireSelections(loadedQuestionnaires);
                shouldApplyInitialPackageSelections = false;
                shouldApplyInitialQuestionnaireSelections = false;
                packageEmptyMessage = "此專案尚無套組資料";
                currentPackagePage = 1;
                currentQuestionnairePage = 1;
                if (questionnaireSearchInput) {
                    questionnaireSearchInput.value = "";
                }
                renderPackageCategoryTabs();
                renderPackagePage(packageEmptyMessage);
                renderQuestionnaireCategoryTabs();
                renderQuestionnairePage("此專案尚無問卷資料");
            })
            .catch(function (error) {
                if (error.name === "AbortError" || requestController.signal.aborted) {
                    return;
                }

                packageLoadError = "套組資料載入失敗，無法確認已選套組，請重新載入後再試。";
                setPackageMessage("套組資料載入失敗，請稍後再試");
                setQuestionnaireMessage("問卷資料載入失敗，請稍後再試");
            });
    }

    function shouldShowQuestionnaireSection() {
        return Boolean(projectSelect && String(projectSelect.value || "").trim());
    }

    function clearQuestionnaireSelections() {
        questionnaireOptions.forEach(function (questionnaire) {
            questionnaire.selected = false;
        });

        if (questionnaireSearchInput) {
            questionnaireSearchInput.value = "";
        }

        currentQuestionnairePage = 1;
    }

    function syncQuestionnaireSection() {
        if (!questionnaireSection) {
            return;
        }

        const shouldShow = shouldShowQuestionnaireSection();
        questionnaireSection.hidden = false;

        if (!shouldShow) {
            clearQuestionnaireSelections();
        }

        renderQuestionnaireCategoryTabs();
        renderQuestionnairePage(shouldShow ? "尚無問卷資料" : "請先選擇健檢專案 / 方案");
    }


    function setQuestionnaireOptions(nextOptions, emptyMessage) {
        questionnaireOptions = nextOptions;

        if (questionnaireSearchInput) {
            questionnaireSearchInput.value = "";
        }

        currentQuestionnairePage = 1;
        renderQuestionnaireCategoryTabs();
        renderQuestionnairePage(emptyMessage || "尚無問卷資料");
    }

    function setQuestionnaireMessage(message) {
        setQuestionnaireOptions([], message);
    }
    function getQuestionnairePageSize() {
        const pageSize = Number.parseInt(questionnairePageSizeSelect ? questionnairePageSizeSelect.value : "5", 10);
        return Number.isFinite(pageSize) && pageSize > 0 ? pageSize : 5;
    }

    function normalizeQuestionnaireOption(questionnaire, index) {
        const id = String(questionnaire && (questionnaire.id || questionnaire.Id || questionnaire.categoryNo || questionnaire.CategoryNo) ? questionnaire.id || questionnaire.Id || questionnaire.categoryNo || questionnaire.CategoryNo : index + 1).trim();
        const name = String(questionnaire && (questionnaire.name || questionnaire.Name) ? questionnaire.name || questionnaire.Name : id).trim();
        const firstChar = String(questionnaire && (questionnaire.firstChar || questionnaire.FirstChar) ? questionnaire.firstChar || questionnaire.FirstChar : Array.from(name).slice(0, 1).join("")).trim();
        const rawPrice = questionnaire && questionnaire.price !== undefined ? questionnaire.price : questionnaire && questionnaire.Price;
        const rawPriceText = questionnaire && (questionnaire.priceText || questionnaire.PriceText) ? questionnaire.priceText || questionnaire.PriceText : "";
        const price = rawPrice !== undefined ? parseCurrency(rawPrice) : parseCurrency(rawPriceText);

        return {
            id: id,
            name: name,
            firstChar: firstChar,
            price: Number.isFinite(price) ? price : 0,
            priceText: rawPriceText ? String(rawPriceText) : formatEditableAmount(price),
            selected: Boolean(questionnaire && (questionnaire.selected || questionnaire.isSelected || questionnaire.IsSelected))
        };
    }

    function readQuestionnaireOptions() {
        if (!questionnaireDataElement) {
            return [];
        }

        try {
            const payload = JSON.parse(questionnaireDataElement.textContent || "[]");

            return Array.isArray(payload)
                ? payload.map(normalizeQuestionnaireOption).filter(function (questionnaire) {
                    return questionnaire.id && questionnaire.name;
                })
                : [];
        } catch (error) {
            return [];
        }
    }


    function cloneQuestionnaireOption(questionnaire) {
        return {
            id: questionnaire.id,
            name: questionnaire.name,
            firstChar: questionnaire.firstChar,
            price: questionnaire.price,
            priceText: questionnaire.priceText,
            selected: questionnaire.selected,
            initialOrder: questionnaire.initialOrder
        };
    }

    function readInitialQuestionnaireOptions() {
        if (initialQuestionnaireOptions !== null) {
            return initialQuestionnaireOptions.map(cloneQuestionnaireOption);
        }

        initialQuestionnaireOptions = readQuestionnaireOptions().map(function (questionnaire, index) {
            questionnaire.initialOrder = index + 1;
            return questionnaire;
        });

        return initialQuestionnaireOptions.map(cloneQuestionnaireOption);
    }

    function normalizeQuestionnaireMatchValue(value) {
        return String(value || "")
            .normalize("NFKC")
            .replace(/\s+/g, "")
            .trim()
            .toLocaleLowerCase();
    }

    function isSameQuestionnaireOption(questionnaire, initialQuestionnaire) {
        const questionnaireId = String(questionnaire.id || "").trim();
        const initialId = String(initialQuestionnaire.id || "").trim();
        const questionnaireName = normalizeQuestionnaireMatchValue(questionnaire.name);
        const initialName = normalizeQuestionnaireMatchValue(initialQuestionnaire.name);
        const hasSameName = questionnaireName && questionnaireName === initialName;
        const hasSameAmount = Number(questionnaire.price) === Number(initialQuestionnaire.price);

        return (questionnaireId && initialId && questionnaireId === initialId)
            || (hasSameName && hasSameAmount)
            || hasSameName;
    }

    function applyInitialQuestionnaireSelections(loadedQuestionnaires) {
        const initialQuestionnaires = readInitialQuestionnaireOptions()
            .filter(function (questionnaire) { return questionnaire.selected; });

        if (!shouldApplyInitialQuestionnaireSelections || initialQuestionnaires.length === 0) {
            loadedQuestionnaires.forEach(function (questionnaire) {
                questionnaire.selected = true;
            });

            return loadedQuestionnaires;
        }

        const remainingInitialQuestionnaires = initialQuestionnaires.slice();
        const selectedQuestionnaires = [];
        const unselectedQuestionnaires = [];

        loadedQuestionnaires.forEach(function (questionnaire) {
            const matchIndex = remainingInitialQuestionnaires.findIndex(function (initialQuestionnaire) {
                return isSameQuestionnaireOption(questionnaire, initialQuestionnaire);
            });

            if (matchIndex >= 0) {
                const initialQuestionnaire = remainingInitialQuestionnaires[matchIndex];
                questionnaire.selected = true;
                questionnaire.initialOrder = initialQuestionnaire.initialOrder;
                selectedQuestionnaires.push(questionnaire);
                remainingInitialQuestionnaires.splice(matchIndex, 1);
                return;
            }

            questionnaire.selected = false;
            unselectedQuestionnaires.push(questionnaire);
        });

        remainingInitialQuestionnaires.forEach(function (initialQuestionnaire) {
            initialQuestionnaire.selected = true;
            selectedQuestionnaires.push(initialQuestionnaire);
        });

        selectedQuestionnaires.sort(function (left, right) {
            return (left.initialOrder || 0) - (right.initialOrder || 0);
        });

        return selectedQuestionnaires.concat(unselectedQuestionnaires);
    }
    function normalizeQuestionnaireSearchValue(value) {
        return String(value || "")
            .normalize("NFKC")
            .replace(/\s+/g, "")
            .trim()
            .toLocaleLowerCase();
    }

    function getQuestionnaireSearchKeyword() {
        const keyword = normalizeQuestionnaireSearchValue(questionnaireSearchInput ? questionnaireSearchInput.value : "");
        return keyword === normalizeQuestionnaireSearchValue("全部") ? "" : keyword;
    }

    function questionnaireCategorySearchValue(value) {
        return Array.from(String(value || "").trim()).slice(0, 2).join("");
    }

    function questionnaireCategoryKey(questionnaire) {
        return questionnaireCategorySearchValue(questionnaire.name || questionnaire.id);
    }

    function createQuestionnaireCategoryButton(value, label, index) {
        const button = document.createElement("button");
        const icon = document.createElement("span");
        const text = document.createElement("span");

        button.type = "button";
        button.className = packageCategoryTone(index);
        button.dataset.questionnaireCategory = value;
        button.setAttribute("aria-pressed", "false");

        icon.className = "package-category-icon " + (value ? "sample" : "stethoscope");
        icon.setAttribute("aria-hidden", "true");
        text.textContent = label;

        button.append(icon, text);
        return button;
    }

    function renderQuestionnaireCategoryTabs() {
        if (!questionnaireCategoryTabs) {
            return;
        }

        const categories = shouldShowQuestionnaireSection()
            ? Array.from(new Set(questionnaireOptions
            .map(questionnaireCategoryKey)
            .filter(function (value) { return value; })))
                .sort(function (left, right) { return left.localeCompare(right, "zh-Hant"); })
            : [];

        questionnaireCategoryTabs.innerHTML = "";
        if (!shouldShowQuestionnaireSection()) {
            return;
        }

        questionnaireCategoryTabs.appendChild(createQuestionnaireCategoryButton("", "全部", 0));

        categories.forEach(function (category, index) {
            questionnaireCategoryTabs.appendChild(createQuestionnaireCategoryButton(category, category, index + 1));
        });

        syncQuestionnaireCategoryButtons();
    }

    function syncQuestionnaireCategoryButtons() {
        if (!questionnaireCategoryTabs) {
            return;
        }

        const keyword = getQuestionnaireSearchKeyword();

        Array.from(questionnaireCategoryTabs.querySelectorAll("[data-questionnaire-category]")).forEach(function (button) {
            const category = normalizeQuestionnaireSearchValue(button.dataset.questionnaireCategory || "");
            const active = category ? category === keyword : !keyword;

            button.classList.toggle("active", active);
            button.setAttribute("aria-pressed", String(active));
        });
    }

    function matchesQuestionnaireSearch(questionnaire, keyword) {
        if (!keyword) {
            return true;
        }

        return [questionnaire.name, questionnaire.id, questionnaire.firstChar, questionnaireCategoryKey(questionnaire)]
            .some(function (value) {
                const normalizedValue = normalizeQuestionnaireSearchValue(value);
                return normalizedValue.includes(keyword) || isSubsequenceMatch(normalizedValue, keyword);
            });
    }

    function getFilteredQuestionnaireOptions() {
        if (!shouldShowQuestionnaireSection()) {
            return [];
        }

        const keyword = getQuestionnaireSearchKeyword();

        if (!keyword) {
            return questionnaireOptions;
        }

        return questionnaireOptions.filter(function (questionnaire) {
            return matchesQuestionnaireSearch(questionnaire, keyword);
        });
    }

    function getQuestionnairePageCount() {
        return Math.max(1, Math.ceil(getFilteredQuestionnaireOptions().length / getQuestionnairePageSize()));
    }

    function createQuestionnaireRow(questionnaire) {
        const row = document.createElement("tr");
        const choiceCell = document.createElement("td");
        const checkbox = document.createElement("input");
        const nameCell = document.createElement("td");
        const name = document.createElement("span");
        const moneyCell = document.createElement("td");

        row.dataset.rowId = questionnaire.id;
        row.dataset.price = String(questionnaire.price);
        row.classList.toggle("is-selected", questionnaire.selected);

        choiceCell.className = "choice-cell";
        checkbox.type = "checkbox";
        checkbox.name = "SelectedQuestionnaireIds";
        checkbox.value = questionnaire.id;
        checkbox.checked = questionnaire.selected;
        checkbox.setAttribute("aria-label", "選擇 " + questionnaire.name);
        choiceCell.appendChild(checkbox);

        name.className = "item-name";
        name.textContent = questionnaire.name;
        nameCell.appendChild(name);

        moneyCell.className = "money-cell";
        moneyCell.textContent = questionnaire.priceText || formatEditableAmount(questionnaire.price);

        row.append(choiceCell, nameCell, moneyCell);

        return row;
    }

    function appendQuestionnairePageButton(pageNumber) {
        if (!questionnairePagination) {
            return;
        }

        const button = document.createElement("button");
        button.type = "button";
        button.textContent = String(pageNumber);
        button.classList.toggle("active", pageNumber === currentQuestionnairePage);

        if (pageNumber === currentQuestionnairePage) {
            button.setAttribute("aria-current", "page");
        }

        button.addEventListener("click", function () {
            currentQuestionnairePage = pageNumber;
            renderQuestionnairePage();
        });

        questionnairePagination.appendChild(button);
    }

    function appendQuestionnairePageEllipsis() {
        if (!questionnairePagination) {
            return;
        }

        const ellipsis = document.createElement("span");
        ellipsis.className = "pagination-ellipsis";
        ellipsis.textContent = "...";
        questionnairePagination.appendChild(ellipsis);
    }

    function renderQuestionnairePagination(pageCount) {
        if (!questionnairePagination) {
            return;
        }

        const totalRows = getFilteredQuestionnaireOptions().length;
        questionnairePagination.innerHTML = "";

        const previousButton = document.createElement("button");
        previousButton.type = "button";
        previousButton.textContent = "‹";
        previousButton.setAttribute("aria-label", "上一頁");
        previousButton.disabled = currentQuestionnairePage <= 1 || totalRows === 0;
        previousButton.addEventListener("click", function () {
            currentQuestionnairePage -= 1;
            renderQuestionnairePage();
        });
        questionnairePagination.appendChild(previousButton);

        getVisiblePaginationItems(currentQuestionnairePage, pageCount).forEach(function (pageItem) {
            if (typeof pageItem === "number") {
                appendQuestionnairePageButton(pageItem);
                return;
            }

            appendQuestionnairePageEllipsis();
        });

        const nextButton = document.createElement("button");
        nextButton.type = "button";
        nextButton.textContent = "›";
        nextButton.setAttribute("aria-label", "下一頁");
        nextButton.disabled = currentQuestionnairePage >= pageCount || totalRows === 0;
        nextButton.addEventListener("click", function () {
            currentQuestionnairePage += 1;
            renderQuestionnairePage();
        });
        questionnairePagination.appendChild(nextButton);
    }

    function renderQuestionnairePage(emptyMessage) {
        if (!questionnaireBody) {
            return;
        }

        const pageSize = getQuestionnairePageSize();
        const filteredQuestionnaires = getFilteredQuestionnaireOptions();
        const totalRows = filteredQuestionnaires.length;
        const pageCount = getQuestionnairePageCount();

        currentQuestionnairePage = Math.min(Math.max(currentQuestionnairePage, 1), pageCount);
        questionnaireBody.innerHTML = "";

        if (totalRows === 0) {
            const keyword = getQuestionnaireSearchKeyword();
            const message = !shouldShowQuestionnaireSection()
                ? "請先選擇健檢專案 / 方案"
                : keyword && questionnaireOptions.length > 0
                ? "找不到符合「" + keyword + "」的問卷"
                : emptyMessage || "尚無問卷資料";

            questionnaireBody.appendChild(createPackageMessageRow(message));

            if (questionnairePageInfo) {
                questionnairePageInfo.textContent = "顯示 0 筆，共 0 筆";
            }

            renderQuestionnairePagination(pageCount);
            syncQuestionnaireCategoryButtons();
            return;
        }

        const startIndex = (currentQuestionnairePage - 1) * pageSize;
        const endIndex = Math.min(startIndex + pageSize, totalRows);

        filteredQuestionnaires.slice(startIndex, endIndex).forEach(function (questionnaire) {
            questionnaireBody.appendChild(createQuestionnaireRow(questionnaire));
        });

        if (questionnairePageInfo) {
            questionnairePageInfo.textContent = "顯示第 " + (startIndex + 1) + " - " + endIndex + " 筆，共 " + totalRows + " 筆";
        }

        renderQuestionnairePagination(pageCount);
        syncQuestionnaireCategoryButtons();
    }

    function getOtherSuitePageSize() {
        const pageSize = Number.parseInt(otherSuitePageSizeSelect ? otherSuitePageSizeSelect.value : "5", 10);
        return Number.isFinite(pageSize) && pageSize > 0 ? pageSize : 5;
    }

    function getOtherSuitePageCount() {
        return Math.max(1, Math.ceil(otherSuiteOptions.length / getOtherSuitePageSize()));
    }

    function createOtherSuiteRow(otherSuite) {
        const row = document.createElement("tr");
        const nameCell = document.createElement("td");
        const codeCell = document.createElement("td");
        const name = document.createElement("span");
        const code = document.createElement("span");
        const accountingCell = document.createElement("td");
        const accountingValue = document.createElement("span");
        const accountingButton = document.createElement("button");
        const moneyCell = document.createElement("td");
        const actionCell = document.createElement("td");
        const deleteButton = document.createElement("button");

        row.dataset.rowId = otherSuite.id;
        row.dataset.price = String(otherSuite.price);

        name.className = "item-name";
        name.textContent = otherSuite.name;
        nameCell.appendChild(name);

        code.className = "item-code";
        code.textContent = otherSuite.code || "--";
        codeCell.appendChild(code);

        accountingCell.className = "other-suite-accounting-cell";
        accountingValue.className = "package-accounting-value";
        accountingValue.textContent = otherSuite.selfPayAccountingUnitDisplay || "[00] 無優待";
        accountingButton.type = "button";
        accountingButton.className = "package-accounting-change";
        accountingButton.value = otherSuite.id;
        accountingButton.dataset.otherSuiteAccounting = "true";
        accountingButton.textContent = "更改";
        accountingButton.setAttribute("aria-label", "更改 " + otherSuite.name + " 優待身分");
        accountingCell.append(accountingValue, accountingButton);

        moneyCell.className = "money-cell";
        moneyCell.textContent = otherSuite.priceText || formatCurrency(otherSuite.price);

        actionCell.className = "other-suite-action-cell";
        deleteButton.type = "button";
        deleteButton.className = "other-suite-row-delete";
        deleteButton.value = otherSuite.id;
        deleteButton.setAttribute("data-delete-other-suite", "");
        deleteButton.setAttribute("aria-label", "刪除 " + otherSuite.name);
        deleteButton.textContent = "刪除";
        actionCell.appendChild(deleteButton);

        row.append(nameCell, codeCell, accountingCell, moneyCell, actionCell);

        return row;
    }

    function normalizeOtherSuiteOption(suite) {
        const code = String(suite && (suite.code || suite.Code) ? suite.code || suite.Code : "").trim();
        const projectNo = String(suite && (suite.projectNo || suite.ProjectNo) ? suite.projectNo || suite.ProjectNo : "").trim();
        const id = String(suite && (suite.id || suite.Id) ? suite.id || suite.Id : (projectNo && code ? projectNo + "|" + code : code)).trim();
        const name = String(suite && (suite.name || suite.Name) ? suite.name || suite.Name : id).trim();
        const rawPrice = suite && suite.price !== undefined ? suite.price : suite && suite.Price;
        const rawPriceText = suite && (suite.priceText || suite.PriceText) ? suite.priceText || suite.PriceText : "";
        const price = Number(rawPrice !== undefined ? rawPrice : parseCurrency(rawPriceText));
        const selfPayAccountingUnit = String(suite && (suite.selfPayAccountingUnit || suite.SelfPayAccountingUnit) ? suite.selfPayAccountingUnit || suite.SelfPayAccountingUnit : "00").trim() || "00";
        const selfPayAccountingUnitDisplay = String(suite && (suite.selfPayAccountingUnitDisplay || suite.SelfPayAccountingUnitDisplay) ? suite.selfPayAccountingUnitDisplay || suite.SelfPayAccountingUnitDisplay : "").trim();

        return {
            id: id,
            projectNo: projectNo,
            projectName: String(suite && (suite.projectName || suite.ProjectName) ? suite.projectName || suite.ProjectName : "").trim(),
            name: name,
            code: code,
            selfPayAccountingUnit: selfPayAccountingUnit,
            selfPayAccountingUnitDisplay: selfPayAccountingUnitDisplay || (selfPayAccountingUnit === "00" ? "[00] 無優待" : selfPayAccountingUnit),
            price: Number.isFinite(price) ? price : 0,
            priceText: rawPriceText ? String(rawPriceText) : formatCurrency(price),
            selected: true
        };
    }

    function readInitialOtherSuiteOptions() {
        if (!initialOtherSuiteDataElement) {
            return [];
        }

        try {
            const payload = JSON.parse(initialOtherSuiteDataElement.textContent || "[]");

            return Array.isArray(payload)
                ? payload.map(normalizeOtherSuiteOption).filter(function (otherSuite) {
                    return otherSuite.id && otherSuite.name;
                })
                : [];
        } catch (error) {
            return [];
        }
    }

    function appendOtherSuitePageButton(pageNumber) {
        if (!otherSuitePagination) {
            return;
        }

        const button = document.createElement("button");
        button.type = "button";
        button.textContent = String(pageNumber);
        button.classList.toggle("active", pageNumber === currentOtherSuitePage);

        if (pageNumber === currentOtherSuitePage) {
            button.setAttribute("aria-current", "page");
        }

        button.addEventListener("click", function () {
            currentOtherSuitePage = pageNumber;
            renderOtherSuitePage();
        });

        otherSuitePagination.appendChild(button);
    }

    function appendOtherSuitePageEllipsis() {
        if (!otherSuitePagination) {
            return;
        }

        const ellipsis = document.createElement("span");
        ellipsis.className = "pagination-ellipsis";
        ellipsis.textContent = "...";
        otherSuitePagination.appendChild(ellipsis);
    }

    function renderOtherSuitePagination(pageCount) {
        if (!otherSuitePagination) {
            return;
        }

        otherSuitePagination.innerHTML = "";

        const previousButton = document.createElement("button");
        previousButton.type = "button";
        previousButton.textContent = "‹";
        previousButton.setAttribute("aria-label", "上一頁");
        previousButton.disabled = currentOtherSuitePage <= 1 || otherSuiteOptions.length === 0;
        previousButton.addEventListener("click", function () {
            currentOtherSuitePage -= 1;
            renderOtherSuitePage();
        });
        otherSuitePagination.appendChild(previousButton);

        getVisiblePaginationItems(currentOtherSuitePage, pageCount).forEach(function (pageItem) {
            if (typeof pageItem === "number") {
                appendOtherSuitePageButton(pageItem);
                return;
            }

            appendOtherSuitePageEllipsis();
        });

        const nextButton = document.createElement("button");
        nextButton.type = "button";
        nextButton.textContent = "›";
        nextButton.setAttribute("aria-label", "下一頁");
        nextButton.disabled = currentOtherSuitePage >= pageCount || otherSuiteOptions.length === 0;
        nextButton.addEventListener("click", function () {
            currentOtherSuitePage += 1;
            renderOtherSuitePage();
        });
        otherSuitePagination.appendChild(nextButton);
    }

    function renderOtherSuitePage(emptyMessage) {
        if (!otherSuiteBody) {
            return;
        }

        const pageSize = getOtherSuitePageSize();
        const totalRows = otherSuiteOptions.length;
        const pageCount = getOtherSuitePageCount();

        currentOtherSuitePage = Math.min(Math.max(currentOtherSuitePage, 1), pageCount);
        otherSuiteBody.innerHTML = "";

        if (totalRows === 0) {
            otherSuiteBody.appendChild(createPackageMessageRow(emptyMessage || "請先新增組套", 5));

            if (otherSuitePageInfo) {
                otherSuitePageInfo.textContent = "顯示 0 筆，共 0 筆";
            }

            renderOtherSuitePagination(pageCount);
            updateBillingSummary();
            return;
        }

        const startIndex = (currentOtherSuitePage - 1) * pageSize;
        const endIndex = Math.min(startIndex + pageSize, totalRows);

        otherSuiteOptions.slice(startIndex, endIndex).forEach(function (otherSuite) {
            otherSuiteBody.appendChild(createOtherSuiteRow(otherSuite));
        });

        if (otherSuitePageInfo) {
            otherSuitePageInfo.textContent = "顯示第 " + (startIndex + 1) + " - " + endIndex + " 筆，共 " + totalRows + " 筆";
        }

        renderOtherSuitePagination(pageCount);
        updateBillingSummary();
    }

    function addOtherSuite() {
        openOtherSuiteLookup();
    }

    function importOtherSuite(suite) {
        importOtherSuites([suite]);
    }

    function importOtherSuites(suites) {
        const incomingSuites = Array.isArray(suites)
            ? suites.map(normalizeOtherSuiteOption).filter(function (otherSuite) {
                return otherSuite.id && otherSuite.name;
            })
            : [];

        if (incomingSuites.length === 0) {
            return;
        }

        incomingSuites.forEach(function (otherSuite) {
            const existingIndex = otherSuiteOptions.findIndex(function (item) {
                const isSameProjectCode = item.code
                    && item.code === otherSuite.code
                    && item.projectNo
                    && otherSuite.projectNo
                    && item.projectNo === otherSuite.projectNo;

                return item.id === otherSuite.id || isSameProjectCode;
            });

            if (existingIndex >= 0) {
                otherSuiteOptions[existingIndex] = {
                    ...otherSuiteOptions[existingIndex],
                    ...otherSuite,
                    selfPayAccountingUnit: otherSuiteOptions[existingIndex].selfPayAccountingUnit || otherSuite.selfPayAccountingUnit || "00",
                    selfPayAccountingUnitDisplay: otherSuiteOptions[existingIndex].selfPayAccountingUnitDisplay || otherSuite.selfPayAccountingUnitDisplay || "[00] 無優待",
                    selected: true
                };
                return;
            }

            otherSuiteOptions.push(otherSuite);
        });

        shouldUseInitialExtraAmount = false;
        shouldPreserveInitialBilling = false;
        currentOtherSuitePage = 1;
        renderOtherSuitePage();
        closeOtherSuiteLookup();
    }

    function removeOtherSuiteById(id) {
        const normalizedId = String(id || "").trim();

        if (!normalizedId) {
            return;
        }

        otherSuiteOptions = otherSuiteOptions.filter(function (otherSuite) {
            return otherSuite.id !== normalizedId;
        });
        shouldUseInitialExtraAmount = false;
        shouldPreserveInitialBilling = false;
        currentOtherSuitePage = Math.min(currentOtherSuitePage, getOtherSuitePageCount());
        renderOtherSuitePage();
    }

    function normalizeLookupValue(value) {
        return String(value || "")
            .normalize("NFKC")
            .replace(/\s+/g, "")
            .trim()
            .toUpperCase();
    }

    function projectFilterValue(name) {
        const input = projectFilterInputs.find(function (candidate) {
            return candidate.dataset.projectFilter === name;
        });

        return normalizeLookupValue(input ? input.value : "");
    }

    function projectDateFilterValue(name) {
        const input = projectDateFilterInputs.find(function (candidate) {
            return candidate.dataset.projectDateFilter === name;
        });
        const digits = String(input ? input.value : "").replace(/\D/g, "");
        return digits.length === 8 ? Number(digits) : 0;
    }

    function projectRowDateValue(value) {
        const digits = String(value || "").replace(/\D/g, "");
        return digits.length === 8 ? Number(digits) : 0;
    }

    function isSubsequenceMatch(source, query) {
        if (!query) {
            return true;
        }

        let queryIndex = 0;

        for (let sourceIndex = 0; sourceIndex < source.length && queryIndex < query.length; sourceIndex += 1) {
            if (source[sourceIndex] === query[queryIndex]) {
                queryIndex += 1;
            }
        }

        return queryIndex === query.length;
    }

    function matchesLookupQuery(value, query) {
        const normalizedValue = normalizeLookupValue(value);

        if (!query) {
            return true;
        }

        return normalizedValue.includes(query) || isSubsequenceMatch(normalizedValue, query);
    }

    function renderProjectRows() {
        const projectNo = projectFilterValue("projectNo");
        const customerNo = projectFilterValue("customerNo");
        const customerName = projectFilterValue("customerName");
        const projectName = projectFilterValue("projectName");
        const requestedStart = projectDateFilterValue("start");
        const requestedEnd = projectDateFilterValue("end");
        const rangeStart = requestedStart && requestedEnd ? Math.min(requestedStart, requestedEnd) : (requestedStart || requestedEnd);
        const rangeEnd = requestedStart && requestedEnd ? Math.max(requestedStart, requestedEnd) : (requestedStart || requestedEnd);
        let visibleCount = 0;

        projectRows.forEach(function (row) {
            const matchesProjectNo = matchesLookupQuery(row.dataset.projectNo, projectNo);
            const matchesCustomerNo = matchesLookupQuery(row.dataset.customerNo, customerNo);
            const matchesCustomerName = matchesLookupQuery(row.dataset.customerName, customerName);
            const matchesProjectName = matchesLookupQuery(row.dataset.projectName, projectName);
            const projectStart = projectRowDateValue(row.dataset.checkDate);
            const matchesDateRange = !rangeStart || (projectStart > 0 && projectStart >= rangeStart && projectStart <= rangeEnd);
            const visible = matchesProjectNo && matchesCustomerNo && matchesCustomerName && matchesProjectName && matchesDateRange;

            row.hidden = !visible;
            if (visible) {
                visibleCount += 1;
            }
        });

        if (projectEmptyRow) {
            projectEmptyRow.hidden = visibleCount > 0;
        }
    }

    function createProjectLookupRow(project) {
        const row = document.createElement("tr");
        const values = [project.projectNo, project.customerNo, project.customerName, project.projectName, project.checkDate, project.endDate];
        row.dataset.projectRow = "";
        row.dataset.projectNo = project.projectNo || "";
        row.dataset.customerNo = project.customerNo || "";
        row.dataset.customerName = project.customerName || "";
        row.dataset.projectName = project.projectName || "";
        row.dataset.selfPayAccounting = project.selfPayAccountingUnit || "";
        row.dataset.checkDate = project.checkDate || "";
        row.dataset.endDate = project.endDate || "";

        const actionCell = document.createElement("td");
        const selectButton = document.createElement("button");
        selectButton.type = "button";
        selectButton.textContent = "選擇";
        selectButton.addEventListener("click", function () {
            setProject(project.projectNo || "", project.projectName || "", project.customerNo || "");
            closeProjectLookup();
        });
        actionCell.appendChild(selectButton);
        row.appendChild(actionCell);

        values.forEach(function (value) {
            const cell = document.createElement("td");
            cell.textContent = value || "";
            cell.title = value || "";
            row.appendChild(cell);
        });
        return row;
    }

    async function loadProjectLookupRows() {
        if (!projectLookupUrl || !projectResultsBody) {
            renderProjectRows();
            return;
        }

        const startInput = projectDateFilterInputs.find(function (input) { return input.dataset.projectDateFilter === "start"; });
        const endInput = projectDateFilterInputs.find(function (input) { return input.dataset.projectDateFilter === "end"; });
        const url = new URL(projectLookupUrl, window.location.href);
        url.searchParams.set("checkDateStart", startInput?.value || "");
        url.searchParams.set("checkDateEnd", endInput?.value || "");
        projectLookupController?.abort();
        projectLookupController = new AbortController();
        const defaultButtonText = projectSearchButton?.textContent || "查詢";

        if (projectSearchButton) {
            projectSearchButton.disabled = true;
            projectSearchButton.textContent = "查詢中...";
        }

        if (projectEmptyRow) {
            projectEmptyRow.hidden = false;
            const cell = projectEmptyRow.querySelector("td");
            if (cell) cell.textContent = "專案資料查詢中...";
        }

        try {
            const response = await fetch(url.toString(), {
                headers: { "X-Requested-With": "XMLHttpRequest" },
                signal: projectLookupController.signal
            });
            if (!response.ok) throw new Error("Project lookup failed");
            const result = await response.json();
            projectRows.forEach(function (row) { row.remove(); });
            (Array.isArray(result.projects) ? result.projects : []).forEach(function (project) {
                projectResultsBody.insertBefore(createProjectLookupRow(project), projectEmptyRow);
            });
            projectRows = Array.from(projectResultsBody.querySelectorAll("[data-project-row]"));
            if (startInput && result.checkDateStart) startInput.value = result.checkDateStart;
            if (endInput && result.checkDateEnd) endInput.value = result.checkDateEnd;
            const cell = projectEmptyRow?.querySelector("td");
            if (cell) cell.textContent = "查無符合條件的專案資料";
            renderProjectRows();
        } catch (error) {
            if (error.name === "AbortError") return;
            projectRows.forEach(function (row) { row.hidden = true; });
            if (projectEmptyRow) {
                projectEmptyRow.hidden = false;
                const cell = projectEmptyRow.querySelector("td");
                if (cell) cell.textContent = "專案資料查詢失敗，請稍後再試。";
            }
        } finally {
            if (projectSearchButton) {
                projectSearchButton.disabled = false;
                projectSearchButton.textContent = defaultButtonText;
            }
        }
    }

    function syncAddressFromMailing() {
        if (!mailingAddressInput || !addressInput || !sameAddressCheckbox || !sameAddressCheckbox.checked) {
            return;
        }

        addressInput.value = mailingAddressInput.value;
        addressInput.dispatchEvent(new Event("input", { bubbles: true }));
    }

    function importPatient(patient) {
        if (!patient) {
            return;
        }

        [
            "name",
            "sex",
            "age",
            "idNo",
            "birthDate",
            "chartNo",
            "healthCode",
            "phone",
            "mobile",
            "email",
            "contactName",
            "contactPhone",
            "contactRelationship",
            "familyNote",
            "mailingAddress",
            "address",
            "notificationNote",
            "allergy",
            "diet"
        ].forEach(function (field) {
            const value = patient[field] || (field === "healthCode" ? (healthCodeInput ? healthCodeInput.value : defaultHealthCode) : "");
            setFormValue(field, value);
            setTextValue(field, value);
        });

        const vipCheckbox = document.querySelector("input[name='IsVip']");
        if (vipCheckbox) {
            vipCheckbox.checked = patient.isVip === true;
            vipCheckbox.dispatchEvent(new Event("change", { bubbles: true }));
        }

        const employeeFamilyCheckbox = document.querySelector("input[name='IsEmployeeFamily']");
        if (employeeFamilyCheckbox) {
            employeeFamilyCheckbox.checked = patient.isEmployeeFamily === true;
            employeeFamilyCheckbox.dispatchEvent(new Event("change", { bubbles: true }));
        }

        if (patientPanel) {
            patientPanel.classList.remove("patient-imported");
            window.requestAnimationFrame(function () {
                patientPanel.classList.add("has-patient", "patient-imported");
            });
        }

        closeLookup();
    }

    openLookupButtons.forEach(function (button) {
        button.addEventListener("click", function () {
            openLookup(button);
        });
    });

    closeLookupButtons.forEach(function (button) {
        button.addEventListener("click", closeLookup);
    });

    appointmentTabs.forEach(function (tab, index) {
        tab.addEventListener("click", function () {
            activeAppointmentTab = tab.dataset.appointmentTab || "patient";
            updateAppointmentTabs();
        });

        tab.addEventListener("keydown", function (event) {
            if (event.key !== "ArrowLeft" && event.key !== "ArrowRight") {
                return;
            }

            event.preventDefault();
            const direction = event.key === "ArrowRight" ? 1 : -1;
            const nextIndex = (index + direction + appointmentTabs.length) % appointmentTabs.length;
            const nextTab = appointmentTabs[nextIndex];
            activeAppointmentTab = nextTab.dataset.appointmentTab || "patient";
            updateAppointmentTabs();
            nextTab.focus();
        });
    });

    if (typeof narrowAppointmentTabsQuery.addEventListener === "function") {
        narrowAppointmentTabsQuery.addEventListener("change", updateAppointmentTabs);
    } else if (typeof narrowAppointmentTabsQuery.addListener === "function") {
        narrowAppointmentTabsQuery.addListener(updateAppointmentTabs);
    }

    updateAppointmentTabs();

    if (healthCodeInput && !String(healthCodeInput.value || "").trim()) {
        loadHealthCode();
    }

    if (openProjectLookupButton) {
        openProjectLookupButton.addEventListener("click", function () {
            openProjectLookup();
        });
    }

    closeProjectLookupButtons.forEach(function (button) {
        button.addEventListener("click", closeProjectLookup);
    });

    closeSelfPayAccountingButtons.forEach(function (button) {
        button.addEventListener("click", closeSelfPayAccountingLookup);
    });

    selfPayAccountingFilters.forEach(function (input) {
        input.addEventListener("input", renderSelfPayAccountingRows);
        input.addEventListener("change", renderSelfPayAccountingRows);
    });

    clearSelfPayAccountingFiltersButton?.addEventListener("click", function () {
        selfPayAccountingFilters.forEach(function (input) { input.value = ""; });
        activeSelfPayAccountingPrefix = "";
        renderSelfPayAccountingTabs();
        renderSelfPayAccountingRows();
        selfPayAccountingFilters[0]?.focus();
    });

    packageAccountingAllQueryButton?.addEventListener("click", function () {
        openSelfPayAccountingLookup("*");
    });

    packageAccountingAllClearButton?.addEventListener("click", function () {
        activePackageAccountingId = "*";
        setActiveSelfPayAccountingUnit("00", "[00] 無優待");
        activePackageAccountingId = "";
    });

    document.querySelectorAll("[data-self-pay-accounting-select]").forEach(function (button) {
        button.addEventListener("click", function () {
            const accountingRow = button.closest("[data-self-pay-accounting-row]");
            const code = accountingRow?.dataset.accountingCode || "";
            const name = accountingRow?.dataset.accountingName || "";
            setActiveSelfPayAccountingUnit(code, code ? "[" + code + "] " + name : "");
            closeSelfPayAccountingLookup();
        });
    });

    selfPayAccountingRows.forEach(function (row) {
        row.addEventListener("dblclick", function () {
            const code = row.dataset.accountingCode || "";
            const name = row.dataset.accountingName || "";
            setActiveSelfPayAccountingUnit(code, code ? "[" + code + "] " + name : "");
            closeSelfPayAccountingLookup();
        });
    });

    document.querySelectorAll("[data-project-select-option]").forEach(function (button) {
        button.addEventListener("click", function () {
            setProject(button.dataset.projectNo || "", button.dataset.projectName || "", button.dataset.customerNo || "");
            closeProjectLookup();
        });
    });

    if (projectSelect) {
        projectSelect.addEventListener("change", function () {
            shouldApplyInitialPackageSelections = false;
            shouldApplyInitialQuestionnaireSelections = false;
            shouldUseInitialPackageAmount = false;
            shouldUseInitialExtraAmount = false;
            shouldPreserveInitialBilling = false;
            setSelectedProjectCustomerNo(projectSelect.value);
            loadProjectPackages(projectSelect.value);
        });

        setSelectedProjectCustomerNo(projectSelect.value);
        loadProjectPackages(projectSelect.value);
    } else {
        setPackageMessage("請先選擇健檢專案 / 方案");
    }

    if (otherSuiteBody) {
        otherSuiteOptions = readInitialOtherSuiteOptions();
        renderOtherSuitePage("請先新增組套");
    }

    if (questionnaireBody) {
        questionnaireOptions = readInitialQuestionnaireOptions();
        renderQuestionnaireCategoryTabs();
        syncQuestionnaireSection();
    }

    if (packagePageSizeSelect) {
        packagePageSizeSelect.addEventListener("change", function () {
            currentPackagePage = 1;
            renderPackagePage();
        });
    }

    if (packageSearchInput) {
        packageSearchInput.addEventListener("input", function () {
            currentPackagePage = 1;
            renderPackagePage();
        });
    }

    if (packageCategoryTabs) {
        initializePackageCategoryLayout();
        packageCategoryTabs.addEventListener("click", function (event) {
            const button = event.target.closest("[data-package-category]");

            if (!button || !packageCategoryTabs.contains(button)) {
                return;
            }

            if (packageSearchInput) {
                packageSearchInput.value = button.dataset.packageCategory || "";
                packageSearchInput.dispatchEvent(new Event("input", { bubbles: true }));
            } else {
                currentPackagePage = 1;
                renderPackagePage();
            }
        });
    }

    if (otherSuitePageSizeSelect) {
        otherSuitePageSizeSelect.addEventListener("change", function () {
            currentOtherSuitePage = 1;
            renderOtherSuitePage();
        });
    }

    if (questionnairePageSizeSelect) {
        questionnairePageSizeSelect.addEventListener("change", function () {
            currentQuestionnairePage = 1;
            renderQuestionnairePage();
        });
    }

    if (questionnaireSearchInput) {
        questionnaireSearchInput.addEventListener("input", function () {
            currentQuestionnairePage = 1;
            renderQuestionnairePage();
        });
    }

    if (questionnaireCategoryTabs) {
        questionnaireCategoryTabs.addEventListener("click", function (event) {
            const button = event.target.closest("[data-questionnaire-category]");

            if (!button || !questionnaireCategoryTabs.contains(button)) {
                return;
            }

            if (questionnaireSearchInput) {
                questionnaireSearchInput.value = button.dataset.questionnaireCategory || "";
                questionnaireSearchInput.dispatchEvent(new Event("input", { bubbles: true }));
            } else {
                currentQuestionnairePage = 1;
                renderQuestionnairePage();
            }
        });
    }

    if (otherSuiteAddButton) {
        otherSuiteAddButton.addEventListener("click", addOtherSuite);
    }

    if (consultingDoctorSelect) {
        consultingDoctorSelect.addEventListener("change", syncConsultingDoctorName);
    }

    syncConsultingDoctorName();
    syncCreateAppointmentSubmitAvailability();

    createAppointmentSubmitButtons.forEach(function (button) {
        button.addEventListener("click", function () {
            submitCreateAppointment(button);
        });
    });

    createAppointmentRegisterButton?.addEventListener("click", registerCurrentAppointment);

    if (createAppointmentResultClose) {
        createAppointmentResultClose.addEventListener("click", hideCreateAppointmentResult);
    }

    closeOtherSuiteLookupButtons.forEach(function (button) {
        button.addEventListener("click", closeOtherSuiteLookup);
    });

    billingAdjustmentInputs.forEach(function (input) {
        input.addEventListener("input", function () {
            shouldPreserveInitialBilling = false;
            updateBillingSummary();
        });
        input.addEventListener("focusout", function () {
            formatBillingInput(input);
            shouldPreserveInitialBilling = false;
            updateBillingSummary();
        });
    });

    if (packageBody) {
        packageBody.addEventListener("click", function (event) {
            const accountingButton = event.target.closest("[data-package-accounting]");

            if (accountingButton) {
                openSelfPayAccountingLookup(accountingButton.value);
                return;
            }

            const expandButton = event.target.closest("[data-package-expand]");

            if (!expandButton) {
                return;
            }

            const packageId = expandButton.value;

            if (expandedPackageIds.has(packageId)) {
                expandedPackageIds.delete(packageId);
            } else {
                expandedPackageIds.add(packageId);
            }

            renderPackagePage();
        });

        packageBody.addEventListener("change", function (event) {
            const input = event.target;

            if (!input.matches("input[type='checkbox'][name='SelectedPackageIds']")) {
                return;
            }

            const packageOption = packageOptions.find(function (item) {
                return item.id === input.value;
            });

            if (packageOption) {
                packageOption.selected = input.checked;
            }

            input.closest("tr")?.classList.toggle("is-selected", input.checked);
            if (packageOptions.some(function (item) { return item.selected; })) {
                packageTable?.removeAttribute("aria-invalid");
            }
            syncPackageSelectAllState();
            updatePackageAccountingBulkState();
            useCalculatedPackageAmount();
        });
    }

    packageSelectAllCheckbox?.addEventListener("change", function () {
        const selected = packageSelectAllCheckbox.checked;

        getFilteredPackageOptions().forEach(function (packageOption) {
            packageOption.selected = selected;
        });

        if (selected) {
            packageTable?.removeAttribute("aria-invalid");
        }

        renderPackagePage();
        useCalculatedPackageAmount();
    });
    if (otherSuiteBody) {
        otherSuiteBody.addEventListener("click", function (event) {
            const accountingButton = event.target.closest("[data-other-suite-accounting]");

            if (accountingButton) {
                openSelfPayAccountingLookup(accountingButton.value, "otherSuite");
                return;
            }

            const deleteButton = event.target.closest("[data-delete-other-suite]");

            if (deleteButton) {
                removeOtherSuiteById(deleteButton.value);
            }
        });

    }

    if (questionnaireBody) {
        questionnaireBody.addEventListener("change", function (event) {
            const input = event.target;

            if (!input.matches("input[type='checkbox'][name='SelectedQuestionnaireIds']")) {
                return;
            }

            const questionnaire = questionnaireOptions.find(function (item) {
                return item.id === input.value;
            });

            if (questionnaire) {
                questionnaire.selected = input.checked;
            }

            renderQuestionnairePage();
        });
    }

    projectSearchButton?.addEventListener("click", loadProjectLookupRows);

    projectFilterInputs.concat(projectDateFilterInputs).forEach(function (input) {
        input.addEventListener("keydown", function (event) {
            if (event.key === "Enter") {
                event.preventDefault();
                loadProjectLookupRows();
            }
        });
    });

    window.addEventListener("keydown", function (event) {
        if (event.key === "Escape" && !lookupModal.hidden) {
            closeLookup();
        }

        if (event.key === "Escape" && projectLookupModal && !projectLookupModal.hidden) {
            closeProjectLookup();
        }

        if (event.key === "Escape" && selfPayAccountingModal && !selfPayAccountingModal.hidden) {
            closeSelfPayAccountingLookup();
        }

        if (event.key === "Escape" && otherSuiteLookupModal && !otherSuiteLookupModal.hidden) {
            closeOtherSuiteLookup();
        }
    });

    window.addEventListener("message", function (event) {
        if (event.origin !== window.location.origin || !event.data) {
            return;
        }

        if (event.data.type === "healthsys:patient-selected") {
            importPatient(event.data.patient);
        }

        if (event.data.type === "healthsys:patient-lookup-close") {
            closeLookup();
        }

        if (event.data.type === "healthsys:other-suite-selected") {
            importOtherSuite(event.data.suite);
        }

        if (event.data.type === "healthsys:other-suites-selected") {
            importOtherSuites(event.data.suites);
        }

        if (event.data.type === "healthsys:other-suite-lookup-close") {
            closeOtherSuiteLookup();
        }
    });

    if (mailingAddressInput) {
        mailingAddressInput.addEventListener("input", syncAddressFromMailing);
    }

    if (sameAddressCheckbox) {
        sameAddressCheckbox.addEventListener("change", syncAddressFromMailing);
        syncAddressFromMailing();
    }

    function canClearField(input) {
        if (!input || input.disabled || input.readOnly) {
            return false;
        }

        if (input.tagName === "TEXTAREA") {
            return true;
        }

        const clearableTypes = ["email", "number", "password", "search", "tel", "text", "url"];
        return clearableTypes.includes((input.getAttribute("type") || "text").toLowerCase());
    }

    function ensureClearableWrapper(input) {
        const existingWrapper = input.closest(".clearable-input");
        if (existingWrapper) {
            return existingWrapper;
        }

        const wrapper = document.createElement("span");
        wrapper.className = "clearable-input";

        if (input.parentElement && input.parentElement.classList.contains("field-with-icon")) {
            input.parentElement.classList.add("clearable-input", "has-trailing-icon");
            return input.parentElement;
        }

        input.parentNode.insertBefore(wrapper, input);
        wrapper.appendChild(input);
        return wrapper;
    }

    function setupClearableField(input) {
        const wrapper = ensureClearableWrapper(input);
        wrapper.classList.toggle("has-textarea", input.tagName === "TEXTAREA");
        wrapper.classList.toggle("has-list-picker", input.hasAttribute("list"));
        let button = wrapper.querySelector("[data-clear-field]");

        if (!button) {
            button = document.createElement("button");
            button.className = "field-clear-button";
            button.type = "button";
            button.setAttribute("data-clear-field", "");
            button.setAttribute("aria-label", "清除文字");
            button.hidden = true;
            wrapper.appendChild(button);
        }

        function updateClearButton() {
            button.hidden = !input.value;
        }

        if (button.dataset.clearReady === "true") {
            updateClearButton();
            return;
        }

        button.dataset.clearReady = "true";
        button.addEventListener("click", function (event) {
            event.preventDefault();
            event.stopPropagation();

            input.value = "";
            input.dispatchEvent(new Event("input", { bubbles: true }));
            input.focus();
        });

        input.addEventListener("input", updateClearButton);
        updateClearButton();
    }

    document.querySelectorAll(".appointment-create-page input, .appointment-create-page textarea").forEach(function (input) {
        if (canClearField(input)) {
            setupClearableField(input);
        }
    });

    document.querySelectorAll("[data-patient-field='age']").forEach(function (input) {
        input.addEventListener("change", function () {
            input.value = normalizeAgeValue(input.value);
        });
    });

    if (employeeNoInput) {
        employeeNoInput.addEventListener("input", queueEmployeeLookup);
        employeeNoInput.addEventListener("change", loadEmployeeProfile);
        employeeNoInput.addEventListener("blur", loadEmployeeProfile);
    }

    document.querySelectorAll("[data-relationship-picker]").forEach(function (picker) {
        const input = picker.querySelector("[data-relationship-input]");
        const toggle = picker.querySelector("[data-relationship-toggle]");
        const options = picker.querySelector("[data-relationship-options]");

        if (!input || !toggle || !options) {
            return;
        }

        function setRelationshipListOpen(isOpen) {
            options.hidden = !isOpen;
            input.setAttribute("aria-expanded", String(isOpen));
            toggle.setAttribute("aria-expanded", String(isOpen));
        }

        toggle.addEventListener("click", function (event) {
            event.preventDefault();
            event.stopPropagation();
            setRelationshipListOpen(options.hidden);
            input.focus();
        });

        options.querySelectorAll("[data-relationship-option]").forEach(function (option) {
            option.addEventListener("click", function () {
                input.value = option.dataset.value || option.textContent.trim();
                input.dispatchEvent(new Event("input", { bubbles: true }));
                input.dispatchEvent(new Event("change", { bubbles: true }));
                setRelationshipListOpen(false);
                input.focus();
            });
        });

        input.addEventListener("keydown", function (event) {
            if (event.key === "ArrowDown") {
                event.preventDefault();
                setRelationshipListOpen(true);
                const firstOption = options.querySelector("[data-relationship-option]");
                if (firstOption) {
                    firstOption.focus();
                }
            }

            if (event.key === "Escape") {
                setRelationshipListOpen(false);
            }
        });

        options.addEventListener("keydown", function (event) {
            if (event.key === "Escape") {
                setRelationshipListOpen(false);
                input.focus();
            }
        });

        document.addEventListener("click", function (event) {
            if (!picker.contains(event.target)) {
                setRelationshipListOpen(false);
            }
        });
    });

    document.querySelectorAll("[data-billing-note-picker]").forEach(function (picker) {
        const input = picker.querySelector("[data-billing-note-input]");
        const toggle = picker.querySelector("[data-billing-note-toggle]");
        const options = picker.querySelector("[data-billing-note-options]");

        if (!input || !toggle || !options) {
            return;
        }

        function getBillingNoteScrollParent() {
            let parent = picker.parentElement;

            while (parent && parent !== document.body) {
                const overflowY = window.getComputedStyle(parent).overflowY;
                if (overflowY === "auto" || overflowY === "scroll" || overflowY === "overlay") {
                    return parent;
                }

                parent = parent.parentElement;
            }

            return document.querySelector("body.hc .page") || document.scrollingElement || document.documentElement;
        }

        function keepBillingNoteListVisible() {
            if (options.hidden) {
                return;
            }

            const scrollParent = getBillingNoteScrollParent();
            const optionsRect = options.getBoundingClientRect();
            const scrollRect = scrollParent === document.scrollingElement || scrollParent === document.documentElement
                ? { top: 0, bottom: window.innerHeight || document.documentElement.clientHeight }
                : scrollParent.getBoundingClientRect();
            const margin = 12;
            let scrollDelta = 0;

            if (optionsRect.top < scrollRect.top + margin) {
                scrollDelta = optionsRect.top - scrollRect.top - margin;
            } else if (optionsRect.bottom > scrollRect.bottom - margin) {
                scrollDelta = optionsRect.bottom - scrollRect.bottom + margin;
            }

            if (scrollDelta !== 0 && typeof scrollParent.scrollBy === "function") {
                scrollParent.scrollBy({ top: scrollDelta });
            }
        }

        function setBillingNoteListOpen(isOpen) {
            options.hidden = !isOpen;
            input.setAttribute("aria-expanded", String(isOpen));
            toggle.setAttribute("aria-expanded", String(isOpen));
            picker.classList.toggle("is-open", isOpen);

            if (isOpen) {
                window.requestAnimationFrame(keepBillingNoteListVisible);
            }
        }

        toggle.addEventListener("click", function (event) {
            event.preventDefault();
            event.stopPropagation();
            setBillingNoteListOpen(options.hidden);
            input.focus();
        });

        options.querySelectorAll("[data-billing-note-option]").forEach(function (option) {
            option.addEventListener("click", function (event) {
                event.preventDefault();
                event.stopPropagation();

                input.value = option.dataset.value || option.textContent.trim();
                input.dispatchEvent(new Event("input", { bubbles: true }));
                input.dispatchEvent(new Event("change", { bubbles: true }));
                setBillingNoteListOpen(false);
                input.focus();
            });
        });

        input.addEventListener("keydown", function (event) {
            if (event.key === "ArrowDown") {
                event.preventDefault();
                setBillingNoteListOpen(true);
                const firstOption = options.querySelector("[data-billing-note-option]");
                if (firstOption) {
                    firstOption.focus();
                }
            }

            if (event.key === "Escape") {
                setBillingNoteListOpen(false);
            }
        });

        options.addEventListener("keydown", function (event) {
            if (event.key === "Escape") {
                setBillingNoteListOpen(false);
                input.focus();
            }
        });

        document.addEventListener("click", function (event) {
            if (!picker.contains(event.target)) {
                setBillingNoteListOpen(false);
            }
        });
    });

    document.querySelectorAll(".choice-data-grid").forEach(function (table) {
        table.addEventListener("change", function (event) {
            const input = event.target;
            if (!input.matches("input[type='radio'], input[type='checkbox']")) {
                return;
            }

            if (input.type === "radio") {
                table.querySelectorAll("tbody tr").forEach(function (row) {
                    row.classList.toggle("is-selected", row.contains(input));
                });
                return;
            }

            const row = input.closest("tr");
            if (row) {
                row.classList.toggle("is-selected", input.checked);
            }
        });
    });

    function appendPhraseToTextarea(textarea, phraseText) {
        if (!textarea || !phraseText) {
            return;
        }

        const prefix = textarea.value.trim() ? "\n" : "";
        const maxLength = Number.parseInt(textarea.getAttribute("maxlength") || "0", 10);
        const nextValue = textarea.value + prefix + phraseText;

        textarea.value = maxLength > 0 ? nextValue.slice(0, maxLength) : nextValue;
        textarea.dispatchEvent(new Event("input", { bubbles: true }));
        textarea.focus();
    }

    document.querySelectorAll("[data-phrase-target]").forEach(function (button) {
        button.addEventListener("click", function () {
            const textarea = document.getElementById(button.dataset.phraseTarget || "");

            appendPhraseToTextarea(textarea, button.dataset.phraseText || "");
        });
    });

    document.querySelectorAll("[data-auto-resize-note]").forEach(function (textarea) {
        function expandTextarea() {
            textarea.style.height = "";
            const availableHeight = textarea.getBoundingClientRect().height;
            textarea.style.overflowY = "hidden";
            textarea.style.height = Math.max(availableHeight, textarea.scrollHeight) + "px";
        }

        function resetTextarea() {
            textarea.style.height = "";
            textarea.style.overflowY = "";
        }

        textarea.addEventListener("focus", expandTextarea);
        textarea.addEventListener("input", expandTextarea);
        textarea.addEventListener("blur", resetTextarea);
    });

    document.querySelectorAll("textarea[maxlength]").forEach(function (textarea) {
        const counterHost = textarea.closest(".field-group") || textarea.parentElement;
        const counter = counterHost ? counterHost.querySelector(".char-counter") : null;
        if (!counter) {
            return;
        }

        function updateCounter() {
            counter.textContent = textarea.value.length + " / " + textarea.maxLength;
        }

        textarea.addEventListener("input", updateCounter);
        updateCounter();
    });
    if (isReadOnlyMode && createAppointmentForm) {
        const lockReadOnlyFields = function (root) {
            const scope = root?.querySelectorAll ? root : createAppointmentForm;

            scope.querySelectorAll("input[name], select[name], textarea[name]").forEach(function (control) {
                if (control.type === "hidden") {
                    return;
                }

                control.disabled = true;
                control.setAttribute("aria-disabled", "true");
            });
        };

        lockReadOnlyFields(createAppointmentForm);

        if ("MutationObserver" in window) {
            const readOnlyObserver = new MutationObserver(function (mutations) {
                mutations.forEach(function (mutation) {
                    mutation.addedNodes.forEach(function (node) {
                        if (node.nodeType === Node.ELEMENT_NODE) {
                            lockReadOnlyFields(node);
                        }
                    });
                });
            });

            readOnlyObserver.observe(createAppointmentForm, { childList: true, subtree: true });
        }

        createAppointmentForm.addEventListener("submit", function (event) {
            event.preventDefault();
        });
    }
})();
