using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using DocumentOcr.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;

namespace DocumentOcr.Tests;

public sealed class S3ArtifactStorageServiceTests
{
    [Fact]
    public async Task StoreArtifactDeclaresContentLengthForR2Compatibility()
    {
        var client = new CapturingAmazonS3();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:S3:Bucket"] = "aurora-document-ocr"
            })
            .Build();
        var service = new S3ArtifactStorageService(client, configuration);
        var tenantId = Guid.CreateVersion7();
        var jobId = Guid.CreateVersion7();
        var data = new byte[] { 1, 2, 3, 4 };

        await service.StoreArtifactAsync(tenantId, jobId, "result.json", "application/json", data);

        Assert.NotNull(client.LastPutObjectRequest);
        Assert.False(client.LastPutObjectRequest!.UseChunkEncoding);
        Assert.True(client.LastPutObjectRequest.DisablePayloadSigning);
        Assert.Equal(data.Length, client.LastPutObjectRequest.InputStream!.Length);
        Assert.Equal($"tenants/{tenantId}/documents/{jobId}/artifacts/result.json", client.LastPutObjectRequest.Key);
    }

    private sealed class CapturingAmazonS3 : AmazonS3Client
    {
        public CapturingAmazonS3() : base(
            new AnonymousAWSCredentials(),
            new AmazonS3Config
            {
                ServiceURL = "https://r2.test",
                AuthenticationRegion = "auto"
            })
        {
        }

        public PutObjectRequest? LastPutObjectRequest { get; private set; }

        public override Task<PutObjectResponse> PutObjectAsync(
            PutObjectRequest request,
            CancellationToken cancellationToken = default)
        {
            LastPutObjectRequest = request;
            return Task.FromResult(new PutObjectResponse());
        }
    }
}
