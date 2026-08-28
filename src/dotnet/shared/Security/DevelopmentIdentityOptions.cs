namespace Shared.Security;

using Shared.Constants;

public sealed class DevelopmentIdentityOptions
{
    public const string SectionName = "DevelopmentIdentity";

    public bool Enabled { get; init; }
    public Guid UserId { get; init; }
    public Guid TenantId { get; init; }
    public int PermissionVersion { get; init; } = 1;
    public string Role { get; init; } = RoleConstants.Staff;
    public List<string> Permissions { get; init; } = [];

    public static bool IsValid(DevelopmentIdentityOptions options) =>
        !options.Enabled ||
        (options.UserId != Guid.Empty &&
         options.TenantId != Guid.Empty &&
         options.PermissionVersion > 0 &&
         !string.IsNullOrWhiteSpace(options.Role) &&
         ValidValues(options.Permissions));

    private static bool ValidValues(IEnumerable<string> values) =>
        values.All(value => !string.IsNullOrWhiteSpace(value) && value.Length <= 128);
}
