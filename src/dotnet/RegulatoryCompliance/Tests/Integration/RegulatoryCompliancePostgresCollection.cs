using Microsoft.EntityFrameworkCore;
using Npgsql;
using RegulatoryCompliance.Infrastructure.Persistences;
using Shared.Interceptors;
using Shared.Security;

namespace RegulatoryCompliance.Tests.Integration;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class RegulatoryCompliancePostgresCollection
    : ICollectionFixture<RegulatoryCompliancePostgresFixture>
{
    public const string Name = "RegulatoryCompliancePostgres";
}

public sealed class RegulatoryCompliancePostgresFixture : IAsyncLifetime
{
    private const string DatabaseName = "aurora_regulatory_compliance_tests";
    private const string AdminConnectionString =
        "Host=localhost;Port=5437;Database=postgres;Username=postgres;Password=postgres";

    public string ConnectionString { get; } =
        $"Host=localhost;Port=5437;Database={DatabaseName};Username=postgres;Password=postgres";

    public async Task InitializeAsync()
    {
        await EnsureDatabaseAsync();
        await using var context = CreateContext(new CurrentUserService());
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => ResetAsync();

    public RegulatoryComplianceDbContext CreateContext(CurrentUserService currentUser)
    {
        var options = new DbContextOptionsBuilder<RegulatoryComplianceDbContext>()
            .UseNpgsql(
                ConnectionString,
                npgsql => npgsql.MigrationsAssembly("RegulatoryCompliance").UseVector())
            .Options;
        return new RegulatoryComplianceDbContext(
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
                compliance_citations,
                compliance_findings,
                retrieval_traces,
                compliance_evaluations,
                regulatory_chunks,
                regulatory_document_versions,
                regulatory_documents,
                inbox_messages,
                outbox_messages
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
