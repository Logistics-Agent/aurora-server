using DocumentOcr.Application.Jobs;
using DocumentOcr.Application.Intake;
using DocumentOcr.Application.Providers;
using DocumentOcr.Application.Storage;
using DocumentOcr.Application.Uploads;
using DocumentOcr.Contracts.Events;
using DocumentOcr.Domain.Entities;
using DocumentOcr.Domain.Enums;
using DocumentOcr.GrpcServices;
using DocumentOcr.Infrastructure.Persistences;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Shared.Interceptors;
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
    public async Task CreateDocumentIntakeMapsTheDocumentOcrOwnedRequest()
    {
        var tenantId = Guid.CreateVersion7();
        var currentUser = CreateCurrentUser(tenantId);
        var fakeIntake = new FakeIntakeService(tenantId);
        var service = new DocumentOcrGrpcService(
            new FakeJobService(tenantId),
            currentUser,
            null,
            fakeIntake);
        var uploadId = Guid.CreateVersion7();

        var response = await service.CreateDocumentIntake(
            new OcrGrpc.CreateDocumentIntakeRequest
            {
                UploadId = uploadId.ToString(),
                IdempotencyKey = "intake-001",
                DocumentTypeHint = OcrGrpc.OcrDocumentType.CommercialInvoice,
                Purpose = OcrGrpc.DocumentOcrPurpose.RegulatoryCorpus,
                ExternalReference = "regulatory-source-001"
            },
            TestServerCallContext.Create());

        Assert.Equal(uploadId, fakeIntake.LastInput!.UploadId);
        Assert.Equal("intake-001", fakeIntake.LastInput.IdempotencyKey);
        Assert.Equal(DocumentOcrPurpose.RegulatoryCorpus, fakeIntake.LastInput.Purpose);
        Assert.Equal("regulatory-source-001", fakeIntake.LastInput.ExternalReference);
        Assert.Equal(fakeIntake.LastJob!.Id.ToString(), response.JobId);
    }

    [Fact]
    public async Task CreateDocumentIntakeMapsMissingUploadToNotFound()
    {
        var tenantId = Guid.CreateVersion7();
        var service = new DocumentOcrGrpcService(
            new FakeJobService(tenantId),
            CreateCurrentUser(tenantId),
            null,
            new MissingUploadIntakeService());

        var exception = await Assert.ThrowsAsync<RpcException>(() => service.CreateDocumentIntake(
            new OcrGrpc.CreateDocumentIntakeRequest
            {
                UploadId = Guid.CreateVersion7().ToString(),
                IdempotencyKey = "intake-001",
                DocumentTypeHint = OcrGrpc.OcrDocumentType.CommercialInvoice,
                Purpose = OcrGrpc.DocumentOcrPurpose.GeneralDocument
            },
            TestServerCallContext.Create()));

        Assert.Equal(StatusCode.NotFound, exception.StatusCode);
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
    public async Task ReviewRequiresOcrReviewPermission()
    {
        var tenantId = Guid.CreateVersion7();
        var currentUser = CreateCurrentUser(tenantId);
        var service = new DocumentOcrGrpcService(new FakeJobService(tenantId), currentUser);

        var exception = await Assert.ThrowsAsync<RpcException>(() => service.ReviewDocumentJob(
            new OcrGrpc.ReviewDocumentJobRequest
            {
                JobId = Guid.CreateVersion7().ToString(),
                Action = "CONFIRM"
            },
            TestServerCallContext.Create()));

        Assert.Equal(StatusCode.PermissionDenied, exception.StatusCode);
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
    public async Task CreateUploadSessionMapsExpiredReplayToFailedPreconditionWithStableTrailer()
    {
        var tenantId = Guid.CreateVersion7();
        var currentUser = CreateCurrentUser(tenantId);
        await using var context = CreateUploadContext(currentUser);
        var uploads = new DocumentUploadService(
            context,
            currentUser,
            new FixedTimeProvider(Now.AddMinutes(16)),
            new DocumentInputPolicy(new DocumentProcessingOptions()),
            new FakeDocumentInputStorage(),
            new DocumentUploadOptions());
        var receipt = await uploads.CreateAsync(
            new CreateDocumentUploadInput("expired-replay", "invoice.pdf", "application/pdf", 1_024, null));
        var session = await context.UploadSessions.SingleAsync();
        session.MarkExpired(Now.AddMinutes(16));
        await context.SaveChangesAsync();
        var service = new DocumentOcrGrpcService(new FakeJobService(tenantId), currentUser, uploads);

        var exception = await Assert.ThrowsAsync<RpcException>(() => service.CreateUploadSession(
            new OcrGrpc.CreateUploadSessionRequest
            {
                IdempotencyKey = "expired-replay",
                FileName = "invoice.pdf",
                MimeType = "application/pdf",
                SizeBytes = 1_024
            },
            TestServerCallContext.Create()));

        Assert.Equal(receipt.UploadId.ToString(), session.Id.ToString());
        Assert.Equal(StatusCode.FailedPrecondition, exception.StatusCode);
        Assert.Equal("UPLOAD_EXPIRED", exception.Trailers.GetValue("document-upload-validation-code"));
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

    private static DocumentOcrDbContext CreateUploadContext(CurrentUserService currentUser)
    {
        var options = new DbContextOptionsBuilder<DocumentOcrDbContext>()
            .UseInMemoryDatabase(Guid.CreateVersion7().ToString())
            .Options;
        return new DocumentOcrDbContext(options, currentUser, new AuditSaveChangesInterceptor(currentUser));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeDocumentInputStorage : IDocumentInputStorage
    {
        public Task<SignedWriteTarget> CreateSignedWriteTargetAsync(
            Guid tenantId, Guid uploadId, string objectKey, string fileName, string mimeType,
            long maximumSizeBytes, DateTimeOffset expiresAt, string? contentSha256,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new SignedWriteTarget(
                $"https://upload.test/{objectKey}",
                new Dictionary<string, string> { ["Content-Type"] = mimeType },
                expiresAt,
                maximumSizeBytes));

        public Task<DocumentObjectMetadata?> HeadAsync(
            Guid tenantId, string objectKey, CancellationToken cancellationToken = default) =>
            Task.FromResult<DocumentObjectMetadata?>(null);

        public Task WriteAsync(
            Guid tenantId, string objectKey, Stream content, long maximumSizeBytes,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteAsync(
            Guid tenantId, string objectKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
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

    private sealed class FakeIntakeService(Guid tenantId) : IDocumentIntakeService
    {
        public CreateDocumentIntakeInput? LastInput { get; private set; }
        public DocumentOcrJob? LastJob { get; private set; }

        public Task<DocumentOcrJob> CreateAsync(
            CreateDocumentIntakeInput input,
            CancellationToken cancellationToken = default)
        {
            LastInput = input;
            LastJob = DocumentOcrJob.Create(
                tenantId,
                input.IdempotencyKey,
                $"objects/{tenantId}/{input.UploadId}/invoice.pdf",
                "invoice.pdf",
                "application/pdf",
                1_024,
                input.DocumentTypeHint,
                input.UploadId,
                null,
                Now,
                OcrExtractionMode.Structured,
                input.ExternalReference,
                input.Purpose,
                input.InitiatingCorrelationId,
                input.UploadId);
            return Task.FromResult(LastJob);
        }
    }

    private sealed class MissingUploadIntakeService : IDocumentIntakeService
    {
        public Task<DocumentOcrJob> CreateAsync(
            CreateDocumentIntakeInput input,
            CancellationToken cancellationToken = default) =>
            throw new Shared.Exceptions.NotFoundException("Document upload session was not found.");
    }
}
