using DocumentOcr.Application.Providers;
using DocumentOcr.Application.Storage;
using DocumentOcr.Application.Uploads;
using DocumentOcr.Domain.Entities;
using DocumentOcr.Infrastructure.Persistences;
using DocumentOcr.Infrastructure.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Shared.Interceptors;
using Shared.Security;

namespace DocumentOcr.Tests;

public sealed class DocumentUploadHttpBridgeTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 11, 7, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task PutRejectsAnInvalidTokenWithoutWritingBytes()
    {
        await using var fixture = await CreateFixtureAsync();

        var statusCode = await fixture.PutAsync("not-a-valid-token");

        Assert.Equal(StatusCodes.Status403Forbidden, statusCode);
        Assert.Empty(fixture.Storage.WrittenBytes);
    }

    [Fact]
    public async Task PutRejectsAnExpiredTokenWithoutWritingBytes()
    {
        await using var fixture = await CreateFixtureAsync();
        var token = fixture.TokenService.CreateToken(
            fixture.TenantId,
            fixture.UploadId,
            fixture.ObjectKey,
            Now.AddSeconds(-1));

        var statusCode = await fixture.PutAsync(token);

        Assert.Equal(StatusCodes.Status410Gone, statusCode);
        Assert.Empty(fixture.Storage.WrittenBytes);
    }

    [Fact]
    public async Task PutRejectsATokenBoundToAnotherTenantOrUpload()
    {
        await using var fixture = await CreateFixtureAsync();
        var token = fixture.TokenService.CreateToken(
            fixture.TenantId,
            fixture.UploadId,
            fixture.ObjectKey,
            Now.AddMinutes(1));

        var statusCode = await fixture.PutAsync(token, Guid.CreateVersion7(), Guid.CreateVersion7());

        Assert.Equal(StatusCodes.Status403Forbidden, statusCode);
        Assert.Empty(fixture.Storage.WrittenBytes);
    }

    [Fact]
    public async Task PutRejectsAnOversizeRequestBeforeWritingBytes()
    {
        await using var fixture = await CreateFixtureAsync();
        var token = fixture.CreateValidToken();

        var statusCode = await fixture.PutAsync(token, bytes: new byte[5]);

        Assert.Equal(StatusCodes.Status413PayloadTooLarge, statusCode);
        Assert.Empty(fixture.Storage.WrittenBytes);
    }

    [Fact]
    public async Task PutStreamsAValidatedRequestToTheTenantBoundObject()
    {
        await using var fixture = await CreateFixtureAsync();
        var token = fixture.CreateValidToken();
        var bytes = new byte[] { 0x25, 0x50, 0x44, 0x46 };

        var statusCode = await fixture.PutAsync(token, bytes: bytes);

        Assert.Equal(StatusCodes.Status204NoContent, statusCode);
        Assert.Equal(bytes, Assert.Single(fixture.Storage.WrittenBytes));
        Assert.Equal(fixture.ObjectKey, fixture.Storage.WrittenObjectKey);
    }

    private static async Task<BridgeFixture> CreateFixtureAsync()
    {
        var tenantId = Guid.CreateVersion7();
        var uploadId = Guid.CreateVersion7();
        var currentUser = new CurrentUserService();
        currentUser.Populate(Guid.CreateVersion7(), tenantId, null, null, null, []);
        var options = new DbContextOptionsBuilder<DocumentOcrDbContext>()
            .UseInMemoryDatabase(Guid.CreateVersion7().ToString())
            .Options;
        var context = new DocumentOcrDbContext(options, currentUser, new AuditSaveChangesInterceptor(currentUser));
        var objectKey = $"objects/{tenantId}/{uploadId}/invoice.pdf";
        context.UploadSessions.Add(DocumentUploadSession.Create(
            tenantId,
            "bridge-test",
            new string('a', 64),
            uploadId,
            objectKey,
            "invoice.pdf",
            "application/pdf",
            4,
            null,
            4,
            Now.AddMinutes(15),
            Now));
        await context.SaveChangesAsync();

        var tokenService = new DocumentUploadBridgeTokenService(
            new DocumentUploadBridgeOptions("https://ocr.test", "test-only-signing-key-with-at-least-32-bytes"));
        return new BridgeFixture(context, tenantId, uploadId, objectKey, tokenService);
    }

    private sealed class BridgeFixture(
        DocumentOcrDbContext context,
        Guid tenantId,
        Guid uploadId,
        string objectKey,
        DocumentUploadBridgeTokenService tokenService) : IAsyncDisposable
    {
        public DocumentOcrDbContext Context { get; } = context;
        public Guid TenantId { get; } = tenantId;
        public Guid UploadId { get; } = uploadId;
        public string ObjectKey { get; } = objectKey;
        public DocumentUploadBridgeTokenService TokenService { get; } = tokenService;
        public FakeDocumentInputStorage Storage { get; } = new();

        public string CreateValidToken() => TokenService.CreateToken(
            TenantId,
            UploadId,
            ObjectKey,
            Now.AddMinutes(1));

        public async Task<int> PutAsync(
            string token,
            Guid? tenantId = null,
            Guid? uploadId = null,
            byte[]? bytes = null)
        {
            var requestContext = new DefaultHttpContext();
            var requestBytes = bytes ?? new byte[] { 0x25, 0x50, 0x44, 0x46 };
            requestContext.Request.ContentType = "application/pdf";
            requestContext.Request.ContentLength = requestBytes.Length;
            requestContext.Request.Body = new MemoryStream(requestBytes);

            var bridge = new DocumentUploadHttpBridge(
                Context,
                Storage,
                TokenService,
                new FixedTimeProvider(Now));
            var result = await bridge.PutAsync(
                tenantId ?? TenantId,
                uploadId ?? UploadId,
                token,
                requestContext.Request,
                CancellationToken.None);
            return Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode
                ?? StatusCodes.Status200OK;
        }

        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeDocumentInputStorage : IDocumentInputStorage
    {
        public List<byte[]> WrittenBytes { get; } = [];
        public string? WrittenObjectKey { get; private set; }

        public Task<SignedWriteTarget> CreateSignedWriteTargetAsync(
            Guid tenantId,
            Guid uploadId,
            string objectKey,
            string fileName,
            string mimeType,
            long maximumSizeBytes,
            DateTimeOffset expiresAt,
            string? contentSha256,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<DocumentObjectMetadata?> HeadAsync(
            Guid tenantId,
            string objectKey,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task DeleteAsync(
            Guid tenantId,
            string objectKey,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public async Task WriteAsync(
            Guid tenantId,
            string objectKey,
            Stream content,
            long maximumSizeBytes,
            CancellationToken cancellationToken = default)
        {
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            WrittenBytes.Add(buffer.ToArray());
            WrittenObjectKey = objectKey;
        }
    }
}
