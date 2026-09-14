using System.Security.Cryptography;
using Asp.Versioning;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Mvc;
using RegulatoryCompliance.Grpc;
using Shared.Security;

namespace SystemBff.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/system/ingestion")]
[Route("api/system")]
public sealed class SystemIngestionController(
    RegulatoryComplianceService.RegulatoryComplianceServiceClient regulatoryClient,
    ICurrentUserService currentUser)
    : ControllerBase
{
    private Metadata CreateHeaders()
    {
        var headers = new Metadata
        {
            { "x-service-id", "system-bff" }
        };

        var tenantId = currentUser.TenantId ?? Guid.Empty;
        headers.Add("x-tenant-id", tenantId.ToString());

        if (currentUser.UserId.HasValue)
            headers.Add("x-user-id", currentUser.UserId.Value.ToString());

        if (!string.IsNullOrEmpty(currentUser.Role))
            headers.Add("x-role", currentUser.Role);

        return headers;
    }

    /// <summary>
    /// Automated System Ingestion: Global Laws & Treaties
    /// </summary>
    [HttpPost("regulatory-sources")]
    public async Task<IActionResult> IngestSystemRegulatorySource(
        [FromBody] SystemRegulatoryRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Authority))
            return BadRequest(new { error = "Title and Authority are required." });

        var content = !string.IsNullOrEmpty(request.RawText)
            ? ByteString.CopyFromUtf8(request.RawText)
            : ByteString.Empty;

        var computedSizeBytes = content.Length > 0
            ? content.Length
            : (request.SizeBytes > 0 ? request.SizeBytes : 1024);

        var computedSha256 = !string.IsNullOrWhiteSpace(request.ContentSha256)
            ? request.ContentSha256
            : (content.Length > 0
                ? Convert.ToHexString(SHA256.HashData(content.ToByteArray())).ToLowerInvariant()
                : new string('0', 64));

        var ingestRequest = new IngestRegulatorySourceRequest
        {
            IdempotencyKey = !string.IsNullOrWhiteSpace(request.IdempotencyKey)
                ? request.IdempotencyKey
                : Guid.NewGuid().ToString(),
            Authority = request.Authority,
            Title = request.Title,
            CanonicalSourceUri = !string.IsNullOrWhiteSpace(request.CanonicalSourceUri)
                ? request.CanonicalSourceUri
                : (!string.IsNullOrWhiteSpace(request.ContentReference) && (request.ContentReference.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || request.ContentReference.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    ? request.ContentReference
                    : $"https://aurora.system/regulatory/law/{Guid.NewGuid()}"),
            JurisdictionCode = request.JurisdictionCode ?? "GLOBAL",
            RegulationType = (RegulationType)(int)request.RegulationType,
            LanguageCode = request.LanguageCode ?? "en",
            VersionLabel = request.VersionLabel ?? "1.0",
            PublishedAt = Timestamp.FromDateTimeOffset(request.PublishedAt ?? DateTimeOffset.UtcNow),
            EffectiveFrom = Timestamp.FromDateTimeOffset(request.EffectiveFrom ?? DateTimeOffset.UtcNow),
            ContentReference = !string.IsNullOrWhiteSpace(request.ContentReference)
                ? request.ContentReference
                : $"regulatory/system-{Guid.NewGuid()}",
            FileName = request.FileName ?? "system-law.pdf",
            MimeType = request.MimeType ?? "application/pdf",
            SizeBytes = computedSizeBytes,
            ContentSha256 = computedSha256,
            Content = content,
            Visibility = RegulatorySourceVisibility.Platform
        };

        var headers = CreateHeaders();
        var response = await regulatoryClient.IngestRegulatorySourceAsync(ingestRequest, headers, cancellationToken: cancellationToken);

        return Ok(new
        {
            regulatoryDocumentId = response.RegulatoryDocumentId,
            documentVersionId = response.DocumentVersionId,
            status = response.Status.ToString(),
            chunkCount = response.ChunkCount,
            replayed = response.Replayed,
            source = "SYSTEM_AUTOMATION"
        });
    }

    /// <summary>
    /// List & Query System Ingested Regulatory Sources
    /// </summary>
    [HttpGet("regulatory-sources")]
    public async Task<IActionResult> QueryRegulatorySources(
        [FromQuery] string? query = null,
        [FromQuery] string? jurisdictionCode = null,
        [FromQuery] DateTimeOffset? effectiveAt = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var headers = CreateHeaders();
            var rpcRequest = new ListRegulatorySourcesRequest
            {
                Page = 1,
                PageSize = 100,
                JurisdictionCode = jurisdictionCode?.Trim() ?? string.Empty
            };

            var response = await regulatoryClient.ListRegulatorySourcesAsync(rpcRequest, headers, cancellationToken: cancellationToken);

            var searchTerm = query?.Trim();
            var items = response.Sources
                .Where(s => string.IsNullOrWhiteSpace(searchTerm) ||
                            s.Title.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                            s.Authority.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                .Select(s => new
                {
                    id = s.Id,
                    title = s.Title,
                    authority = s.Authority,
                    canonicalSourceUri = !string.IsNullOrWhiteSpace(s.CanonicalSourceUri)
                        ? s.CanonicalSourceUri
                        : $"https://aurora.system/laws/{s.Id}",
                    jurisdictionCode = s.JurisdictionCode,
                    regulationType = s.RegulationType.ToString(),
                    languageCode = s.LanguageCode,
                    versionLabel = s.LatestVersion?.VersionLabel ?? "1.0",
                    chunkCount = s.LatestVersion?.ChunkCount ?? 0,
                    status = s.LatestVersion?.Status.ToString() ?? "Completed",
                    relevanceScore = 1.0,
                    excerpt = $"{s.Title} — Authority: {s.Authority} ({s.JurisdictionCode})"
                }).ToList();

            return Ok(new { items, total = items.Count });
        }
        catch (RpcException)
        {
            return Ok(new { items = Array.Empty<object>(), total = 0 });
        }
        catch (Exception)
        {
            return Ok(new { items = Array.Empty<object>(), total = 0 });
        }
    }

    /// <summary>
    /// Automated System Ingestion: Global Knowledge & Standards
    /// </summary>
    [HttpPost("knowledge-documents")]
    public async Task<IActionResult> IngestSystemKnowledgeDocument(
        [FromBody] SystemKnowledgeRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest(new { error = "Title is required." });

        var content = !string.IsNullOrEmpty(request.RawText)
            ? ByteString.CopyFromUtf8(request.RawText)
            : ByteString.Empty;

        var computedSizeBytes = content.Length > 0
            ? content.Length
            : (request.SizeBytes > 0 ? request.SizeBytes : 1024);

        var computedSha256 = !string.IsNullOrWhiteSpace(request.ContentSha256)
            ? request.ContentSha256
            : (content.Length > 0
                ? Convert.ToHexString(SHA256.HashData(content.ToByteArray())).ToLowerInvariant()
                : new string('0', 64));

        var ingestRequest = new IngestKnowledgeSourceRequest
        {
            IdempotencyKey = !string.IsNullOrWhiteSpace(request.IdempotencyKey)
                ? request.IdempotencyKey
                : Guid.NewGuid().ToString(),
            Title = request.Title,
            Category = (KnowledgeCategory)(int)request.Category,
            SourceReference = request.SourceReference ?? $"urn:system:knowledge:{Guid.NewGuid()}",
            LanguageCode = request.LanguageCode ?? "en",
            VersionLabel = request.VersionLabel ?? "1.0",
            ContentReference = !string.IsNullOrWhiteSpace(request.ContentReference) && request.ContentReference.StartsWith("knowledge/", StringComparison.Ordinal)
                ? request.ContentReference
                : $"knowledge/system-{Guid.NewGuid()}",
            FileName = request.FileName ?? "system-knowledge.pdf",
            MimeType = request.MimeType ?? "application/pdf",
            SizeBytes = computedSizeBytes,
            ContentSha256 = computedSha256,
            Content = content,
            Visibility = RegulatorySourceVisibility.Platform
        };

        var headers = CreateHeaders();
        var response = await regulatoryClient.IngestKnowledgeDocumentAsync(ingestRequest, headers, cancellationToken: cancellationToken);

        return Ok(new
        {
            knowledgeDocumentId = response.KnowledgeDocumentId,
            documentVersionId = response.DocumentVersionId,
            status = response.Status.ToString(),
            chunkCount = response.ChunkCount,
            replayed = response.Replayed,
            source = "SYSTEM_AUTOMATION"
        });
    }

    /// <summary>
    /// List & Query System Ingested Knowledge Documents
    /// </summary>
    [HttpGet("knowledge-documents")]
    public async Task<IActionResult> QueryKnowledgeDocuments(
        [FromQuery] string? query = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var headers = CreateHeaders();
            var rpcRequest = new ListKnowledgeDocumentsRequest
            {
                Page = 1,
                PageSize = 100
            };

            var response = await regulatoryClient.ListKnowledgeDocumentsAsync(rpcRequest, headers, cancellationToken: cancellationToken);

            var searchTerm = query?.Trim();
            var items = response.Documents
                .Where(d => string.IsNullOrWhiteSpace(searchTerm) ||
                            d.Title.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                .Select(d => new
                {
                    id = d.Id,
                    title = d.Title,
                    category = d.Category == KnowledgeCategory.Sop ? "SOP" :
                               d.Category == KnowledgeCategory.Contract ? "Contract" :
                               d.Category == KnowledgeCategory.Guide ? "Guide" :
                               d.Category == KnowledgeCategory.InternalPolicy ? "Internal Policy" : d.Category.ToString(),
                    canonicalSourceUri = d.SourceReference,
                    sourceReference = d.SourceReference,
                    versionLabel = d.LatestVersion?.VersionLabel ?? "1.0",
                    chunkCount = d.LatestVersion?.ChunkCount ?? 0,
                    status = d.LatestVersion?.Status.ToString() ?? "Completed",
                    relevanceScore = 1.0,
                    excerpt = $"{d.Title} — Category: {d.Category}"
                }).ToList();

            return Ok(new { items, total = items.Count });
        }
        catch (RpcException)
        {
            return Ok(new { items = Array.Empty<object>(), total = 0 });
        }
        catch (Exception)
        {
            return Ok(new { items = Array.Empty<object>(), total = 0 });
        }
    }
}

public sealed record SystemRegulatoryRequest(
    string? IdempotencyKey,
    string Authority,
    string Title,
    string? CanonicalSourceUri,
    string? JurisdictionCode,
    int RegulationType,
    string? LanguageCode,
    string? VersionLabel,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? EffectiveFrom,
    string? ContentReference,
    string? FileName,
    string? MimeType,
    long SizeBytes,
    string? ContentSha256,
    string? RawText);

public sealed record SystemKnowledgeRequest(
    string? IdempotencyKey,
    string Title,
    int Category,
    string? SourceReference,
    string? LanguageCode,
    string? VersionLabel,
    string? ContentReference,
    string? FileName,
    string? MimeType,
    long SizeBytes,
    string? ContentSha256,
    string? RawText);
