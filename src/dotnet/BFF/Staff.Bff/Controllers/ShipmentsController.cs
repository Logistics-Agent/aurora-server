using Amazon.S3;
using Amazon.S3.Model;
using Asp.Versioning;
using BuildingBlocks.BFF.Attributes;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Shared.Constants;
using Shared.Security;
using ShipmentWorkflow.Grpc;

namespace StaffBff.Controllers;

/// <summary>
/// Quản lý vận đơn (Shipment Workflow Service).
/// Route: /api/v1/shipments — phân quyền chi tiết bằng [RequirePermission].
/// </summary>
[ApiVersion("1.0")]
public class ShipmentsController(
    ShipmentWorkflowService.ShipmentWorkflowServiceClient shipmentClient,
    ICurrentUserService currentUser,
    IConfiguration configuration,
    ILogger<ShipmentsController> logger) : StaffControllerBase
{
    [HttpPost]
    [RequirePermission(PermissionConstants.Shipment.Create, "documents:create")]
    public async Task<IActionResult> CreateShipment([FromBody] CreateShipmentBody body, CancellationToken ct = default)
    {
        try
        {
            var req = new CreateShipmentRequest
            {
                OrderId = body.OrderId ?? string.Empty,
                CustomerName = body.CustomerName ?? string.Empty,
                OriginAddress = body.OriginAddress ?? string.Empty,
                DestinationAddress = body.DestinationAddress ?? string.Empty,
                OriginCountry = body.OriginCountry ?? string.Empty,
                DestinationCountry = body.DestinationCountry ?? string.Empty
            };

            if (body.CargoItems != null)
            {
                req.CargoItems.AddRange(body.CargoItems.Select(c => new CargoItemRequest
                {
                    Name = c.Name ?? string.Empty,
                    Quantity = c.Quantity,
                    WeightKg = (double)c.WeightKg,
                    HsCode = c.HsCode ?? string.Empty
                }));
            }

            var response = await shipmentClient.CreateShipmentAsync(req, cancellationToken: ct);
            logger.LogInformation("Shipment {ShipmentId} created for tenant {TenantId} by {UserId}",
                response.Id, currentUser.TenantId, currentUser.UserId);

            return Created($"/api/v1/shipments/{response.Id}", response);
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.InvalidArgument)
        {
            return BadRequest(new { detail = ex.Status.Detail });
        }
    }

    [HttpGet("{id}")]
    [RequirePermission(PermissionConstants.Shipment.Read, "documents:read")]
    public async Task<IActionResult> GetShipment([FromRoute] string id, CancellationToken ct = default)
    {
        try
        {
            var response = await shipmentClient.GetShipmentAsync(new GetShipmentRequest { Id = id }, cancellationToken: ct);
            return Ok(response);
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return NotFound(new { detail = ex.Status.Detail });
        }
    }

    [HttpGet]
    [RequirePermission(PermissionConstants.Shipment.Read, "documents:read")]
    public async Task<IActionResult> ListShipments(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 10,
        [FromQuery] string? status = null,
        [FromQuery] string? shipmentNo = null,
        [FromQuery] string? customerName = null,
        [FromQuery] DateTimeOffset? createdFrom = null,
        [FromQuery] DateTimeOffset? createdTo = null,
        CancellationToken ct = default)
    {
        var req = new ListShipmentsRequest
        {
            Page = page,
            Limit = limit,
            Status = status ?? string.Empty,
            ShipmentNo = shipmentNo ?? string.Empty,
            CustomerName = customerName ?? string.Empty,
            CreatedFrom = createdFrom.HasValue ? Timestamp.FromDateTimeOffset(createdFrom.Value) : null,
            CreatedTo = createdTo.HasValue ? Timestamp.FromDateTimeOffset(createdTo.Value) : null
        };

        var response = await shipmentClient.ListShipmentsAsync(req, cancellationToken: ct);
        return Ok(response);
    }

    [HttpPut("{id}")]
    [RequirePermission(PermissionConstants.Shipment.Update, "documents:update")]
    public async Task<IActionResult> UpdateShipment([FromRoute] string id, [FromBody] UpdateShipmentBody body, CancellationToken ct = default)
    {
        try
        {
            var req = new UpdateShipmentRequest
            {
                Id = id,
                CustomerName = body.CustomerName ?? string.Empty,
                DestinationAddress = body.DestinationAddress ?? string.Empty,
                Priority = body.Priority ?? string.Empty,
                TransportMode = body.TransportMode ?? string.Empty,
                Notes = body.Notes ?? string.Empty
            };

            var response = await shipmentClient.UpdateShipmentAsync(req, cancellationToken: ct);
            return Ok(response);
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return NotFound(new { detail = ex.Status.Detail });
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.FailedPrecondition)
        {
            return StatusCode(412, new { detail = ex.Status.Detail });
        }
    }

    [HttpPost("{id}/submit")]
    [RequirePermission(PermissionConstants.Shipment.Submit, "documents:update")]
    public async Task<IActionResult> SubmitShipment([FromRoute] string id, CancellationToken ct = default)
    {
        try
        {
            var response = await shipmentClient.SubmitShipmentAsync(new SubmitShipmentRequest { Id = id }, cancellationToken: ct);
            return Ok(response);
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return NotFound(new { detail = ex.Status.Detail });
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.FailedPrecondition)
        {
            return StatusCode(412, new { detail = ex.Status.Detail });
        }
    }

    [HttpPatch("{id}/status")]
    [RequirePermission(PermissionConstants.Shipment.Update, "documents:update")]
    public async Task<IActionResult> UpdateShipmentStatus([FromRoute] string id, [FromBody] UpdateStatusBody body, CancellationToken ct = default)
    {
        try
        {
            var req = new UpdateShipmentStatusRequest
            {
                Id = id,
                Status = body.Status ?? string.Empty,
                Note = body.Note ?? string.Empty
            };
            var response = await shipmentClient.UpdateShipmentStatusAsync(req, cancellationToken: ct);
            return Ok(response);
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return NotFound(new { detail = ex.Status.Detail });
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.InvalidArgument || ex.StatusCode == Grpc.Core.StatusCode.FailedPrecondition)
        {
            return BadRequest(new { detail = ex.Status.Detail });
        }
    }

    [HttpPost("{id}/cancel")]
    [RequirePermission(PermissionConstants.Shipment.Cancel, "documents:update")]
    public async Task<IActionResult> CancelShipment([FromRoute] string id, [FromBody] CancelShipmentBody body, CancellationToken ct = default)
    {
        try
        {
            var req = new CancelShipmentRequest { Id = id, Reason = body.Reason ?? string.Empty };
            var response = await shipmentClient.CancelShipmentAsync(req, cancellationToken: ct);
            return Ok(response);
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return NotFound(new { detail = ex.Status.Detail });
        }
    }

    [HttpDelete("{id}")]
    [RequirePermission(PermissionConstants.Shipment.Delete, "documents:delete")]
    public async Task<IActionResult> DeleteDraftShipment([FromRoute] string id, CancellationToken ct = default)
    {
        try
        {
            var response = await shipmentClient.DeleteDraftShipmentAsync(new DeleteDraftShipmentRequest { Id = id }, cancellationToken: ct);
            return Ok(response);
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return NotFound(new { detail = ex.Status.Detail });
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.FailedPrecondition)
        {
            return StatusCode(412, new { detail = ex.Status.Detail });
        }
    }

    [HttpPost("import")]
    [RequirePermission(PermissionConstants.Shipment.Import, "documents:import")]
    public async Task<IActionResult> ImportShipments([FromBody] ImportShipmentsBody body, CancellationToken ct = default)
    {
        var req = new ImportShipmentsRequest
        {
            FileName = body.FileName ?? "import.csv",
            Content = body.Content ?? string.Empty,
            ImportRequestId = body.ImportRequestId ?? Guid.NewGuid().ToString()
        };

        var response = await shipmentClient.ImportShipmentsAsync(req, cancellationToken: ct);
        return Ok(response);
    }

    [HttpPost("{id}/cargo")]
    [RequirePermission(PermissionConstants.Shipment.Create, "documents:create")]
    public async Task<IActionResult> AddCargoItem([FromRoute] string id, [FromBody] CargoItemDto item, CancellationToken ct = default)
    {
        var req = new AddCargoItemRequest
        {
            ShipmentId = id,
            Name = item.Name ?? string.Empty,
            Quantity = item.Quantity,
            WeightKg = (double)item.WeightKg,
            HsCode = item.HsCode ?? string.Empty
        };
        var response = await shipmentClient.AddCargoItemAsync(req, cancellationToken: ct);
        return Ok(response);
    }

    [HttpPut("{id}/cargo/{itemId}")]
    [RequirePermission(PermissionConstants.Shipment.Update, "documents:update")]
    public async Task<IActionResult> UpdateCargoItem([FromRoute] string id, [FromRoute] string itemId, [FromBody] CargoItemDto item, CancellationToken ct = default)
    {
        var req = new UpdateCargoItemRequest
        {
            ShipmentId = id,
            CargoItemId = itemId,
            Name = item.Name ?? string.Empty,
            Quantity = item.Quantity,
            WeightKg = (double)item.WeightKg,
            HsCode = item.HsCode ?? string.Empty
        };
        var response = await shipmentClient.UpdateCargoItemAsync(req, cancellationToken: ct);
        return Ok(response);
    }

    [HttpDelete("{id}/cargo/{itemId}")]
    [RequirePermission(PermissionConstants.Shipment.Delete, "documents:delete")]
    public async Task<IActionResult> RemoveCargoItem([FromRoute] string id, [FromRoute] string itemId, CancellationToken ct = default)
    {
        var response = await shipmentClient.RemoveCargoItemAsync(new RemoveCargoItemRequest { ShipmentId = id, CargoItemId = itemId }, cancellationToken: ct);
        return Ok(response);
    }

    [HttpPost("{id}/locations")]
    [RequirePermission(PermissionConstants.Shipment.Create, "documents:create")]
    public async Task<IActionResult> AddShipmentLocation([FromRoute] string id, [FromBody] LocationDto loc, CancellationToken ct = default)
    {
        var req = new AddShipmentLocationRequest
        {
            ShipmentId = id,
            Type = loc.Type ?? string.Empty,
            Name = loc.Name ?? string.Empty,
            Address = loc.Address ?? string.Empty,
            Sequence = loc.Sequence,
            Latitude = loc.Latitude,
            Longitude = loc.Longitude,
            ContactName = loc.ContactName ?? string.Empty,
            ContactPhone = loc.ContactPhone ?? string.Empty
        };
        var response = await shipmentClient.AddShipmentLocationAsync(req, cancellationToken: ct);
        return Ok(response);
    }

    [HttpPut("{id}/locations/{locationId}")]
    [RequirePermission(PermissionConstants.Shipment.Update, "documents:update")]
    public async Task<IActionResult> UpdateShipmentLocation([FromRoute] string id, [FromRoute] string locationId, [FromBody] LocationDto loc, CancellationToken ct = default)
    {
        var req = new UpdateShipmentLocationRequest
        {
            ShipmentId = id,
            LocationId = locationId,
            Type = loc.Type ?? string.Empty,
            Name = loc.Name ?? string.Empty,
            Address = loc.Address ?? string.Empty,
            Sequence = loc.Sequence,
            Latitude = loc.Latitude,
            Longitude = loc.Longitude,
            ContactName = loc.ContactName ?? string.Empty,
            ContactPhone = loc.ContactPhone ?? string.Empty
        };
        var response = await shipmentClient.UpdateShipmentLocationAsync(req, cancellationToken: ct);
        return Ok(response);
    }

    [HttpDelete("{id}/locations/{locationId}")]
    [RequirePermission(PermissionConstants.Shipment.Delete, "documents:delete")]
    public async Task<IActionResult> RemoveShipmentLocation([FromRoute] string id, [FromRoute] string locationId, CancellationToken ct = default)
    {
        var response = await shipmentClient.RemoveShipmentLocationAsync(new RemoveShipmentLocationRequest { ShipmentId = id, LocationId = locationId }, cancellationToken: ct);
        return Ok(response);
    }

    [HttpPost("{id}/documents")]
    [RequirePermission(PermissionConstants.Shipment.Create, "documents:create")]
    public async Task<IActionResult> AttachShipmentDocument([FromRoute] string id, [FromBody] DocumentDto doc, CancellationToken ct = default)
    {
        var req = new AttachShipmentDocumentRequest
        {
            ShipmentId = id,
            FileName = doc.FileName ?? string.Empty,
            DocumentType = doc.DocumentType ?? string.Empty,
            StorageUrl = doc.StorageUrl ?? string.Empty,
            OcrStatus = doc.OcrStatus ?? string.Empty,
            ExtractedDataJson = doc.ExtractedDataJson ?? "{}"
        };
        if (doc.OcrConfidence.HasValue)
        {
            req.OcrConfidence = doc.OcrConfidence.Value;
        }

        var response = await shipmentClient.AttachShipmentDocumentAsync(req, cancellationToken: ct);
        return Ok(response);
    }

    [HttpDelete("{id}/documents/{documentId}")]
    [RequirePermission(PermissionConstants.Shipment.Delete, "documents:delete")]
    public async Task<IActionResult> RemoveShipmentDocument([FromRoute] string id, [FromRoute] string documentId, CancellationToken ct = default)
    {
        var response = await shipmentClient.RemoveShipmentDocumentAsync(new RemoveShipmentDocumentRequest { ShipmentId = id, DocumentId = documentId }, cancellationToken: ct);
        return Ok(response);
    }

    [HttpPost("{id}/milestones")]
    [RequirePermission(PermissionConstants.Shipment.Create, "documents:create")]
    public async Task<IActionResult> AddShipmentMilestone([FromRoute] string id, [FromBody] MilestoneDto m, CancellationToken ct = default)
    {
        var req = new AddShipmentMilestoneRequest
        {
            ShipmentId = id,
            Status = m.Status ?? string.Empty,
            Description = m.Description ?? string.Empty,
            RecordedAt = m.RecordedAt.HasValue ? Timestamp.FromDateTimeOffset(m.RecordedAt.Value) : Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow),
            Source = m.Source ?? "MANUAL"
        };
        if (m.Latitude.HasValue)
        {
            req.Latitude = m.Latitude.Value;
        }
        if (m.Longitude.HasValue)
        {
            req.Longitude = m.Longitude.Value;
        }

        var response = await shipmentClient.AddShipmentMilestoneAsync(req, cancellationToken: ct);
        return Ok(response);
    }

    [HttpGet("{id}/timeline")]
    [RequirePermission(PermissionConstants.Shipment.Read, "documents:read")]
    public async Task<IActionResult> GetShipmentTimeline([FromRoute] string id, CancellationToken ct = default)
    {
        var response = await shipmentClient.GetShipmentTimelineAsync(new GetShipmentTimelineRequest { ShipmentId = id }, cancellationToken: ct);
        return Ok(response);
    }

    [HttpPost("{id}/assign-vehicle")]
    [RequirePermission(PermissionConstants.Shipment.Update, "documents:update")]
    public async Task<IActionResult> AssignVehicle([FromRoute] string id, [FromBody] AssignVehicleBody body, CancellationToken ct = default)
    {
        try
        {
            var req = new AssignVehicleRequest
            {
                ShipmentId = id,
                VehicleId = body.VehicleId ?? string.Empty
            };
            if (body.DocumentIds != null)
            {
                req.DocumentIds.AddRange(body.DocumentIds);
            }

            var response = await shipmentClient.AssignVehicleAsync(req, cancellationToken: ct);
            logger.LogInformation("Vehicle {VehicleId} assigned to shipment {ShipmentId} by {UserId}",
                body.VehicleId, id, currentUser.UserId);

            return Ok(response);
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return NotFound(new { detail = ex.Status.Detail });
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.InvalidArgument || ex.StatusCode == Grpc.Core.StatusCode.FailedPrecondition)
        {
            return BadRequest(new { detail = ex.Status.Detail });
        }
    }

    /// <summary>
    /// Tạo Presigned URL đẩy file trực tiếp lên Cloudflare R2 với Object Key chuẩn cho Shipment:
    /// - Phương tiện: tenants/{tenantId}/shipments/{id}/vehicles/{uploadId}_{fileName}
    /// - Chứng từ:   tenants/{tenantId}/shipments/{id}/documents/{uploadId}_{fileName}
    /// </summary>
    [HttpPost("{id}/upload-session")]
    [RequirePermission(PermissionConstants.Shipment.Update, "documents:create")]
    public Task<IActionResult> CreateShipmentUploadSession(
        [FromRoute] string id,
        [FromBody] ShipmentUploadSessionRequest body,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(body.FileName))
        {
            return Task.FromResult<IActionResult>(BadRequest(new { detail = "FileName is required." }));
        }

        var tenantId = currentUser.TenantId?.ToString() ?? "00000000-0000-0000-0000-000000000000";
        var uploadId = Guid.CreateVersion7().ToString();
        var rawFileName = Path.GetFileName(body.FileName).Trim();
        var safeFileName = string.Join("_", rawFileName.Split(Path.GetInvalidFileNameChars())).Replace(" ", "_");

        var category = (body.Category ?? "documents").Trim().ToLowerInvariant();
        var categoryFolder = category is "vehicle" or "vehicles" ? "vehicles" : "documents";

        // Format chuẩn rõ ràng thuộc về Shipment
        var objectKey = $"tenants/{tenantId}/shipments/{id}/{categoryFolder}/{uploadId}_{safeFileName}";

        var mimeType = !string.IsNullOrWhiteSpace(body.MimeType) ? body.MimeType : "application/octet-stream";
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(30);

        var s3ServiceUrl = configuration["Storage:S3:ServiceUrl"]
            ?? (!string.IsNullOrWhiteSpace(configuration["R2:AccountId"]) ? $"https://{configuration["R2:AccountId"]}.r2.cloudflarestorage.com" : null)
            ?? "https://0e000fc3102c5511f71eaab39664a054.r2.cloudflarestorage.com";

        var s3Bucket = configuration["Storage:S3:Bucket"] 
            ?? configuration["R2:BucketName"] 
            ?? "aurora-document-ocr";

        var s3AccessKey = configuration["Storage:S3:AccessKey"] ?? configuration["R2:AccessKey"];
        var s3SecretKey = configuration["Storage:S3:SecretKey"] ?? configuration["R2:SecretKey"];
        var s3Region = configuration["Storage:S3:Region"] ?? "auto";

        string writeUrl;
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Content-Type"] = mimeType
        };

        if (!string.IsNullOrWhiteSpace(s3AccessKey) && !string.IsNullOrWhiteSpace(s3SecretKey))
        {
            try
            {
                var s3Config = new AmazonS3Config
                {
                    ServiceURL = s3ServiceUrl,
                    ForcePathStyle = true,
                    AuthenticationRegion = s3Region
                };
                using var s3Client = new AmazonS3Client(s3AccessKey, s3SecretKey, s3Config);
                var preSignRequest = new GetPreSignedUrlRequest
                {
                    BucketName = s3Bucket,
                    Key = objectKey,
                    Verb = HttpVerb.PUT,
                    Protocol = Protocol.HTTPS,
                    Expires = expiresAt.UtcDateTime,
                    ContentType = mimeType
                };
                preSignRequest.Headers["Content-Type"] = mimeType;
                writeUrl = s3Client.GetPreSignedURL(preSignRequest);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to generate presigned R2 URL, falling back to direct target URL");
                writeUrl = $"{s3ServiceUrl.TrimEnd('/')}/{s3Bucket}/{objectKey}";
            }
        }
        else
        {
            writeUrl = $"{s3ServiceUrl.TrimEnd('/')}/{s3Bucket}/{objectKey}";
        }

        var response = new ShipmentUploadSessionResponse(
            UploadId: uploadId,
            StorageReference: objectKey,
            WriteUrl: writeUrl,
            RequiredHeaders: headers,
            ExpiresAt: expiresAt,
            Category: categoryFolder,
            FileName: safeFileName
        );

        logger.LogInformation("Generated R2 upload session for shipment {ShipmentId}, category {Category}, key: {ObjectKey}",
            id, categoryFolder, objectKey);

        return Task.FromResult<IActionResult>(Ok(response));
    }

    /// <summary>
    /// Upload trực tiếp file lên R2 qua BFF (relay fallback nếu trình duyệt gặp lỗi CORS hoặc network chặn PUT trực tiếp)
    /// </summary>
    [HttpPost("{id}/upload-direct")]
    [RequirePermission(PermissionConstants.Shipment.Update, "documents:create")]
    public async Task<IActionResult> DirectUploadFile(
        [FromRoute] string id,
        [FromForm] IFormFile file,
        [FromForm] string? category = "documents",
        CancellationToken ct = default)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { detail = "No file uploaded." });

        var tenantId = currentUser.TenantId?.ToString() ?? "00000000-0000-0000-0000-000000000000";
        var uploadId = Guid.CreateVersion7().ToString();
        var rawFileName = Path.GetFileName(file.FileName).Trim();
        var safeFileName = string.Join("_", rawFileName.Split(Path.GetInvalidFileNameChars())).Replace(" ", "_");

        var categoryNorm = (category ?? "documents").Trim().ToLowerInvariant();
        var categoryFolder = categoryNorm is "vehicle" or "vehicles" ? "vehicles" : "documents";
        var objectKey = $"tenants/{tenantId}/shipments/{id}/{categoryFolder}/{uploadId}_{safeFileName}";

        var s3ServiceUrl = configuration["Storage:S3:ServiceUrl"]
            ?? (!string.IsNullOrWhiteSpace(configuration["R2:AccountId"]) ? $"https://{configuration["R2:AccountId"]}.r2.cloudflarestorage.com" : null)
            ?? "https://0e000fc3102c5511f71eaab39664a054.r2.cloudflarestorage.com";
        var s3Bucket = configuration["Storage:S3:Bucket"] ?? configuration["R2:BucketName"] ?? "aurora-document-ocr";
        var s3AccessKey = configuration["Storage:S3:AccessKey"] ?? configuration["R2:AccessKey"];
        var s3SecretKey = configuration["Storage:S3:SecretKey"] ?? configuration["R2:SecretKey"];
        var s3Region = configuration["Storage:S3:Region"] ?? "auto";

        if (!string.IsNullOrWhiteSpace(s3AccessKey) && !string.IsNullOrWhiteSpace(s3SecretKey))
        {
            var s3Config = new AmazonS3Config
            {
                ServiceURL = s3ServiceUrl,
                ForcePathStyle = true,
                AuthenticationRegion = s3Region
            };
            using var s3Client = new AmazonS3Client(s3AccessKey, s3SecretKey, s3Config);
            using var stream = file.OpenReadStream();
            var putReq = new PutObjectRequest
            {
                BucketName = s3Bucket,
                Key = objectKey,
                InputStream = stream,
                ContentType = file.ContentType
            };
            await s3Client.PutObjectAsync(putReq, ct);
        }

        var storageUrl = $"{s3ServiceUrl.TrimEnd('/')}/{s3Bucket}/{objectKey}";
        return Ok(new
        {
            uploadId,
            storageReference = objectKey,
            storageUrl,
            fileName = safeFileName,
            category = categoryFolder
        });
    }
}

