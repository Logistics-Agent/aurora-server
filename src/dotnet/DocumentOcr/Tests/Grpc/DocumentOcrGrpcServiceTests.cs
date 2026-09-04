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

    [Fact]
    public async Task GetMapsPersistedJobFields()
    {
        var tenantId = Guid.CreateVersion7();
        var fake = new FakeJobService(tenantId);
        var job = fake.SeedJob("get-job");
        var service = new DocumentOcrGrpcService(fake, CreateCurrentUser(tenantId));

        var response = await service.GetDocumentJob(
            new OcrGrpc.GetDocumentJobRequest { JobId = job.Id.ToString() },
            TestServerCallContext.Create());

        Assert.Equal(job.Id.ToString(), response.JobId);
        Assert.Equal(job.ExternalDocumentId.ToString(), response.ExternalDocumentId);
        Assert.Equal(OcrGrpc.DocumentOcrJobStatus.Queued, response.Status);
        Assert.Equal(Now.UtcDateTime, response.CreatedAt.ToDateTime());
    }

    [Fact]
    public async Task ListMapsFiltersAndPaginationMetadata()
    {
        var tenantId = Guid.CreateVersion7();
        var fake = new FakeJobService(tenantId);
        var job = fake.SeedJob("list-job");
        var service = new DocumentOcrGrpcService(fake, CreateCurrentUser(tenantId));

        var response = await service.ListDocumentJobs(
            new OcrGrpc.ListDocumentJobsRequest
            {
                Page = 2,
                PageSize = 10,
                Status = OcrGrpc.DocumentOcrJobStatus.Queued,
                ExternalDocumentId = job.ExternalDocumentId.ToString()
            },
            TestServerCallContext.Create());

        Assert.Equal(2, fake.LastListInput!.Page);
        Assert.Equal(10, fake.LastListInput.PageSize);
        Assert.Equal(DocumentOcrJobStatus.Queued, fake.LastListInput.Status);
        Assert.Equal(job.ExternalDocumentId, fake.LastListInput.ExternalDocumentId);
        Assert.Single(response.Jobs);
        Assert.Equal(1, response.TotalItems);
        Assert.Equal(2, response.Page);
    }

    private static CurrentUserService CreateCurrentUser(Guid tenantId)
    {
        var currentUser = new CurrentUserService();
        currentUser.Populate(Guid.CreateVersion7(), tenantId, null, null, null, []);
        return currentUser;
    }

    private sealed class FakeJobService(Guid tenantId) : IDocumentOcrJobService
    {
        public DocumentOcrJob? LastSubmittedJob { get; private set; }
        public ListDocumentJobsInput? LastListInput { get; private set; }

        public DocumentOcrJob SeedJob(string key)
        {
            LastSubmittedJob = DocumentOcrJob.Create(
                tenantId,
                key,
                "objects/tenant/invoice.pdf",
                "invoice.pdf",
                "application/pdf",
                1_024,
                OcrDocumentType.CommercialInvoice,
                Guid.CreateVersion7(),
                null,
                Now);
            return LastSubmittedJob;
        }

        public Task<DocumentOcrJob> SubmitAsync(
            SubmitDocumentJobInput input,
            CancellationToken cancellationToken = default) =>
            SubmitOcrAsync(
                new SubmitOcrJobInput(
                    input.IdempotencyKey,
                    input.StorageReference,
                    input.FileName,
                    input.MimeType,
                    input.SizeBytes,
                    input.DocumentTypeHint,
                    OcrExtractionMode.Structured,
                    input.ExternalDocumentId,
                    null,
                    input.ExternalShipmentId),
                cancellationToken);

        public Task<DocumentOcrJob> SubmitOcrAsync(
            SubmitOcrJobInput input,
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
                Now,
                input.ExtractionMode,
                input.ExternalContextId);
            return Task.FromResult(LastSubmittedJob);
        }

        public Task<DocumentOcrJob> GetAsync(
            Guid jobId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(LastSubmittedJob!);

        public Task<DocumentOcrJobPage> ListAsync(
            ListDocumentJobsInput input,
            CancellationToken cancellationToken = default)
        {
            LastListInput = input;
            var items = LastSubmittedJob is null ? [] : new[] { LastSubmittedJob };
            return Task.FromResult(new DocumentOcrJobPage(items, input.Page, input.PageSize, items.Length, 1));
        }

        public Task<DocumentOcrJob?> ProcessAsync(
            Guid tenantId,
            Guid jobId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<DocumentOcrJob?>(null);

        public Task<DocumentOcrJob> CancelAsync(
            Guid jobId,
            CancellationToken cancellationToken = default)
        {
            LastSubmittedJob?.Cancel(Now);
            return Task.FromResult(LastSubmittedJob!);
        }

        public Task<DocumentOcrJob> RetryAsync(
            Guid jobId,
            CancellationToken cancellationToken = default)
        {
            LastSubmittedJob?.ScheduleRetry(Now, Now);
            return Task.FromResult(LastSubmittedJob!);
        }

        public Task<DocumentOcrJob> ReviewAsync(
            Guid jobId,
            string action,
            string? correctedJson,
            string? comment,
            CancellationToken cancellationToken = default)
        {
            LastSubmittedJob?.ApplyReview(action, correctedJson, comment, null, Now);
            return Task.FromResult(LastSubmittedJob!);
        }
    }
}
