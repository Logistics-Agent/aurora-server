using DocumentOcr.Contracts.Events;
using DocumentOcr.Grpc;
using Google.Protobuf.Reflection;

namespace DocumentOcr.Tests;

public sealed class DocumentOcrContractTests
{
    [Fact]
    public void SubmitRequestDoesNotExposeTenantOrUnsafeLocationFields()
    {
        var fields = SubmitDocumentJobRequest.Descriptor.Fields.InDeclarationOrder()
            .Select(field => field.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("tenant_id", fields);
        Assert.DoesNotContain("url", fields);
        Assert.DoesNotContain("local_path", fields);
        Assert.DoesNotContain("callback_url", fields);
        Assert.Contains("storage_reference", fields);
    }

    [Fact]
    public void ServiceExposesOnlyApprovedJobOperations()
    {
        var methods = DocumentOcrService.Descriptor.Methods
            .Select(method => method.Name)
            .ToArray();

        Assert.Equal(
            ["SubmitDocumentJob", "GetDocumentJob", "ListDocumentJobs"],
            methods);
    }

    [Fact]
    public void JobResponseUsesTimestampForLifecycleDates()
    {
        var timestampFields = DocumentOcrJobResponse.Descriptor.Fields.InDeclarationOrder()
            .Where(field => field.FieldType == FieldType.Message)
            .Select(field => field.MessageType.FullName)
            .ToArray();

        Assert.NotEmpty(timestampFields);
        Assert.All(timestampFields, typeName => Assert.Equal("google.protobuf.Timestamp", typeName));
    }

    [Fact]
    public void IntegrationEventsDefaultToVersionOneAndUniqueIds()
    {
        var completed = new DocumentOcrCompletedEvent();
        var failed = new DocumentOcrFailedEvent();

        Assert.Equal(1, completed.ContractVersion);
        Assert.Equal(1, failed.ContractVersion);
        Assert.NotEqual(Guid.Empty, completed.EventId);
        Assert.NotEqual(Guid.Empty, failed.EventId);
        Assert.NotEqual(completed.EventId, failed.EventId);
    }
}
