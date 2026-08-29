using IamTenant.Domain;
using IamTenant.Infrastructure.Persistences;
using Microsoft.EntityFrameworkCore;
using Shared.Constants;
using Shared.Enums;

namespace IamTenant.Infrastructure.Data;

public static class DatabaseSeeder
{
    public static async Task SeedPermissionsAndRolesAsync(IamTenantDbContext context, Guid tenantId)
    {
        // 1. Seed All Possible Permissions into the DB
        var allPermissionCodes = PermissionConstants.GetAllPermissions();

        foreach (var code in allPermissionCodes)
        {
            var exists = await context.Permissions.AnyAsync(p => p.Code == code);
            if (!exists)
            {
                var parts = code.Split(':');
                context.Permissions.Add(new Permission
                {
                    Id = IamTenantDbContext.DeterministicPermissionId(code),
                    Code = code,
                    Module = parts[0],
                    Description = $"Allows {code}"
                });
            }
        }
        await context.SaveChangesAsync();
    }
}
