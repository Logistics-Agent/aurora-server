using System.Reflection;
using System.Text.Json;
using BuildingBlocks.BFF.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Shared.Constants;
using StaffBff.Controllers;
using StaffBff.Services;

namespace StaffBff.Tests;

public sealed class DocumentsPublishedContractTests
{
    private const string FixtureResourceName =
        "Staff.Bff.Tests.Contracts.staff-bff-documents.openapi.json";

    [Fact]
    public void Published_fixture_contains_only_current_documents_contract_routes()
    {
        using var stream = typeof(DocumentsPublishedContractTests).Assembly
            .GetManifestResourceStream(FixtureResourceName);
        Assert.NotNull(stream);

        using var document = JsonDocument.Parse(stream!);
        var root = document.RootElement;
        var paths = root.GetProperty("paths");

        Assert.Equal("3.0.3", root.GetProperty("openapi").GetString());
        Assert.Contains("/api/v1/documents/uploads", paths.EnumerateObject().Select(path => path.Name));
        Assert.Contains("/api/v1/shipments/{id}/document-intakes", paths.EnumerateObject().Select(path => path.Name));
        Assert.Contains("/api/v1/documents/shipment-documents", paths.EnumerateObject().Select(path => path.Name));
        Assert.Contains("/api/v1/documents/shipment/{id}", paths.EnumerateObject().Select(path => path.Name));
        Assert.Contains("/api/v1/documents/shipment-documents/{id}/review", paths.EnumerateObject().Select(path => path.Name));
        Assert.Contains("/api/v1/documents/shipment-documents/{id}/cancel", paths.EnumerateObject().Select(path => path.Name));
        Assert.Contains("/api/v1/documents/shipment-documents/{id}/retry", paths.EnumerateObject().Select(path => path.Name));
        Assert.DoesNotContain(paths.EnumerateObject(), path =>
            path.Name.StartsWith("/api/v1/documents/jobs/", StringComparison.Ordinal) ||
            path.Name.StartsWith("/api/v1/documents/ocr/jobs/", StringComparison.Ordinal));

        AssertPermission(paths, "/api/v1/documents/uploads", "post", PermissionConstants.Documents.Ingest);
        AssertPermission(paths, "/api/v1/shipments/{id}/document-intakes", "post", PermissionConstants.Documents.Ingest);
        AssertPermission(paths, "/api/v1/documents/shipment-documents", "get", PermissionConstants.Documents.Read);
        AssertPermission(paths, "/api/v1/documents/shipment/{id}", "get", PermissionConstants.Documents.Read);
        AssertPermission(paths, "/api/v1/documents/shipment-documents/{id}/review", "get", PermissionConstants.Ocr.Review);
        AssertPermission(paths, "/api/v1/documents/shipment-documents/{id}/review", "post", PermissionConstants.Ocr.Review);
        AssertPermission(paths, "/api/v1/documents/shipment-documents/{id}/cancel", "post", PermissionConstants.Documents.Manage);
        AssertPermission(paths, "/api/v1/documents/shipment-documents/{id}/retry", "post", PermissionConstants.Documents.Manage);

        AssertStatus(paths, "/api/v1/documents/uploads", "post", "201");
        AssertStatus(paths, "/api/v1/shipments/{id}/document-intakes", "post", "202");
        AssertStatus(paths, "/api/v1/documents/uploads", "post", "400");
        AssertStatus(paths, "/api/v1/documents/uploads", "post", "409");
        AssertStatus(paths, "/api/v1/documents/uploads", "post", "422");
        AssertStatus(paths, "/api/v1/documents/uploads", "post", "503");
        AssertStatus(paths, "/api/v1/shipments/{id}/document-intakes", "post", "400");
        AssertStatus(paths, "/api/v1/shipments/{id}/document-intakes", "post", "404");
        AssertStatus(paths, "/api/v1/shipments/{id}/document-intakes", "post", "409");
        AssertStatus(paths, "/api/v1/shipments/{id}/document-intakes", "post", "422");
        AssertStatus(paths, "/api/v1/shipments/{id}/document-intakes", "post", "503");
    }

