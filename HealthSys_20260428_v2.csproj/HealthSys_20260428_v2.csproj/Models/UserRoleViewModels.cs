using System.Collections.Generic;

namespace HealthSys_20260428_v2.csproj.Models;

public sealed class UserRoleViewModels
{
    public IReadOnlyList<StaffRoleViewModel> StaffFilterRoles { get; init; } = [];

    public IReadOnlyList<StaffRoleViewModel> RoleItems { get; init; } = [];

    public IReadOnlyList<StaffUserViewModel> StaffUsers { get; init; } = [];

    public IReadOnlyList<RolePermissionModuleViewModel> PermissionModules { get; init; } = [];
}

public sealed class StaffUserViewModel
{
    public string StaffNo { get; init; } = "";

    public string Initial { get; init; } = "";

    public string Name { get; init; } = "";

    public string Department { get; init; } = "";

    public string Position { get; init; } = "";

    public string Status { get; init; } = "";

    public string StatusTone { get; init; } = "";

    public IReadOnlyList<StaffRoleViewModel> Roles { get; init; } = [];

    public string JoinedAt { get; init; } = "";
}

public sealed class StaffRoleViewModel
{
    public string Description { get; init; } = "";
    public string Code { get; init; } = "";

    public string Name { get; init; } = "";

    public int StaffCount { get; init; }

    public string Tone { get; init; } = "";

    public string Color { get; init; } = "";

    public string Icon { get; init; } = "";

    public bool Active { get; init; }
}

public sealed class RolePermissionModuleViewModel
{
    public string Code { get; init; } = "";

    public string Name { get; init; } = "";

    public int DisplayOrder { get; init; }

    public bool CanView { get; init; }

    public bool CanReview { get; init; }

    public IReadOnlyList<RolePermissionFunctionViewModel> Functions { get; init; } = [];
}

public sealed class RolePermissionFunctionViewModel
{
    public string Code { get; init; } = "";

    public string ParentCode { get; init; } = "";

    public string Name { get; init; } = "";

    public int DisplayOrder { get; init; }

    public bool CanView { get; init; }

    public bool CanReview { get; init; }
}

public sealed class StaffEditStatusPostModel
{
    public string? StaffNo { get; init; }

    public string? StaffName { get; init; }

    public string? Department { get; init; }

    public string? Position { get; init; }

    public string? Status { get; init; }

    public string[] RoleNames { get; init; } = [];

    public string[] RoleCodes { get; init; } = [];
}

public sealed class RolePermissionSavePostModel
{
    public string? RoleCode { get; init; }

    public RolePermissionPostModel[] PermissionItems { get; init; } = [];
}

public sealed class RoleEditPostModel
{
    public string RoleCode { get; init; } = "";
    public string RoleName { get; init; } = "";
    public string RoleDescription { get; init; } = "";
    public bool IsEnabled { get; init; } = true;
    public bool IsEdit { get; init; }
}

public sealed class RolePermissionPostModel
{
    public string? Code { get; init; }

    public string? ParentCode { get; init; }

    public string? Name { get; init; }

    public int DisplayOrder { get; init; }

    public bool CanView { get; init; }

    public bool CanReview { get; init; }
}
