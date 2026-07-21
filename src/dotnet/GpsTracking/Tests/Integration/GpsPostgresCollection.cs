using GpsTracking.Infrastructure.Persistences;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shared.Interceptors;
using Shared.Security;

namespace GpsTracking.Tests.Integration;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class GpsPostgresCollection : ICollectionFixture<GpsPostgresFixture>
{
    public const string Name = "GpsPostgres";
}

public sealed class GpsPostgresFixture : IAsyncLifetime
{
    private const string DatabaseName = "aurora_gps_tracking_tests";
    private const string AdminConnectionString =
        "Host=localhost;Port=5435;Database=postgres;Username=postgres;Password=postgres";

    public string ConnectionString { get; } =
        $"Host=localhost;Port=5435;Database={DatabaseName};Username=postgres;Password=postgres";

    public async Task InitializeAsync()
    {
        await EnsureDatabaseAsync();
        await using var context = CreateContext(new CurrentUserService());
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => ResetAsync();

    public GpsTrackingDbContext CreateContext(CurrentUserService currentUser)
    {
        var options = new DbContextOptionsBuilder<GpsTrackingDbContext>()
            .UseNpgsql(
                ConnectionString,
                npgsql => npgsql.MigrationsAssembly("GpsTracking"))
            .Options;
        return new GpsTrackingDbContext(
            options,
            currentUser,
            new AuditSaveChangesInterceptor(currentUser));
    }

    public async Task ResetAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            TRUNCATE TABLE
                geofence_presences,
                monitoring_alerts,
                current_locations,
                gps_positions,
                geofences,
                outbox_messages,
                consumed_integration_events,
                shipment_tracking_states,
                vehicle_shipment_assignments
            CASCADE;
            """;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task EnsureDatabaseAsync()
    {
        await using var connection = new NpgsqlConnection(AdminConnectionString);
        await connection.OpenAsync();
        await using var existsCommand = connection.CreateCommand();
        existsCommand.CommandText = "SELECT 1 FROM pg_database WHERE datname = @name";
        existsCommand.Parameters.AddWithValue("name", DatabaseName);
        if (await existsCommand.ExecuteScalarAsync() is not null)
            return;

        await using var createCommand = connection.CreateCommand();
        createCommand.CommandText = $"CREATE DATABASE {DatabaseName}";
        await createCommand.ExecuteNonQueryAsync();
    }
}
