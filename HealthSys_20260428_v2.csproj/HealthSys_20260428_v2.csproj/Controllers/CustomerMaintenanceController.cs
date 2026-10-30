using HealthSys_20260428_v2.csproj.Data;
using HealthSys_20260428_v2.csproj.Models;
using Microsoft.AspNetCore.Mvc;
using System.Data;

namespace HealthSys_20260428_v2.csproj.Controllers
{
    public class CustomerMaintenanceController : Controller
    {
        public IActionResult CustomerMaintenance()
        {
            var customers = GetCustomerMaintenanceItems();
            var model = new CustomerMaintenanceViewModel
            {
                Customers = customers,
                SelectedCustomer = customers.FirstOrDefault(customer => customer.Selected) ?? customers.FirstOrDefault(),
                PageSizeOptions = [10, 20, 30, 40, 50],
                TotalCustomerCount = customers.Count
            };

            return View(model);
        }

        public IActionResult CustomerDetailFrame(string customerNo)
        {
            var normalizedCustomerNo = Normalize(customerNo);

            if (string.IsNullOrWhiteSpace(normalizedCustomerNo))
            {
                return NotFound();
            }

            var customer = GetHealthCustomer(normalizedCustomerNo);

            if (customer is null)
            {
                return NotFound();
            }

            return View(CreateCustomerDetailItem(customer));
        }

        public IActionResult AddCustomerFrame()
        {
            var customers = GetCustomerMaintenanceItems();
            var nextCustomerNo = customers
                .Select(customer => int.TryParse(customer.CustomerNo, out var customerNo) ? customerNo : 0)
                .DefaultIfEmpty(0)
                .Max() + 1;

            var model = new AddCustomerFrameViewModel
            {
                SuggestedCustomerNo = nextCustomerNo.ToString("D4")
            };

            return View(model);
        }

