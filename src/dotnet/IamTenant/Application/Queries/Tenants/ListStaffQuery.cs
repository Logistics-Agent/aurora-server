using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IamTenant.Infrastructure.Persistences;
using IamTenant.Application.DTOs.Tenants;
using MediatR;
using Shared.Enums;
using Shared.Pagination;

namespace IamTenant.Application.Queries.Tenants;

public class ListStaffQuery : PagedRequest, IRequest<PagedResult<StaffDto>> { }

public class ListStaffHandler(IamTenantDbContext context) : IRequestHandler<ListStaffQuery, PagedResult<StaffDto>>
{
    public async Task<PagedResult<StaffDto>> Handle(ListStaffQuery request, CancellationToken cancellationToken)
    {
        var query = context.Users
            .Where(u => !u.IsDeleted)
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new StaffDto
            {
                Id = u.Id,
                TenantId = u.TenantId,
                Email = u.Email,
                FirstName = u.FirstName,
                LastName = u.LastName,
                Role = u.Role.ToCode(),
                Permissions = u.UserPermissions
                    .Where(up => up.Permission != null)
                    .Select(up => up.Permission!.Code)
                    .OrderBy(p => p)
                    .ToList(),
                PermissionVersion = u.PermissionVersion,
                Status = u.Status,
                CreatedAt = u.CreatedAt
            });

        return await query.ToPagedResultAsync(request, cancellationToken);
    }
}
