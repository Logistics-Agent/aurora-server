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

namespace IamTenant.Application.Commands.Tenants;

public record UpdateStaffCommand(
    Guid Id,
    Guid TenantId,
    string FirstName,
    string LastName) : IRequest<StaffDto>;

public class UpdateStaffHandler(IamTenantDbContext context) : IRequestHandler<UpdateStaffCommand, StaffDto>
{
    public async Task<StaffDto> Handle(UpdateStaffCommand request, CancellationToken cancellationToken)
    {
        var staffUser = await context.Users
            .Include(u => u.UserPermissions)
            .ThenInclude(up => up.Permission)
            .FirstOrDefaultAsync(u => u.Id == request.Id && u.TenantId == request.TenantId && !u.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Staff not found");

        staffUser.FirstName = request.FirstName;
        staffUser.LastName = request.LastName;

        await context.SaveChangesAsync(cancellationToken);

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
