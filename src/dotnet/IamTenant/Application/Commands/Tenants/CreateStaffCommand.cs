using IamTenant.Domain;
using IamTenant.Infrastructure.Persistences;
using IamTenant.Application.DTOs.Tenants;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Events;
using Shared.Exceptions;
using Shared.Security;

namespace IamTenant.Application.Commands.Tenants;

/// <summary>
/// TenantId không cần truyền vào — được resolve từ ICurrentUserService
/// (đã được populate bởi AuthInterceptor từ gRPC metadata).
/// Global Query Filter trên DbContext cũng đảm bảo không có data cross-tenant.
/// StaffType: rỗng/null = Normal; giá trị sai → DomainException.
/// </summary>
public record CreateStaffCommand(
    string Email,
    string FirstName,
    string LastName,
    string? StaffType = null) : IRequest<StaffDto>;

public class CreateStaffHandler(
    IamTenantDbContext context,
    IPublishEndpoint publishEndpoint,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateStaffCommand, StaffDto>
{
    public async Task<StaffDto> Handle(CreateStaffCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.TenantId.HasValue)
            throw new ForbiddenException("TenantId is required.");

        // Lấy đúng tenant của người gọi (không lấy "tenant đầu tiên")
        var tenant = await context.Tenants
            .FirstOrDefaultAsync(t => t.Id == currentUser.TenantId.Value, cancellationToken)
            ?? throw new NotFoundException("Tenant not found.");

        // Validate Email Domain — BẮT BUỘC theo yêu cầu nghiệp vụ
        if (!request.Email.EndsWith($"@{tenant.CompanyDomain}", StringComparison.OrdinalIgnoreCase))
            throw new DomainException($"Staff Email must belong to the Company Domain: {tenant.CompanyDomain}");

        var staffType = ParseStaffType(request.StaffType);

        var staffUser = new User
        {
            TenantId = currentUser.TenantId.Value,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            UserType = Domain.Enums.UserType.TenantStaff,
            Status = Domain.Enums.UserStatus.Invited,
            StaffType = staffType,
        };

        context.Users.Add(staffUser);
        await context.SaveChangesAsync(cancellationToken);

        // Publish invitation event (snake-case name → NestJS consumer)
        await publishEndpoint.Publish(new TenantStaffCreatedEvent
        {
            TenantId = tenant.Id,
            UserId = staffUser.Id,
            Email = staffUser.Email,
            FirstName = staffUser.FirstName,
            LastName = staffUser.LastName,
        }, cancellationToken);

        return new StaffDto
        {
            Id = staffUser.Id,
            TenantId = staffUser.TenantId,
            Email = staffUser.Email,
            FirstName = staffUser.FirstName,
            LastName = staffUser.LastName,
            UserType = staffUser.UserType.ToString(),
            Status = staffUser.Status.ToString(),
            StaffType = staffUser.StaffType.ToString(),
            CreatedAt = staffUser.CreatedAt
        };
    }

    internal static Domain.Enums.StaffType ParseStaffType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Domain.Enums.StaffType.Normal;

        if (!Enum.TryParse<Domain.Enums.StaffType>(value, true, out var staffType))
            throw new DomainException(
                $"StaffType '{value}' không hợp lệ. Giá trị cho phép: {string.Join(", ", Enum.GetNames<Domain.Enums.StaffType>())}");

        return staffType;
    }
}
