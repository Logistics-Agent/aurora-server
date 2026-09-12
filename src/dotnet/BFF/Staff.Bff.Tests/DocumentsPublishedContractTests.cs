using System.Reflection;
using System.Text.Json;
using BuildingBlocks.BFF.Attributes;
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

    private static readonly OperationContract[] Operations =
    [
        new(typeof(DocumentsController), nameof(DocumentsController.CreateUploadSession), "/api/v1/documents/uploads", "post", "CreateUploadSession", PermissionConstants.Documents.Ingest, typeof(CreateDocumentUploadSessionRequest), typeof(DocumentUploadSessionResponse)),
        new(typeof(DocumentsController), nameof(DocumentsController.CreateDocumentIntake), "/api/v1/documents/intakes", "post", "CreateDocumentIntake", PermissionConstants.Documents.Ingest, typeof(CreateDocumentIntakeBody), typeof(UnifiedDocumentStatusResponse)),
        new(typeof(DocumentsController), nameof(DocumentsController.ListShipmentDocuments), "/api/v1/documents/shipment-documents", "get", "ListShipmentDocuments", PermissionConstants.Documents.Read, null, typeof(ListShipmentDocumentsResponse)),
        new(typeof(DocumentsController), nameof(DocumentsController.GetShipmentDocumentStatus), "/api/v1/documents/shipment/{id}", "get", "GetShipmentDocumentStatus", PermissionConstants.Documents.Read, null, typeof(UnifiedDocumentStatusResponse)),
        new(typeof(DocumentsController), nameof(DocumentsController.GetShipmentDocumentStatus), "/api/v1/documents/shipment-documents/{id}", "get", "GetShipmentDocumentStatusAlias", PermissionConstants.Documents.Read, null, typeof(UnifiedDocumentStatusResponse)),
        new(typeof(DocumentsController), nameof(DocumentsController.DownloadShipmentDocument), "/api/v1/documents/shipment-documents/{id}/download", "get", "GetShipmentDocumentDownload", PermissionConstants.Documents.Read, null, typeof(DocumentDownloadResponse)),
        new(typeof(DocumentsController), nameof(DocumentsController.GetShipmentDocumentReview), "/api/v1/documents/shipment-documents/{id}/review", "get", "GetShipmentDocumentReview", PermissionConstants.Ocr.Review, null, typeof(OcrReviewDetailsResponse)),
        new(typeof(DocumentsController), nameof(DocumentsController.SubmitShipmentDocumentReview), "/api/v1/documents/shipment-documents/{id}/review", "post", "SubmitShipmentDocumentReview", PermissionConstants.Ocr.Review, typeof(SubmitOcrReviewRequest), typeof(UnifiedDocumentStatusResponse)),
        new(typeof(DocumentsController), nameof(DocumentsController.CancelShipmentDocument), "/api/v1/documents/shipment-documents/{id}/cancel", "post", "CancelShipmentDocument", PermissionConstants.Documents.Manage, null, typeof(UnifiedDocumentStatusResponse)),
        new(typeof(DocumentsController), nameof(DocumentsController.RetryShipmentDocument), "/api/v1/documents/shipment-documents/{id}/retry", "post", "RetryShipmentDocument", PermissionConstants.Documents.Manage, null, typeof(UnifiedDocumentStatusResponse)),
        new(typeof(DocumentsController), nameof(DocumentsController.SubmitShipmentDocument), "/api/v1/documents/shipment-documents", "post", "SubmitShipmentDocumentLegacyAlias", PermissionConstants.Documents.Ingest, typeof(SubmitShipmentDocumentRequest), typeof(UnifiedDocumentStatusResponse)),
        new(typeof(DocumentsController), nameof(DocumentsController.SubmitShipmentDocument), "/api/v1/documents/shipment", "post", "SubmitShipmentDocumentLegacy", PermissionConstants.Documents.Ingest, typeof(SubmitShipmentDocumentRequest), typeof(UnifiedDocumentStatusResponse))
    ];

    private static readonly Type[] PublishedDtos =
    [
        typeof(CreateDocumentUploadSessionRequest),
        typeof(DocumentUploadSessionResponse),
        typeof(DocumentDownloadResponse),
        typeof(CreateDocumentIntakeBody),
        typeof(SubmitShipmentDocumentRequest),
        typeof(UnifiedDocumentStatusResponse),
        typeof(ListShipmentDocumentsResponse),
        typeof(OcrReviewDetailsResponse),
        typeof(OcrFieldReviewItem),
        typeof(SubmitOcrReviewRequest),
        typeof(OcrFieldCorrection)
    ];

    [Fact]
    public void Published_fixture_matches_current_routes_and_source_metadata()
    {
        using var document = LoadFixture();
        var root = document.RootElement;
        var paths = root.GetProperty("paths");
        var expectedOperations = Operations
            .Select(operation => $"{operation.Path} {operation.Verb}")
            .ToHashSet(StringComparer.Ordinal);
        var actualOperations = paths.EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject()
                .Where(property => IsHttpMethod(property.Name))
                .Select(property => $"{path.Name} {property.Name}"))
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(expectedOperations, actualOperations);
        Assert.Equal("3.0.3", root.GetProperty("openapi").GetString());

        foreach (var operationContract in Operations)
        {
            var operation = paths.GetProperty(operationContract.Path).GetProperty(operationContract.Verb);
            var method = GetMethod(operationContract);

            Assert.Equal(operationContract.OperationId, operation.GetProperty("operationId").GetString());
            Assert.Equal(operationContract.Permission, operation.GetProperty("x-required-permission").GetString());
            AssertSourceRoute(operationContract, method);
            Assert.Equal(
                operationContract.Permission,
                method.GetCustomAttribute<RequirePermissionAttribute>()?.RequiredPermission);
            AssertRequestBody(operationContract, method, operation);
            AssertResponses(root, operationContract, method, operation);
            AssertErrorContract(root, operationContract, method, operation);
        }
    }

    [Fact]
    public void Published_fixture_dto_schemas_match_source_properties_types_and_required_fields()
    {
        using var document = LoadFixture();
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");

        foreach (var dto in PublishedDtos)
            AssertDtoSchema(schemas, dto);
    }

    [Fact]
    public void Published_problem_details_schema_requires_canonical_error_fields()
    {
        using var document = LoadFixture();
        var schema = document.RootElement.GetProperty("components").GetProperty("schemas").GetProperty("ProblemDetails");

        Assert.Equal(
            new[] { "status", "code", "retryable" },
            schema.GetProperty("required").EnumerateArray().Select(value => value.GetString()).ToArray());
        Assert.Contains(400, schema.GetProperty("properties").GetProperty("status").GetProperty("enum").EnumerateArray().Select(value => value.GetInt32()));
        Assert.Contains("UPLOAD_IDEMPOTENCY_CONFLICT", schema.GetProperty("properties").GetProperty("code").GetProperty("enum").EnumerateArray().Select(value => value.GetString()));
        Assert.Contains("IDEMPOTENCY_CONFLICT", schema.GetProperty("properties").GetProperty("code").GetProperty("enum").EnumerateArray().Select(value => value.GetString()));
        Assert.True(schema.GetProperty("properties").GetProperty("retryable").GetProperty("type").GetString() == "boolean");
    }

    [Fact]
    public void Published_problem_examples_are_nested_under_problem_media_types()
    {
        using var document = LoadFixture();
        var responses = document.RootElement.GetProperty("components").GetProperty("responses");

        foreach (var response in responses.EnumerateObject())
        {
            Assert.False(
                response.Value.TryGetProperty("examples", out _),
                $"{response.Name} must keep examples under application/problem+json.");

            var mediaType = response.Value
                .GetProperty("content")
                .GetProperty("application/problem+json");
            Assert.True(mediaType.TryGetProperty("examples", out var examples));
            Assert.NotEmpty(examples.EnumerateObject());
        }
    }

    [Fact]
    public void Published_endpoint_catalog_contains_runtime_document_codes()
    {
        var uploadCodes = DocumentEndpointProblemContracts.Get(DocumentEndpointProblemContracts.CreateUploadSession)
            .Select(contract => contract.Code)
            .ToHashSet(StringComparer.Ordinal);
        var reviewCodes = DocumentEndpointProblemContracts.Get(DocumentEndpointProblemContracts.GetShipmentDocumentReview)
            .Select(contract => contract.Code)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains(DocumentProblemContractCatalog.DocumentOcrUnavailableCode, uploadCodes);
        Assert.Contains(DocumentProblemContractCatalog.UploadNotReadyCode, uploadCodes);
        Assert.Contains(DocumentProblemContractCatalog.DocumentNotFoundCode, reviewCodes);
    }

    private static JsonDocument LoadFixture()
    {
        using var stream = typeof(DocumentsPublishedContractTests).Assembly
            .GetManifestResourceStream(FixtureResourceName);
        Assert.NotNull(stream);
        return JsonDocument.Parse(stream!);
    }

    private static void AssertSourceRoute(OperationContract contract, MethodInfo method)
    {
        var templates = method.GetCustomAttributes<HttpMethodAttribute>()
            .Where(attribute => attribute.HttpMethods.Contains(contract.Verb, StringComparer.OrdinalIgnoreCase))
            .Select(attribute => attribute.Template)
            .ToArray();

        var controllerRoot = contract.Controller
            .GetCustomAttributes<RouteAttribute>(inherit: true)
            .Select(attribute => attribute.Template)
            .First(template => template.Contains("v{version:apiVersion}", StringComparison.Ordinal));
        var controllerName = contract.Controller.Name[..^"Controller".Length].ToLowerInvariant();
        var expectedRoot = "/" + controllerRoot
            .Replace("[controller]", controllerName, StringComparison.Ordinal)
            .Replace("v{version:apiVersion}", "v1", StringComparison.Ordinal)
            .Trim('/');
        var expectedPaths = templates.Select(template => $"{expectedRoot}/{template}");

        Assert.Contains(contract.Path, expectedPaths);
    }

    private static void AssertRequestBody(
        OperationContract contract,
        MethodInfo method,
        JsonElement operation)
    {
        var bodyParameter = method.GetParameters()
            .SingleOrDefault(parameter => parameter.GetCustomAttribute<FromBodyAttribute>() is not null);

        if (bodyParameter is null)
        {
            Assert.False(operation.TryGetProperty("requestBody", out _));
            return;
        }

        Assert.Equal(contract.RequestType, bodyParameter.ParameterType);
        var requestBody = operation.GetProperty("requestBody");
        Assert.True(requestBody.GetProperty("required").GetBoolean());
        Assert.Equal(
            $"#/components/schemas/Staff_{bodyParameter.ParameterType.Name}",
            requestBody.GetProperty("content").GetProperty("application/json").GetProperty("schema").GetProperty("$ref").GetString());
    }

    private static void AssertResponses(
        JsonElement root,
        OperationContract contract,
        MethodInfo method,
        JsonElement operation)
    {
        var sourceResponses = method.GetCustomAttributes<ProducesResponseTypeAttribute>()
            .Concat(contract.Controller.GetCustomAttributes<ProducesResponseTypeAttribute>())
            .GroupBy(attribute => attribute.StatusCode)
            .ToDictionary(group => group.Key, group => group.First());
        var publishedResponses = operation.GetProperty("responses");

        var successResponse = sourceResponses.Values.Single(response => response.Type != typeof(ProblemDetails));
        Assert.Equal(contract.SuccessType, successResponse.Type);

        Assert.Equal(
            sourceResponses.Keys.OrderBy(status => status),
            publishedResponses.EnumerateObject().Select(property => int.Parse(property.Name)).OrderBy(status => status));

        foreach (var sourceResponse in sourceResponses.Values)
        {
            var publishedResponse = ResolveResponse(root, operation, sourceResponse.StatusCode.ToString());
            var contentType = sourceResponse.Type == typeof(ProblemDetails)
                ? "application/problem+json"
                : "application/json";
            var schema = publishedResponse.GetProperty("content").GetProperty(contentType).GetProperty("schema");

            if (sourceResponse.Type == typeof(ProblemDetails))
            {
                Assert.True(schema.TryGetProperty("oneOf", out _));
                Assert.False(schema.TryGetProperty("$ref", out _));
            }
            else
            {
                Assert.Equal(
                    $"#/components/schemas/Staff_{sourceResponse.Type!.Name}",
                    schema.GetProperty("$ref").GetString());
            }
        }
    }

    private static void AssertErrorContract(
        JsonElement root,
        OperationContract operationContract,
        MethodInfo method,
        JsonElement operation)
    {
        var operationIds = method.GetCustomAttributes<DocumentProblemContractAttribute>()
            .SelectMany(attribute => attribute.OperationIds)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Contains(operationContract.OperationId, operationIds);

        var sourceContracts = DocumentEndpointProblemContracts.Get(operationContract.OperationId)
            .GroupBy(contract => contract.StatusCode)
            .ToDictionary(
                group => group.Key.ToString(),
                group => group.Select(contract => new ErrorEntry(contract.Code, contract.Retryable))
                    .OrderBy(entry => entry.Code)
                    .ThenBy(entry => entry.Retryable)
                    .ToArray());
        var publishedContracts = operation.GetProperty("x-error-contract");

        Assert.Equal(sourceContracts.Keys.OrderBy(key => key), publishedContracts.EnumerateObject().Select(property => property.Name).OrderBy(key => key));

        foreach (var sourceContract in sourceContracts)
        {
            var publishedEntries = publishedContracts.GetProperty(sourceContract.Key)
                .EnumerateArray()
                .Select(entry => new ErrorEntry(
                    entry.GetProperty("code").GetString()!,
                    entry.GetProperty("retryable").GetBoolean()))
                .OrderBy(entry => entry.Code)
                .ThenBy(entry => entry.Retryable)
                .ToArray();
            Assert.Equal(sourceContract.Value, publishedEntries);

            var responseSchema = ResolveResponse(root, operation, sourceContract.Key)
                .GetProperty("content").GetProperty("application/problem+json").GetProperty("schema");
            var variants = responseSchema.GetProperty("oneOf").EnumerateArray().ToArray();
            Assert.Equal(sourceContract.Value.Select(entry => entry.Retryable).Distinct().Count(), variants.Length);

            foreach (var entry in sourceContract.Value)
            {
                var variant = variants.Single(value => value
                    .GetProperty("allOf")[1]
                    .GetProperty("properties")
                    .GetProperty("code")
                    .GetProperty("enum")
                    .EnumerateArray()
                    .Any(code => code.GetString() == entry.Code));
                var properties = variant.GetProperty("allOf")[1].GetProperty("properties");
                Assert.Equal(int.Parse(sourceContract.Key), properties.GetProperty("status").GetProperty("enum")[0].GetInt32());
                Assert.Equal(entry.Retryable, properties.GetProperty("retryable").GetProperty("enum")[0].GetBoolean());
                var example = ResolveResponse(root, operation, sourceContract.Key)
                    .GetProperty("content").GetProperty("application/problem+json")
                    .GetProperty("examples").GetProperty(entry.Code).GetProperty("value");
                Assert.Equal(entry.Code, example.GetProperty("code").GetString());
                Assert.Equal(int.Parse(sourceContract.Key), example.GetProperty("status").GetInt32());
                Assert.Equal(entry.Retryable, example.GetProperty("retryable").GetBoolean());
            }
        }
    }

    private static JsonElement ResolveResponse(JsonElement root, JsonElement operation, string statusCode)
    {
        var response = operation.GetProperty("responses").GetProperty(statusCode);
        if (!response.TryGetProperty("$ref", out var reference))
            return response;

        var referenceValue = reference.GetString()!;
        Assert.StartsWith("#/components/responses/Problem", referenceValue, StringComparison.Ordinal);
        var componentName = referenceValue[(referenceValue.LastIndexOf('/') + 1)..];
        return root.GetProperty("components").GetProperty("responses").GetProperty(componentName);
    }

    private static void AssertDtoSchema(JsonElement schemas, Type dto)
    {
        var schema = schemas.GetProperty($"Staff_{dto.Name}");
        var properties = schema.GetProperty("properties");
        var nullability = new NullabilityInfoContext();
        var sourceProperties = dto.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.GetMethod is not null)
            .ToArray();
        var required = sourceProperties
            .Where(property => IsRequired(property, nullability))
            .Select(property => ToCamelCase(property.Name))
            .OrderBy(name => name)
            .ToArray();
        var publishedRequired = schema.TryGetProperty("required", out var requiredElement)
            ? requiredElement.EnumerateArray().Select(value => value.GetString()!).OrderBy(name => name).ToArray()
            : [];

        Assert.Equal(required, publishedRequired);
        Assert.Equal(sourceProperties.Select(property => ToCamelCase(property.Name)).OrderBy(name => name), properties.EnumerateObject().Select(property => property.Name).OrderBy(name => name));

        foreach (var property in sourceProperties)
            AssertPropertySchema(properties.GetProperty(ToCamelCase(property.Name)), property, schemas);
    }

    private static void AssertPropertySchema(JsonElement schema, PropertyInfo property, JsonElement schemas)
    {
        var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        var isNullable = Nullable.GetUnderlyingType(property.PropertyType) is not null ||
            new NullabilityInfoContext().Create(property).ReadState == NullabilityState.Nullable;

        if (isNullable)
            Assert.True(schema.GetProperty("nullable").GetBoolean());
        else
            Assert.False(schema.TryGetProperty("nullable", out var nullable) && nullable.GetBoolean());

        if (type == typeof(string))
        {
            Assert.Equal("string", schema.GetProperty("type").GetString());
            return;
        }
        if (type == typeof(Guid))
        {
            Assert.Equal("string", schema.GetProperty("type").GetString());
            Assert.Equal("uuid", schema.GetProperty("format").GetString());
            return;
        }
        if (type == typeof(DateTimeOffset) || type == typeof(DateTime))
        {
            Assert.Equal("string", schema.GetProperty("type").GetString());
            Assert.Equal("date-time", schema.GetProperty("format").GetString());
            return;
        }
        if (type == typeof(bool))
        {
            Assert.Equal("boolean", schema.GetProperty("type").GetString());
            return;
        }
        if (type == typeof(int) || type == typeof(short) || type == typeof(byte))
        {
            Assert.Equal("integer", schema.GetProperty("type").GetString());
            Assert.Equal("int32", schema.GetProperty("format").GetString());
            return;
        }
        if (type == typeof(long))
        {
            Assert.Equal("integer", schema.GetProperty("type").GetString());
            Assert.Equal("int64", schema.GetProperty("format").GetString());
            return;
        }
        if (type == typeof(double) || type == typeof(float) || type == typeof(decimal))
        {
            Assert.Equal("number", schema.GetProperty("type").GetString());
            Assert.Equal("double", schema.GetProperty("format").GetString());
            return;
        }
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>))
        {
            Assert.Equal("object", schema.GetProperty("type").GetString());
            Assert.Equal("string", schema.GetProperty("additionalProperties").GetProperty("type").GetString());
            return;
        }
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
        {
            Assert.Equal("array", schema.GetProperty("type").GetString());
            AssertArrayItemSchema(schema.GetProperty("items"), type.GetGenericArguments()[0]);
            return;
        }

        Assert.Fail($"No OpenAPI mapping defined for {property.DeclaringType?.Name}.{property.Name} ({property.PropertyType}).");
    }

    private static void AssertArrayItemSchema(JsonElement schema, Type itemType)
    {
        if (itemType == typeof(string))
        {
            Assert.Equal("string", schema.GetProperty("type").GetString());
            return;
        }

        Assert.Equal($"#/components/schemas/Staff_{itemType.Name}", schema.GetProperty("$ref").GetString());
    }

    private static bool IsRequired(PropertyInfo property, NullabilityInfoContext nullability)
    {
        if (Nullable.GetUnderlyingType(property.PropertyType) is not null)
            return false;
        if (property.PropertyType.IsValueType)
            return true;
        return nullability.Create(property).ReadState == NullabilityState.NotNull;
    }

    private static MethodInfo GetMethod(OperationContract contract) =>
        contract.Controller.GetMethod(contract.MethodName, BindingFlags.Public | BindingFlags.Instance)!;

    private static bool IsHttpMethod(string name) =>
        name is "get" or "post" or "put" or "patch" or "delete";

    private static string ToCamelCase(string name) =>
        name.Length == 0 ? name : char.ToLowerInvariant(name[0]) + name[1..];

    private sealed record OperationContract(
        Type Controller,
        string MethodName,
        string Path,
        string Verb,
        string OperationId,
        string Permission,
        Type? RequestType,
        Type SuccessType);

    private sealed record ErrorEntry(string Code, bool Retryable);
}