// ── DTOs ───────────────────────────────────────────────────────────────────

public record ShipmentUploadSessionRequest(
    string FileName,
    string? MimeType,
    long? FileSize,
    string? Category);

public record ShipmentUploadSessionResponse(
    string UploadId,
    string StorageReference,
    string WriteUrl,
    Dictionary<string, string> RequiredHeaders,
    DateTimeOffset ExpiresAt,
    string Category,
    string FileName);

public record AssignVehicleBody(string? VehicleId, List<string>? DocumentIds);

public record CreateShipmentBody(
    string? OrderId,
    string? CustomerName,
    string? OriginAddress,
    string? DestinationAddress,
    string? OriginCountry,
    string? DestinationCountry,
    List<CargoItemDto>? CargoItems);

public record UpdateShipmentBody(
    string? CustomerName,
    string? DestinationAddress,
    string? Priority,
    string? TransportMode,
    string? Notes);

public record UpdateStatusBody(string? Status, string? Note);
public record CancelShipmentBody(string? Reason);
public record ImportShipmentsBody(string? FileName, string? Content, string? ImportRequestId);

public record CargoItemDto(
    string? Name,
    int Quantity,
    decimal WeightKg,
    string? HsCode);

public record LocationDto(
    string? Type,
    string? Name,
    string? Address,
    int Sequence,
    double Latitude,
    double Longitude,
    string? ContactName,
    string? ContactPhone);

public record DocumentDto(
    string? FileName,
    string? DocumentType,
    string? StorageUrl,
    string? OcrStatus,
    double? OcrConfidence,
    string? ExtractedDataJson);

public record MilestoneDto(
    string? Status,
    string? Description,
    DateTimeOffset? RecordedAt,
    string? Source,
    double? Latitude,
    double? Longitude);