    [Fact]
    public void Controller_metadata_matches_published_documents_contract()
    {
        AssertHttpTemplate<DocumentsController>(
            nameof(DocumentsController.CreateUploadSession), "POST", "uploads");
        AssertHttpTemplate<ShipmentsController>(
            nameof(ShipmentsController.CreateDocumentIntake), "POST", "{id}/document-intakes");
        AssertHttpTemplate<DocumentsController>(
            nameof(DocumentsController.ListShipmentDocuments), "GET", "shipment-documents");
        AssertHttpTemplate<DocumentsController>(
            nameof(DocumentsController.GetShipmentDocumentStatus), "GET", "shipment/{id}");
        AssertHttpTemplate<DocumentsController>(
            nameof(DocumentsController.GetShipmentDocumentReview), "GET", "shipment-documents/{id}/review");
        AssertHttpTemplate<DocumentsController>(
            nameof(DocumentsController.SubmitShipmentDocumentReview), "POST", "shipment-documents/{id}/review");
        AssertHttpTemplate<DocumentsController>(
            nameof(DocumentsController.CancelShipmentDocument), "POST", "shipment-documents/{id}/cancel");
        AssertHttpTemplate<DocumentsController>(
            nameof(DocumentsController.RetryShipmentDocument), "POST", "shipment-documents/{id}/retry");

        AssertPermission<DocumentsController>(nameof(DocumentsController.CreateUploadSession), PermissionConstants.Documents.Ingest);
        AssertPermission<ShipmentsController>(nameof(ShipmentsController.CreateDocumentIntake), PermissionConstants.Documents.Ingest);
        AssertPermission<DocumentsController>(nameof(DocumentsController.ListShipmentDocuments), PermissionConstants.Documents.Read);
        AssertPermission<DocumentsController>(nameof(DocumentsController.GetShipmentDocumentStatus), PermissionConstants.Documents.Read);
        AssertPermission<DocumentsController>(nameof(DocumentsController.GetShipmentDocumentReview), PermissionConstants.Ocr.Review);
        AssertPermission<DocumentsController>(nameof(DocumentsController.SubmitShipmentDocumentReview), PermissionConstants.Ocr.Review);
        AssertPermission<DocumentsController>(nameof(DocumentsController.CancelShipmentDocument), PermissionConstants.Documents.Manage);
        AssertPermission<DocumentsController>(nameof(DocumentsController.RetryShipmentDocument), PermissionConstants.Documents.Manage);

        AssertResponse<DocumentsController>(nameof(DocumentsController.CreateUploadSession), StatusCodes.Status201Created, typeof(DocumentUploadSessionResponse));
        AssertResponse<ShipmentsController>(nameof(ShipmentsController.CreateDocumentIntake), StatusCodes.Status202Accepted, typeof(DocumentIntakeHttpResponse));
        AssertResponse<DocumentsController>(nameof(DocumentsController.ListShipmentDocuments), StatusCodes.Status200OK, typeof(ListShipmentDocumentsResponse));
        AssertResponse<DocumentsController>(nameof(DocumentsController.GetShipmentDocumentStatus), StatusCodes.Status200OK, typeof(UnifiedDocumentStatusResponse));
        AssertResponse<DocumentsController>(nameof(DocumentsController.GetShipmentDocumentReview), StatusCodes.Status200OK, typeof(OcrReviewDetailsResponse));
        AssertResponse<DocumentsController>(nameof(DocumentsController.SubmitShipmentDocumentReview), StatusCodes.Status200OK, typeof(UnifiedDocumentStatusResponse));
        AssertResponse<DocumentsController>(nameof(DocumentsController.CancelShipmentDocument), StatusCodes.Status200OK, typeof(UnifiedDocumentStatusResponse));
        AssertResponse<DocumentsController>(nameof(DocumentsController.RetryShipmentDocument), StatusCodes.Status200OK, typeof(UnifiedDocumentStatusResponse));

        Assert.Contains(typeof(ProblemDetails), typeof(DocumentsController)
            .GetMethod(nameof(DocumentsController.CreateUploadSession))!
            .GetCustomAttributes<ProducesResponseTypeAttribute>()
            .Select(attribute => attribute.Type));
        Assert.Contains(typeof(ProblemDetails), typeof(ShipmentsController)
            .GetMethod(nameof(ShipmentsController.CreateDocumentIntake))!
            .GetCustomAttributes<ProducesResponseTypeAttribute>()
            .Select(attribute => attribute.Type));
    }

