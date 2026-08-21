namespace Shared.Security;

public sealed class DevelopmentIdentityOptions
{
    public const string SectionName = "DevelopmentIdentity";

    public bool Enabled { get; init; }
    public Guid UserId { get; init; }
    public Guid TenantId { get; init; }
    public int PermissionVersion { get; init; } = 1;
    public List<string> RoleIds { get; init; } = [];
    public List<string> Permissions { get; init; } = [];

    public static bool IsValid(DevelopmentIdentityOptions options) =>
        !options.Enabled ||
        (options.UserId != Guid.Empty &&
         options.TenantId != Guid.Empty &&
         options.PermissionVersion > 0 &&
         ValidValues(options.RoleIds) &&
         ValidValues(options.Permissions));

    private static bool ValidValues(IEnumerable<string> values) =>
        values.All(value => !string.IsNullOrWhiteSpace(value) && value.Length <= 128);
}
