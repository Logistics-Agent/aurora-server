using Shared.Entity;
using Shared.Exceptions;
namespace IamTenant.Domain;

public class Tenant : AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? TaxCode { get; set; }
    public string CompanyDomain { get; set; } = string.Empty;
    public Enums.PlanType PlanType { get; set; } = Enums.PlanType.Standard;
    public Enums.TenantStatus Status { get; set; } = Enums.TenantStatus.Provisioning;
    public Guid IdempotencyKey { get; set; }

    public string? AdminGroupId { get; set; }
    public string? UserGroupId { get; set; }

    // AWS Cognito User Pools (Admin & User) per Tenant Code
    public string? AdminUserPoolId { get; set; }
    public string? AdminUserPoolClientId { get; set; }
    public string? UserUserPoolId { get; set; }
    public string? UserUserPoolClientId { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
    public bool IsDeleted { get; private set; }

    public void SoftDelete()
    {
        IsDeleted = true;
        DeletedAt = DateTimeOffset.UtcNow;
    }

    public ICollection<User> Users { get; set; } = new List<User>();

    private Tenant() { } // EF Core

    public static Tenant Create(string name, string companyDomain, string? taxCode, Guid idempotencyKey)
    {
        return Create(name, companyDomain, taxCode, Enums.PlanType.Standard, idempotencyKey);
    }

    public static Tenant Create(string name, string companyDomain, string? taxCode, Enums.PlanType planType, Guid idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(companyDomain))
            throw new DomainException("Company domain is required.");

        return new Tenant
        {
            Name = name,
            Code = GenerateCode(companyDomain),
            CompanyDomain = companyDomain.ToLowerInvariant(),
            TaxCode = taxCode,
            PlanType = planType,
            Status = Enums.TenantStatus.Active,
            IdempotencyKey = idempotencyKey
        };
    }

    private static string GenerateCode(string domain) =>
        domain.Split('.')[0].ToUpperInvariant()[..Math.Min(10, domain.Split('.')[0].Length)];
}
