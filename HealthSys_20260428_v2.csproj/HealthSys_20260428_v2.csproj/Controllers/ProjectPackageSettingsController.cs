using HealthSys_20260428_v2.csproj.Data;
using HealthSys_20260428_v2.csproj.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Data;
using System.Globalization;
using System.Text.Json;

namespace HealthSys_20260428_v2.csproj.Controllers;

public class ProjectPackageSettingsController : Controller
{
    private const string SpecialOperationItemsCacheKey = "ProjectPackageSettings.SpecialOperationItems";
    private const string InternalSuiteSource = "internal";
    private const string InternalSuiteImportNote = "院內組套匯入";

    private readonly IMemoryCache _cache;

    public ProjectPackageSettingsController(IMemoryCache cache)
    {
        _cache = cache;
    }

    public IActionResult HealthCheckProjectManagement()
    {
        var projects = GetHealthCheckProjects();

        var model = new HealthCheckProjectManagementViewModel
        {
            Projects = projects,
            HospitalOptions = BuildOptions("請選擇體檢醫院", projects.Select(project => project.Hospital)),
            ClientUnitOptions = BuildClientUnitOptions(additionalClientUnits: projects.Select(project => project.ClientUnit)),
            CustomerNoOptions = BuildOptions("請選擇公司編號", projects.Select(project => project.CustomerNo)),
            YearOptions = BuildOptions("請選擇年度", projects.Select(project => project.Year)),
            CheckYearOptions = BuildOptions("請選擇檢查年度", projects.Select(project => project.CheckYear)),
            StatusOptions = BuildOptions("請選擇狀態", projects.Select(project => project.Status)),
            PageSizeOptions = [10, 20, 30, 40, 50],
            TotalProjectCount = projects.Count
        };

        return View(model);
    }

    public IActionResult HealthCheckProjectDetail(string? code = null)
    {
        return View(BuildHealthCheckProjectDetailModel(code));
    }

    public IActionResult HealthCheckProjectOrders(string? fixItems = null)
    {
        var orders = GetProjectDetailOrders(fixItems ?? "");

        return Json(new
        {
            orders = orders.Select(order => new
            {
                code = order.Code,
                name = order.Name
            })
        });
    }

    public IActionResult HealthCheckProjectCheckItems(string? category = null, string? keyword = null)
    {
        var items = GetWizardAvailableItems(category, keyword);

        return Json(new
        {
            items = items.Select(item => new
            {
                code = item.Code,
                name = item.Name,
                category = item.CategoryKey,
                categoryName = item.Category,
                itemCategory = item.ItemCategory,
                reportName = item.ReportName,
                description = FirstNonEmpty(item.ReportName, item.ItemCategory),
                required = item.Required,
                icon = item.Icon
            })
        });
    }

    public IActionResult HealthCheckProjectSpecialOperationItems()
    {
        var items = GetCachedWizardSpecialOperationItems();
        Response.Headers.CacheControl = "private, max-age=300";

        return Json(new
        {
            items = items.Select(item => new
            {
                code = item.Code,
                name = item.Name,
                category = item.CategoryKey,
                categoryName = item.Category,
                categoryKey = item.CategoryKey,
                note = item.Note,
                manualText = item.ManualText,
                billingCategory = item.BillingCategory,
                price = item.Price
            })
        });
    }

