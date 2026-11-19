using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RegulatoryCompliance.Application.Retrieval;
using RegulatoryCompliance.Contracts.Events;
using RegulatoryCompliance.Domain.Entities;
using RegulatoryCompliance.Domain.Enums;
using RegulatoryCompliance.Infrastructure.Persistences;
using Shared.Security;

namespace RegulatoryCompliance.Application.Evaluations;

public sealed record CargoEvaluationSnapshot(
    string Name,
    string? HsCode,
    int Quantity,
    string Unit,
    decimal WeightKg,
    decimal VolumeM3,
    bool IsDangerousGoods,
    string? DangerousGoodsCode,
    string? PackageType);

public sealed record OcrEvaluationSnapshot(
    Guid ExternalDocumentId,
    string DocumentType,
    string NormalizedJson,
    decimal ExtractionConfidence,
    bool NeedsReview);

public sealed record ComplianceEvaluationInput(
    string IdempotencyKey,
    Guid ExternalShipmentId,
    IReadOnlyCollection<CargoEvaluationSnapshot> Cargo,
    string OriginCountryCode,
    string DestinationCountryCode,
    IReadOnlyCollection<string> JurisdictionCodes,
    string TransportMode,
    IReadOnlyCollection<OcrEvaluationSnapshot> Documents,
    DateTimeOffset EffectiveAt,
    string ShipmentVersion = "");

public sealed record ComplianceEvaluationPage(
    IReadOnlyList<ComplianceEvaluation> Items,
    int Page,
    int PageSize,
    int TotalCount);

public interface IComplianceEvaluationService
{
    Task<ComplianceEvaluation> EvaluateAsync(
        ComplianceEvaluationInput input,
        CancellationToken cancellationToken = default);

    Task<ComplianceEvaluation> GetAsync(
        Guid evaluationId,
        CancellationToken cancellationToken = default);

    Task<ComplianceEvaluationPage> ListAsync(
        int page,
        int pageSize,
        ComplianceEvaluationStatus? status = null,
        CancellationToken cancellationToken = default);
}

