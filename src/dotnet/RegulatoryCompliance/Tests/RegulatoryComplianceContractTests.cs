using Google.Protobuf.Reflection;
using RegulatoryCompliance.Contracts.Events;
using RegulatoryCompliance.Grpc;

namespace RegulatoryCompliance.Tests;

public sealed class RegulatoryComplianceContractTests
{
    [Fact]
    public void ClientRequestsDoNotExposeTenantOrProviderInternals()
    {
        var requestDescriptors = new[]
        {
            EvaluateComplianceRequest.Descriptor,
            GetComplianceEvaluationRequest.Descriptor,
            QueryRegulationsRequest.Descriptor,
            IngestRegulatorySourceRequest.Descriptor
        };

        var forbiddenNames = new[]
        {
            "tenant_id", "embedding", "prompt", "provider_credential", "api_key"
        };
        foreach (var descriptor in requestDescriptors)
        foreach (var forbidden in forbiddenNames)
            Assert.DoesNotContain(descriptor.Fields.InDeclarationOrder(), field => field.Name == forbidden);
    }

    [Fact]
    public void ServiceExposesOnlyApprovedMvpOperations()
    {
        var methods = RegulatoryComplianceService.Descriptor.Methods
            .Select(method => method.Name)
            .OrderBy(name => name)
            .ToArray();

        Assert.Equal(
            ["EvaluateCompliance", "GetComplianceEvaluation", "IngestRegulatorySource", "QueryRegulations"],
            methods);
    }

    [Fact]
    public void EvaluationAndQueryShapesCarryEvidenceAndSufficiency()
    {
        Assert.NotNull(ComplianceFinding.Descriptor.FindFieldByName("citations"));
        Assert.NotNull(RegulationEvidence.Descriptor.FindFieldByName("citation"));
        Assert.NotNull(ComplianceEvaluationResponse.Descriptor.FindFieldByName("evidence_sufficiency"));
        Assert.NotNull(QueryRegulationsResponse.Descriptor.FindFieldByName("evidence_sufficiency"));
        Assert.NotNull(QueryRegulationsResponse.Descriptor.FindFieldByName("evidence"));
        Assert.NotNull(QueryRegulationsResponse.Descriptor.FindFieldByName("generated_explanation"));
    }

    [Fact]
    public void LifecycleAndEffectiveDatesUseProtobufTimestamps()
    {
        AssertTimestamp(EvaluateComplianceRequest.Descriptor, "effective_at");
        AssertTimestamp(ComplianceEvaluationResponse.Descriptor, "requested_at");
        AssertTimestamp(ComplianceEvaluationResponse.Descriptor, "completed_at");
        AssertTimestamp(RegulationCitation.Descriptor, "effective_from");
        AssertTimestamp(IngestRegulatorySourceRequest.Descriptor, "published_at");
    }

    [Fact]
    public void IntegrationEventsAreVersionedAndUseUniqueIdentifiers()
    {
        var completed = new ComplianceEvaluationCompletedEvent();
        var failed = new ComplianceEvaluationFailedEvent();

        Assert.Equal(1, completed.ContractVersion);
        Assert.Equal(1, failed.ContractVersion);
        Assert.NotEqual(Guid.Empty, completed.EventId);
        Assert.NotEqual(Guid.Empty, failed.EventId);
        Assert.NotEqual(completed.EventId, failed.EventId);
    }

    private static void AssertTimestamp(MessageDescriptor descriptor, string fieldName)
    {
        var field = descriptor.FindFieldByName(fieldName);
        Assert.NotNull(field);
        Assert.Equal("google.protobuf.Timestamp", field.MessageType.FullName);
    }
}
