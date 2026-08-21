using DocumentOcr.Infrastructure.Persistences;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shared.Interceptors;
using Shared.Security;

namespace DocumentOcr.Tests.Integration;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class DocumentOcrPostgresCollection : ICollectionFixture<DocumentOcrPostgresFixture>
{
    public const string Name = "DocumentOcrPostgres";
}

public sealed class DocumentOcrPostgresFixture : IAsyncLifetime
{
    private const string DatabaseName = "aurora_document_ocr_tests";
    private const string AdminConnectionString =
        "Host=localhost;Port=5436;Database=postgres;Username=postgres;Password=postgres";

    public string ConnectionString { get; } =
        $"Host=localhost;Port=5436;Database={DatabaseName};Username=postgres;Password=postgres";

    public async Task InitializeAsync()
    {
        await EnsureDatabaseAsync();
        await using var context = CreateContext(new CurrentUserService());
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => ResetAsync();

    public DocumentOcrDbContext CreateContext(CurrentUserService currentUser)
    {
        var options = new DbContextOptionsBuilder<DocumentOcrDbContext>()
            .UseNpgsql(
                ConnectionString,
                npgsql => npgsql.MigrationsAssembly("DocumentOcr"))
            .Options;
        return new DocumentOcrDbContext(
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
                ocr_provider_attempts,
                outbox_messages,
                inbox_messages,
                document_ocr_jobs
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
