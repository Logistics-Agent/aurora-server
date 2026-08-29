using System;
using System.Collections.Generic;
using IamTenant.Domain.Enums;
using Shared.Enums;

namespace IamTenant.Application.DTOs.Tenants;

public class TenantDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? TaxCode { get; set; }
    public string CompanyDomain { get; set; } = string.Empty;
    public PlanType PlanType { get; set; } = PlanType.Standard;
    public TenantStatus Status { get; set; } = TenantStatus.Active;
    public DateTimeOffset CreatedAt { get; set; }
}

public class StaffDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public UserStatus Status { get; set; } = UserStatus.Invited;
    public string Role { get; set; } = BaseRoleExtensions.StaffCode;
    public List<string> Permissions { get; set; } = [];
    public int PermissionVersion { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
}
