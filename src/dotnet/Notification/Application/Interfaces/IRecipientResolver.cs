namespace Notification.Application.Interfaces;

public interface IRecipientResolver
{
    Task<IReadOnlyCollection<Guid>> ResolveAsync(Guid tenantId, Guid? shipmentId, CancellationToken cancellationToken);
}
