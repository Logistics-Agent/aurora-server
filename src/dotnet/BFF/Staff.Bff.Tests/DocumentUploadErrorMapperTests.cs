using System.Text.Json;
using Grpc.Core;
using StaffBff.Services;

namespace StaffBff.Tests;

public sealed class DocumentUploadErrorMapperTests
{
    [Theory]
    [InlineData("UPLOAD_EXPIRED", 409, false)]
    [InlineData("UPLOAD_OBJECT_NOT_FOUND", 404, false)]
    [InlineData("UPLOAD_TENANT_MISMATCH", 404, false)]
    [InlineData("UPLOAD_MIME_MISMATCH", 422, false)]
    [InlineData("UPLOAD_SIZE_MISMATCH", 422, false)]
    [InlineData("UPLOAD_HASH_MISMATCH", 422, false)]
    [InlineData("UPLOAD_SIZE_EXCEEDED", 422, false)]
    [InlineData("UPLOAD_IDEMPOTENCY_CONFLICT", 409, false)]
    public void Stable_upload_errors_serialize_code_and_retryable(string code, int status, bool retryable)
    {
        var exception = new RpcException(
            new Status(StatusCode.InvalidArgument, "upload failed"),
            new Metadata { { "document-upload-validation-code", code } });

        var mapped = DocumentUploadErrorMapper.Map(exception);
        var problem = DocumentsContract.CreateProblemDetails(mapped);
        var serialized = JsonSerializer.Serialize(problem);

        Assert.Equal(code, mapped.Code);
        Assert.Equal(status, mapped.StatusCode);
        Assert.Equal(retryable, mapped.Retryable);
        Assert.Equal(code, problem.Extensions["code"]);
        Assert.Equal(retryable, problem.Extensions["retryable"]);
        Assert.Contains($"\"code\":\"{code}\"", serialized);
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
}
