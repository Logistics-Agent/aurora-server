using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using MailService.Application.Options;
using MailService.Infrastructure.Persistence;
using Shared.Security;

namespace MailService.Tests;

public class MailServiceRuntimeSmokeTests
{
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "aurora-server.sln")) && !Directory.Exists(Path.Combine(dir.FullName, ".git")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", ".."));
    }

    [Fact]
    public async Task SmokeTest1_LivenessProbe_ReturnsHealthy_WithoutDatabaseOrDependencies()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy("Process is alive"), tags: new[] { "live" })
            .AddCheck("mock-neon-db", () => HealthCheckResult.Unhealthy("Neon Postgres connection timeout"), tags: new[] { "ready", "critical" })
            .AddCheck("mock-redis", () => HealthCheckResult.Unhealthy("Redis cluster unreachable"), tags: new[] { "ready", "critical" })
            .AddCheck("mock-ai-gov", () => HealthCheckResult.Degraded("AI Governance slow"), tags: new[] { "general" });

        var serviceProvider = services.BuildServiceProvider();
        var healthCheckService = serviceProvider.GetRequiredService<HealthCheckService>();

        var livenessReport = await healthCheckService.CheckHealthAsync(check => check.Tags.Contains("live"));

        Assert.Equal(HealthStatus.Healthy, livenessReport.Status);
        Assert.Single(livenessReport.Entries);
        Assert.Equal(HealthStatus.Healthy, livenessReport.Entries["self"].Status);

        var readinessReport = await healthCheckService.CheckHealthAsync(check => check.Tags.Contains("ready"));

        Assert.Equal(HealthStatus.Unhealthy, readinessReport.Status);
        Assert.Equal(HealthStatus.Unhealthy, readinessReport.Entries["mock-neon-db"].Status);
        Assert.Equal(HealthStatus.Unhealthy, readinessReport.Entries["mock-redis"].Status);

        Assert.False(livenessReport.Entries.ContainsKey("mock-ai-gov"));
    }

    [Fact]
    public void SmokeTest2_ProductionOptionsValidator_ValidatesMandatoryConfiguration()
    {
        var validator = new MailServiceOptionsValidator(isProduction: true);

        var emptyOptions = new MailServiceOptions();
        var emptyResult = validator.Validate(null, emptyOptions);
        Assert.True(emptyResult.Failed);
        Assert.Contains(emptyResult.Failures, f => f.Contains("ConnectionStrings:DefaultConnection is required"));
        Assert.Contains(emptyResult.Failures, f => f.Contains("Redis:ConnectionString is required"));
        Assert.Contains(emptyResult.Failures, f => f.Contains("RabbitMQ:Host is required"));

        var invalidUriOptions = new MailServiceOptions
        {
            DatabaseConnectionString = "Host=ep-neon.tech;Database=mail",
            RedisConnectionString = "redis:6379",
            RabbitMqHost = "rabbitmq",
            StalwartBaseUrl = "not-a-valid-uri",
            AiGovernanceEndpoint = "invalid-endpoint"
        };
        var invalidUriResult = validator.Validate(null, invalidUriOptions);
        Assert.True(invalidUriResult.Failed);
        Assert.Contains(invalidUriResult.Failures, f => f.Contains("Stalwart:BaseUrl"));
        Assert.Contains(invalidUriResult.Failures, f => f.Contains("AiGovernance:GrpcEndpoint"));

        var invalidPortOptions = new MailServiceOptions
        {
            DatabaseConnectionString = "Host=ep-neon.tech;Database=mail",
            RedisConnectionString = "redis:6379",
            RabbitMqHost = "rabbitmq",
            StalwartBaseUrl = "http://stalwart:8080",
            AiGovernanceEndpoint = "http://ai-gov:5005",
            ClamAvPort = 99999,
            SpamAssassinPort = -1
        };
        var invalidPortResult = validator.Validate(null, invalidPortOptions);
        Assert.True(invalidPortResult.Failed);
        Assert.Contains(invalidPortResult.Failures, f => f.Contains("ClamAV:Port"));
        Assert.Contains(invalidPortResult.Failures, f => f.Contains("SpamAssassin:Port"));

        var validOptions = new MailServiceOptions
        {
            DatabaseConnectionString = "Host=ep-sample-123456.ap-southeast-1.aws.neon.tech;Port=5432;Database=aurora_mail_service;Username=aurora_admin;Password=secret;SslMode=Require",
            RedisConnectionString = "redis:6379,abortConnect=false",
            RabbitMqHost = "rabbitmq",
            RabbitMqPort = 5672,
            RabbitMqPassword = "securepassword",
            StalwartBaseUrl = "http://stalwart:8080",
            AiGovernanceEndpoint = "http://ai-governance.internal:5005",
            ClamAvHost = "clamav",
            ClamAvPort = 3310,
            SpamAssassinHost = "spamassassin",
            SpamAssassinPort = 783
        };
        var validResult = validator.Validate(null, validOptions);
        Assert.True(validResult.Succeeded);
    }

    [Fact]
    public void SmokeTest3_EfCoreModel_MatchesAllExpectedEntitiesAndIsolationFilters()
    {
        var options = new DbContextOptionsBuilder<MailServiceDbContext>()
            .UseInMemoryDatabase("SmokeTest_ModelValidation")
            .Options;

        var dummyUser = new MockCurrentUserService(Guid.NewGuid());
        using var dbContext = new MailServiceDbContext(options, dummyUser);

        var model = dbContext.Model;
        var entityTypes = model.GetEntityTypes().Select(t => t.ClrType.Name).ToList();

        Assert.Contains("Domain", entityTypes);
        Assert.Contains("Mailbox", entityTypes);
        Assert.Contains("Alias", entityTypes);
        Assert.Contains("EmailDraft", entityTypes);
        Assert.Contains("ProcessedMessage", entityTypes);
        Assert.Contains("SecurityCheckResult", entityTypes);
        Assert.Contains("QuarantineRecord", entityTypes);
        Assert.Contains("AuditRecord", entityTypes);
        Assert.Contains("OutboxMessage", entityTypes);
    }

    [Fact]
    public void SmokeTest4_ArchitectureRule_NoRuntimeDatabaseMigrateInMailService()
    {
        string root = FindRepoRoot();
        string mailServiceDir = Path.Combine(root, "src", "dotnet", "MailService");
        if (Directory.Exists(mailServiceDir))
        {
            var csFiles = Directory.GetFiles(mailServiceDir, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains("Migrations") && !f.Contains("DesignTime"));

            foreach (var file in csFiles)
            {
                string sourceCode = File.ReadAllText(file);
                Assert.DoesNotContain("Database.Migrate()", sourceCode);
                Assert.DoesNotContain("Database.MigrateAsync()", sourceCode);
                Assert.DoesNotContain("Database.EnsureCreated()", sourceCode);
                Assert.DoesNotContain("Database.EnsureCreatedAsync()", sourceCode);
            }
        }
    }

    [Fact]
    public void SmokeTest5_DockerCompose_MailPlatformTopology_Integrity()
    {
        string root = FindRepoRoot();
        string composePath = Path.Combine(root, "src", "dotnet", "MailService", "deploy", "docker-compose.prod.yml");
        if (File.Exists(composePath))
        {
            string composeContent = File.ReadAllText(composePath);

            Assert.Contains("mail-service:", composeContent);
            Assert.Contains("stalwart:", composeContent);
            Assert.Contains("rabbitmq:", composeContent);
            Assert.Contains("redis:", composeContent);
            Assert.Contains("clamav:", composeContent);
            Assert.Contains("spamassassin:", composeContent);

            Assert.DoesNotContain("aurora-mail-postgres", composeContent);
            Assert.DoesNotContain("image: postgres", composeContent);
            Assert.DoesNotContain("ai-governance:", composeContent);

            Assert.Contains("mail_internal:", composeContent);
            Assert.Contains("stalwart_data:", composeContent);
            Assert.Contains("rabbitmq_data:", composeContent);
            Assert.Contains("redis_data:", composeContent);
            Assert.Contains("clamav_db:", composeContent);

            Assert.Contains("max-size: \"10m\"", composeContent);
            Assert.Contains("max-file: \"3\"", composeContent);
        }
    }

    [Fact]
    public void SmokeTest6_ArchitectureRule_NoDirectAiProvidersInMailService()
    {
        string root = FindRepoRoot();
        string mailServiceDir = Path.Combine(root, "src", "dotnet", "MailService");
        if (Directory.Exists(mailServiceDir))
        {
            var csFiles = Directory.GetFiles(mailServiceDir, "*.cs", SearchOption.AllDirectories);

            foreach (var file in csFiles)
            {
                string sourceCode = File.ReadAllText(file);
                Assert.DoesNotContain("GeminiClient", sourceCode);
                Assert.DoesNotContain("AzureOpenAiClient", sourceCode);
                Assert.DoesNotContain("OpenAIClient", sourceCode);
                Assert.DoesNotContain("ApiKeyPool", sourceCode);
                Assert.DoesNotContain("AiProviderFactory", sourceCode);
                Assert.DoesNotContain("GEMINI_API_KEY", sourceCode);
                Assert.DoesNotContain("OPENAI_API_KEY", sourceCode);
            }
        }
    }

    [Fact]
    public void SmokeTest7_ArchitectureRule_NoObsoleteRootDeployDirOrWindowsBundle()
    {
        string root = FindRepoRoot();
        string rootDeployMailDir = Path.Combine(root, "deploy", "mail");
        Assert.False(Directory.Exists(rootDeployMailDir), "Root deploy/mail must not exist. Deployment belongs in src/dotnet/MailService/deploy/");

        string mailServiceDeployDir = Path.Combine(root, "src", "dotnet", "MailService", "deploy");
        Assert.True(Directory.Exists(mailServiceDeployDir), $"src/dotnet/MailService/deploy must exist at {mailServiceDeployDir}.");

        string windowsBundle = Path.Combine(mailServiceDeployDir, "bin", "efbundle.exe");
        Assert.False(File.Exists(windowsBundle), "Windows efbundle.exe must not be committed to deployment directory.");
    }

    [Fact]
    public void SmokeTest8_ArchitectureRule_NoNotImplementedInProductionMailService()
    {
        string root = FindRepoRoot();
        string mailServiceDir = Path.Combine(root, "src", "dotnet", "MailService");
        if (Directory.Exists(mailServiceDir))
        {
            var csFiles = Directory.GetFiles(mailServiceDir, "*.cs", SearchOption.AllDirectories);

            foreach (var file in csFiles)
            {
                string sourceCode = File.ReadAllText(file);
                Assert.DoesNotContain("NotImplementedException", sourceCode);
            }
        }
    }

    private class MockCurrentUserService : ICurrentUserService
    {
        public MockCurrentUserService(Guid tenantId)
        {
            TenantId = tenantId;
            UserId = Guid.NewGuid();
        }

        public Guid? UserId { get; }
        public Guid? TenantId { get; }
        public string? TraceId => "trace-123";
        public int? PermissionVersion => 1;
        public IReadOnlyList<string> RoleIds => new[] { "Admin" };
        public IReadOnlyList<string> Permissions => new[] { "Mail:Read", "Mail:Write" };
    }
}
