using DocumentOcr.Application.Uploads;
using Microsoft.Extensions.Configuration;

namespace DocumentOcr.Tests;

public sealed class DocumentInputStorageConfigurationTests
{
    [Fact]
    public void UnknownInputProviderFailsFast()
    {
        var configuration = CreateConfiguration(("Storage:InputProvider", "AzureBlob"));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            DocumentInputStorageConfiguration.GetProvider(configuration));

        Assert.Contains("Storage:InputProvider", exception.Message);
    }

    [Fact]
    public void S3ConfigurationRequiresNonEmptyBucketBeforeApplicationBuild()
    {
        var configuration = CreateConfiguration(
            ("Storage:S3:Bucket", " "),
            ("Storage:S3:ServiceUrl", "https://s3.example.test"),
            ("Storage:S3:Region", "us-east-1"),
            ("Storage:S3:AccessKey", "access"),
            ("Storage:S3:SecretKey", "secret"));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            DocumentInputStorageConfiguration.GetS3Settings(configuration));

        Assert.Equal("Storage:S3:Bucket is required for S3 input storage.", exception.Message);
    }

    private static IConfiguration CreateConfiguration(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(value => value.Key, value => (string?)value.Value))
            .Build();
}