    [Fact]
    public void Published_dtos_keep_browser_safe_upload_and_idempotent_intake_fields()
    {
        AssertProperty(typeof(CreateDocumentUploadSessionRequest), nameof(CreateDocumentUploadSessionRequest.FileName));
        AssertProperty(typeof(CreateDocumentUploadSessionRequest), nameof(CreateDocumentUploadSessionRequest.MimeType));
        AssertProperty(typeof(CreateDocumentUploadSessionRequest), nameof(CreateDocumentUploadSessionRequest.SizeBytes));
        AssertProperty(typeof(CreateDocumentUploadSessionRequest), nameof(CreateDocumentUploadSessionRequest.ContentSha256));
        AssertProperty(typeof(DocumentUploadSessionResponse), nameof(DocumentUploadSessionResponse.UploadId));
        AssertProperty(typeof(DocumentUploadSessionResponse), nameof(DocumentUploadSessionResponse.WriteUrl));
        AssertProperty(typeof(DocumentUploadSessionResponse), nameof(DocumentUploadSessionResponse.RequiredHeaders));
        AssertProperty(typeof(DocumentUploadSessionResponse), nameof(DocumentUploadSessionResponse.ExpiresAt));
        AssertProperty(typeof(CreateDocumentIntakeBody), nameof(CreateDocumentIntakeBody.UploadId));
        AssertProperty(typeof(CreateDocumentIntakeBody), nameof(CreateDocumentIntakeBody.DocumentTypeHint));
        AssertProperty(typeof(CreateDocumentIntakeBody), nameof(CreateDocumentIntakeBody.IdempotencyKey));
        AssertProperty(typeof(DocumentIntakeHttpResponse), nameof(DocumentIntakeHttpResponse.IntakeId));
        AssertProperty(typeof(DocumentIntakeHttpResponse), nameof(DocumentIntakeHttpResponse.DocumentId));
        AssertProperty(typeof(DocumentIntakeHttpResponse), nameof(DocumentIntakeHttpResponse.OcrJobId));
        AssertProperty(typeof(DocumentIntakeHttpResponse), nameof(DocumentIntakeHttpResponse.IntakeStatus));
    }

    private static void AssertPermission<TController>(string methodName, string expectedPermission)
    {
        var method = typeof(TController).GetMethod(methodName);
        var permission = method?.GetCustomAttribute<RequirePermissionAttribute>();

        Assert.NotNull(permission);
        Assert.Equal(expectedPermission, permission!.RequiredPermission);
    }

    private static void AssertHttpTemplate<TController>(string methodName, string verb, string template)
    {
        var method = typeof(TController).GetMethod(methodName);
        var templates = method?.GetCustomAttributes<HttpMethodAttribute>()
            .Where(attribute => attribute.HttpMethods.Contains(verb, StringComparer.OrdinalIgnoreCase))
            .Select(attribute => attribute.Template)
            .ToArray();

        Assert.NotNull(templates);
        Assert.Contains(template, templates!);
    }

    private static void AssertResponse<TController>(string methodName, int statusCode, Type responseType)
    {
        var method = typeof(TController).GetMethod(methodName);
        var response = method?.GetCustomAttributes<ProducesResponseTypeAttribute>()
            .Single(attribute => attribute.StatusCode == statusCode);

        Assert.NotNull(response);
        Assert.Equal(responseType, response!.Type);
    }

    private static void AssertProperty(Type type, string propertyName) =>
        Assert.NotNull(type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance));

    private static void AssertPermission(
        JsonElement paths,
        string path,
        string method,
        string expectedPermission)
    {
        var operation = paths.GetProperty(path).GetProperty(method);
        Assert.Equal(expectedPermission, operation.GetProperty("x-required-permission").GetString());
    }

    private static void AssertStatus(JsonElement paths, string path, string method, string statusCode)
    {
        var operation = paths.GetProperty(path).GetProperty(method);
        Assert.True(operation.GetProperty("responses").TryGetProperty(statusCode, out _));
    }
}
