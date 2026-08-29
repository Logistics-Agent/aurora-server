using Microsoft.EntityFrameworkCore;
using Notification.Application.Interfaces;
using Notification.Infrastructure.Persistences;

namespace Notification.Infrastructure.Messaging;

public sealed class SubscriptionRecipientResolver(NotificationDbContext db) : IRecipientResolver
{
    public async Task<IReadOnlyCollection<Guid>> ResolveAsync(Guid tenantId, Guid shipmentId, CancellationToken cancellationToken) =>
        await db.Subscriptions.Where(x => x.TenantId == tenantId && x.ShipmentId == shipmentId)
            .Select(x => x.UserId).Distinct().ToListAsync(cancellationToken);
}
