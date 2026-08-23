using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using IamTenant.Domain;
using IamTenant.Infrastructure.Persistences;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Events;

namespace IamTenant.Application.Commands.Tenants;

/// <summary>
/// TenantId không cần truyền vào — Global Query Filter đã đảm bảo
/// chỉ trả về Staff thuộc TenantId của người gọi.
/// </summary>
public record ResetStaffPasswordCommand(Guid UserId) : IRequest;

public class ResetStaffPasswordHandler(IamTenantDbContext context)
    : IRequestHandler<ResetStaffPasswordCommand>
{
    public async Task Handle(ResetStaffPasswordCommand request, CancellationToken cancellationToken)
    {
        // Global Query Filter đảm bảo user này phải thuộc TenantId hiện tại
        var staffUser = await context.Users.FirstOrDefaultAsync(
            u => u.Id == request.UserId, cancellationToken)
            ?? throw new Exception("Staff not found.");

        var resetEvent = new TenantStaffPasswordResetEvent
        {
            TenantId = staffUser.TenantId,
            UserId = staffUser.Id,
            Email = staffUser.Email,
        };

        var outboxMessage = new OutboxMessage
        {
            EventType = nameof(TenantStaffPasswordResetEvent),
            Payload = JsonSerializer.Serialize(resetEvent),
            CreatedAt = DateTimeOffset.UtcNow
        };

        context.OutboxMessages.Add(outboxMessage);
        await context.SaveChangesAsync(cancellationToken);
    }
}
