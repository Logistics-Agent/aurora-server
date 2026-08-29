using Microsoft.EntityFrameworkCore;
using Notification.Application.Interfaces;
using Notification.Infrastructure.Persistences;

namespace Notification.Infrastructure.Messaging;

public sealed class SubscriptionRecipientResolver(NotificationDbContext db) : IRecipientResolver
{
    public async Task<IReadOnlyCollection<Guid>> ResolveAsync(Guid tenantId, Guid? shipmentId, CancellationToken cancellationToken) =>
        shipmentId is null
            ? []
            : await db.Subscriptions.Where(x => x.TenantId == tenantId && x.ShipmentId == shipmentId.Value)
            .Select(x => x.UserId).Distinct().ToListAsync(cancellationToken);
}