    private IReadOnlyList<ProjectSpecialOperationItemViewModel> GetCachedWizardSpecialOperationItems()
    {
        return _cache.GetOrCreate(SpecialOperationItemsCacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            entry.SlidingExpiration = TimeSpan.FromMinutes(3);

            return GetWizardSpecialOperationItems();
        }) ?? [];
    }


    public IActionResult InternalMedicalOrderSuites(
        string? code = null,
        string? name = null,
        string? startDate = null,
        string? endDate = null,
        int page = 1,
        int pageSize = 10)
    {
        var normalizedPage = Math.Max(1, page);
        var normalizedPageSize = pageSize == 20 ? 20 : 10;
        var endDateValue = ParseProjectDate(endDate)?.Date ?? DateTime.Today;
        var startDateValue = ParseProjectDate(startDate)?.Date ?? endDateValue.AddYears(-1);

        if (startDateValue > endDateValue)
        {
            (startDateValue, endDateValue) = (endDateValue, startDateValue);
        }

        try
        {
            var suites = GetInternalMedicalOrderSuites(code, name, startDateValue, endDateValue);
            var totalCount = suites.Count;
            var items = suites
                .Skip((normalizedPage - 1) * normalizedPageSize)
                .Take(normalizedPageSize)
                .ToArray();

            return Json(new
            {
                items = items.Select(item => new
                {
                    code = item.Code,
                    name = item.Name,
                    price = item.Price,
                    priceDate = item.PriceDate,
                    orderCount = item.OrderCount,
                    checkNos = Array.Empty<string>()
                }),
                totalCount,
                page = normalizedPage,
                pageSize = normalizedPageSize,
                startDate = startDateValue.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                endDate = endDateValue.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            });
        }
        catch
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                items = Array.Empty<object>(),
                totalCount = 0,
                page = normalizedPage,
                pageSize = normalizedPageSize,
                message = "院內醫令資料讀取失敗，請稍後再試。"
            });
        }
    }

    public IActionResult InternalMedicalOrderSuiteOrders(string? code = null)
    {
        var suiteCode = Normalize(code);

        if (string.IsNullOrWhiteSpace(suiteCode))
        {
            return Json(new
            {
                orders = Array.Empty<object>()
            });
        }

        try
        {
            var orders = GetInternalMedicalOrderSuiteOrders(suiteCode);

            return Json(new
            {
                orders = orders.Select(order => new
                {
                    suiteCode = order.SuiteCode,
                    code = order.Code,
                    name = order.Name,
                    category = "院內組套"
                })
            });
        }
        catch
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                orders = Array.Empty<object>(),
                message = "院內組套細項讀取失敗，請稍後再試。"
            });
        }
    }

    public IActionResult CreateHealthCheckProject(
        string? mode = null,
        string? code = null,
        string? customerNo = null,
        string? customerName = null,
        string? clientUnit = null,
        string? isPastHistory = null,
        string? isOralCheck = null,
        string? isOnlineQuestion = null)
    {
        return View(BuildWizardModel(1, ReadWizardInput(
            mode,
            code,
            customerNo,
            customerName,
            clientUnit,
            isPastHistory,
            isOralCheck,
            isOnlineQuestion)));
    }

    public IActionResult CreateHealthCheckProjectOrders(
        string? mode = null,
        string? code = null,
        string? isPastHistory = null,
        string? isOralCheck = null,
        string? isOnlineQuestion = null)
    {
        return View(BuildWizardModel(2, ReadWizardInput(
            mode,
            code,
            isPastHistory: isPastHistory,
            isOralCheck: isOralCheck,
            isOnlineQuestion: isOnlineQuestion)));
    }

    public IActionResult CreateHealthCheckProjectSpecialOperations(
        string? mode = null,
        string? code = null,
        string? isPastHistory = null,
        string? isOralCheck = null,
        string? isOnlineQuestion = null)
    {
        return View(BuildWizardModel(3, ReadWizardInput(
            mode,
            code,
            isPastHistory: isPastHistory,
            isOralCheck: isOralCheck,
            isOnlineQuestion: isOnlineQuestion)));
    }

    public IActionResult ConfirmHealthCheckProject(
        string? mode = null,
        string? code = null,
        string? isPastHistory = null,
        string? isOralCheck = null,
        string? isOnlineQuestion = null)
    {
        return View(BuildWizardModel(4, ReadWizardInput(
            mode,
            code,
            isPastHistory: isPastHistory,
            isOralCheck: isOralCheck,
            isOnlineQuestion: isOnlineQuestion)));
    }

    public IActionResult CompleteHealthCheckProject(
        string? mode = null,
        string? code = null,
        string? isPastHistory = null,
        string? isOralCheck = null,
        string? isOnlineQuestion = null)
    {
        var input = ReadWizardInput(
            mode,
            code,
            isPastHistory: isPastHistory,
            isOralCheck: isOralCheck,
            isOnlineQuestion: isOnlineQuestion);
        var model = BuildWizardModel(5, input);
        var createUserNo = FirstNonEmpty(User?.Identity?.Name, model.Project.Owner);

        SaveHealthProjectList(model, createUserNo);
        SaveCheckProjectSchemes(model.Project.Code, input);
        SaveCheckProjectSpecialItems(model.Project.Code, input);

        return RedirectToAction(nameof(HealthCheckProjectManagement), new { focusProject = model.Project.Code });
    }



    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult DeleteHealthCheckProject([FromForm] string? projectNo)
    {
        projectNo = Normalize(projectNo);

        if (string.IsNullOrWhiteSpace(projectNo))
        {
            return Json(new
            {
                deleted = false,
                deletedCount = 0,
                projectNo = "",
                message = "缺少專案編號，無法刪除專案。"
            });
        }

        var deleteSql = OracleSql.DELETE.DeleteHealthProjectList();

        try
        {
            var deletedCount = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().ExecuteNonQuery(deleteSql, new Dictionary<string, object?>
            {
                ["PROJECTNO"] = projectNo
            });

            return Json(new
            {
                deleted = deletedCount >= 1,
                deletedCount,
                projectNo,
                message = deletedCount >= 1
                    ? "專案已刪除。"
                    : "未刪除任何專案資料，請確認專案是否仍存在。"
            });
        }
        catch
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                deleted = false,
                deletedCount = 0,
                projectNo,
                message = "專案刪除失敗，請稍後再試。"
            });
        }
    }

    public IActionResult ProjectPackageSettings()
    {
        var selectedItems = GetSelectedItems();
        var selectedSubtotal = selectedItems.Sum(item => item.Price);

        var model = new ProjectPackageSettingsViewModel
        {
            Project = new ProjectPackageInfoViewModel
            {
                Code = "HC240501001",
                Name = "成人健檢標準專案",
                Target = "2024 基礎健檢適用者",
                Category = "一般健檢",
                Status = "啟動",
                StatusTone = "enabled",
                ItemCount = selectedItems.Count,
                Price = 4850,
                CreatedAt = "2024/05/20",
                Owner = "管理員",
                UpdatedAt = "2024/05/28"
            },
            Categories = GetCategories(),
            AvailableItems = GetAvailableItems(),
            SelectedItems = selectedItems,
            DiscountAmount = 750,
            DiscountRate = 13.39m,
            CostEstimate = selectedSubtotal
        };

        return View(model);
    }

    private WizardProjectInput ReadWizardInput(
        string? mode = null,
        string? code = null,
        string? customerNo = null,
        string? customerName = null,
        string? clientUnit = null,
        string? isPastHistory = null,
        string? isOralCheck = null,
        string? isOnlineQuestion = null)
    {
        var query = Request.Query;

        return new()
        {
            Mode = FirstNonEmpty(mode, query["mode"].ToString()),
            Code = FirstNonEmpty(code, query["code"].ToString()),
            ProjectNo = query["projectNo"].ToString(),
            ProjectName = query["projectName"].ToString(),
            CustomerNo = FirstNonEmpty(customerNo, query["customerNo"].ToString()),
            CustomerName = FirstNonEmpty(customerName, query["customerName"].ToString()),
            ClientUnit = FirstNonEmpty(clientUnit, query["clientUnit"].ToString()),
            SelfPayAccountingUnit = query["selfPayAccountingUnit"].ToString(),
            Year = query["year"].ToString(),
            CheckDate = query["checkDate"].ToString(),
            CheckStartDate = query["checkStartDate"].ToString(),
            CheckEndDate = query["checkEndDate"].ToString(),
            CompareVal = query["compareVal"].ToString(),
            FlowName = query["flowName"].ToString(),
            DefaultArea = query["defaultArea"].ToString(),
            Notes = query["notes"].ToString(),
            IsPastHistory = FirstNonEmpty(isPastHistory, query["isPastHistory"].ToString()),
            IsOralCheck = FirstNonEmpty(isOralCheck, query["isOralCheck"].ToString()),
            IsOnlineQuestion = FirstNonEmpty(isOnlineQuestion, query["isOnlineQuestion"].ToString()),
            Questionnaire = query["questionnaire"].ToString(),
            SelectedOrders = query["selectedOrders"].ToString(),
            SelectedSuites = query["selectedSuites"].ToString(),
            SelectedSpecialOperations = query["selectedSpecialOperations"].ToString()
        };
    }

    private static HealthProjectWizardViewModel BuildWizardModel(
        int currentStep,
        WizardProjectInput input)
    {
        var isEditMode = string.Equals(input.Mode, "edit", StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrWhiteSpace(input.Code);
        var projectCode = FirstNonEmpty(input.Code, input.ProjectNo);
        var hasSelectedSuiteInput = !string.IsNullOrWhiteSpace(input.SelectedSuites);
        var hasSelectedOrderInput = !string.IsNullOrWhiteSpace(input.SelectedOrders);
        var selectedSuites = ParseSelectedProjectSuites(input.SelectedSuites);

        if (selectedSuites.Count == 0
            && isEditMode
            && !hasSelectedSuiteInput
            && !hasSelectedOrderInput)
        {
            selectedSuites = GetExistingSelectedProjectSuites(projectCode);
        }

        var selectedOrders = selectedSuites.Count > 0
            ? selectedSuites.SelectMany(suite => suite.Orders).ToArray()
            : ParseSelectedProjectOrders(input.SelectedOrders);
        var shouldLoadSelectedItems = currentStep != 3 || selectedSuites.Count == 0;
        var selectedItems = shouldLoadSelectedItems
            ? GetWizardSelectedItems(selectedOrders)
            : [];
        var itemSubtotal = selectedItems.Sum(item => item.Price);
        var suiteSubtotal = selectedSuites.Sum(suite => suite.PublicExpense);
        var medicalOrderSubtotal = selectedSuites.Count > 0 ? suiteSubtotal : itemSubtotal;
        var hasSelectedSpecialOperationsInput = !string.IsNullOrWhiteSpace(input.SelectedSpecialOperations);
        var selectedSpecialOperations = GetWizardSelectedSpecialOperations(input.SelectedSpecialOperations);

        if (selectedSpecialOperations.Count == 0
            && isEditMode
            && !hasSelectedSpecialOperationsInput)
        {
            selectedSpecialOperations = GetExistingProjectSpecialOperations(projectCode);
        }

        var specialOperationSubtotal = selectedSpecialOperations.Sum(item => item.Price);
        var subtotal = medicalOrderSubtotal + specialOperationSubtotal;
        var discountAmount = 0;
        var finalPrice = Math.Max(0, subtotal - discountAmount);
        var costEstimate = medicalOrderSubtotal + specialOperationSubtotal;
        var projects = currentStep == 1 || isEditMode
            ? GetHealthCheckProjects()
            : Array.Empty<HealthCheckProjectListItemViewModel>();
        var selfPayAccountingUnits = currentStep == 2
            ? GetSelfPayAccountingUnits()
            : Array.Empty<SelfPayAccountingUnitViewModel>();
        var project = BuildWizardProject(
            projects,
            currentStep,
            isEditMode,
            input,
            selectedItems.Count > 0 ? selectedItems.Count : selectedOrders.Count,
            finalPrice,
            selfPayAccountingUnits);
        var needsCreateProjectOptions = currentStep == 1;

        return new()
        {
            CurrentStep = currentStep,
            IsEditMode = isEditMode,
            Project = project,
            Steps = BuildWizardSteps(currentStep, isEditMode),
            ClientUnitOptions = needsCreateProjectOptions ? BuildClientUnitOptions(project.ClientUnit, projects.Select(item => item.ClientUnit)) : [],
            SelfPayAccountingUnits = selfPayAccountingUnits,
            DefaultAreaOptions = needsCreateProjectOptions ? BuildDefaultAreaOptions(project.DefaultArea) : [],
            CustomerLookupItems = currentStep == 1 ? GetCustomerLookupItems(projects) : [],
            Categories = currentStep == 2 ? GetWizardCategories() : [],
            AvailableItems = currentStep == 2 ? GetWizardAvailableItems() : [],
            SelectedItems = selectedItems,
            SelectedSuites = BuildWizardSelectedSuites(selectedSuites, selectedItems),
            AvailableSpecialOperations = [],
            SelectedSpecialOperations = selectedSpecialOperations,
            MedicalOrderSubtotal = medicalOrderSubtotal,
            SpecialOperationSubtotal = specialOperationSubtotal,
            Subtotal = subtotal,
            DiscountAmount = discountAmount,
            DiscountRate = 0m,
            FinalPrice = finalPrice,
            CostEstimate = costEstimate
        };
    }

    private static HealthCheckProjectDetailViewModel BuildHealthCheckProjectDetailModel(string? code)
    {
        var projects = GetHealthCheckProjects();
        var projectCode = FirstNonEmpty(code, projects.FirstOrDefault()?.Code);
        var packages = GetProjectDetailPackages(projectCode).ToArray();
        var specialItems = GetProjectDetailSpecialItems(projectCode).ToArray();
        var selectedPackage = packages.FirstOrDefault(package => package.Active) ?? packages.FirstOrDefault();
        var orders = GetProjectDetailOrders(selectedPackage?.FixItems ?? "");
        var totalPublicExpense = packages.Sum(package => package.PublicExpense)
            + specialItems.Sum(item => item.PublicExpense);
        var project = BuildWizardProject(
            projects,
            4,
            true,
            new WizardProjectInput { Code = projectCode },
            orders.Count,
            totalPublicExpense,
            []);

        return new()
        {
            Project = project,
            Packages = packages,
            SpecialItems = specialItems,
            SelectedPackage = selectedPackage,
            Orders = orders
        };
    }

    private static IReadOnlyList<HealthCheckProjectPackageRowViewModel> GetProjectDetailPackages(string projectNo)
    {
        if (string.IsNullOrWhiteSpace(projectNo))
        {
            return [];
        }

        var sql = OracleSql.SELECT.getCheckProjectScheme(projectNo);
        var dataTable = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().GETDataTable(sql, "", true);

        return dataTable.Rows
            .Cast<DataRow>()
            .Select(ProjectDetailPackage)
            .ToArray();
    }

    private static IReadOnlyList<HealthCheckProjectOrderRowViewModel> GetProjectDetailOrders(string fixItems)
    {
        var checkNos = ParseCheckNoList(fixItems);

        if (checkNos.Count == 0)
        {
            return [];
        }

        var itemsByCode = GetSelectedItemLookup(checkNos);

        return checkNos
            .Select(checkNo =>
            {
                itemsByCode.TryGetValue(checkNo, out var item);

                return new HealthCheckProjectOrderRowViewModel
                {
                    Code = checkNo,
                    Name = FirstNonEmpty(item?.Name, checkNo)
                };
            })
            .ToArray();
    }

    private static IReadOnlyList<InternalMedicalOrderSuiteViewModel> GetInternalMedicalOrderSuites(
        string? code,
        string? name,
        DateTime startDate,
        DateTime endDate)
    {
        var normalizedCode = Normalize(code);
        var normalizedName = Normalize(name);
        var parameters = new Dictionary<string, object?>
        {
            ["START_DATE"] = startDate.Date,
            ["END_DATE"] = endDate.Date
        };

        if (!string.IsNullOrWhiteSpace(normalizedCode))
        {
            parameters["ORDER_CODE"] = "%" + normalizedCode.ToUpperInvariant() + "%";
        }

        if (!string.IsNullOrWhiteSpace(normalizedName))
        {
            parameters["ORDER_NAME"] = "%" + normalizedName.ToUpperInvariant() + "%";
        }

        var sql = OracleSql.SELECT.getInternalMedicalOrderSuites(normalizedCode, normalizedName);
        var dataTable = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().GETDataTable(sql, parameters, "", false);

        return dataTable.Rows
            .Cast<DataRow>()
            .Select(InternalMedicalOrderSuite)
            .Where(item => !string.IsNullOrWhiteSpace(item.Code) || !string.IsNullOrWhiteSpace(item.Name))
            .ToArray();
    }

    private static InternalMedicalOrderSuiteViewModel InternalMedicalOrderSuite(DataRow row) =>
        new()
        {
            Code = GetString(row, "CHORDNO"),
            Name = GetString(row, "CHORDENAME").TrimEnd(),
            Price = GetInt(row, 0, "SELF_PAY_AMOUNT", "自費金額", "RLORDPRIA2"),
            PriceDate = FirstNonEmpty(GetString(row, "PRICE_DATE"), GetString(row, "日期")),
            OrderCount = GetInt(row, 0, "ORDER_COUNT", "件數")
        };

    private static IReadOnlyList<InternalMedicalOrderSuiteOrderViewModel> GetInternalMedicalOrderSuiteOrders(string suiteCode)
    {
        var sql = OracleSql.SELECT.getInternalMedicalOrderSuiteOrders();
        var dataTable = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().GETDataTable(sql, new Dictionary<string, object?>
        {
            ["CHPRICENO"] = suiteCode
        }, "", false);

        return dataTable.Rows
            .Cast<DataRow>()
            .Select(InternalMedicalOrderSuiteOrder)
            .Where(order => !string.IsNullOrWhiteSpace(order.Code) || !string.IsNullOrWhiteSpace(order.Name))
            .ToArray();
    }

    private static InternalMedicalOrderSuiteOrderViewModel InternalMedicalOrderSuiteOrder(DataRow row) =>
        new()
        {
            SuiteCode = GetString(row, "CHPRICENO"),
            Code = GetString(row, "CHPRICENOKID"),
            Name = FirstNonEmpty(GetString(row, "CHORDEXTNAME").TrimEnd(), GetString(row, "CHPRICENOKID"))
        };

    private static IReadOnlyList<HealthCheckProjectSpecialItemRowViewModel> GetProjectDetailSpecialItems(string projectNo)
    {
        if (string.IsNullOrWhiteSpace(projectNo))
        {
            return [];
        }

        var sql = OracleSql.SELECT.getCheckProjectSpecialItemTbl(projectNo);
        var dataTable = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().GETDataTable(sql, "", true);

        return dataTable.Rows
            .Cast<DataRow>()
            .Select(ProjectDetailSpecialItem)
            .ToArray();
    }


    private static IReadOnlyList<SelectedProjectSuite> GetExistingSelectedProjectSuites(string? projectNo)
    {
        projectNo = Normalize(projectNo);

        if (string.IsNullOrWhiteSpace(projectNo))
        {
            return [];
        }

        return GetProjectDetailPackages(projectNo)
            .Select((package, index) =>
            {
                var schemeNo = FirstNonEmpty(package.SchemeNo, (index + 1).ToString(CultureInfo.InvariantCulture));
                var checkNos = ParseCheckNoList(package.FixItems);
                var isInternalSuite = IsInternalSuiteImport(package.Note);
                var internalOrderCodes = isInternalSuite
                    ? GetInternalSuiteOrderCodeSet(schemeNo)
                    : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var lockAllOrders = isInternalSuite && internalOrderCodes.Count == 0;
                var selectedOrders = new List<SelectedProjectOrder>();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var checkNo in checkNos)
                {
                    var locked = isInternalSuite && (lockAllOrders || internalOrderCodes.Contains(checkNo));
                    AddSelectedProjectOrder(
                        selectedOrders,
                        seen,
                        schemeNo,
                        checkNo,
                        source: locked ? InternalSuiteSource : "",
                        locked: locked);
                }

                return new SelectedProjectSuite
                {
                    SchemeNo = schemeNo,
                    Source = isInternalSuite ? InternalSuiteSource : "manual",
                    Name = FirstNonEmpty(package.Name, "組套 " + schemeNo),
                    Note = package.Note,
                    FeeType = "自費",
                    SelfPayAccountingUnit = package.SelfPayAccountingUnit,
                    PublicExpense = Math.Max(0, package.PublicExpense),
                    Orders = selectedOrders,
                    CheckNos = selectedOrders.Select(order => order.CheckNo).ToArray()
                };
            })
            .Where(suite => suite.CheckNos.Count > 0)
            .ToArray();
    }


    private static IReadOnlyList<HealthProjectSelectedSuiteViewModel> BuildWizardSelectedSuites(
        IReadOnlyList<SelectedProjectSuite> selectedSuites,
        IReadOnlyList<ProjectPackageItemViewModel> selectedItems)
    {
        var itemsByCode = selectedItems
            .Where(item => !string.IsNullOrWhiteSpace(item.Code))
            .GroupBy(item => item.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        return selectedSuites
            .Select((suite, index) =>
            {
                var schemeNo = FirstNonEmpty(suite.SchemeNo, (index + 1).ToString(CultureInfo.InvariantCulture));
                var suiteSource = IsInternalSuiteSource(suite.Source) || IsInternalSuiteImport(suite.Note)
                    ? InternalSuiteSource
                    : "manual";
                var lockedOrderCodes = suite.Orders
                    .Where(order => order.Locked || IsInternalSuiteSource(order.Source))
                    .Select(order => Normalize(order.CheckNo))
                    .Where(checkNo => !string.IsNullOrWhiteSpace(checkNo))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                if (IsInternalSuiteSource(suiteSource) && lockedOrderCodes.Count == 0)
                {
                    lockedOrderCodes = GetInternalSuiteOrderCodeSet(schemeNo);
                }

                var orders = suite.Orders
                    .Select(order =>
                    {
                        itemsByCode.TryGetValue(order.CheckNo, out var item);
                        var category = FirstNonEmpty(item?.Category, item?.ItemCategory, order.Category);
                        var itemCategory = FirstNonEmpty(item?.ItemCategory, category);
                        var locked = IsInternalSuiteSource(suiteSource)
                            && (order.Locked || IsInternalSuiteSource(order.Source) || lockedOrderCodes.Contains(order.CheckNo));

                        return new HealthProjectSelectedSuiteOrderViewModel
                        {
                            Code = order.CheckNo,
                            CheckNo = order.CheckNo,
                            Source = locked ? InternalSuiteSource : Normalize(order.Source),
                            Locked = locked,
                            Name = FirstNonEmpty(item?.Name, order.Name, order.CheckNo),
                            Category = category,
                            CategoryName = category,
                            ItemCategory = itemCategory
                        };
                    })
                    .Where(order => !string.IsNullOrWhiteSpace(order.CheckNo))
                    .ToArray();

                return new HealthProjectSelectedSuiteViewModel
                {
                    Id = schemeNo,
                    SchemeNo = schemeNo,
                    Source = suiteSource,
                    Name = FirstNonEmpty(suite.Name, "組套 " + schemeNo),
                    Note = suite.Note,
                    FeeType = FirstNonEmpty(suite.FeeType, "自費"),
                    SelfPayAccountingUnit = suite.SelfPayAccountingUnit,
                    SelfPayAccountingUnitName = suite.SelfPayAccountingUnitName,
                    PublicExpense = Math.Max(0, suite.PublicExpense),
                    Orders = orders,
                    CheckNos = orders.Select(order => order.CheckNo).ToArray()
                };
            })
            .Where(suite => suite.CheckNos.Count > 0)
            .ToArray();
    }

    private static IReadOnlyList<ProjectSpecialOperationItemViewModel> GetExistingProjectSpecialOperations(string? projectNo)
    {
        projectNo = Normalize(projectNo);

        if (string.IsNullOrWhiteSpace(projectNo))
        {
            return [];
        }

        return GetProjectDetailSpecialItems(projectNo)
            .Select(item => new ProjectSpecialOperationItemViewModel
            {
                SortOrder = item.SortOrder,
                Code = item.Code,
                Name = FirstNonEmpty(item.Name, item.Code),
                Category = "",
                CategoryKey = "",
                Note = "",
                ManualText = "",
                BillingCategory = "問卷項目",
                Price = Math.Max(0, item.PublicExpense),
                Selected = true
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Code) || !string.IsNullOrWhiteSpace(item.Name))
            .ToArray();
    }

    private static HealthCheckProjectPackageRowViewModel ProjectDetailPackage(DataRow row, int index) =>
        new()
        {
            SchemeNo = GetString(row, "SCHEMENO"),
            Name = GetString(row, "NAME"),
            Note = GetString(row, "NOTE"),
            SelfPayAccountingUnit = GetString(row, "CHDCTTYPE"),
            SelfPayAccountingUnitDisplay = GetSelfPayAccountingUnitDisplay(GetString(row, "CHDCTTYPE")),
            PublicExpense = GetInt(row, 0, "PUBLICEXPENSE"),
            FixItems = GetString(row, "FIXITEMS"),
            Active = index == 0
        };

    private static HealthCheckProjectOrderRowViewModel ProjectDetailOrder(DataRow row) =>
        new()
        {
            Code = FirstNonEmpty(
                GetString(row, "CHECKNO"),
                GetString(row, "ITEMNO"),
                GetString(row, "ORDERNO"),
                GetString(row, "ORDERCODE"),
                GetString(row, "CODE"),
                GetString(row, "SCHEMENO")),
            Name = FirstNonEmpty(
                GetString(row, "ITEMNAME"),
                GetString(row, "ORDERNAME"),
                GetString(row, "NAME"))
        };

    private static HealthCheckProjectSpecialItemRowViewModel ProjectDetailSpecialItem(DataRow row, int index) =>
        new()
        {
            SortOrder = index + 1,
            Code = GetString(row, "SPECIALITEMNO"),
            Name = FirstNonEmpty(
                GetString(row, "NAME"),
                GetString(row, "SPECIALITEMNO"),
                GetString(row, "CATEGORYNO")),
            PublicExpense = GetInt(row, 0, "PUBLICEXPENSE", "PRICE", "AMOUNT", "FEE")
        };


    private static ProjectPackageInfoViewModel BuildWizardProject(
        IReadOnlyList<HealthCheckProjectListItemViewModel> projects,
        int currentStep,
        bool isEditMode,
        WizardProjectInput input,
        int itemCount,
        int price,
        IReadOnlyList<SelfPayAccountingUnitViewModel> selfPayAccountingUnits)
    {
        var currentYear = DateTime.Today.Year.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var currentRocYear = ToRocYearText(currentYear);
        var todayDate = DateTime.Today;
        var today = FormatProjectDate(todayDate);
        var halfYearLater = FormatProjectDate(todayDate.AddMonths(6));
        var project = !string.IsNullOrWhiteSpace(input.Code)
            ? projects.FirstOrDefault(item => string.Equals(item.Code, input.Code, StringComparison.OrdinalIgnoreCase))
            : null;
        var incomingCustomerNo = Normalize(input.CustomerNo);
        var incomingCustomerName = Normalize(input.CustomerName);
        var incomingClientUnit = NormalizeClientUnitOption(FirstNonEmpty(input.ClientUnit, BuildClientUnitText(incomingCustomerNo, incomingCustomerName)));
        var selectedClientUnit = FirstNonEmpty(incomingClientUnit, NormalizeClientUnitOption(project?.ClientUnit));
        var selfPayAccountingUnit = FirstNonEmpty(
            input.SelfPayAccountingUnit,
            project?.SelfPayAccountingUnit,
            FindLikelySelfPayAccountingUnit(selectedClientUnit, selfPayAccountingUnits),
            "00");
        var projectName = FirstNonEmpty(input.ProjectName, project?.Name);
        var checkDate = FirstNonEmpty(input.CheckDate, input.CheckStartDate, project?.CheckDate, today);
        var checkStartDate = checkDate;
        var checkEndDate = FirstNonEmpty(input.CheckEndDate, project?.EndDate, halfYearLater);
        var questionnaire = FirstNonEmpty(input.Questionnaire, project?.Questionnaire);
        var isOnlineQuestionEnabled = IsYesNoFlag(
            FirstNonEmpty(input.IsOnlineQuestion, project?.IsOnlineQuestion),
            !string.IsNullOrWhiteSpace(questionnaire));
        var projectCode = project?.Code;

        if (string.IsNullOrWhiteSpace(projectCode))
        {
            projectCode = !string.IsNullOrWhiteSpace(input.ProjectNo)
                ? input.ProjectNo
                : BuildHealthProjectNo();
        }

        return new()
        {
            Code = projectCode,
            Name = projectName,
            Target = FirstNonEmpty(project?.DisplayName, project?.Note, projectName),
            ShortName = projectName,
            Hospital = "亞東紀念醫院",
            ClientUnit = selectedClientUnit,
            SelfPayAccountingUnit = selfPayAccountingUnit,
            CustomerNo = FirstNonEmpty(incomingCustomerNo, project?.CustomerNo),
            CustomerName = FirstNonEmpty(incomingCustomerName, project?.CustomerName),
            Year = ToRocYearText(FirstNonEmpty(input.Year, project?.Year, project?.CheckYear, currentRocYear)),
            CheckDate = checkDate,
            CheckStartDate = checkStartDate,
            CheckEndDate = checkEndDate,
            EndDate = checkEndDate,
            Questionnaire = isOnlineQuestionEnabled ? questionnaire : "",
            FlowSheet = FirstNonEmpty(input.FlowName, project?.FlowName),
            DefaultArea = FirstNonEmpty(input.DefaultArea, project?.DefaultArea),
            IsPastHistoryEnabled = IsYesNoFlag(FirstNonEmpty(input.IsPastHistory, project?.IsPastHistory), false),
            IsOralCheckEnabled = IsYesNoFlag(FirstNonEmpty(input.IsOralCheck, project?.IsOralCheck), false),
            IsOnlineQuestionEnabled = isOnlineQuestionEnabled,
            Notes = FirstNonEmpty(input.Notes, project?.Note),
            Category = FirstNonEmpty(input.CompareVal, project?.CompareVal),
            Status = currentStep >= 4 ? "啟動" : project?.Status ?? "草稿",
            StatusTone = currentStep >= 4 ? "enabled" : project?.StatusTone ?? "draft",
            ItemCount = itemCount,
            Price = price,
            CreatedAt = FirstNonEmpty(project?.CreatedAt, today),
            Owner = "管理員",
            UpdatedAt = FirstNonEmpty(project?.UpdatedAt, isEditMode ? "2026/06/02 14:30" : "2026/06/02 10:30")
        };
    }

    private static IReadOnlyList<HealthProjectWizardStepViewModel> BuildWizardSteps(int currentStep, bool isEditMode) =>
    [
        WizardStep(1, "專案基本資料", isEditMode ? "編輯專案資料與名稱" : "建立專案資料與名稱", currentStep),
        WizardStep(2, "加入組套醫令", "選取專案檢查項目", currentStep),
        WizardStep(3, "問卷項目", "選取問卷項目", currentStep),
        WizardStep(4, "確認發布", isEditMode ? "檢視設定並更新" : "檢視設定並發布", currentStep),
        WizardStep(5, isEditMode ? "編輯完成" : "建立完成", isEditMode ? "完成編輯專案" : "完成建立專案", currentStep)
    ];

    private static HealthProjectWizardStepViewModel WizardStep(int number, string title, string subtitle, int currentStep) =>
        new()
        {
            Number = number,
            Title = title,
            Subtitle = subtitle,
            State = number < currentStep ? "completed" : number == currentStep ? "active" : "pending"
        };

    private static IReadOnlyList<HealthCheckProjectListItemViewModel> GetHealthCheckProjects()
    {
        var sql = OracleSql.SELECT.getHealthProjectList();
        var dataTable = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().GETDataTable(sql, "", true);

        return dataTable.Rows
            .Cast<DataRow>()
            .Select(Project)
            .ToArray();
    }

    private static int SaveHealthProjectList(HealthProjectWizardViewModel model, string createUserNo)
    {
        var sql = HasHealthProject(model.Project.Code)
            ? OracleSql.UPDATE.UpdateHealthProjectList()
            : OracleSql.INSERT.InsertHealthProjectList();

        if (string.IsNullOrWhiteSpace(sql))
        {
            return 0;
        }

        return /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().ExecuteNonQuery(sql, CreateHealthProjectListParameters(model, createUserNo));
    }

    private static void SaveCheckProjectSchemes(string projectNo, WizardProjectInput input)
    {
        projectNo = Normalize(projectNo);

        if (string.IsNullOrWhiteSpace(projectNo))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(input.SelectedSuites)
            && string.IsNullOrWhiteSpace(input.SelectedOrders))
        {
            return;
        }

        var suites = ParseSelectedProjectSuites(input.SelectedSuites);

        if (suites.Count == 0)
        {
            var selectedOrders = ParseSelectedProjectOrders(input.SelectedOrders);

            if (selectedOrders.Count > 0)
            {
                suites = selectedOrders
                    .GroupBy(order => order.SchemeNo, StringComparer.OrdinalIgnoreCase)
                    .Select(group => new SelectedProjectSuite
                    {
                        SchemeNo = FirstNonEmpty(group.Key, "1"),
                        Name = "組套 " + FirstNonEmpty(group.Key, "1"),
                        FeeType = "自費",
                        SelfPayAccountingUnit = "",
                        PublicExpense = 0,
                        Orders = group.ToArray(),
                        CheckNos = group.Select(order => order.CheckNo).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
                    })
                    .ToArray();
            }
        }

        var connectionFactory = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData();

        connectionFactory.ExecuteNonQuery(OracleSql.DELETE.DeleteCheckProjectScheme(), new Dictionary<string, object?>
        {
            ["PROJECTNO"] = projectNo
        });

        if (suites.Count == 0)
        {
            return;
        }

        var insertSql = OracleSql.INSERT.InsertCheckProjectScheme();

        foreach (var suite in suites)
        {
            var schemeNo = FirstNonEmpty(suite.SchemeNo, "1");
            var checkNos = suite.CheckNos
                .Select(Normalize)
                .Where(checkNo => !string.IsNullOrWhiteSpace(checkNo))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (checkNos.Length == 0)
            {
                continue;
            }

            connectionFactory.ExecuteNonQuery(insertSql, new Dictionary<string, object?>
            {
                ["PROJECTNO"] = projectNo,
                ["SCHEMENO"] = schemeNo,
                ["NAME"] = FirstNonEmpty(suite.Name, "組套 " + schemeNo),
                ["NOTE"] = SuitePersistedNote(suite),
                ["PUBLICEXPENSE"] = suite.PublicExpense,
                ["CHDCTTYPE"] = Normalize(suite.SelfPayAccountingUnit),
                ["FIXITEMS"] = string.Join(",", checkNos)
            });
        }
    }

    private static void SaveCheckProjectSpecialItems(string projectNo, WizardProjectInput input)
    {
        projectNo = Normalize(projectNo);

        if (string.IsNullOrWhiteSpace(projectNo))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(input.SelectedSpecialOperations))
        {
            return;
        }

        var connectionFactory = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData();

        connectionFactory.ExecuteNonQuery(OracleSql.DELETE.DeleteCheckProjectSpecialItemTbl(), new Dictionary<string, object?>
        {
            ["PROJECTNO"] = projectNo
        });

        var specialItems = GetWizardSelectedSpecialOperations(input.SelectedSpecialOperations);

        if (specialItems.Count == 0)
        {
            return;
        }

        var insertSql = OracleSql.INSERT.InsertCheckProjectSpecialItemTbl();
        var distinctItems = specialItems
            .Where(item => !string.IsNullOrWhiteSpace(item.Code))
            .GroupBy(item => Normalize(item.Code), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First());

        foreach (var item in distinctItems)
        {
            connectionFactory.ExecuteNonQuery(insertSql, new Dictionary<string, object?>
            {
                ["PROJECTNO"] = projectNo,
                ["SPECIALITEMNO"] = Normalize(item.Code),
                ["PUBLICEXPENSE"] = Math.Max(0, item.Price)
            });
        }
    }


    private static bool HasHealthProject(string projectNo)
    {
        if (string.IsNullOrWhiteSpace(projectNo))
        {
            return false;
        }

        var sql = OracleSql.SELECT.GetHealthProjectListCount(projectNo);
        var dataTable = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().GETDataTable(sql, "", true);

        if (dataTable.Rows.Count == 0)
        {
            return false;
        }

        return GetInt(dataTable.Rows[0], 0, "ITEMCOUNT") > 0;
    }

    private static IReadOnlyDictionary<string, object?> CreateHealthProjectListParameters(
        HealthProjectWizardViewModel model,
        string createUserNo)
    {
        var project = model.Project;
        var now = DateTime.Now;
        var checkStartDate = ParseProjectDate(project.CheckStartDate) ?? now.Date;
        var checkEndDate = ParseProjectDate(FirstNonEmpty(project.CheckEndDate, project.EndDate)) ?? checkStartDate;
        var projectNo = !string.IsNullOrWhiteSpace(project.Code)
            ? project.Code
            : BuildHealthProjectNo();

        return new Dictionary<string, object?>
        {
            ["PROJECTNO"] = projectNo,
            ["CREATEDATE"] = ParseProjectDate(project.CreatedAt) ?? now,
            ["EDITDATE"] = ParseProjectDate(project.UpdatedAt) ?? now,
            ["CHECKDATE"] = checkStartDate,
            ["CUSTOMERNO"] = project.CustomerNo,
            ["SELFCOSTNO"] = project.SelfPayAccountingUnit,
            ["YEAR"] = project.Year,
            ["CHECKYEAR"] = project.Year,
            ["PROJECTNAME"] = FirstNonEmpty(project.Name, project.ShortName, project.Target),
            ["NOTE"] = FirstNonEmpty(project.Notes, project.Target),
            ["COMPAREVAL"] = project.Category,
            ["ENDDATE"] = checkEndDate,
            ["EXECAREA"] = project.DefaultArea,
            ["FLOWNO"] = "",
            ["FLOWNAME"] = project.FlowSheet,
            ["QUESTIONNAIRENO"] = "",
            ["QUESTIONNAIRENAME"] = project.IsOnlineQuestionEnabled ? project.Questionnaire : "",
            ["ISPASTHISTORY"] = ToYesNo(project.IsPastHistoryEnabled),
            ["ISORALCHECK"] = ToYesNo(project.IsOralCheckEnabled),
            ["ISONLINEQUESTION"] = ToYesNo(project.IsOnlineQuestionEnabled),
            ["STATUS"] = "1",
            ["CREATEUSERNO"] = FirstNonEmpty(createUserNo, project.Owner)
        };
    }

    private static DateTime? ParseProjectDate(string? value)
    {
        var dateText = Normalize(value);

        if (string.IsNullOrWhiteSpace(dateText))
        {
            return null;
        }

        string[] formats =
        [
            "yyyy/MM/dd HH:mm",
            "yyyy/MM/dd",
            "yyyy-MM-dd HH:mm",
            "yyyy-MM-dd"
        ];

        if (DateTime.TryParseExact(dateText, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var exactDate))
        {
            return exactDate;
        }

        return DateTime.TryParse(dateText, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate)
            ? parsedDate
            : null;
    }

    private static string BuildHealthProjectNo()
    {
        var sql = OracleSql.SELECT.getSYSTIMESTAMP();
        var dataTable = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().GETDataTable(sql, "", true);
        var timestamp = dataTable.Rows.Count > 0
            ? FirstNonEmpty(
                GetString(dataTable.Rows[0], "dt_ms"),
                GetString(dataTable.Rows[0], "DT_MS"))
            : "";

        if (string.IsNullOrWhiteSpace(timestamp))
        {
            throw new InvalidOperationException("專案編號產生失敗。");
        }

        return "HC" + timestamp;
    }

    private static IReadOnlyList<SelectOptionViewModel> BuildOptions(string placeholder, IEnumerable<string> values)
    {
        var options = new List<SelectOptionViewModel>
        {
            new() { Value = "", Text = placeholder, IsSelected = true }
        };

        options.AddRange(values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value)
            .Select(value => new SelectOptionViewModel { Value = value, Text = value }));

        return options;
    }

    private static IReadOnlyList<SelectOptionViewModel> BuildDefaultAreaOptions(string? selectedDefaultArea = null)
    {
        var selectedValue = Normalize(selectedDefaultArea);
        var sql = OracleSql.SELECT.getHealthRESERVEAREATBL();
        var dataTable = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().GETDataTable(sql, "", true);
        var areaNames = dataTable.Rows
            .Cast<DataRow>()
            .Select(row => GetString(row, "AREA_NAME"))
            .Where(areaName => !string.IsNullOrWhiteSpace(areaName))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var hasSelectedOption = areaNames.Any(areaName => string.Equals(areaName, selectedValue, StringComparison.Ordinal));

        if (!hasSelectedOption && !string.IsNullOrWhiteSpace(selectedValue))
        {
            areaNames.Add(selectedValue);
            hasSelectedOption = true;
        }

        var options = new List<SelectOptionViewModel>
        {
            new() { Value = "", Text = "請選擇預設執行區域", IsSelected = !hasSelectedOption }
        };

        options.AddRange(areaNames.Select(areaName => new SelectOptionViewModel
        {
            Value = areaName,
            Text = areaName,
            IsSelected = string.Equals(areaName, selectedValue, StringComparison.Ordinal)
        }));

        return options;
    }

    private static IReadOnlyList<SelectOptionViewModel> BuildClientUnitOptions(
        string? selectedClientUnit = null,
        IEnumerable<string>? additionalClientUnits = null)
    {
        var customerOptions = CustomerMaintenanceController.GetCustomerUnitOptions(selectedClientUnit);
        var selectedValue = NormalizeClientUnitOption(FirstNonEmpty(
            customerOptions.FirstOrDefault(option => option.IsSelected && !string.IsNullOrWhiteSpace(option.Value))?.Value,
            selectedClientUnit));
        var clientUnits = customerOptions
            .Select(option => NormalizeClientUnitOption(option.Value))
            .Concat((additionalClientUnits ?? Enumerable.Empty<string>()).Select(NormalizeClientUnitOption))
            .Append(selectedValue)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value)
            .ToArray();
        var hasSelectedOption = clientUnits.Any(value => string.Equals(value, selectedValue, StringComparison.Ordinal));
        var options = new List<SelectOptionViewModel>
        {
            new() { Value = "", Text = "請選擇受檢單位", IsSelected = !hasSelectedOption }
        };

        options.AddRange(clientUnits.Select(value => new SelectOptionViewModel
        {
            Value = value,
            Text = value,
            IsSelected = string.Equals(value, selectedValue, StringComparison.Ordinal)
        }));

        return options;
    }

    private static IReadOnlyList<SelfPayAccountingUnitViewModel> GetSelfPayAccountingUnits()
    {
        var sql = OracleSql.SELECT.getSelfPayAccountingUnits();
        var dataTable = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().GETDataTable(sql, "", false);

        return dataTable.Rows
            .Cast<DataRow>()
            .Select(row => new SelfPayAccountingUnitViewModel
            {
                Code = Normalize(GetString(row, "CHDCTTYPE")),
                Name = Normalize(GetString(row, "CHDCTTYPENAME")),
                EndDateValue = Normalize(GetString(row, "VCHEDATE"))
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Code))
            .GroupBy(item => item.Code, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(item => ParseSelfPayAccountingEndDate(item.EndDateValue))
                .First())
            .ToArray();
    }

    private static string GetSelfPayAccountingUnitDisplay(string? selfCostNo)
    {
        selfCostNo = Normalize(selfCostNo);

        if (string.IsNullOrWhiteSpace(selfCostNo))
        {
            return string.Empty;
        }

        var dataTable = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().GETDataTable(
            OracleSql.SELECT.getSelfPayAccountingUnitDisplay(),
            new Dictionary<string, object?> { ["SELFCOSTNO"] = selfCostNo },
            string.Empty,
            false);

        return dataTable.Rows
            .Cast<DataRow>()
            .Select(row => GetString(row, "DCTTYPE_DISPLAY"))
            .FirstOrDefault()
            ?? string.Empty;
    }

    private static string FindLikelySelfPayAccountingUnit(
        string? clientUnit,
        IReadOnlyList<SelfPayAccountingUnitViewModel> accountingUnits)
    {
        var clientUnitName = NormalizeClientUnitOption(clientUnit);

        if (clientUnitName.StartsWith("[", StringComparison.Ordinal))
        {
            var closingBracketIndex = clientUnitName.IndexOf(']');

            if (closingBracketIndex >= 0)
            {
                clientUnitName = clientUnitName[(closingBracketIndex + 1)..].Trim();
            }
        }

        var clientUnitPrefix = new string(clientUnitName
            .ToUpperInvariant()
            .Take(2)
            .ToArray());

        if (string.IsNullOrWhiteSpace(clientUnitPrefix))
        {
            return "00";
        }

        return accountingUnits
            .Select(item => new
            {
                Item = item,
                Name = Normalize(item.Name).ToUpperInvariant()
            })
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate.Name)
                && candidate.Name.Contains(clientUnitPrefix, StringComparison.Ordinal))
            .OrderByDescending(candidate => ParseSelfPayAccountingEndDate(candidate.Item.EndDateValue))
            .ThenBy(candidate => candidate.Item.Code, StringComparer.OrdinalIgnoreCase)
            .Select(candidate => candidate.Item.Code)
            .FirstOrDefault() ?? "00";
    }

    private static long ParseSelfPayAccountingEndDate(string? value) =>
        long.TryParse(Normalize(value), NumberStyles.Integer, CultureInfo.InvariantCulture, out var endDate)
            ? endDate
            : long.MinValue;

    private static HealthCheckProjectListItemViewModel Project(DataRow row)
    {
        var projectName = GetString(row, "PROJECTNAME");
        var note = GetString(row, "NOTE");
        var customerNo = GetString(row, "CUSTOMERNO");
        var custUnit = GetString(row, "CustUnit");
        var checkYear = GetString(row, "CHECKYEAR");
        var year = FirstNonEmpty(GetString(row, "YEAR"), checkYear);

        return new()
        {
            Code = GetString(row, "PROJECTNO"),
            Name = projectName,
            DisplayName = FirstNonEmpty(note, projectName),
            Hospital = "",
            ClientUnit = custUnit,
            SelfPayAccountingUnit = GetString(row, "SELFCOSTNO"),
            CustomerNo = customerNo,
            CustomerName = GetString(row, "CUSTOMERNAME"),
            Year = year,
            CheckYear = checkYear,
            Status = "啟動",
            StatusTone = "enabled",
            CreatedAt = GetDateString(row, "CREATEDATE"),
            UpdatedAt = GetDateString(row, "EDITDATE"),
            CheckDate = GetDateString(row, "CHECKDATE"),
            EndDate = GetDateString(row, "ENDDATE"),
            CompareVal = GetString(row, "COMPAREVAL"),
            FlowName = GetString(row, "FLOWNAME"),
            DefaultArea = GetString(row, "EXECAREA"),
            Questionnaire = GetString(row, "QUESTIONNAIRENAME"),
            IsPastHistory = GetString(row, "ISPASTHISTORY"),
            IsOralCheck = GetString(row, "ISORALCHECK"),
            IsOnlineQuestion = GetString(row, "ISONLINEQUESTION"),
            Note = note
        };
    }

    private static IReadOnlyList<ProjectCustomerLookupItemViewModel> GetCustomerLookupItems(
        IEnumerable<HealthCheckProjectListItemViewModel> projects)
    {
        var projectCustomers = projects
            .Where(project => !string.IsNullOrWhiteSpace(project.CustomerNo)
                || !string.IsNullOrWhiteSpace(project.CustomerName)
                || !string.IsNullOrWhiteSpace(project.ClientUnit))
            .GroupBy(project => string.Join("|", project.CustomerNo, project.CustomerName, project.ClientUnit, project.CheckYear), StringComparer.Ordinal)
            .Select(group =>
            {
                var project = group.First();

                return new ProjectCustomerLookupItemViewModel
                {
                    CustomerNo = project.CustomerNo,
                    CustomerName = project.CustomerName,
                    CustUnit = project.ClientUnit,
                    CheckYear = project.CheckYear
                };
            })
            .ToArray();
        var customerMasterItems = CustomerMaintenanceController.GetHealthCustomerListDataTable()
            .Rows
            .Cast<DataRow>()
            .Select(row => new ProjectCustomerLookupItemViewModel
            {
                CustomerNo = GetString(row, "CUSTOMERNO"),
                CustomerName = GetString(row, "CUSTOMERNAME"),
                CustUnit = GetString(row, "CustUnit"),
                CheckYear = ""
            });

        return projectCustomers
            .Concat(customerMasterItems)
            .Where(item => !string.IsNullOrWhiteSpace(item.CustomerNo)
                || !string.IsNullOrWhiteSpace(item.CustomerName)
                || !string.IsNullOrWhiteSpace(item.CustUnit))
            .GroupBy(item => string.Join("|", item.CustomerNo, item.CustomerName, item.CustUnit), StringComparer.Ordinal)
            .Select(group => group
                .OrderByDescending(item => item.CheckYear)
                .First())
            .OrderByDescending(item => item.CheckYear)
            .ThenBy(item => item.CustUnit)
            .ToArray();
    }

    private static string GetString(DataRow row, string columnName)
    {
        if (!row.Table.Columns.Contains(columnName) || row[columnName] == DBNull.Value)
        {
            return "";
        }

        return TrimDataValue(row[columnName]);
    }

    private static string GetStringAny(DataRow row, params string[] columnNames)
    {
        foreach (var columnName in columnNames)
        {
            var actualColumnName = row.Table.Columns.Contains(columnName)
                ? columnName
                : row.Table.Columns
                    .Cast<DataColumn>()
                    .FirstOrDefault(column => string.Equals(column.ColumnName, columnName, StringComparison.OrdinalIgnoreCase))
                    ?.ColumnName;

            if (string.IsNullOrWhiteSpace(actualColumnName))
            {
                continue;
            }

            var value = GetString(row, actualColumnName);

            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return "";
    }

    private static string GetFirstStringValue(DataRow row)
    {
        foreach (DataColumn column in row.Table.Columns)
        {
            var value = GetString(row, column.ColumnName);

            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return "";
    }

    private static string GetDateString(DataRow row, string columnName)
    {
        if (!row.Table.Columns.Contains(columnName) || row[columnName] == DBNull.Value)
        {
            return "";
        }

        return row[columnName] is DateTime date
            ? date.ToString("yyyy/MM/dd")
            : TrimDataValue(row[columnName]);
    }

    private static int GetInt(DataRow row, int defaultValue, params string[] columnNames)
    {
        foreach (var columnName in columnNames)
        {
            var actualColumnName = row.Table.Columns.Contains(columnName)
                ? columnName
                : row.Table.Columns
                    .Cast<DataColumn>()
                    .FirstOrDefault(column => string.Equals(column.ColumnName, columnName, StringComparison.OrdinalIgnoreCase))
                    ?.ColumnName;

            if (string.IsNullOrWhiteSpace(actualColumnName) || row[actualColumnName] == DBNull.Value)
            {
                continue;
            }

            if (row[actualColumnName] is IConvertible convertible)
            {
                try
                {
                    return Convert.ToInt32(convertible, CultureInfo.InvariantCulture);
                }
                catch (FormatException)
                {
                }
                catch (InvalidCastException)
                {
                }
                catch (OverflowException)
                {
                }
            }

            var value = TrimDataValue(row[actualColumnName]);

            if (int.TryParse(value, NumberStyles.AllowThousands | NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedValue))
            {
                return parsedValue;
            }

            if (decimal.TryParse(value, NumberStyles.AllowThousands | NumberStyles.AllowDecimalPoint | NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedDecimal))
            {
                return Convert.ToInt32(parsedDecimal);
            }
        }

        return defaultValue;
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "";

    private static string NormalizeClientUnitOption(string? value)
    {
        var clientUnit = Normalize(value);

        return clientUnit.StartsWith("[]", StringComparison.Ordinal)
            ? clientUnit.Substring(2).Trim()
            : clientUnit;
    }

    private static string BuildClientUnitText(string customerNo, string customerName)
    {
        if (!string.IsNullOrWhiteSpace(customerNo) && !string.IsNullOrWhiteSpace(customerName))
        {
            return $"[{customerNo}] {customerName}";
        }

        return customerName;
    }

    private static string ToRocYearText(string? value)
    {
        var yearText = Normalize(value);

        if (!int.TryParse(yearText, out var parsedYear))
        {
            return yearText;
        }

        var rocYear = parsedYear >= 1912 ? parsedYear - 1911 : parsedYear;

        return rocYear.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static bool IsYesNoFlag(string? value, bool defaultValue)
    {
        var flag = Normalize(value).ToUpperInvariant();

        return flag switch
        {
            "Y" or "YES" or "TRUE" or "1" or "ON" => true,
            "N" or "NO" or "FALSE" or "0" or "OFF" => false,
            _ => defaultValue
        };
    }

    private static string ToYesNo(bool value) => value ? "Y" : "N";

    private static string SuitePersistedNote(SelectedProjectSuite suite) =>
        IsInternalSuiteSource(suite.Source)
            ? FirstNonEmpty(suite.Note, InternalSuiteImportNote)
            : suite.Note;

    private static bool IsInternalSuiteSource(string? value) =>
        string.Equals(Normalize(value), InternalSuiteSource, StringComparison.OrdinalIgnoreCase);

    private static bool IsInternalSuiteImport(string? value) =>
        Normalize(value).IndexOf(InternalSuiteImportNote, StringComparison.OrdinalIgnoreCase) >= 0;

    private static HashSet<string> GetInternalSuiteOrderCodeSet(string suiteCode)
    {
        suiteCode = Normalize(suiteCode);

        if (string.IsNullOrWhiteSpace(suiteCode))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            return GetInternalMedicalOrderSuiteOrders(suiteCode)
                .Select(order => Normalize(order.Code))
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static string TrimDataValue(object value) =>
        Convert.ToString(value)?.Trim() ?? "";

    private static string Normalize(string? value) =>
        value?.Trim() ?? "";

    private static IReadOnlyList<string> ParseCheckNoList(string? value) =>
        Normalize(value)
            .Split([',', ';', '\r', '\n', '\t', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(token => Normalize(token.Trim('\'', '"', '(', ')')))
            .Where(checkNo => !string.IsNullOrWhiteSpace(checkNo))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static IReadOnlyList<SelectedProjectOrder> ParseSelectedProjectOrders(string? value)
    {
        var selectedOrders = new List<SelectedProjectOrder>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var token in Normalize(value).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = token.Split(':', 2, StringSplitOptions.TrimEntries);
            var schemeNo = parts.Length > 1 ? parts[0] : "1";
            var checkNo = parts.Length > 1 ? parts[1] : parts[0];

            AddSelectedProjectOrder(selectedOrders, seen, schemeNo, checkNo);
        }

        return selectedOrders;
    }

    private static IReadOnlyList<SelectedProjectSuite> ParseSelectedProjectSuites(string? value)
    {
        value = Normalize(value);

        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        try
        {
            var payload = JsonSerializer.Deserialize<List<SelectedProjectSuitePayload>>(value, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (payload is null || payload.Count == 0)
            {
                return [];
            }

            return payload
                .Select((suite, index) =>
                {
                    var schemeNo = FirstNonEmpty(suite.Id, suite.SchemeNo, (index + 1).ToString(CultureInfo.InvariantCulture));
                    var feeType = FirstNonEmpty(suite.FeeType, suite.CategoryName, suite.Category == "public" ? "公費" : "自費");
                    var suiteSource = IsInternalSuiteSource(suite.Source) || IsInternalSuiteImport(suite.Note)
                        ? InternalSuiteSource
                        : "manual";
                    var isInternalSuite = IsInternalSuiteSource(suiteSource);
                    var internalOrderCodes = isInternalSuite
                        ? GetInternalSuiteOrderCodeSet(schemeNo)
                        : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var selectedOrders = new List<SelectedProjectOrder>();
                    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    bool isInternalImportedOrder(string? source, bool locked) =>
                        locked || IsInternalSuiteSource(source);

                    bool allowOrder(string? checkNo, bool internalImported) =>
                        !isInternalSuite
                        || !internalImported
                        || (internalOrderCodes.Count > 0
                            ? internalOrderCodes.Contains(Normalize(checkNo))
                            : internalImported);

                    foreach (var order in suite.Orders ?? Array.Empty<SelectedProjectSuiteOrderPayload>())
                    {
                        var checkNo = FirstNonEmpty(order.Code, order.CheckNo);
                        var internalImported = isInternalSuite && isInternalImportedOrder(order.Source, order.Locked);

                        if (!allowOrder(checkNo, internalImported))
                        {
                            continue;
                        }

                        AddSelectedProjectOrder(
                            selectedOrders,
                            seen,
                            schemeNo,
                            checkNo,
                            order.Name,
                            FirstNonEmpty(order.Category, order.CategoryName, order.ItemCategory),
                            internalImported ? InternalSuiteSource : order.Source,
                            internalImported);
                    }

                    foreach (var checkNo in suite.CheckNos ?? Array.Empty<string>())
                    {
                        var locked = isInternalSuite;

                        if (!allowOrder(checkNo, locked))
                        {
                            continue;
                        }

                        AddSelectedProjectOrder(
                            selectedOrders,
                            seen,
                            schemeNo,
                            checkNo,
                            source: locked ? InternalSuiteSource : "",
                            locked: locked);
                    }

                    if (isInternalSuite && internalOrderCodes.Count > 0)
                    {
                        foreach (var checkNo in internalOrderCodes.OrderBy(code => code, StringComparer.OrdinalIgnoreCase))
                        {
                            AddSelectedProjectOrder(
                                selectedOrders,
                                seen,
                                schemeNo,
                                checkNo,
                                source: InternalSuiteSource,
                                locked: true);
                        }
                    }

                    return new SelectedProjectSuite
                    {
                        SchemeNo = schemeNo,
                        Source = suiteSource,
                        Name = FirstNonEmpty(suite.Name, "組套 " + schemeNo),
                        Note = IsInternalSuiteSource(suiteSource) ? FirstNonEmpty(suite.Note, InternalSuiteImportNote) : suite.Note,
                        FeeType = feeType,
                        SelfPayAccountingUnit = Normalize(suite.SelfPayAccountingUnit),
                        SelfPayAccountingUnitName = Normalize(suite.SelfPayAccountingUnitName),
                        PublicExpense = Math.Max(0, suite.Price),
                        Orders = selectedOrders,
                        CheckNos = selectedOrders.Select(order => order.CheckNo).ToArray()
                    };
                })
                .Where(suite => suite.CheckNos.Count > 0)
                .ToArray();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static void AddSelectedProjectOrder(
        List<SelectedProjectOrder> selectedOrders,
        HashSet<string> seen,
        string? schemeNo,
        string? checkNo,
        string? name = null,
        string? category = null,
        string? source = null,
        bool locked = false)
    {
        schemeNo = FirstNonEmpty(Normalize(schemeNo), "1");
        checkNo = Normalize(checkNo);

        if (string.IsNullOrWhiteSpace(checkNo))
        {
            return;
        }

        var key = string.Join("|", schemeNo, checkNo);

        if (seen.Add(key))
        {
            selectedOrders.Add(new SelectedProjectOrder(
                schemeNo,
                checkNo,
                Normalize(name),
                Normalize(category),
                Normalize(source),
                locked));
        }
    }

    private static IReadOnlyList<ProjectSpecialOperationItemViewModel> GetWizardSpecialOperationItems()
    {
        var sql = OracleSql.SELECT.getSpecialCategoryTBL();
        var dataTable = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().GETDataTable(sql, "", true);

        return dataTable.Rows
            .Cast<DataRow>()
            .Select(SpecialOperation)
            .Where(item => !string.IsNullOrWhiteSpace(item.Code) || !string.IsNullOrWhiteSpace(item.Name))
            .GroupBy(item => FirstNonEmpty(item.Code, item.Name), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
    }

    private static IReadOnlyList<ProjectSpecialOperationItemViewModel> GetWizardSelectedSpecialOperations(string? value)
    {
        value = Normalize(value);

        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        try
        {
            var payload = JsonSerializer.Deserialize<List<SelectedSpecialOperationPayload>>(value, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (payload is null || payload.Count == 0)
            {
                return [];
            }

            return payload
                .Select((item, index) =>
                {
                    var code = Normalize(item.Code);
                    var manualText = FirstNonEmpty(item.ManualText, item.Note);

                    return new ProjectSpecialOperationItemViewModel
                    {
                        SortOrder = index + 1,
                        Code = code,
                        Name = FirstNonEmpty(item.Name, manualText, code),
                        Category = "",
                        CategoryKey = "",
                        Note = manualText,
                        ManualText = manualText,
                        BillingCategory = FirstNonEmpty(item.BillingCategory, "問卷項目"),
                        Price = Math.Max(0, item.Price),
                        Selected = true
                    };
                })
                .Where(item => !string.IsNullOrWhiteSpace(item.Code) || !string.IsNullOrWhiteSpace(item.Name))
                .ToArray();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static ProjectSpecialOperationItemViewModel SpecialOperation(DataRow row, int index)
    {
        var fallbackValue = GetFirstStringValue(row);
        var code = FirstNonEmpty(
            GetStringAny(row, "SPECIALCATEGORYNO", "SPECIALCATEGORYCODE", "SPECIALNO", "CATEGORYNO", "CATEGORYCODE", "SPECIALCATEGORY", "CATEGORY"),
            GetStringAny(row, "CODE", "NO", "ID"),
            fallbackValue);
        var name = FirstNonEmpty(
            GetStringAny(row, "SPECIALCATEGORYNAME", "SPECIALNAME", "CATEGORYNAME", "SPECIALCATEGORY", "CATEGORY", "NAME"),
            GetStringAny(row, "DESCRIPTION", "DESC", "TEXT", "DISPLAYNAME"),
            code,
            fallbackValue);
        var note = GetStringAny(row, "NOTE", "MEMO", "REMARK", "DESCRIPTION");
        var categoryKey = FirstNonEmpty(
            GetStringAny(row, "CATEGORYKEY", "SPECIALCATEGORYKEY"),
            SpecialOperationCategoryKey(FirstNonEmpty(GetStringAny(row, "FIRSTCHAR"), name, code)));

        return new()
        {
            SortOrder = index + 1,
            Code = FirstNonEmpty(code, name),
            Name = FirstNonEmpty(name, code),
            Category = categoryKey,
            CategoryKey = categoryKey,
            Note = note,
            BillingCategory = "問卷項目",
            Price = GetInt(row, 0, "PRICE", "AMOUNT", "PUBLICEXPENSE", "FEE"),
            Selected = false
        };
    }

    private static string SpecialOperationCategoryKey(string value)
    {
        value = Normalize(value);

        if (string.IsNullOrWhiteSpace(value))
        {
            return "其他";
        }

        var firstChar = StringInfo.GetNextTextElement(value, 0);
        var codePoint = char.ConvertToUtf32(firstChar, 0);
        const string excludedChars = "〇零一二三四五六七八九十百千萬万億亿兩两壹貳贰參叁肆伍陸陆柒捌玖拾佰仟甲乙丙丁戊己庚辛壬癸";

        return codePoint is >= 0x4E00 and <= 0x9FFF && !excludedChars.Contains(firstChar, StringComparison.Ordinal)
            ? firstChar
            : "其他";
    }


    private static string FormatProjectDate(DateTime value) =>
        value.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);

    private static IReadOnlyList<ProjectPackageCategoryViewModel> GetWizardCategories()
    {
        var sql = OracleSql.SELECT.getCheckItemCategories();
        var dataTable = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().GETDataTable(sql, "", true);

        return dataTable.Rows
            .Cast<DataRow>()
            .Select(row => GetString(row, "CHECKCATEGORY"))
            .Where(category => !string.IsNullOrWhiteSpace(category))
            .Distinct(StringComparer.Ordinal)
            .Select(category => Category(category, category, CategoryTone(category), CategoryIcon(category)))
            .ToArray();
    }

    private static IReadOnlyList<ProjectPackageItemViewModel> GetWizardAvailableItems(string? category = null, string? keyword = null)
    {
        var sql = OracleSql.SELECT.getCheckItemsByCategory(category ?? "", keyword ?? "");
        var dataTable = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().GETDataTable(sql, "", true);

        return dataTable.Rows
            .Cast<DataRow>()
            .Select((row, index) => CheckItem(row, index, category))
            .Where(item => !string.IsNullOrWhiteSpace(item.Code) || !string.IsNullOrWhiteSpace(item.Name))
            .ToArray();
    }

    private static IReadOnlyList<ProjectPackageItemViewModel> GetWizardSelectedItems(
        IReadOnlyList<SelectedProjectOrder> selectedOrders)
    {
        if (selectedOrders.Count == 0)
        {
            return [];
        }

        var itemsByCode = GetSelectedItemLookup(selectedOrders.Select(order => order.CheckNo));

        return selectedOrders
            .Select((selectedOrder, index) =>
            {
                itemsByCode.TryGetValue(selectedOrder.CheckNo, out var item);

                var carriedCategory = Normalize(selectedOrder.Category);
                var carriedName = Normalize(selectedOrder.Name);
                var category = FirstNonEmpty(item?.Category, item?.ItemCategory, carriedCategory);
                var reportName = FirstNonEmpty(item?.ReportName, carriedName);

                return new ProjectPackageItemViewModel
                {
                    SortOrder = index + 1,
                    Code = selectedOrder.CheckNo,
                    Name = FirstNonEmpty(item?.Name, carriedName, selectedOrder.CheckNo),
                    Category = category,
                    CategoryKey = FirstNonEmpty(item?.CategoryKey, category),
                    ItemCategory = FirstNonEmpty(item?.ItemCategory, category),
                    ReportName = reportName,
                    Icon = CategoryIcon(FirstNonEmpty(category, reportName, carriedName)),
                    Price = item?.Price ?? 0,
                    Required = item?.Required ?? false,
                    Status = "啟用",
                    StatusTone = "enabled",
                    Selected = true
                };
            })
            .ToArray();
    }

    private static IReadOnlyDictionary<string, ProjectPackageItemViewModel> GetSelectedItemLookup(IEnumerable<string> checkNos)
    {
        var normalizedCheckNos = checkNos
            .Select(Normalize)
            .Where(checkNo => !string.IsNullOrWhiteSpace(checkNo))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var itemsByCode = new Dictionary<string, ProjectPackageItemViewModel>(StringComparer.OrdinalIgnoreCase);

        AddLookupItems(itemsByCode, GetHealthCheckItemsByCodes(normalizedCheckNos));

        var missingCheckNos = normalizedCheckNos
            .Where(checkNo => !itemsByCode.ContainsKey(checkNo))
            .ToArray();

        if (missingCheckNos.Length > 0)
        {
            AddLookupItems(itemsByCode, GetInternalMedicalOrderItemsByCodes(missingCheckNos));
        }

        return itemsByCode;
    }

    private static IReadOnlyList<ProjectPackageItemViewModel> GetHealthCheckItemsByCodes(IReadOnlyList<string> checkNos)
    {
        if (checkNos.Count == 0)
        {
            return [];
        }

        var sql = OracleSql.SELECT.getCheckItemsByCodes(string.Join(",", checkNos));
        var dataTable = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().GETDataTable(sql, "", true);

        return dataTable.Rows
            .Cast<DataRow>()
            .Select((row, index) => CheckItem(row, index, null))
            .Where(item => !string.IsNullOrWhiteSpace(item.Code))
            .ToArray();
    }

    private static IReadOnlyList<ProjectPackageItemViewModel> GetInternalMedicalOrderItemsByCodes(IReadOnlyList<string> checkNos)
    {
        if (checkNos.Count == 0)
        {
            return [];
        }

        try
        {
            var sql = OracleSql.SELECT.getInternalMedicalOrderItemsByCodes(string.Join(",", checkNos));
            var dataTable = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().GETDataTable(sql, "", false);

            return dataTable.Rows
                .Cast<DataRow>()
                .Select((row, index) => CheckItem(row, index, "院內組套"))
                .Where(item => !string.IsNullOrWhiteSpace(item.Code))
                .ToArray();
        }
        catch
        {
            return [];
        }
    }

    private static void AddLookupItems(
        IDictionary<string, ProjectPackageItemViewModel> itemsByCode,
        IEnumerable<ProjectPackageItemViewModel> items)
    {
        foreach (var item in items)
        {
            if (!string.IsNullOrWhiteSpace(item.Code) && !itemsByCode.ContainsKey(item.Code))
            {
                itemsByCode.Add(item.Code, item);
            }
        }
    }

    private static IReadOnlyList<ProjectPackageCategoryViewModel> GetCategories() =>
    [
        Category("一般檢查", "general", "blue", "stethoscope", true),
        Category("醫學檢查", "medical", "red", "heart"),
        Category("血液檢查", "blood", "red", "drop"),
        Category("生化檢查", "biochemistry", "green", "flask"),
        Category("尿液檢查", "urine", "amber", "beaker"),
        Category("糞便檢查", "stool", "brown", "sample"),
        Category("影像檢查", "image", "blue", "image"),
        Category("超音波", "ultrasound", "purple", "wave"),
        Category("心電圖", "ecg", "red", "pulse"),
        Category("肺功能", "lung", "teal", "lung"),
        Category("腸胃檢查", "gi", "orange", "stomach"),
        Category("眼科", "eye", "indigo", "eye"),
        Category("耳鼻喉", "ent", "purple", "ear"),
        Category("婦科", "gyn", "pink", "female"),
        Category("泌尿", "urology", "blue", "water"),
        Category("內分泌", "endocrine", "teal", "nodes"),
        Category("肝功能", "liver", "purple", "liver"),
        Category("腎功能", "kidney", "green", "kidney"),
        Category("過敏", "allergy", "orange", "spark"),
        Category("癌症篩檢", "cancer", "teal", "ribbon")
    ];

    private static IReadOnlyList<ProjectPackageItemViewModel> GetAvailableItems() =>
    [
        Item(1, "身高", "一般檢查", "general", "stethoscope", 100, true, true),
        Item(2, "體重", "一般檢查", "general", "scale", 100, true, true),
        Item(3, "BMI", "一般檢查", "general", "calculator", 150, true, true),
        Item(4, "血壓", "一般檢查", "general", "pulse", 100, true, true),
        Item(5, "視力檢查", "一般檢查", "general", "eye", 300, true, true),
        Item(6, "聽力檢查", "一般檢查", "general", "ear", 300, true, true),
        Item(7, "血液檢查", "血液檢查", "blood", "drop", 500, true, true),
        Item(8, "肝功能檢查", "生化檢查", "biochemistry", "flask", 950, false, false),
        Item(9, "腎功能檢查", "生化檢查", "biochemistry", "kidney", 1050, false, false),
        Item(10, "尿液常規", "尿液檢查", "urine", "beaker", 400, false, false),
        Item(11, "糞便潛血檢查", "糞便檢查", "stool", "sample", 300, false, false),
        Item(12, "胸部 X 光檢查", "影像檢查", "image", "image", 900, false, false),
        Item(13, "腹部超音波", "超音波", "ultrasound", "wave", 1200, false, false),
        Item(14, "心電圖檢查", "心電圖", "ecg", "pulse", 450, false, false),
        Item(15, "肺功能檢查", "肺功能", "lung", "lung", 450, false, false),
        Item(16, "胃幽門螺旋桿菌", "腸胃檢查", "gi", "stomach", 800, false, false),
        Item(17, "眼壓檢查", "眼科", "eye", "eye", 250, false, false),
        Item(18, "耳鼻喉檢查", "耳鼻喉", "ent", "ear", 300, false, false),
        Item(19, "甲狀腺功能", "內分泌", "endocrine", "nodes", 700, false, false),
        Item(20, "過敏原檢測", "過敏", "allergy", "spark", 1600, false, false)
    ];

    private static IReadOnlyList<ProjectPackageItemViewModel> GetSelectedItems() =>
    [
        Item(1, "身高", "一般檢查", "general", "stethoscope", 100, true, true),
        Item(2, "體重", "一般檢查", "general", "scale", 100, true, true),
        Item(3, "BMI", "一般檢查", "general", "calculator", 150, true, true),
        Item(4, "血壓", "一般檢查", "general", "pulse", 100, true, true),
        Item(5, "血液檢查", "生化檢查", "biochemistry", "drop", 500, true, true),
        Item(6, "肝功能檢查", "生化檢查", "biochemistry", "flask", 950, true, true),
        Item(7, "腎功能檢查", "生化檢查", "biochemistry", "kidney", 1050, true, true),
        Item(8, "尿液常規檢查", "尿液檢查", "urine", "beaker", 400, true, true),
        Item(9, "糞便常規檢查", "糞便檢查", "stool", "sample", 300, true, true),
        Item(10, "胸部X光檢查", "影像檢查", "image", "image", 900, true, true),
        Item(11, "心電圖檢查", "心電圖", "ecg", "pulse", 450, true, true),
        Item(12, "視力檢查", "一般檢查", "general", "eye", 300, true, true),
        Item(13, "聽力檢查", "一般檢查", "general", "ear", 300, true, true)
    ];

    private static ProjectPackageCategoryViewModel Category(
        string name,
        string key,
        string tone,
        string icon,
        bool active = false) =>
        new()
        {
            Name = name,
            Key = key,
            Tone = tone,
            Icon = icon,
            Active = active
        };

    private static ProjectPackageItemViewModel Item(
        int sortOrder,
        string name,
        string category,
        string categoryKey,
        string icon,
        int price,
        bool required,
        bool selected,
        string? code = null,
        string? itemCategory = null,
        string? reportName = null) =>
        new()
        {
            SortOrder = sortOrder,
            Code = FirstNonEmpty(code, $"ORD{sortOrder.ToString("000", CultureInfo.InvariantCulture)}"),
            Name = name,
            Category = category,
            CategoryKey = categoryKey,
            ItemCategory = FirstNonEmpty(itemCategory, category),
            ReportName = reportName ?? "",
            Icon = icon,
            Price = price,
            Required = required,
            Status = "啟用",
            StatusTone = "enabled",
            Selected = selected
        };

    private static ProjectPackageItemViewModel CheckItem(DataRow row, int index, string? fallbackCategory)
    {
        var checkCategory = FirstNonEmpty(GetString(row, "CHECKCATEGORY"), fallbackCategory);
        var itemCategory = GetString(row, "ITEMCATEGORY");
        var reportName = GetString(row, "REPORTNAME");

        return new()
        {
            SortOrder = index + 1,
            Code = GetString(row, "CHECKNO"),
            Name = GetString(row, "NAME"),
            Category = checkCategory,
            CategoryKey = checkCategory,
            ItemCategory = itemCategory,
            ReportName = reportName,
            Icon = CategoryIcon(FirstNonEmpty(checkCategory, itemCategory, reportName)),
            Price = 0,
            Required = false,
            Status = "啟用",
            StatusTone = "enabled",
            Selected = false
        };
    }

    private static string CategoryTone(string value)
    {
        var category = Normalize(value);

        if (category.Contains("血", StringComparison.Ordinal) || category.Contains("心", StringComparison.Ordinal))
        {
            return "red";
        }

        if (category.Contains("生化", StringComparison.Ordinal)
            || category.Contains("肝", StringComparison.Ordinal)
            || category.Contains("腎", StringComparison.Ordinal))
        {
            return "green";
        }

        if (category.Contains("尿", StringComparison.Ordinal)
            || category.Contains("影像", StringComparison.Ordinal)
            || category.Contains("超音波", StringComparison.Ordinal))
        {
            return "blue";
        }

        if (category.Contains("婦", StringComparison.Ordinal))
        {
            return "pink";
        }

        if (category.Contains("糞", StringComparison.Ordinal) || category.Contains("腸胃", StringComparison.Ordinal))
        {
            return "brown";
        }

        if (category.Contains("特殊", StringComparison.Ordinal) || category.Contains("過敏", StringComparison.Ordinal))
        {
            return "orange";
        }

        return "teal";
    }

    private static string CategoryIcon(string value)
    {
        var category = Normalize(value);

        if (category.Contains("影像", StringComparison.Ordinal) || category.Contains("X", StringComparison.OrdinalIgnoreCase))
        {
            return "image";
        }

        if (category.Contains("血", StringComparison.Ordinal))
        {
            return "drop";
        }

        if (category.Contains("生化", StringComparison.Ordinal) || category.Contains("檢驗", StringComparison.Ordinal))
        {
            return "flask";
        }

        if (category.Contains("尿", StringComparison.Ordinal))
        {
            return "beaker";
        }

        if (category.Contains("心", StringComparison.Ordinal))
        {
            return "pulse";
        }

        if (category.Contains("肺", StringComparison.Ordinal))
        {
            return "lung";
        }

        if (category.Contains("眼", StringComparison.Ordinal))
        {
            return "eye";
        }

        if (category.Contains("耳", StringComparison.Ordinal))
        {
            return "ear";
        }

        if (category.Contains("超音波", StringComparison.Ordinal))
        {
            return "wave";
        }

        if (category.Contains("肝", StringComparison.Ordinal))
        {
            return "liver";
        }

        if (category.Contains("腎", StringComparison.Ordinal))
        {
            return "kidney";
        }

        if (category.Contains("糞", StringComparison.Ordinal))
        {
            return "sample";
        }

        if (category.Contains("婦", StringComparison.Ordinal))
        {
            return "female";
        }

        if (category.Contains("特殊", StringComparison.Ordinal) || category.Contains("過敏", StringComparison.Ordinal))
        {
            return "spark";
        }

        return "stethoscope";
    }
}





