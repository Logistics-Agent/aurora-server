using Microsoft.EntityFrameworkCore;
using Notification.Domain.Entities;
using Notification.Infrastructure.Persistences;
using Shared.Interceptors;
using Shared.Security;

namespace Notification.Tests;

public sealed class NotificationPersistenceModelTests
{
    [Fact]
    public void TenantOwnedEntitiesHaveQueryFilters()
    {
        using var context = CreateContext();

        var entityTypes = new[]
        {
            typeof(NotificationMessage),
            typeof(NotificationPreference),
            typeof(NotificationDeliveryAttempt),
            typeof(ConsumedIntegrationEvent)
        };

        Assert.All(entityTypes, type =>
            Assert.NotEmpty(context.Model.FindEntityType(type)!.GetDeclaredQueryFilters()));
    }

    [Fact]
    public void PreferenceAndInboxDedupeIndexesAreUnique()
    {
        using var context = CreateContext();

        Assert.Contains(
            context.Model.FindEntityType(typeof(NotificationPreference))!.GetIndexes(),
            index => index.IsUnique && index.Properties.Select(property => property.Name)
                .SequenceEqual(["TenantId", "RecipientUserId", "EventType", "Channel"]));

        Assert.Contains(
            context.Model.FindEntityType(typeof(ConsumedIntegrationEvent))!.GetIndexes(),
            index => index.IsUnique && index.Properties.Select(property => property.Name)
                .SequenceEqual(["SourceEventType", "SourceEventId"]));
    }

    [Fact]
    public void DeliveryAttemptsCascadeWithNotification()
    {
        using var context = CreateContext();
        var foreignKey = context.Model.FindEntityType(typeof(NotificationDeliveryAttempt))!
            .GetForeignKeys().Single();

        Assert.Equal(DeleteBehavior.Cascade, foreignKey.DeleteBehavior);
    }

    private static NotificationDbContext CreateContext()
    {
        var currentUser = new CurrentUserService();
        currentUser.Populate(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "test", 1, [], []);

        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseNpgsql("Host=localhost;Database=notification_model_test")
            .Options;

        return new NotificationDbContext(
            options, currentUser, new AuditSaveChangesInterceptor(currentUser));
    }
}
