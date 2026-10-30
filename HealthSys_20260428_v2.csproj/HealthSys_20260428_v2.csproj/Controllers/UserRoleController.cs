using HealthSys_20260428_v2.csproj.Data;
using HealthSys_20260428_v2.csproj.Models;
using Microsoft.AspNetCore.Mvc;
using System.Data;

namespace HealthSys_20260428_v2.csproj.Controllers
{
    public class UserRoleController : Controller
    {
        private const string DefaultRoleColor = "#64748b";

        public IActionResult SystemSettings()
        {
            var model = CreateUserRoleViewModels();

            return View(model);
        }

        // private readonly OracleConnectionFactory db = new();
        private readonly ControllerDemoData db = new();
        private static Dictionary<string, object?> Args(params (string Key, object? Value)[] values) => values.ToDictionary(v => v.Key, v => v.Value);
        private DataTable Query(string sql, Dictionary<string, object?>? args = null) => db.GETDataTable(sql, args ?? new(), TestConnection: true);
        // This screen configures role grants; it does not enforce application access.
        // Preserve an honest audit marker when the host has no authenticated user.
        private string UpdateUser => User.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(User.Identity.Name)
            ? User.Identity.Name : "ANONYMOUS";
        private bool RoleExists(string code) => Query(OracleSql.UserRole.ActiveRole, Args(("ROLE_ID", code))).Rows.Count == 1;
        private RolePermissionPostModel[] ReadPermissions(string code) => Query(OracleSql.UserRole.Permissions, Args(("ROLE_ID", code)))
            .Rows.Cast<DataRow>().Select(row => new RolePermissionPostModel
            {
                Code = GetString(row, "FUNCTION_ID"), ParentCode = GetString(row, "PARENT_ID"), Name = GetString(row, "FUNCTION_NAME"),
                DisplayOrder = row["SORT_NO"] == DBNull.Value ? int.MaxValue : Convert.ToInt32(row["SORT_NO"]),
                CanView = GetString(row, "CAN_VIEW") == "1", CanReview = GetString(row, "CAN_REVIEW") == "1"
            }).ToArray();
        [HttpGet]
        public IActionResult RolePermissions(string roleCode)
        {
            if (!RoleExists(roleCode)) return NotFound(new { message = "角色不存在或已停用。" });
            return Json(new { permissionItems = ReadPermissions(roleCode) });
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SaveRolePermissions(RolePermissionSavePostModel input)
        {
            if (!ModelState.IsValid) return BadRequest(new { saved = false, message = "權限資料格式錯誤。" });
            var code = (input.RoleCode ?? "").Trim();
            if (!RoleExists(code)) return NotFound(new { saved = false, message = "角色不存在或已停用。" });
            var functions = ReadPermissions(code).Select(item => item.Code).ToHashSet(StringComparer.Ordinal);
            var items = input.PermissionItems ?? [];
            if (items.Length != functions.Count || items.Select(item => item.Code).Distinct().Count() != functions.Count || items.Any(item => !functions.Contains(item.Code)))
                return BadRequest(new { saved = false, message = "功能清單已變更，請重新整理。" });
            var commands = new List<OracleTransactionCommand> { new(OracleSql.UserRole.LockRole, Args(("ROLE_ID", code))) };
            commands.AddRange(items.Select(item => new OracleTransactionCommand(OracleSql.UserRole.MergePermission,
                Args(("ROLE_ID", code), ("FUNCTION_ID", item.Code), ("CAN_VIEW", item.CanView ? 1 : 0), ("CAN_REVIEW", item.CanReview ? 1 : 0), ("UPDATE_USER", UpdateUser)))));
            try
            {
                db.ExecuteNonQueryTransaction(commands, TestConnection: true);
                return Json(new { saved = true, persisted = true, roleCode = code, permissionItems = ReadPermissions(code) });
            }
            catch (Exception ex) { return DatabaseFailure(ex); }
        }
        private IActionResult DatabaseFailure(Exception ex)
        {
            HttpContext.RequestServices.GetRequiredService<ILogger<UserRoleController>>().LogError(ex, "Unable to save role data");
            return StatusCode(500, new { saved = false, message = "儲存失敗，請確認角色代號未重複、欄位長度符合限制，並重新讀取資料。" });
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SaveRole(RoleEditPostModel input)
        {
            var code = (input.RoleCode ?? "").Trim(); var name = (input.RoleName ?? "").Trim(); var description = (input.RoleDescription ?? "").Trim();
            if (!ModelState.IsValid || !System.Text.RegularExpressions.Regex.IsMatch(code, "^[A-Za-z0-9_]+$") || name.Length == 0 || (!input.IsEdit && !input.IsEnabled))
                return BadRequest(new { saved = false, message = "請填寫有效的角色代號、名稱並啟用新角色。" });
            var commands = new List<OracleTransactionCommand>();
            if (input.IsEdit)
            {
                commands.Add(new(OracleSql.UserRole.UpdateRole, Args(("ROLE_ID", code), ("ROLE_NAME", name), ("ROLE_DESC", description), ("IS_ACTIVE", input.IsEnabled ? 1 : 0))));
                commands.Add(new(OracleSql.UserRole.AuditRoleChange, Args(("ROLE_ID", code), ("UPDATE_USER", UpdateUser)), allowZeroAffectedRows: true));
            }
            else
            {
                commands.Add(new(OracleSql.UserRole.InsertRole, Args(("ROLE_ID", code), ("ROLE_NAME", name), ("ROLE_DESC", description))));
                commands.Add(new(OracleSql.UserRole.InitializePermissions, Args(("ROLE_ID", code), ("UPDATE_USER", UpdateUser)), allowZeroAffectedRows: true));
            }
            try
            {
                db.ExecuteNonQueryTransaction(commands, TestConnection: true);
                return Json(new { saved = true, role = CreateRoleItems().FirstOrDefault(role => role.Code == code), roleCode = code, enabled = input.IsEnabled });
            }
            catch (Exception ex) { return DatabaseFailure(ex); }
        }

        public IActionResult AddRoleFrame(string? roleCode)
        {
            var model = CreateRoleOnlyViewModels();
            if (!string.IsNullOrEmpty(roleCode))
            {
                var role = model.RoleItems.FirstOrDefault(role => role.Code == roleCode);
                if (role == null) return NotFound();
                ViewData["EditRole"] = role;
            }

            return View(model);
        }

        public IActionResult AddEmployeeFrame()
        {
            var model = CreateRoleOnlyViewModels();

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateStaff(StaffEditStatusPostModel input)
        {
            var normalizedStaffNo = (input.StaffNo ?? string.Empty).Trim();
            var normalizedStatus = NormalizeStaffStatus(input.Status ?? string.Empty);
            normalizedStatus = string.IsNullOrWhiteSpace(normalizedStatus) ? "啟用" : normalizedStatus;
            var accountStatusValue = normalizedStatus == "停用" ? "inactive" : "active";
            var roleCodes = NormalizePostedValues(input.RoleCodes);
            var availableRoles = CreateAllRoleItems().ToDictionary(role => role.Code, StringComparer.Ordinal);
            if (roleCodes.Length == 0 || roleCodes.Any(code => !availableRoles.ContainsKey(code)))
                return BadRequest(new { saved = false, message = "請選擇有效的角色代號。" });
            var roleNames = roleCodes.Select(code => availableRoles[code].Name).ToArray();
            var roleCodeValue = string.Join(",", roleCodes);
            var staffName = (input.StaffName ?? string.Empty).Trim();
            var department = (input.Department ?? string.Empty).Trim();
            var position = (input.Position ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(normalizedStaffNo) || roleNames.Length == 0)
            {
                return Json(new { saved = false, updated = false, updatedCount = 0 });
            }
           
            var updateSql = OracleSql.UserRole.UpdateStaff;

            try
            {
                var updatedCount = string.IsNullOrWhiteSpace(updateSql)
                    ? 0
                    : db.ExecuteNonQuery(updateSql, Args(("ROLE_CODE", roleCodeValue), ("STATUS", accountStatusValue), ("EMP_NO", normalizedStaffNo)));

                return Json(new
                {
                    saved = updatedCount >= 1,
                    updated = updatedCount >= 1,
                    updatedCount,
                    staffNo = normalizedStaffNo,
                    staffName,
                    department,
                    position,
                    status = normalizedStatus,
                    roleNames,
                    roleCodes
                });
            }
            catch
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { saved = false, updated = false, updatedCount = 0 });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteStaff(string staffNo)
        {
            var normalizedStaffNo = (staffNo ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(normalizedStaffNo))
            {
                return Json(new { deleted = false, deletedCount = 0 });
            }
            var deleteSql = OracleSql.UserRole.DeleteStaff;

            try
            {
                var deletedCount = string.IsNullOrWhiteSpace(deleteSql)
                    ? 0
                    : db.ExecuteNonQuery(deleteSql, Args(("EMP_NO", normalizedStaffNo)));

                return Json(new { deleted = deletedCount >= 1, deletedCount });
            }
            catch
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { deleted = false, deletedCount = 0 });
            }
        }

        private UserRoleViewModels CreateUserRoleViewModels()
        {
            var sql = OracleSql.SELECT.getAccount();
            var roleItems = CreateRoleItems();
            var allRoleItems = CreateAllRoleItems();
            // Personnel badges and editors resolve ROLE_ID to the current ROLE_MASTER.ROLE_NAME,
            // including disabled roles. ACCOUNT stores stable IDs, never copied display names.
            var roleLookup = CreateRoleLookup(allRoleItems);

            var dataTable = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().GETDataTable(sql, "", true);
            var staffUsers = dataTable.Rows
                .Cast<DataRow>()
                .Select(row =>
                {
                    var status = GetString(row, "STATUS");
                    var roles = ResolveStaffRoles(GetString(row, "ROLE_CODE"), roleLookup);

                    return Staff(
                        GetString(row, "H_EMPNO"),
                        GetInitial(GetString(row, "H_EMPNANE")),
                        GetString(row, "H_EMPNANE"),
                        GetString(row, "H_DPTNAME"),
                        GetString(row, "H_EMPPOSITION"),
                        NormalizeStaffStatus(status),
                        ResolveStaffStatusTone(status),
                        GetDateString(row, "CDATE"),
                        roles);
                })
                .ToArray();

            return new UserRoleViewModels
            {
                RoleItems = ApplyRoleStaffCounts(roleItems, staffUsers),
                StaffFilterRoles = allRoleItems,
                StaffUsers = staffUsers,
                PermissionModules = CreatePermissionModules(roleItems.FirstOrDefault()?.Code ?? "")
            };
        }

        private StaffRoleViewModel[] CreateAllRoleItems() => Query(OracleSql.UserRole.RoleFilterOptions).Rows.Cast<DataRow>()
            .Select(row => new StaffRoleViewModel
            {
                Code = GetString(row, "ROLE_ID"), Name = GetString(row, "ROLE_NAME"),
                Tone = RoleTone(GetString(row, "ROLE_ID")), Color = RoleColor(GetString(row, "ROLE_ID"))
            }).ToArray();

        private StaffRoleViewModel[] CreateRoleItems() => Query(OracleSql.UserRole.Roles).Rows.Cast<DataRow>().Select((row, index) => new StaffRoleViewModel
        {
            Code = GetString(row, "ROLE_ID"), Name = GetString(row, "ROLE_NAME"), Description = GetString(row, "ROLE_DESC"),
            Tone = RoleTone(GetString(row, "ROLE_ID")), Color = RoleColor(GetString(row, "ROLE_ID")),
            Icon = ResolveRoleIcon(GetString(row, "ROLE_ID"), ""), Active = index == 0
        }).ToArray();
        private static string RoleTone(string code) => code switch { "ADMIN" => "blue", "COUNTER" => "green", "DOCTOR" => "purple", "FINANCE" => "orange", "EXAM" => "cyan", _ => "gray" };
        private static string RoleColor(string code) => RoleTone(code) switch { "blue" => "#3b82f6", "green" => "#22c55e", "purple" => "#a855f7", "orange" => "#f97316", "cyan" => "#06b6d4", _ => DefaultRoleColor };
        private UserRoleViewModels CreateRoleOnlyViewModels() => new() { RoleItems = CreateRoleItems() };
        private RolePermissionModuleViewModel[] CreatePermissionModules(string code)
        {
            var items = ReadPermissions(code);
            var roots = items.Where(item => string.IsNullOrEmpty(item.ParentCode)).ToArray();
            if (items.Any(item => !string.IsNullOrEmpty(item.ParentCode) && !roots.Any(root => root.Code == item.ParentCode)))
                throw new InvalidOperationException("功能父項目無效或超出目前兩層選單，請確認 SYSTEM_FUNCTION。");
            return roots.Select(root => new RolePermissionModuleViewModel
            {
                Code = root.Code!, Name = root.Name!, DisplayOrder = root.DisplayOrder, CanView = root.CanView, CanReview = root.CanReview,
                Functions = items.Where(item => item.ParentCode == root.Code).Select(item => new RolePermissionFunctionViewModel
                {
                    Code = item.Code!, ParentCode = item.ParentCode!, Name = item.Name!, DisplayOrder = item.DisplayOrder, CanView = item.CanView, CanReview = item.CanReview
                }).ToArray()
            }).ToArray();
        }

        private static StaffUserViewModel Staff(
            string staffNo,
            string initial,
            string name,
            string department,
            string position,
            string status,
            string statusTone,
            string joinedAt,
            params StaffRoleViewModel[] roles) =>
            new()
            {
                StaffNo = staffNo,
                Initial = initial,
                Name = name,
                Department = department,
                Position = position,
                Status = status,
                StatusTone = statusTone,
                Roles = roles,
                JoinedAt = joinedAt
            };

        private static Dictionary<string, StaffRoleViewModel> CreateRoleLookup(IEnumerable<StaffRoleViewModel> roles)
        {
            var roleLookup = new Dictionary<string, StaffRoleViewModel>(StringComparer.OrdinalIgnoreCase);

            foreach (var role in roles)
            {
                if (!string.IsNullOrWhiteSpace(role.Code))
                {
                    roleLookup[role.Code.Trim()] = role;
                }


            }

            return roleLookup;
        }

        private static StaffRoleViewModel[] ResolveStaffRoles(string roleCodeValue, IReadOnlyDictionary<string, StaffRoleViewModel> roleLookup)
        {
            return SplitRoleCodes(roleCodeValue)
                .Select(roleCode => roleLookup.TryGetValue(roleCode, out var role)
                    ? role
                    : RoleItem(roleCode, roleCode, "gray", DefaultRoleColor, "user"))
                .ToArray();
        }

        private static IEnumerable<string> SplitRoleCodes(string roleCodeValue) =>
            roleCodeValue
                .Split([',', ';', '|', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        private static StaffRoleViewModel[] ApplyRoleStaffCounts(IEnumerable<StaffRoleViewModel> roles, IReadOnlyCollection<StaffUserViewModel> staffUsers) =>
            roles.Select(role => new StaffRoleViewModel
            {
                Code = role.Code, Name = role.Name, Description = role.Description, Tone = role.Tone, Color = role.Color, Icon = role.Icon, Active = role.Active,
                StaffCount = staffUsers.Count(staff => staff.Roles.Any(item => item.Code == role.Code))
            }).ToArray();

        private static StaffRoleViewModel RoleItem(string code, string name, string tone, string color, string icon, bool active = false, int staffCount = 0) =>
            new()
            {
                Code = code,
                Name = name,
                StaffCount = staffCount,
                Tone = tone,
                Color = color,
                Icon = icon,
                Active = active
            };

        private static bool IsDefaultRole(string code, string name)
        {
            var roleKey = $"{code} {name}";
            return roleKey.Contains("ADMIN", StringComparison.OrdinalIgnoreCase)
                || roleKey.Contains("系統管理員", StringComparison.Ordinal);
        }

        private static string NormalizeRoleTone(string roleColor)
        {
            var color = roleColor.Trim().ToLowerInvariant();
            return color switch
            {
                "blue" => "blue",
                "green" => "green",
                "purple" => "purple",
                "orange" => "orange",
                "cyan" => "cyan",
                "gray" => "gray",
                "grey" => "gray",
                _ => "gray"
            };
        }

        private static string NormalizeRoleColor(string roleColor)
        {
            var color = (roleColor ?? string.Empty).Trim();

            if (string.IsNullOrEmpty(color))
            {
                return DefaultRoleColor;
            }

            return IsSafeRoleColor(color) ? color : DefaultRoleColor;
        }

        private static bool IsSafeRoleColor(string color) =>
            IsHexColor(color)
            || IsNamedColor(color)
            || IsCssColorFunction(color, "rgb")
            || IsCssColorFunction(color, "rgba")
            || IsCssColorFunction(color, "hsl")
            || IsCssColorFunction(color, "hsla");

        private static bool IsHexColor(string color)
        {
            if (color.Length is not (4 or 5 or 7 or 9) || color[0] != '#')
            {
                return false;
            }

            return color.Skip(1).All(IsHexDigit);
        }

        private static bool IsNamedColor(string color) =>
            color.Length <= 32 && color.All(static c => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z');

        private static bool IsCssColorFunction(string color, string functionName)
        {
            if (color.Length > 80)
            {
                return false;
            }

            var prefix = functionName + "(";

            if (!color.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !color.EndsWith(')'))
            {
                return false;
            }

            var values = color[prefix.Length..^1];

            return values.Length > 0 && values.All(IsSafeCssColorFunctionCharacter);
        }

        private static bool IsHexDigit(char value) =>
            value is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';

        private static bool IsSafeCssColorFunctionCharacter(char value) =>
            char.IsWhiteSpace(value)
            || value is >= '0' and <= '9'
            || value is >= 'a' and <= 'z'
            || value is >= 'A' and <= 'Z'
            || value is ',' or '.' or '%' or '/' or '+' or '-';

        private static string ResolveRoleIcon(string code, string name)
        {
            var roleKey = $"{code} {name}";

            if (roleKey.Contains("ADMIN", StringComparison.OrdinalIgnoreCase)
                || roleKey.Contains("系統管理", StringComparison.Ordinal))
            {
                return "admin";
            }

            if (roleKey.Contains("DESK", StringComparison.OrdinalIgnoreCase)
                || roleKey.Contains("FRONT", StringComparison.OrdinalIgnoreCase)
                || roleKey.Contains("櫃台", StringComparison.Ordinal))
            {
                return "desk";
            }

            if (roleKey.Contains("NURSE", StringComparison.OrdinalIgnoreCase)
                || roleKey.Contains("MED", StringComparison.OrdinalIgnoreCase)
                || roleKey.Contains("醫", StringComparison.Ordinal)
                || roleKey.Contains("護", StringComparison.Ordinal))
            {
                return "medical";
            }

            if (roleKey.Contains("LAB", StringComparison.OrdinalIgnoreCase)
                || roleKey.Contains("檢驗", StringComparison.Ordinal))
            {
                return "lab";
            }

            if (roleKey.Contains("REPORT", StringComparison.OrdinalIgnoreCase)
                || roleKey.Contains("報表", StringComparison.Ordinal))
            {
                return "report";
            }

            return "user";
        }

        private static string GetString(DataRow row, string columnName)
        {
            if (!row.Table.Columns.Contains(columnName) || row[columnName] == DBNull.Value)
            {
                return "";
            }

            return Convert.ToString(row[columnName]) ?? "";
        }

        private static string GetDateString(DataRow row, string columnName)
        {
            if (!row.Table.Columns.Contains(columnName) || row[columnName] == DBNull.Value)
            {
                return "";
            }

            return row[columnName] is DateTime date
                ? date.ToString("yyyy/MM/dd")
                : Convert.ToString(row[columnName]) ?? "";
        }

        private static string NormalizeStaffStatus(string status)
        {
            var value = status.Trim();

            if (value.Equals("active", StringComparison.OrdinalIgnoreCase)
                || value.Equals("enabled", StringComparison.OrdinalIgnoreCase)
                || value.Equals("1", StringComparison.OrdinalIgnoreCase)
                || value.Equals("true", StringComparison.OrdinalIgnoreCase)
                || value.Equals("y", StringComparison.OrdinalIgnoreCase)
                || value.Equals("啟用", StringComparison.Ordinal))
            {
                return "啟用";
            }

            if (value.Equals("inactive", StringComparison.OrdinalIgnoreCase)
                || value.Equals("disabled", StringComparison.OrdinalIgnoreCase)
                || value.Equals("0", StringComparison.OrdinalIgnoreCase)
                || value.Equals("false", StringComparison.OrdinalIgnoreCase)
                || value.Equals("n", StringComparison.OrdinalIgnoreCase)
                || value.Equals("停用", StringComparison.Ordinal))
            {
                return "停用";
            }

            return value;
        }

        private static string ResolveStaffStatusTone(string status) =>
            NormalizeStaffStatus(status) == "停用" ? "inactive" : "active";

        private static string GetInitial(string name) =>
            string.IsNullOrWhiteSpace(name) ? "" : name.Trim()[0].ToString();

        private static string[] NormalizePostedValues(IEnumerable<string>? values) =>
            values?
                .Select(value => (value ?? string.Empty).Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray() ?? [];
    }

}