        public IActionResult EditCustomerFrame(string customerNo)
        {
            var normalizedCustomerNo = Normalize(customerNo);

            if (string.IsNullOrWhiteSpace(normalizedCustomerNo))
            {
                return NotFound();
            }

            var customer = GetHealthCustomer(normalizedCustomerNo);

            if (customer is null)
            {
                return NotFound();
            }

            return View("AddCustomerFrame", CreateEditCustomerFrameModel(customer));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateCustomer([FromForm] CustomerCreatePostModel input)
        {
            var customer = NormalizeCustomerCreatePostModel(input);

            if (string.IsNullOrWhiteSpace(customer.CustomerNo) || string.IsNullOrWhiteSpace(customer.CustomerName))
            {
                return Json(new
                {
                    saved = false,
                    created = false,
                    insertedCount = 0,
                    message = "請填寫公司編號與公司名稱。"
                });
            }

            var insertSql = OracleSql.INSERT.InsertHealthCustomer();

            try
            {
                var insertedCount = string.IsNullOrWhiteSpace(insertSql)
                    ? 0
                    : /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().ExecuteNonQuery(insertSql, CreateHealthCustomerParameters(customer));
                var saved = insertedCount >= 1;

                return Json(new
                {
                    saved,
                    created = saved,
                    insertedCount,
                    message = saved ? "公司資料已儲存。" : "公司資料未寫入，請確認資料庫狀態。",
                    customer = CreateCustomerResponse(customer, projectCount: 0)
                });
            }
            catch
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    saved = false,
                    created = false,
                    insertedCount = 0,
                    message = "資料庫寫入失敗，請稍後再試。"
                });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateCustomer([FromForm] CustomerCreatePostModel input)
        {
            var customer = NormalizeCustomerCreatePostModel(input);

            if (string.IsNullOrWhiteSpace(customer.CustomerNo) || string.IsNullOrWhiteSpace(customer.CustomerName))
            {
                return Json(new
                {
                    saved = false,
                    updated = false,
                    updatedCount = 0,
                    message = "請填寫公司編號與公司名稱。"
                });
            }

            var updateSql = OracleSql.UPDATE.UpdateHealthCustomer();

            try
            {
                var updatedCount = string.IsNullOrWhiteSpace(updateSql)
                    ? 0
                    : /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().ExecuteNonQuery(updateSql, CreateHealthCustomerParameters(customer));
                var saved = updatedCount >= 1;

                return Json(new
                {
                    saved,
                    updated = saved,
                    updatedCount,
                    message = saved ? "公司資料已更新。" : "公司資料未更新，請確認資料庫狀態。",
                    customer = CreateCustomerResponse(customer, projectCount: GetCustomerProjectItems(customer.CustomerNo ?? "").Count)
                });
            }
            catch
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    saved = false,
                    updated = false,
                    updatedCount = 0,
                    message = "公司資料更新失敗，請稍後再試。"
                });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteCustomers([FromForm] CustomerDeletePostModel input)
        {
            var customerNos = NormalizeCustomerNos(input.CustomerNos.Concat(GetCustomerNosFromForm(Request.Form)));

            if (customerNos.Length == 0)
            {
                return Json(new
                {
                    deleted = false,
                    deletedCount = 0,
                    requestedCount = 0,
                    customerNos = Array.Empty<string>(),
                    message = "請先選擇要刪除的公司。"
                });
            }

            try
            {
                var oracleConnection = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData();
                var deletedCustomerNos = new List<string>();
                var deletedCount = 0;

                foreach (var customerNo in customerNos)
                {
                    var deleteSql = OracleSql.DELETE.DeleteHealthCustomer(customerNo);
                    var affectedCount = string.IsNullOrWhiteSpace(deleteSql)
                        ? 0
                        : oracleConnection.ExecuteNonQuery(deleteSql);

                    if (affectedCount >= 1)
                    {
                        deletedCount += affectedCount;
                        deletedCustomerNos.Add(customerNo);
                    }
                }

                return Json(new
                {
                    deleted = deletedCustomerNos.Count > 0,
                    deletedCount,
                    requestedCount = customerNos.Length,
                    customerNos = deletedCustomerNos,
                    failedCustomerNos = customerNos.Except(deletedCustomerNos, StringComparer.Ordinal).ToArray(),
                    message = deletedCustomerNos.Count > 0
                        ? $"已刪除 {deletedCustomerNos.Count} 筆公司資料。"
                        : "未刪除任何公司資料，請確認資料庫狀態。"
                });
            }
            catch
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    deleted = false,
                    deletedCount = 0,
                    requestedCount = customerNos.Length,
                    customerNos = Array.Empty<string>(),
                    message = "公司刪除失敗，請稍後再試。"
                });
            }
        }

        private static IReadOnlyList<CustomerListItemViewModel> GetCustomerMaintenanceItems()
        {
            var dataTable = GetHealthCustomerListDataTable();

            return dataTable.Rows
                .Cast<DataRow>()
                .Select((row, index) =>
                {
                    var customerNo = GetString(row, "CUSTOMERNO");

                    return new CustomerListItemViewModel
                    {
                        Selected = index == 0,
                        CustomerNo = customerNo,
                        CustomerName = GetString(row, "CUSTOMERNAME"),
                        EnglishName = GetString(row, "ENGLISHNAME"),
                        UniformNo = GetString(row, "UNIFORMNO"),
                        Address = GetString(row, "ADDRESS"),
                        Tel = GetString(row, "TEL"),
                        Fax = GetString(row, "FAX"),
                        Email = GetString(row, "EMAIL"),
                        ContactPerson = GetString(row, "CONTACTPERSON"),
                        BusinessRegAddress = GetString(row, "BUSINESSREGADDRESS"),
                        TaxOfficeCode = GetString(row, "TAXOFFICECODE"),
                        BusinessUnion = GetString(row, "BUSINESSUNION"),
                        IndustryCategory = GetString(row, "INDUSTRYCATEGORY"),
                        BusinessUnitName = GetString(row, "BUSINESSUNITNAME"),
                        BusinessUnitAddress = GetString(row, "BUSINESSUNITADDRESS"),
                        MedicalManageCode = GetString(row, "MEDICALMANAGECODE"),
                        AccountingUnit = GetString(row, "ACCOUNTINGUNIT"),
                        CustomerNote = GetString(row, "CUSTOMERNOTE"),
                        Projects = GetCustomerProjectItems(customerNo)
                    };
                })
                .ToArray();
        }

        internal static IReadOnlyList<SelectOptionViewModel> GetCustomerUnitOptions(string? selectedClientUnit = null)
        {
            var selectedValue = NormalizeCustomerUnitOption(selectedClientUnit);
            var customerUnits = GetHealthCustomerListDataTable()
                .Rows
                .Cast<DataRow>()
                .Select(row => new
                {
                    CustomerNo = GetString(row, "CUSTOMERNO"),
                    CustomerName = GetString(row, "CUSTOMERNAME"),
                    CustUnit = NormalizeCustomerUnitOption(GetString(row, "CustUnit"))
                })
                .Where(customer => !string.IsNullOrWhiteSpace(customer.CustUnit))
                .GroupBy(customer => customer.CustUnit, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(customer => customer.CustUnit)
                .ToArray();

            var hasSelectedOption = customerUnits.Any(customer => IsSelectedCustomerUnit(customer.CustUnit, customer.CustomerNo, customer.CustomerName, selectedValue));
            var options = new List<SelectOptionViewModel>
            {
                new() { Value = "", Text = "請選擇受檢單位", IsSelected = !hasSelectedOption }
            };

            options.AddRange(customerUnits.Select(customer => new SelectOptionViewModel
            {
                Value = customer.CustUnit,
                Text = customer.CustUnit,
                IsSelected = IsSelectedCustomerUnit(customer.CustUnit, customer.CustomerNo, customer.CustomerName, selectedValue)
            }));

            return options;
        }

        internal static DataTable GetHealthCustomerListDataTable()
        {
            var sql = OracleSql.SELECT.getHealthCustomerList();

            return /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().GETDataTable(sql, "", true);
        }

        private static CustomerCreatePostModel? GetHealthCustomer(string customerNo)
        {
            var sql = OracleSql.SELECT.getHealthCustomer(customerNo);
            var dataTable = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().GETDataTable(sql, "", true);
            var row = dataTable.Rows.Cast<DataRow>().FirstOrDefault();

            return row is null
                ? null
                : new CustomerCreatePostModel
                {
                    CustomerNo = GetString(row, "CUSTOMERNO"),
                    CustomerName = GetString(row, "CUSTOMERNAME"),
                    Address = GetString(row, "ADDRESS"),
                    Tel = GetString(row, "TEL"),
                    Fax = GetString(row, "FAX"),
                    Email = GetString(row, "EMAIL"),
                    CustomerNote = GetString(row, "CUSTOMERNOTE"),
                    EnglishName = GetString(row, "ENGLISHNAME"),
                    UniformNo = GetString(row, "UNIFORMNO"),
                    ContactPerson = GetString(row, "CONTACTPERSON"),
                    BusinessRegAddress = GetString(row, "BUSINESSREGADDRESS"),
                    TaxOfficeCode = GetString(row, "TAXOFFICECODE"),
                    BusinessUnion = GetString(row, "BUSINESSUNION"),
                    IndustryCategory = GetString(row, "INDUSTRYCATEGORY"),
                    BusinessUnitName = GetString(row, "BUSINESSUNITNAME"),
                    BusinessUnitAddress = GetString(row, "BUSINESSUNITADDRESS"),
                    MedicalManageCode = GetString(row, "MEDICALMANAGECODE"),
                    AccountingUnit = GetString(row, "ACCOUNTINGUNIT")
                };
        }

        private static IReadOnlyList<CustomerProjectListItemViewModel> GetCustomerProjectItems(string customerNo)
        {
            if (string.IsNullOrWhiteSpace(customerNo))
            {
                return [];
            }

            var sql = OracleSql.SELECT.getHealthCustomerProjects();
            var dataTable = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().GETDataTable(sql, new Dictionary<string, object?>
            {
                ["CUSTOMERNO"] = customerNo
            }, "", true);

            return dataTable.Rows
                .Cast<DataRow>()
                .Select((row, index) =>
                {
                    var checkYear = GetString(row, "CHECKYEAR");
                    var appointmentDetails = GetFakeCustomerProjectAppointmentDetails(checkYear, index);
                    var projectStatus = appointmentDetails
                        .Select(detail => (detail.Status ?? "").Trim())
                        .Where(status => !string.IsNullOrWhiteSpace(status))
                        .Distinct()
                        .ToArray();

                    return new CustomerProjectListItemViewModel
                    {
                        CustomerNo = customerNo,
                        ProjectNo = GetString(row, "PROJECTNO"),
                        CheckYear = checkYear,
                        ProjectName = GetString(row, "PROJECTNAME"),
                        ReserveCount = appointmentDetails
                            .Sum(detail => int.TryParse(detail.ReserveCount, out var count) ? count : 0)
                            .ToString(),
                        StartDate = appointmentDetails.FirstOrDefault()?.StartDate ?? "",
                        EndDate = appointmentDetails.LastOrDefault()?.EndDate ?? "",
                        Status = projectStatus.Length > 0 ? string.Join(" / ", projectStatus) : "",
                        AppointmentDetails = appointmentDetails
                    };
                })
                .ToArray();
        }

        private static IReadOnlyList<CustomerProjectAppointmentDetailViewModel> GetFakeCustomerProjectAppointmentDetails(string checkYear, int projectIndex)
        {
            var year = int.TryParse(checkYear, out var parsedYear) && parsedYear >= 1911
                ? parsedYear
                : 2026;
            var firstMonth = Math.Min(10, 3 + projectIndex % 4 * 2);
            var firstStartDate = new DateTime(year, firstMonth, 1);

            return
            [
                new CustomerProjectAppointmentDetailViewModel
                {
                    ReserveCount = (36 + projectIndex * 3).ToString(),
                    StartDate = FormatDate(firstStartDate),
                    EndDate = FormatDate(firstStartDate.AddDays(27)),
                    Status = "開放預約"
                }
            ];
        }

        private static string FormatDate(DateTime value) =>
            value.ToString("yyyy/MM/dd");

        private static string GetString(DataRow row, string columnName)
        {
            var column = row.Table.Columns
                .Cast<DataColumn>()
                .FirstOrDefault(item => string.Equals(item.ColumnName, columnName, StringComparison.OrdinalIgnoreCase));

            if (column is null || row[column] == DBNull.Value)
            {
                return "";
            }

            return Convert.ToString(row[column])?.Trim() ?? "";
        }

        private static string NormalizeCustomerUnitOption(string? value)
        {
            var custUnit = Normalize(value);

            return custUnit.StartsWith("[]", StringComparison.Ordinal)
                ? custUnit.Substring(2).Trim()
                : custUnit;
        }

        private static bool IsSelectedCustomerUnit(string custUnit, string customerNo, string customerName, string selectedValue)
        {
            if (string.IsNullOrWhiteSpace(selectedValue))
            {
                return false;
            }

            return string.Equals(custUnit, selectedValue, StringComparison.Ordinal)
                || string.Equals(customerNo, selectedValue, StringComparison.Ordinal)
                || string.Equals(customerName, selectedValue, StringComparison.Ordinal);
        }

        private static string Normalize(string? value) =>
            value?.Trim() ?? "";

        private static string[] NormalizeCustomerNos(IEnumerable<string>? customerNos) =>
            customerNos?
                .Select(Normalize)
                .Where(customerNo => !string.IsNullOrWhiteSpace(customerNo))
                .Distinct(StringComparer.Ordinal)
                .ToArray() ?? [];

        private static IEnumerable<string> GetCustomerNosFromForm(IFormCollection form)
        {
            foreach (var item in form)
            {
                if (!string.Equals(item.Key, "CustomerNos", StringComparison.Ordinal)
                    && !item.Key.StartsWith("CustomerNos[", StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (var value in item.Value)
                {
                    yield return value ?? "";
                }
            }
        }

        private static AddCustomerFrameViewModel CreateEditCustomerFrameModel(CustomerCreatePostModel customer) =>
            new()
            {
                IsEdit = true,
                FrameTitle = "編輯公司",
                FormActionName = "UpdateCustomer",
                SubmitButtonText = "儲存變更",
                ShowSaveNext = false,
                CustomerNo = Normalize(customer.CustomerNo),
                CustomerName = Normalize(customer.CustomerName),
                Address = Normalize(customer.Address),
                Tel = Normalize(customer.Tel),
                Fax = Normalize(customer.Fax),
                Email = Normalize(customer.Email),
                CustomerNote = Normalize(customer.CustomerNote),
                EnglishName = Normalize(customer.EnglishName),
                UniformNo = Normalize(customer.UniformNo),
                ContactPerson = Normalize(customer.ContactPerson),
                BusinessRegAddress = Normalize(customer.BusinessRegAddress),
                TaxOfficeCode = Normalize(customer.TaxOfficeCode),
                BusinessUnion = Normalize(customer.BusinessUnion),
                IndustryCategory = Normalize(customer.IndustryCategory),
                BusinessUnitName = Normalize(customer.BusinessUnitName),
                BusinessUnitAddress = Normalize(customer.BusinessUnitAddress),
                MedicalManageCode = Normalize(customer.MedicalManageCode),
                AccountingUnit = Normalize(customer.AccountingUnit)
            };

        private static CustomerListItemViewModel CreateCustomerDetailItem(CustomerCreatePostModel customer)
        {
            var customerNo = Normalize(customer.CustomerNo);

            return new CustomerListItemViewModel
            {
                CustomerNo = customerNo,
                CustomerName = Normalize(customer.CustomerName),
                Address = Normalize(customer.Address),
                Tel = Normalize(customer.Tel),
                Fax = Normalize(customer.Fax),
                Email = Normalize(customer.Email),
                CustomerNote = Normalize(customer.CustomerNote),
                EnglishName = Normalize(customer.EnglishName),
                UniformNo = Normalize(customer.UniformNo),
                ContactPerson = Normalize(customer.ContactPerson),
                BusinessRegAddress = Normalize(customer.BusinessRegAddress),
                TaxOfficeCode = Normalize(customer.TaxOfficeCode),
                BusinessUnion = Normalize(customer.BusinessUnion),
                IndustryCategory = Normalize(customer.IndustryCategory),
                BusinessUnitName = Normalize(customer.BusinessUnitName),
                BusinessUnitAddress = Normalize(customer.BusinessUnitAddress),
                MedicalManageCode = Normalize(customer.MedicalManageCode),
                AccountingUnit = Normalize(customer.AccountingUnit),
                Projects = GetCustomerProjectItems(customerNo)
            };
        }

        private static object CreateCustomerResponse(CustomerCreatePostModel customer, int projectCount) =>
            new
            {
                customerNo = customer.CustomerNo,
                customerName = customer.CustomerName,
                englishName = customer.EnglishName,
                uniformNo = customer.UniformNo,
                address = customer.Address,
                tel = customer.Tel,
                fax = customer.Fax,
                email = customer.Email,
                contactPerson = customer.ContactPerson,
                businessRegAddress = customer.BusinessRegAddress,
                taxOfficeCode = customer.TaxOfficeCode,
                businessUnion = customer.BusinessUnion,
                industryCategory = customer.IndustryCategory,
                businessUnitName = customer.BusinessUnitName,
                businessUnitAddress = customer.BusinessUnitAddress,
                medicalManageCode = customer.MedicalManageCode,
                accountingUnit = customer.AccountingUnit,
                customerNote = customer.CustomerNote,
                projectCount
            };

        private static CustomerCreatePostModel NormalizeCustomerCreatePostModel(CustomerCreatePostModel input) =>
            new()
            {
                CustomerNo = Normalize(input.CustomerNo),
                CustomerName = Normalize(input.CustomerName),
                Address = Normalize(input.Address),
                Tel = Normalize(input.Tel),
                Fax = Normalize(input.Fax),
                Email = Normalize(input.Email),
                CustomerNote = Normalize(input.CustomerNote),
                EnglishName = Normalize(input.EnglishName),
                UniformNo = Normalize(input.UniformNo),
                ContactPerson = Normalize(input.ContactPerson),
                BusinessRegAddress = Normalize(input.BusinessRegAddress),
                TaxOfficeCode = Normalize(input.TaxOfficeCode),
                BusinessUnion = Normalize(input.BusinessUnion),
                IndustryCategory = Normalize(input.IndustryCategory),
                BusinessUnitName = Normalize(input.BusinessUnitName),
                BusinessUnitAddress = Normalize(input.BusinessUnitAddress),
                MedicalManageCode = Normalize(input.MedicalManageCode),
                AccountingUnit = Normalize(input.AccountingUnit)
            };

        private static IReadOnlyDictionary<string, object?> CreateHealthCustomerParameters(CustomerCreatePostModel customer) =>
            new Dictionary<string, object?>
            {
                ["CUSTOMERNO"] = customer.CustomerNo,
                ["CUSTOMERNAME"] = customer.CustomerName,
                ["ADDRESS"] = customer.Address,
                ["TEL"] = customer.Tel,
                ["FAX"] = customer.Fax,
                ["EMAIL"] = customer.Email,
                ["CUSTOMERNOTE"] = customer.CustomerNote,
                ["ENGLISHNAME"] = customer.EnglishName,
                ["UNIFORMNO"] = customer.UniformNo,
                ["CONTACTPERSON"] = customer.ContactPerson,
                ["BUSINESSREGADDRESS"] = customer.BusinessRegAddress,
                ["TAXOFFICECODE"] = customer.TaxOfficeCode,
                ["BUSINESSUNION"] = customer.BusinessUnion,
                ["INDUSTRYCATEGORY"] = customer.IndustryCategory,
                ["BUSINESSUNITNAME"] = customer.BusinessUnitName,
                ["BUSINESSUNITADDRESS"] = customer.BusinessUnitAddress,
                ["MEDICALMANAGECODE"] = customer.MedicalManageCode,
                ["ACCOUNTINGUNIT"] = customer.AccountingUnit
            };
    }
}
