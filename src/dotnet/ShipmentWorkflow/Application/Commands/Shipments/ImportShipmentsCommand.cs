using System.Globalization;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Exceptions;
using Shared.Security;
using Shipment.Contracts.Events;
using ShipmentWorkflow.Application.DTOs.Shipments;
using ShipmentWorkflow.Application.Interfaces;
using ShipmentWorkflow.Domain.Entities;
using ShipmentWorkflow.Domain.Enums;
using ShipmentWorkflow.Infrastructure.Persistences;
using ShipmentEntity = global::ShipmentWorkflow.Domain.Entities.Shipment;

namespace ShipmentWorkflow.Application.Commands.Shipments;

public sealed record ImportShipmentsCommand(
    string FileName,
    string Content,
    string? ImportRequestId) : IRequest<ImportShipmentsResult>;

public sealed class ImportShipmentsCommandHandler(
    ShipmentWorkflowDbContext dbContext,
    ICurrentUserService currentUser,
    IShipmentNumberGenerator shipmentNumberGenerator)
    : IRequestHandler<ImportShipmentsCommand, ImportShipmentsResult>
{
    private const int MaxRows = 100;
    private const int MaxContentBytes = 256 * 1024;

    public async Task<ImportShipmentsResult> Handle(
        ImportShipmentsCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = ShipmentCommandHelpers.RequireTenantId(currentUser);
        ValidateRequest(request);

        var rows = ParseRows(request.Content);
        if (rows.Count > MaxRows)
        {
            throw new DomainException($"Shipment import is limited to {MaxRows} rows.");
        }

        var results = new List<ImportShipmentRowResult>();
        var shipments = new List<ShipmentEntity>();

        for (var i = 0; i < rows.Count; i++)
        {
            var rowNumber = i + 2;
            try
            {
                var shipment = await BuildShipmentAsync(tenantId, rows[i], cancellationToken);
                shipments.Add(shipment);
                results.Add(new ImportShipmentRowResult(rowNumber, true, shipment.Id, shipment.ShipmentNo, null));
            }
            catch (Exception ex) when (ex is ArgumentException or DomainException or FormatException)
            {
                results.Add(new ImportShipmentRowResult(rowNumber, false, null, null, ex.Message));
            }
        }

        if (shipments.Count > 0)
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            foreach (var shipment in shipments)
            {
                dbContext.Shipments.Add(shipment);
                dbContext.OutboxMessages.Add(CreateShipmentCreatedOutbox(shipment));
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        return new ImportShipmentsResult(
            request.ImportRequestId,
            rows.Count,
            results.Count(result => result.Success),
            results.Count(result => !result.Success),
            results);
    }

    private static void ValidateRequest(ImportShipmentsCommand request)
    {
        if (string.IsNullOrWhiteSpace(request.FileName) ||
            !request.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException("Only CSV shipment imports are supported for the MVP.");
        }

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            throw new DomainException("Import content is required.");
        }

        if (System.Text.Encoding.UTF8.GetByteCount(request.Content) > MaxContentBytes)
        {
            throw new DomainException($"Shipment import content must be {MaxContentBytes} bytes or less.");
        }
    }

    private static IReadOnlyList<ImportShipmentCsvRow> ParseRows(string content)
    {
        var lines = content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (lines.Length < 2)
        {
            throw new DomainException("Import file must include a header and at least one data row.");
        }

        var headers = SplitCsvLine(lines[0]);
        if (headers.Any(header => string.Equals(header, "tenantId", StringComparison.OrdinalIgnoreCase)))
        {
            throw new DomainException("Import files must not include TenantId.");
        }

        var index = BuildHeaderIndex(headers);
        var rows = new List<ImportShipmentCsvRow>();
        foreach (var line in lines.Skip(1).Where(line => !string.IsNullOrWhiteSpace(line)))
        {
            var fields = SplitCsvLine(line);
            rows.Add(new ImportShipmentCsvRow(
                GetField(fields, index, "orderId"),
                GetField(fields, index, "customerName"),
                GetField(fields, index, "destinationAddress"),
                GetField(fields, index, "cargoName"),
                GetField(fields, index, "quantity"),
                GetField(fields, index, "weightKg"),
                GetField(fields, index, "hsCode")));
        }

        return rows;
    }

    private async Task<ShipmentEntity> BuildShipmentAsync(
        Guid tenantId,
        ImportShipmentCsvRow row,
        CancellationToken cancellationToken)
    {
        var shipment = ShipmentEntity.Create(
            tenantId,
            await GenerateUniqueShipmentNumberAsync(tenantId, cancellationToken),
            NormalizeOptionalText(row.OrderId),
            Required(row.CustomerName, "customerName"),
            Required(row.DestinationAddress, "destinationAddress"));

        if (!int.TryParse(row.Quantity, NumberStyles.Integer, CultureInfo.InvariantCulture, out var quantity))
        {
            throw new FormatException("quantity must be a whole number.");
        }

        if (!double.TryParse(row.WeightKg, NumberStyles.Float, CultureInfo.InvariantCulture, out var weightKg))
        {
            throw new FormatException("weightKg must be a number.");
        }

        shipment.AddCargoItem(Required(row.CargoName, "cargoName"), quantity, weightKg, NormalizeOptionalText(row.HsCode));
        shipment.StatusHistories.Add(new ShipmentStatusHistory
        {
            ShipmentId = shipment.Id,
            Status = ShipmentStatus.Created,
            Note = "Shipment imported."
        });

        return shipment;
    }

    private async Task<string> GenerateUniqueShipmentNumberAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var shipmentNumber = shipmentNumberGenerator.Generate();
            var exists = await dbContext.Shipments.AnyAsync(
                shipment => shipment.TenantId == tenantId && shipment.ShipmentNo == shipmentNumber,
                cancellationToken);
            if (!exists)
            {
                return shipmentNumber;
            }
        }

        throw new ConflictException("Could not generate a unique shipment number.");
    }

    private static OutboxMessage CreateShipmentCreatedOutbox(ShipmentEntity shipment)
    {
        var createdAt = DateTimeOffset.UtcNow;
        return new OutboxMessage
        {
            EventType = nameof(ShipmentCreatedEvent),
            Payload = JsonSerializer.Serialize(new ShipmentCreatedEvent
            {
                ShipmentId = shipment.Id,
                TenantId = shipment.TenantId,
                ShipmentNumber = shipment.ShipmentNo,
                OrderId = shipment.OrderId,
                CreatedAt = createdAt
            }),
            CreatedAt = createdAt
        };
    }

    private static Dictionary<string, int> BuildHeaderIndex(IReadOnlyList<string> headers)
    {
        var required = new[] { "customerName", "destinationAddress", "cargoName", "quantity", "weightKg" };
        var index = headers
            .Select((header, i) => new { Header = header.Trim(), Index = i })
            .ToDictionary(item => item.Header, item => item.Index, StringComparer.OrdinalIgnoreCase);

        foreach (var header in required)
        {
            if (!index.ContainsKey(header))
            {
                throw new DomainException($"Missing required import column: {header}.");
            }
        }

        return index;
    }

    private static string? GetField(IReadOnlyList<string> fields, IReadOnlyDictionary<string, int> index, string name)
    {
        return index.TryGetValue(name, out var fieldIndex) && fieldIndex < fields.Count
            ? fields[fieldIndex]
            : null;
    }

    private static IReadOnlyList<string> SplitCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (ch == ',' && !inQuotes)
            {
                fields.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }

        if (inQuotes)
        {
            throw new FormatException("CSV row has an unterminated quoted field.");
        }

        fields.Add(current.ToString().Trim());
        return fields;
    }

    private static string Required(string? value, string name)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new DomainException($"{name} is required.")
            : value.Trim();
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private sealed record ImportShipmentCsvRow(
        string? OrderId,
        string? CustomerName,
        string? DestinationAddress,
        string? CargoName,
        string? Quantity,
        string? WeightKg,
        string? HsCode);
}
