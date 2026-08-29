using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Notification.Infrastructure.Persistences;

public sealed class NotificationDbContextFactory : IDesignTimeDbContextFactory<NotificationDbContext>
{
    public NotificationDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<NotificationDbContext>();
        builder.UseNpgsql("Host=localhost;Port=5433;Database=aurora_notification;Username=postgres;Password=postgres");
        return new NotificationDbContext(builder.Options);
    }
}