public sealed class ComplianceEvaluationService(
    RegulatoryComplianceDbContext dbContext,
    IRegulationRetrievalService retrievalService,
    ICurrentUserService currentUser,
    TimeProvider timeProvider) : IComplianceEvaluationService
{
    private static readonly RegulationType[] BaseCheckTypes =
    [
        RegulationType.ImportRestriction,
        RegulationType.ExportRestriction,
        RegulationType.RequiredDocument,
        RegulationType.TransportMode,
        RegulationType.Customs
    ];

    public async Task<ComplianceEvaluation> EvaluateAsync(
        ComplianceEvaluationInput input,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenant();
        Validate(input);
        var canonicalSnapshot = ComplianceSnapshotCanonicalizer.Canonicalize(input);
        var snapshotJson = canonicalSnapshot.Json;
        var requestHash = canonicalSnapshot.SnapshotHash;
        var existing = await dbContext.ComplianceEvaluations
            .Include(evaluation => evaluation.Findings)
            .ThenInclude(finding => finding.Citations)
            .Include(evaluation => evaluation.RetrievalTraces)
            .SingleOrDefaultAsync(
                evaluation => evaluation.IdempotencyKey == input.IdempotencyKey.Trim(),
                cancellationToken);
        if (existing is not null)
        {
            if (existing.RequestHash != requestHash)
                throw new InvalidOperationException("The idempotency key was already used with a different request.");
            return existing;
        }

        var now = timeProvider.GetUtcNow();
        var evaluation = ComplianceEvaluation.Create(
            tenantId,
            input.IdempotencyKey,
            input.ExternalShipmentId,
            requestHash,
            snapshotJson,
            input.EffectiveAt,
            now);
        evaluation.Start(now);
        dbContext.ComplianceEvaluations.Add(evaluation);

        try
        {
            var types = input.Cargo.Any(cargo => cargo.IsDangerousGoods)
                ? BaseCheckTypes.Append(RegulationType.DangerousGoods).ToArray()
                : BaseCheckTypes;
            var jurisdictions = input.JurisdictionCodes
                .Append(input.OriginCountryCode)
                .Append(input.DestinationCountryCode)
                .Select(code => code.Trim().ToUpperInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var retrievalResults = new List<(RegulationType Type, string Jurisdiction, RegulationQueryResult Result)>();
            foreach (var jurisdiction in jurisdictions)
            foreach (var type in types)
            {
                var result = await retrievalService.QueryAsync(
                    new RegulationQueryInput(
                        BuildQuery(input, type, jurisdiction),
                        jurisdiction,
                        input.EffectiveAt,
                        "en",
                        [type],
                        5,
                        0.2m,
                        PersistTrace: false),
                    cancellationToken);
                retrievalResults.Add((type, jurisdiction, result));
                var trace = dbContext.RetrievalTraces.Local.Single(item => item.Id == result.RetrievalTraceId);
                evaluation.AttachRetrievalTrace(trace);
                AddEvidenceFinding(evaluation, type, jurisdiction, result, now);
            }

            var missingDocuments = RequiredDocuments(input)
                .Where(required => input.Documents.All(document =>
                    !document.DocumentType.Equals(required, StringComparison.OrdinalIgnoreCase)))
                .ToArray();
            AddMissingDocumentFinding(evaluation, missingDocuments, retrievalResults, now);

            var hasCrossDocViolations = AddCrossDocumentValidationFindings(
                evaluation,
                input,
                retrievalResults,
                now);

            var evidenceSufficiency = retrievalResults.Any(item =>
                item.Result.EvidenceSufficiency == EvidenceSufficiency.Conflicting)
                ? EvidenceSufficiency.Conflicting
                : retrievalResults.Any(item =>
                    item.Result.EvidenceSufficiency == EvidenceSufficiency.Insufficient)
                    ? EvidenceSufficiency.Insufficient
                    : EvidenceSufficiency.Sufficient;
            var assumptions = BuildAssumptions(input, evidenceSufficiency);
            var confidence = CalculateConfidence(input, retrievalResults);
            var risk = evidenceSufficiency != EvidenceSufficiency.Sufficient
                ? ComplianceRiskLevel.Unknown
                : missingDocuments.Length > 0 || hasCrossDocViolations
                    ? ComplianceRiskLevel.High
                    : input.Cargo.Any(cargo => cargo.IsDangerousGoods)
                        ? ComplianceRiskLevel.Medium
                        : ComplianceRiskLevel.Low;
            evaluation.Complete(
                risk,
                evidenceSufficiency,
                confidence,
                assumptions,
                missingDocuments,
                now);
            AddCompletedOutbox(evaluation, input, missingDocuments, now);
        }
        catch (Exception exception) when (
            exception is not OperationCanceledException && exception is not DbUpdateException)
        {
            evaluation.Fail("EVALUATION_FAILED", Bound(exception.Message, 2_000), now);
            AddFailedOutbox(evaluation, input, exception.Message, now);
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return evaluation;
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            dbContext.ChangeTracker.Clear();
            var winner = await dbContext.ComplianceEvaluations
                .Include(item => item.Findings)
                .ThenInclude(item => item.Citations)
                .Include(item => item.RetrievalTraces)
                .SingleOrDefaultAsync(
                    item => item.IdempotencyKey == input.IdempotencyKey.Trim(),
                    cancellationToken);
            if (winner is null)
                throw;
            if (winner.RequestHash != requestHash)
                throw new InvalidOperationException(
                    "The idempotency key was already used with a different request.", exception);
            return winner;
        }
    }

    public async Task<ComplianceEvaluation> GetAsync(
        Guid evaluationId,
        CancellationToken cancellationToken = default)
    {
        RequireTenant();
        if (evaluationId == Guid.Empty)
            throw new ArgumentException("EvaluationId is required.", nameof(evaluationId));
        return await dbContext.ComplianceEvaluations
            .AsNoTracking()
            .Include(evaluation => evaluation.Findings)
            .ThenInclude(finding => finding.Citations)
            .Include(evaluation => evaluation.RetrievalTraces)
            .SingleOrDefaultAsync(evaluation => evaluation.Id == evaluationId, cancellationToken)
            ?? throw new KeyNotFoundException("Compliance evaluation was not found.");
    }

    public async Task<ComplianceEvaluationPage> ListAsync(
        int page,
        int pageSize,
        ComplianceEvaluationStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        RequireTenant();
        var normalizedPage = Math.Max(page, 1);
        var normalizedPageSize = Math.Clamp(pageSize, 1, 100);
        var query = dbContext.ComplianceEvaluations
            .AsNoTracking()
            .Include(evaluation => evaluation.Findings)
            .ThenInclude(finding => finding.Citations)
            .Include(evaluation => evaluation.RetrievalTraces)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(evaluation => evaluation.Status == status.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(evaluation => evaluation.RequestedAt)
            .ThenByDescending(evaluation => evaluation.Id)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(cancellationToken);

        return new ComplianceEvaluationPage(items, normalizedPage, normalizedPageSize, totalCount);
    }

    private static void AddEvidenceFinding(
        ComplianceEvaluation evaluation,
        RegulationType type,
        string jurisdiction,
        RegulationQueryResult result,
        DateTimeOffset now)
    {
        if (result.Evidence.Count == 0)
            return;
        var finding = evaluation.AddFinding(
            ComplianceFindingType.Requirement,
            $"REG-{type}-{jurisdiction}".ToUpperInvariant(),
            type.ToString(),
            $"{type} evidence applies in {jurisdiction}",
            $"Review the cited {type} evidence before operational approval.",
            type == RegulationType.DangerousGoods ? ComplianceRiskLevel.High : ComplianceRiskLevel.Medium,
            now);
        foreach (var evidence in result.Evidence.Take(3))
            AddCitation(finding, evidence, now);
    }

    private static void AddMissingDocumentFinding(
        ComplianceEvaluation evaluation,
        IReadOnlyCollection<string> missingDocuments,
        IEnumerable<(RegulationType Type, string Jurisdiction, RegulationQueryResult Result)> retrievalResults,
        DateTimeOffset now)
    {
        if (missingDocuments.Count == 0)
            return;
        var evidence = retrievalResults
            .Where(item => item.Type == RegulationType.RequiredDocument)
            .SelectMany(item => item.Result.Evidence)
            .Take(3)
            .ToArray();
        if (evidence.Length == 0)
            return;
        var finding = evaluation.AddFinding(
            ComplianceFindingType.Warning,
            "MISSING-DOCUMENTS",
            "Documents",
            "Required shipment documents are missing",
            $"Missing document snapshots: {string.Join(", ", missingDocuments)}.",
            ComplianceRiskLevel.High,
            now);
        foreach (var item in evidence)
            AddCitation(finding, item, now);
    }

    /// <summary>
    /// Item 10: 3-Way Document Cross-Validation across OCR documents (CommercialInvoice, PackingList, BillOfLading)
    /// and declared shipment cargo (WeightKg, HsCode, Quantity).
    /// </summary>
    private static bool AddCrossDocumentValidationFindings(
        ComplianceEvaluation evaluation,
        ComplianceEvaluationInput input,
        IReadOnlyCollection<(RegulationType Type, string Jurisdiction, RegulationQueryResult Result)> retrievalResults,
        DateTimeOffset now)
    {
        if (input.Documents.Count == 0)
            return false;

        var citationPool = retrievalResults
            .Where(item => item.Type is RegulationType.RequiredDocument or RegulationType.Customs)
            .SelectMany(item => item.Result.Evidence)
            .Concat(retrievalResults.SelectMany(item => item.Result.Evidence))
            .DistinctBy(item => item.ChunkId)
            .Take(3)
            .ToArray();

        var parsedDocs = new List<(string DocType, decimal? WeightKg, string? HsCode, int? Quantity)>();
        foreach (var doc in input.Documents)
        {
            if (string.IsNullOrWhiteSpace(doc.NormalizedJson))
                continue;
            try
            {
                using var jsonDoc = JsonDocument.Parse(doc.NormalizedJson);
                var root = jsonDoc.RootElement;
                var target = root.ValueKind == JsonValueKind.Object &&
                             root.TryGetProperty("fields", out var fieldsElem) &&
                             fieldsElem.ValueKind == JsonValueKind.Object
                    ? fieldsElem
                    : root;

                if (target.ValueKind != JsonValueKind.Object)
                    continue;

                var weight = TryReadDecimal(target, "weightKg", "grossWeightKg", "totalWeightKg", "netWeightKg");
                var hsCode = TryReadDigits(target, "hsCode", "tariffCode");
                var quantity = TryReadInt(target, "quantity", "totalPackages", "packageCount");
                parsedDocs.Add((doc.DocumentType, weight, hsCode, quantity));
            }
            catch (JsonException)
            {
                // Validated earlier
            }
        }

        bool hasViolation = false;
        var totalCargoWeight = input.Cargo.Sum(c => c.WeightKg);
        var totalCargoQty = input.Cargo.Sum(c => c.Quantity);
        var cargoHsHeadings = input.Cargo
            .Where(c => !string.IsNullOrWhiteSpace(c.HsCode) && c.HsCode!.Length >= 4)
            .Select(c => c.HsCode![..4])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 1. Cross-check Weight across OCR documents and Cargo (>5% tolerance)
        var docWeights = parsedDocs.Where(d => d.WeightKg is > 0m).ToArray();
        foreach (var doc in docWeights)
        {
            var diffRatio = Math.Abs(doc.WeightKg!.Value - totalCargoWeight) / Math.Max(doc.WeightKg.Value, totalCargoWeight);
            if (diffRatio > 0.05m)
            {
                AddViolationFinding(
                    evaluation,
                    "CROSS-DOC-WEIGHT-MISMATCH",
                    $"Weight discrepancy detected in {doc.DocType}",
                    $"OCR {doc.DocType} declares {doc.WeightKg.Value:0.##} kg whereas shipment cargo declares {totalCargoWeight:0.##} kg (>5% tolerance).",
                    citationPool,
                    now);
                hasViolation = true;
                break;
            }
        }

        if (!hasViolation && docWeights.Length >= 2)
        {
            var minW = docWeights.Min(d => d.WeightKg!.Value);
            var maxW = docWeights.Max(d => d.WeightKg!.Value);
            if ((maxW - minW) / maxW > 0.05m)
            {
                AddViolationFinding(
                    evaluation,
                    "CROSS-DOC-WEIGHT-MISMATCH",
                    "Cross-document weight discrepancy across OCR snapshots",
                    $"OCR documents report conflicting weights between {minW:0.##} kg and {maxW:0.##} kg.",
                    citationPool,
                    now);
                hasViolation = true;
            }
        }

        // 2. Cross-check HS Code heading (4-digit) across OCR documents and Cargo
        var docHsCodes = parsedDocs.Where(d => !string.IsNullOrWhiteSpace(d.HsCode) && d.HsCode!.Length >= 4).ToArray();
        foreach (var doc in docHsCodes)
        {
            var heading = doc.HsCode![..4];
            if (cargoHsHeadings.Count > 0 && !cargoHsHeadings.Contains(heading))
            {
                AddViolationFinding(
                    evaluation,
                    "CROSS-DOC-HSCODE-MISMATCH",
                    $"HS Code discrepancy detected in {doc.DocType}",
                    $"OCR {doc.DocType} declares HS Code {doc.HsCode} (heading {heading}) which conflicts with shipment cargo HS heading(s): {string.Join(", ", cargoHsHeadings)}.",
                    citationPool,
                    now);
                hasViolation = true;
                break;
            }
        }

        // 3. Cross-check Package Quantity across OCR documents and Cargo
        var docQuantities = parsedDocs.Where(d => d.Quantity is > 0).ToArray();
        foreach (var doc in docQuantities)
        {
            if (doc.Quantity!.Value != totalCargoQty)
            {
                AddViolationFinding(
                    evaluation,
                    "CROSS-DOC-QUANTITY-MISMATCH",
                    $"Package quantity discrepancy detected in {doc.DocType}",
                    $"OCR {doc.DocType} declares {doc.Quantity.Value} package(s) whereas shipment cargo declares {totalCargoQty} package(s).",
                    citationPool,
                    now);
                hasViolation = true;
                break;
            }
        }

        return hasViolation;
    }

    private static void AddViolationFinding(
        ComplianceEvaluation evaluation,
        string code,
        string title,
        string description,
        RegulationEvidenceResult[] citationPool,
        DateTimeOffset now)
    {
        var finding = evaluation.AddFinding(
            ComplianceFindingType.Violation,
            code,
            "DocumentCrossCheck",
            title,
            description,
            ComplianceRiskLevel.High,
            now);
        foreach (var item in citationPool)
            AddCitation(finding, item, now);
    }

    private static decimal? TryReadDecimal(JsonElement obj, params string[] propertyNames)
    {
        foreach (var prop in obj.EnumerateObject())
        {
            if (!propertyNames.Any(name => prop.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                continue;
            if (prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetDecimal(out var dec))
                return dec;
            if (prop.Value.ValueKind == JsonValueKind.String &&
                decimal.TryParse(prop.Value.GetString(), out var parsed))
                return parsed;
        }
        return null;
    }

    private static int? TryReadInt(JsonElement obj, params string[] propertyNames)
    {
        foreach (var prop in obj.EnumerateObject())
        {
            if (!propertyNames.Any(name => prop.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                continue;
            if (prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetInt32(out var num))
                return num;
            if (prop.Value.ValueKind == JsonValueKind.String &&
                int.TryParse(prop.Value.GetString(), out var parsed))
                return parsed;
        }
        return null;
    }

    private static string? TryReadDigits(JsonElement obj, params string[] propertyNames)
    {
        foreach (var prop in obj.EnumerateObject())
        {
            if (!propertyNames.Any(name => prop.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                continue;
            var raw = prop.Value.ValueKind == JsonValueKind.String
                ? prop.Value.GetString()
                : prop.Value.ToString();
            if (string.IsNullOrWhiteSpace(raw))
                continue;
            var digits = new string(raw.Where(char.IsDigit).ToArray());
            if (digits.Length >= 4)
                return digits;
        }
        return null;
    }

    private static void AddCitation(
        ComplianceFinding finding,
        RegulationEvidenceResult evidence,
        DateTimeOffset now) =>
        finding.AddCitation(
            evidence.RegulatoryDocumentId,
            evidence.DocumentVersionId,
            evidence.ChunkId,
            evidence.Authority,
            evidence.Title,
            evidence.CanonicalSourceUri,
            evidence.VersionLabel,
            evidence.SectionLabel,
            evidence.PageLabel,
            evidence.EffectiveFrom,
            evidence.EffectiveTo,
            evidence.Excerpt,
            evidence.RelevanceScore,
            now);

    private void AddCompletedOutbox(
        ComplianceEvaluation evaluation,
        ComplianceEvaluationInput input,
        IReadOnlyCollection<string> missingDocuments,
        DateTimeOffset now)
    {
        var completed = new ComplianceEvaluationCompletedEvent
        {
            TenantId = evaluation.TenantId,
            EvaluationId = evaluation.Id,
            ExternalShipmentId = input.ExternalShipmentId,
            ExternalDocumentIds = input.Documents.Select(document => document.ExternalDocumentId).ToArray(),
            RiskLevel = evaluation.RiskLevel!.Value.ToString(),
            EvidenceSufficiency = evaluation.EvidenceSufficiency!.Value.ToString(),
            ComplianceConfidence = evaluation.Confidence!.Value,
            ViolationCount = evaluation.Findings.Count(finding =>
                finding.Type == ComplianceFindingType.Violation),
            MissingDocuments = missingDocuments.ToArray(),
            Summary = "Compliance evaluation completed with cited regulatory evidence.",
            OccurredAt = now
        };
        dbContext.OutboxMessages.Add(OutboxMessage.Create(
            evaluation.TenantId,
            completed.EventId,
            typeof(ComplianceEvaluationCompletedEvent).FullName!,
            JsonSerializer.Serialize(completed),
            now));
    }

    private void AddFailedOutbox(
        ComplianceEvaluation evaluation,
        ComplianceEvaluationInput input,
        string error,
        DateTimeOffset now)
    {
        var failed = new ComplianceEvaluationFailedEvent
        {
            TenantId = evaluation.TenantId,
            EvaluationId = evaluation.Id,
            ExternalShipmentId = input.ExternalShipmentId,
            ExternalDocumentIds = input.Documents.Select(document => document.ExternalDocumentId).ToArray(),
            ErrorCode = "EVALUATION_FAILED",
            ErrorMessage = Bound(error, 2_000),
            Summary = "Compliance evaluation failed and requires review.",
            OccurredAt = now
        };
        dbContext.OutboxMessages.Add(OutboxMessage.Create(
            evaluation.TenantId,
            failed.EventId,
            typeof(ComplianceEvaluationFailedEvent).FullName!,
            JsonSerializer.Serialize(failed),
            now));
    }

    private static string BuildQuery(
        ComplianceEvaluationInput input,
        RegulationType type,
        string jurisdiction)
    {
        var hsCodes = string.Join(" ", input.Cargo
            .Where(c => !string.IsNullOrWhiteSpace(c.HsCode))
            .Select(c => c.HsCode));
        var dgCodes = string.Join(" ", input.Cargo
            .Where(c => !string.IsNullOrWhiteSpace(c.DangerousGoodsCode))
            .Select(c => c.DangerousGoodsCode));
        var extraCodes = $"{hsCodes} {dgCodes}".Trim();
        return string.IsNullOrWhiteSpace(extraCodes)
            ? $"{type} requirements for {input.TransportMode} shipment from {input.OriginCountryCode} to {input.DestinationCountryCode} in {jurisdiction}"
            : $"{type} requirements for {input.TransportMode} shipment ({extraCodes}) from {input.OriginCountryCode} to {input.DestinationCountryCode} in {jurisdiction}";
    }

    private static IReadOnlyCollection<string> RequiredDocuments(ComplianceEvaluationInput input)
    {
        var required = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CommercialInvoice",
            "PackingList"
        };
        if (input.Cargo.Any(cargo => cargo.IsDangerousGoods))
        {
            required.Add("DangerousGoodsDeclaration");
            required.Add("SafetyDataSheet");
        }
        return required;
    }

    private static IReadOnlyCollection<string> BuildAssumptions(
        ComplianceEvaluationInput input,
        EvidenceSufficiency sufficiency)
    {
        var assumptions = new List<string>
        {
            "Shipment and cargo snapshots are treated as immutable for this evaluation."
        };
        if (sufficiency != EvidenceSufficiency.Sufficient)
            assumptions.Add("Regulatory evidence is incomplete or conflicting; manual review is required.");
        if (input.Documents.Any(document =>
                document.NeedsReview || document.ExtractionConfidence < 0.7m))
            assumptions.Add("One or more OCR document snapshots require human verification.");
        return assumptions;
    }

    private static decimal CalculateConfidence(
        ComplianceEvaluationInput input,
        IReadOnlyCollection<(RegulationType Type, string Jurisdiction, RegulationQueryResult Result)> results)
    {
        if (results.Count == 0)
            return 0m;
        var coverage = results.Count(item => item.Result.Evidence.Count > 0) / (decimal)results.Count;
        var relevance = results
            .SelectMany(item => item.Result.Evidence)
            .Select(item => item.RelevanceScore)
            .DefaultIfEmpty(0m)
            .Average();
        var extraction = input.Documents.Count == 0
            ? 1m
            : input.Documents.Average(document => document.ExtractionConfidence);
        return Math.Clamp(decimal.Round(coverage * relevance * extraction, 4), 0m, 1m);
    }

    private Guid RequireTenant() =>
        currentUser.TenantId.HasValue && currentUser.TenantId != Guid.Empty
            ? currentUser.TenantId.Value
            : throw new InvalidOperationException("Tenant context is required.");

    private static void Validate(ComplianceEvaluationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (string.IsNullOrWhiteSpace(input.IdempotencyKey) || input.IdempotencyKey.Trim().Length > 150)
            throw new ArgumentException("IdempotencyKey is required.", nameof(input.IdempotencyKey));
        if (input.ExternalShipmentId == Guid.Empty)
            throw new ArgumentException("ExternalShipmentId is required.", nameof(input.ExternalShipmentId));
        if (input.Cargo.Count == 0 || input.Cargo.Count > 500)
            throw new ArgumentOutOfRangeException(nameof(input.Cargo));
        foreach (var cargo in input.Cargo)
        {
            if (string.IsNullOrWhiteSpace(cargo.Name) || cargo.Quantity <= 0 ||
                cargo.WeightKg <= 0 || cargo.VolumeM3 < 0 || string.IsNullOrWhiteSpace(cargo.Unit))
                throw new ArgumentException("Cargo name, quantity, unit, weight, and volume are invalid.");
            if (cargo.IsDangerousGoods && string.IsNullOrWhiteSpace(cargo.DangerousGoodsCode))
                throw new ArgumentException("DangerousGoodsCode is required for dangerous cargo.");
            if (!string.IsNullOrWhiteSpace(cargo.HsCode))
            {
                var cleanHs = cargo.HsCode.Replace(".", "").Replace("-", "").Replace(" ", "").Trim();
                if (cleanHs.Length is < 4 or > 12 || cleanHs.Any(character => !char.IsDigit(character)))
                    throw new ArgumentException("Cargo HS code must contain 4-12 digits.");
            }
        }
        if (string.IsNullOrWhiteSpace(input.OriginCountryCode) ||
            string.IsNullOrWhiteSpace(input.DestinationCountryCode) ||
            input.JurisdictionCodes.Count == 0 ||
            input.JurisdictionCodes.Count > 20)
            throw new ArgumentException("Route jurisdictions are required.");
        if (string.IsNullOrWhiteSpace(input.TransportMode) || input.EffectiveAt == default)
            throw new ArgumentException("TransportMode and EffectiveAt are required.");
        foreach (var document in input.Documents)
        {
            if (document.ExternalDocumentId == Guid.Empty || string.IsNullOrWhiteSpace(document.DocumentType))
                throw new ArgumentException("OCR document identity and type are required.");
            if (document.ExtractionConfidence is < 0m or > 1m)
                throw new ArgumentOutOfRangeException(nameof(document.ExtractionConfidence));
            try
            {
                using var _ = JsonDocument.Parse(document.NormalizedJson);
            }
            catch (JsonException exception)
            {
                throw new ArgumentException("OCR NormalizedJson is invalid.", exception);
            }
        }
    }

    private static string Sha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static string Bound(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation
        };
}
