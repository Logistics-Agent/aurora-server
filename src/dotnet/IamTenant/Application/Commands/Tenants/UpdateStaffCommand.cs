using IamTenant.Infrastructure.Persistences;
using IamTenant.Application.DTOs.Tenants;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Exceptions;

namespace IamTenant.Application.Commands.Tenants;

/// <summary>StaffType: rỗng/null = giữ nguyên; giá trị sai → DomainException.</summary>
public record UpdateStaffCommand(
    Guid Id,
    Guid TenantId,
    string FirstName,
    string LastName,
    string? StaffType = null) : IRequest<StaffDto>;

public class UpdateStaffHandler(IamTenantDbContext context) : IRequestHandler<UpdateStaffCommand, StaffDto>
{
    public async Task<StaffDto> Handle(UpdateStaffCommand request, CancellationToken cancellationToken)
    {
        var staffUser = await context.Users
            .FirstOrDefaultAsync(u => u.Id == request.Id && u.TenantId == request.TenantId && !u.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Staff not found");

        staffUser.FirstName = request.FirstName;
        staffUser.LastName = request.LastName;

        if (!string.IsNullOrWhiteSpace(request.StaffType))
        {
            staffUser.StaffType = CreateStaffHandler.ParseStaffType(request.StaffType);
        }

        await context.SaveChangesAsync(cancellationToken);

        return new StaffDto
        {
            Id = staffUser.Id,
            TenantId = staffUser.TenantId,
            Email = staffUser.Email,
            FirstName = staffUser.FirstName,
            LastName = staffUser.LastName,
            UserType = staffUser.UserType,
            Status = staffUser.Status,
            StaffType = staffUser.StaffType,
            CreatedAt = staffUser.CreatedAt
        };
    }
}
