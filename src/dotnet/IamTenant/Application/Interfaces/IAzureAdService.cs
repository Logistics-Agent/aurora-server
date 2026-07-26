namespace IamTenant.Application.Interfaces;

public interface IAzureAdService
{
    Task<(string AdminGroupId, string UserGroupId)> CreateTenantGroupsAsync(string tenantName, string tenantCode, CancellationToken ct = default);
    Task<string> CreateUserAsync(string email, string tempPassword, CancellationToken ct = default);
    Task AddUserToGroupAsync(string azureUserId, string azureGroupId, CancellationToken ct = default);
    Task RemoveUserFromGroupAsync(string azureUserId, string azureGroupId, CancellationToken ct = default);
}
