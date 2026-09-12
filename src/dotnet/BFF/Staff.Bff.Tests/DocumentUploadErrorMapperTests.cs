using System.Text.Json;
using Grpc.Core;
using StaffBff.Services;

namespace StaffBff.Tests;

public sealed class DocumentUploadErrorMapperTests
{
    [Theory]
    [InlineData("UPLOAD_EXPIRED", "UPLOAD_EXPIRED", 409, false)]
    [InlineData("UPLOAD_OBJECT_NOT_FOUND", "UPLOAD_OBJECT_NOT_FOUND", 404, false)]
    [InlineData("UPLOAD_TENANT_MISMATCH", "UPLOAD_TENANT_MISMATCH", 404, false)]
    [InlineData("UPLOAD_MIME_MISMATCH", "UPLOAD_MIME_MISMATCH", 422, false)]
    [InlineData("UPLOAD_SIZE_MISMATCH", "UPLOAD_SIZE_MISMATCH", 422, false)]
    [InlineData("UPLOAD_HASH_MISMATCH", "UPLOAD_HASH_MISMATCH", 422, false)]
    [InlineData("UPLOAD_CONTENT_MISMATCH", "UPLOAD_CONTENT_MISMATCH", 422, false)]
    [InlineData("UPLOAD_SIZE_EXCEEDED", "UPLOAD_SIZE_EXCEEDED", 422, false)]
    [InlineData("UPLOAD_IDEMPOTENCY_CONFLICT", "UPLOAD_IDEMPOTENCY_CONFLICT", 409, false)]
    [InlineData("UPLOAD_VERIFICATION_IN_PROGRESS", "UPLOAD_VERIFICATION_IN_PROGRESS", 409, true)]
    [InlineData("UPLOAD_NOT_VERIFIED", "UPLOAD_NOT_VERIFIED", 409, false)]
    [InlineData("UPLOAD_NOT_READY", "UPLOAD_NOT_READY", 409, false)]
    [InlineData("UPLOAD_INVALID", "UPLOAD_INVALID", 422, false)]
    [InlineData("UPLOAD_INVALID_REQUEST", "INVALID_UPLOAD_REQUEST", 400, false)]
    [InlineData("DOCUMENT_OCR_UNAVAILABLE", "DOCUMENT_OCR_UNAVAILABLE", 503, true)]
    public void Stable_upload_errors_serialize_code_and_retryable(
        string trailerCode,
        string expectedCode,
        int status,
        bool retryable)
    {
        var exception = new RpcException(
            new Status(StatusCode.InvalidArgument, "upload failed"),
            new Metadata { { "document-upload-validation-code", trailerCode } });

        var mapped = DocumentUploadErrorMapper.Map(exception);
        var problem = DocumentsContract.CreateProblemDetails(mapped);
        var serialized = JsonSerializer.Serialize(problem);

        Assert.Equal(expectedCode, mapped.Code);
        Assert.Equal(status, mapped.StatusCode);
        Assert.Equal(retryable, mapped.Retryable);
        Assert.Equal(expectedCode, problem.Extensions["code"]);
        Assert.Equal(retryable, problem.Extensions["retryable"]);
        Assert.Contains($"\"code\":\"{expectedCode}\"", serialized);
        Assert.Contains($"\"retryable\":{retryable.ToString().ToLowerInvariant()}", serialized);
    }

    [Fact]
    public void Unavailable_upload_errors_are_retryable()
    {
        var mapped = DocumentUploadErrorMapper.Map(
            new RpcException(new Status(StatusCode.Unavailable, "unavailable")));

        Assert.Equal("DOCUMENT_OCR_UNAVAILABLE", mapped.Code);
        Assert.Equal(503, mapped.StatusCode);
        Assert.True(mapped.Retryable);
    }

    [Fact]
    public void Already_exists_without_trailer_maps_to_idempotency_conflict()
    {
        var mapped = DocumentUploadErrorMapper.Map(
            new RpcException(new Status(StatusCode.AlreadyExists, "conflict")));

        Assert.Equal("UPLOAD_IDEMPOTENCY_CONFLICT", mapped.Code);
        Assert.Equal(409, mapped.StatusCode);
        Assert.False(mapped.Retryable);
    }

    [Fact]
    public void Not_found_without_trailer_maps_to_upload_not_found()
    {
        var mapped = DocumentUploadErrorMapper.Map(
            new RpcException(new Status(StatusCode.NotFound, "missing")));

        Assert.Equal("UPLOAD_NOT_FOUND", mapped.Code);
        Assert.Equal(404, mapped.StatusCode);
        Assert.False(mapped.Retryable);
    }

    [Fact]
    public void Unknown_upload_trailer_maps_to_stable_upload_invalid_contract()
    {
        var mapped = DocumentUploadErrorMapper.Map(
            new RpcException(
                new Status(StatusCode.InvalidArgument, "missing contract"),
                new Metadata { { "document-upload-validation-code", "UPLOAD_UNKNOWN_INTERNAL_CODE" } }));

        Assert.Equal("UPLOAD_INVALID", mapped.Code);
        Assert.Equal(422, mapped.StatusCode);
        Assert.False(mapped.Retryable);
        Assert.DoesNotContain("UPLOAD_UNKNOWN_INTERNAL_CODE", mapped.Detail);
    }

    [Fact]
    public void Unknown_non_upload_trailer_maps_to_stable_upload_invalid_contract()
    {
        var mapped = DocumentUploadErrorMapper.Map(
            new RpcException(
                new Status(StatusCode.InvalidArgument, "missing contract"),
                new Metadata { { "document-upload-validation-code", "DOCUMENT_UNKNOWN_INTERNAL_CODE" } }));

        Assert.Equal("UPLOAD_INVALID", mapped.Code);
        Assert.Equal(422, mapped.StatusCode);
        Assert.False(mapped.Retryable);
        Assert.DoesNotContain("DOCUMENT_UNKNOWN_INTERNAL_CODE", mapped.Detail);
    }
}
