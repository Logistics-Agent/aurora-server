using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RoutePlanningAgent.Infrastructure.Persistences;
using Shared.Interceptors;
using Shared.Security;

namespace RoutePlanningAgent.Tests.TestHelpers;

/// <summary>
/// DbContext test dùng Sqlite in-memory (relational thật — tôn trọng query filters/index,
/// tốt hơn provider InMemory).
/// </summary>
public static class TestDb
{
    public static readonly Guid TenantId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    public static readonly Guid UserId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");

    public static (RoutePlanningDbContext Context, SqliteConnection Connection) Create(
        Guid? tenantId = null, Guid? userId = null)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var currentUser = new FakeCurrentUser(tenantId ?? TenantId, userId ?? UserId);

        var options = new DbContextOptionsBuilder<RoutePlanningDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new RoutePlanningDbContext(
            options, currentUser, new AuditSaveChangesInterceptor(currentUser));
        context.Database.EnsureCreated();

        return (context, connection);
    }
}

public class FakeCurrentUser(Guid? tenantId, Guid? userId) : ICurrentUserService
{
    public Guid? UserId => userId;
    public Guid? TenantId => tenantId;
    public string? TraceId => "test-trace";
    public int? PermissionVersion => 1;
    public string? Role => "STAFF";
    public IReadOnlyList<string> Permissions => [];
}
