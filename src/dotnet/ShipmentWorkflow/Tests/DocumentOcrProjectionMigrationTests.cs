using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Npgsql;
using ShipmentWorkflow.Infrastructure.Persistences.Migrations;

namespace ShipmentWorkflow.Tests;

[Collection("ShipmentWorkflowDatabase")]
public sealed class DocumentOcrProjectionMigrationTests
{
    private const string AdminConnectionString =
        "Host=localhost;Port=5433;Database=postgres;Username=postgres;Password=postgres";
    private const string ConnectionString =
        "Host=localhost;Port=5433;Database=aurora_shipment_workflow_migration_tests;Username=postgres;Password=postgres";
    private const string OwnershipMarker = "aurora:migration:AddDocumentOcrProjectionEvent:owned";

    [Fact]
    public async Task Up_adds_nullable_uuid_column_and_down_removes_only_owned_column()
    {
        await using var connection = await OpenDatabaseAsync();
        await ResetTableAsync(connection);
        await ExecuteAsync(connection, "CREATE TABLE public.shipment_documents (\"Id\" uuid PRIMARY KEY);");

        try
        {
            await ApplyUpAsync(connection);

            var shape = await ReadColumnShapeAsync(connection);
            Assert.Equal("uuid", shape.DataType);
            Assert.True(shape.IsNullable);
            Assert.Equal(OwnershipMarker, shape.Comment);

            await ApplyDownAsync(connection);

            Assert.False(await ColumnExistsAsync(connection));
        }
        finally
        {
            await ResetTableAsync(connection);
        }
    }

    [Fact]
    public async Task Up_accepts_pre_existing_nullable_uuid_and_down_preserves_column_and_data()
    {
        await using var connection = await OpenDatabaseAsync();
        await ResetTableAsync(connection);
        await ExecuteAsync(connection, "CREATE TABLE public.shipment_documents (\"Id\" uuid PRIMARY KEY, \"LastOcrEventId\" uuid NULL);");
        var documentId = Guid.CreateVersion7();
        var eventId = Guid.CreateVersion7();
        await ExecuteAsync(connection, "INSERT INTO public.shipment_documents (\"Id\", \"LastOcrEventId\") VALUES (@document_id, @event_id);", ("document_id", documentId), ("event_id", eventId));

        try
        {
            await ApplyUpAsync(connection);
            await ApplyDownAsync(connection);

            var value = await ExecuteScalarAsync<Guid?>(connection, "SELECT \"LastOcrEventId\" FROM public.shipment_documents WHERE \"Id\" = @document_id;", ("document_id", documentId));
            Assert.Equal(eventId, value);
            Assert.True(await ColumnExistsAsync(connection));
            Assert.Null((await ReadColumnShapeAsync(connection)).Comment);
        }
        finally
        {
            await ResetTableAsync(connection);
        }
    }

    [Fact]
    public async Task Up_rejects_existing_non_uuid_column_without_hiding_schema_mismatch()
    {
        await using var connection = await OpenDatabaseAsync();
        await ResetTableAsync(connection);
        await ExecuteAsync(connection, "CREATE TABLE public.shipment_documents (\"Id\" uuid PRIMARY KEY, \"LastOcrEventId\" text NULL);");

        try
        {
            var exception = await Assert.ThrowsAsync<PostgresException>(() => ApplyUpAsync(connection));

            Assert.Contains("LastOcrEventId", exception.Message, StringComparison.Ordinal);
            Assert.Contains("uuid", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("text", (await ReadColumnShapeAsync(connection)).DataType);
        }
        finally
        {
            await ResetTableAsync(connection);
        }
    }

    [Fact]
    public async Task Up_rejects_existing_non_nullable_uuid_column()
    {
        await using var connection = await OpenDatabaseAsync();
        await ResetTableAsync(connection);
        await ExecuteAsync(connection, "CREATE TABLE public.shipment_documents (\"Id\" uuid PRIMARY KEY, \"LastOcrEventId\" uuid NOT NULL);");

        try
        {
            var exception = await Assert.ThrowsAsync<PostgresException>(() => ApplyUpAsync(connection));

            Assert.Contains("nullable", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await ResetTableAsync(connection);
        }
    }

    private static async Task ApplyUpAsync(NpgsqlConnection connection) =>
        await ExecuteMigrationSqlAsync(connection, migration => migration.GetUpOperations());

    private static async Task ApplyDownAsync(NpgsqlConnection connection) =>
        await ExecuteMigrationSqlAsync(connection, migration => migration.GetDownOperations());

    private static async Task ExecuteMigrationSqlAsync(
        NpgsqlConnection connection,
        Func<InspectableAddDocumentOcrProjectionEvent, IReadOnlyList<MigrationOperation>> getOperations)
    {
        foreach (var operation in getOperations(new InspectableAddDocumentOcrProjectionEvent()).OfType<SqlOperation>())
            await ExecuteAsync(connection, operation.Sql);
    }

    private static async Task<NpgsqlConnection> OpenDatabaseAsync()
    {
        await EnsureDatabaseAsync();
        var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    private static async Task EnsureDatabaseAsync()
    {
        await using var connection = new NpgsqlConnection(AdminConnectionString);
        await connection.OpenAsync();
        await using var exists = connection.CreateCommand();
        exists.CommandText = "SELECT 1 FROM pg_database WHERE datname = 'aurora_shipment_workflow_migration_tests';";
        if (await exists.ExecuteScalarAsync() is not null)
            return;

        await using var create = connection.CreateCommand();
        create.CommandText = "CREATE DATABASE aurora_shipment_workflow_migration_tests;";
        await create.ExecuteNonQueryAsync();
    }

    private static async Task ResetTableAsync(NpgsqlConnection connection)
    {
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();
        await ExecuteAsync(connection, "DROP TABLE IF EXISTS public.shipment_documents;");
    }

    private static async Task<bool> ColumnExistsAsync(NpgsqlConnection connection) =>
        await ExecuteScalarAsync<bool>(connection, "SELECT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'shipment_documents' AND column_name = 'LastOcrEventId');");

    private static async Task<(string DataType, bool IsNullable, string? Comment)> ReadColumnShapeAsync(NpgsqlConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT format_type(attribute.atttypid, attribute.atttypmod),
                   NOT attribute.attnotnull,
                   col_description('public.shipment_documents'::regclass, attribute.attnum)
            FROM pg_attribute attribute
            WHERE attribute.attrelid = 'public.shipment_documents'::regclass
              AND attribute.attname = 'LastOcrEventId'
              AND NOT attribute.attisdropped;
            """;
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return (reader.GetString(0), reader.GetBoolean(1), reader.IsDBNull(2) ? null : reader.GetString(2));
    }

    private static async Task<T> ExecuteScalarAsync<T>(NpgsqlConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        return (T)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("Expected a scalar result."));
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync();
    }

    private sealed class InspectableAddDocumentOcrProjectionEvent : AddDocumentOcrProjectionEvent
    {
        public IReadOnlyList<MigrationOperation> GetUpOperations()
        {
            var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
            Up(builder);
            return builder.Operations;
        }

        public IReadOnlyList<MigrationOperation> GetDownOperations()
        {
            var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
            Down(builder);
            return builder.Operations;
        }
    }
}
