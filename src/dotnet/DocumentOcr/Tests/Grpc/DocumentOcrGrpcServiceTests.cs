using DocumentOcr.Application.Jobs;
using DocumentOcr.Domain.Entities;
using DocumentOcr.Domain.Enums;
using DocumentOcr.GrpcServices;
using Grpc.Core;
using Shared.Security;
using OcrGrpc = DocumentOcr.Grpc;

namespace DocumentOcr.Tests.Grpc;

public sealed class DocumentOcrGrpcServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 22, 5, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SubmitMapsApprovedRequestWithoutClientTenant()
    {
        var tenantId = Guid.CreateVersion7();
        var currentUser = CreateCurrentUser(tenantId);
        var fake = new FakeJobService(tenantId);
        var service = new DocumentOcrGrpcService(fake, currentUser);
        var documentId = Guid.CreateVersion7();

        var response = await service.SubmitDocumentJob(
            new OcrGrpc.SubmitDocumentJobRequest
            {
                IdempotencyKey = "request-001",
                StorageReference = "objects/tenant/invoice.pdf",
                FileName = "invoice.pdf",
                MimeType = "application/pdf",
                SizeBytes = 1_024,
                DocumentTypeHint = OcrGrpc.OcrDocumentType.CommercialInvoice,
                ExternalDocumentId = documentId.ToString()
            },
            TestServerCallContext.Create());

        Assert.Equal(tenantId, fake.LastSubmittedJob!.TenantId);
        Assert.Equal(documentId.ToString(), response.ExternalDocumentId);
        Assert.Equal(OcrGrpc.DocumentOcrJobStatus.Queued, response.Status);
    }

    [Fact]
    public async Task MissingTenantIsUnauthenticated()
    {
        var service = new DocumentOcrGrpcService(
            new FakeJobService(Guid.CreateVersion7()), new CurrentUserService());

        var exception = await Assert.ThrowsAsync<RpcException>(() => service.ListDocumentJobs(
            new OcrGrpc.ListDocumentJobsRequest(), TestServerCallContext.Create()));

        Assert.Equal(StatusCode.Unauthenticated, exception.StatusCode);
    }

    [Fact]
    public async Task InvalidExternalIdIsInvalidArgument()
    {
        var tenantId = Guid.CreateVersion7();
        var service = new DocumentOcrGrpcService(
            new FakeJobService(tenantId), CreateCurrentUser(tenantId));

        var exception = await Assert.ThrowsAsync<RpcException>(() => service.SubmitDocumentJob(
            new OcrGrpc.SubmitDocumentJobRequest { ExternalDocumentId = "invalid" },
            TestServerCallContext.Create()));

        Assert.Equal(StatusCode.InvalidArgument, exception.StatusCode);
    }

    private static CurrentUserService CreateCurrentUser(Guid tenantId)
    {
        var currentUser = new CurrentUserService();
        currentUser.Populate(Guid.CreateVersion7(), tenantId, null, null, [], []);
        return currentUser;
    }

    private sealed class FakeJobService(Guid tenantId) : IDocumentOcrJobService
    {
        public DocumentOcrJob? LastSubmittedJob { get; private set; }

        public Task<DocumentOcrJob> SubmitAsync(
            SubmitDocumentJobInput input,
            CancellationToken cancellationToken = default)
        {
            LastSubmittedJob = DocumentOcrJob.Create(
                tenantId,
                input.IdempotencyKey,
                input.StorageReference,
                input.FileName,
                input.MimeType,
                input.SizeBytes,
                input.DocumentTypeHint,
                input.ExternalDocumentId,
                input.ExternalShipmentId,
                Now);
            return Task.FromResult(LastSubmittedJob);
        }

        public Task<DocumentOcrJob> GetAsync(
            Guid jobId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(LastSubmittedJob!);

        public Task<DocumentOcrJobPage> ListAsync(
            ListDocumentJobsInput input,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new DocumentOcrJobPage([], 1, 20, 0, 0));

        public Task<DocumentOcrJob?> ProcessAsync(
            Guid tenantId,
            Guid jobId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<DocumentOcrJob?>(null);
    }
}
