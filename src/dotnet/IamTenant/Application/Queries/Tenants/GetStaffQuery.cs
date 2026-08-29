using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IamTenant.Infrastructure.Persistences;
using IamTenant.Application.DTOs.Tenants;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Enums;
using Shared.Exceptions;

namespace IamTenant.Application.Queries.Tenants;

public record GetStaffQuery(Guid Id, Guid TenantId) : IRequest<StaffDto>;

public class GetStaffHandler(IamTenantDbContext context) : IRequestHandler<GetStaffQuery, StaffDto>
{
    public async Task<StaffDto> Handle(GetStaffQuery request, CancellationToken cancellationToken)
    {
        var staffUser = await context.Users
            .Include(u => u.UserPermissions)
            .ThenInclude(up => up.Permission)
            .FirstOrDefaultAsync(u => u.Id == request.Id && u.TenantId == request.TenantId && !u.IsDeleted, cancellationToken) 
            ?? throw new NotFoundException($"Staff '{request.Id}' not found");

        return new StaffDto
        {
            Id = staffUser.Id,
            TenantId = staffUser.TenantId,
            Email = staffUser.Email,
            FirstName = staffUser.FirstName,
            LastName = staffUser.LastName,
            Role = staffUser.Role.ToCode(),
            Permissions = staffUser.UserPermissions
                .Where(up => up.Permission != null)
                .Select(up => up.Permission!.Code)
                .OrderBy(p => p)
                .ToList(),
            PermissionVersion = staffUser.PermissionVersion,
            Status = staffUser.Status,
            CreatedAt = staffUser.CreatedAt
        };
    }
}
