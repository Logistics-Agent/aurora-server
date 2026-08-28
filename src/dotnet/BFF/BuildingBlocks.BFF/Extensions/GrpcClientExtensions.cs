using Grpc.Net.ClientFactory;
using Shared.Interceptors;

namespace BuildingBlocks.BFF.Extensions;

public static class GrpcClientExtensions
{
    /// <summary>
    /// Đăng ký tất cả gRPC clients với Resilience Pipelines (retry + circuit breaker).
    /// - IamTenant (IamService + AuthService): bắt buộc — Grpc:IamTenant:Url
    /// - RoutePlanningAgent: optional theo config — Grpc:RoutePlanning:Url (System.Bff không cần)
    /// ClientMetadataInterceptor forward x-user-id/x-tenant-id/x-role/x-permission-version
    /// xuống gRPC service (AuthInterceptor phía server đọc lại các metadata này).
    /// ⚠ InterceptorScope.Client bắt buộc — interceptor phụ thuộc scoped ICurrentUserService.
    /// </summary>
    public static IServiceCollection AddBffGrpcClients(
        this IServiceCollection services,
        IConfiguration config)
    {
        services.AddScoped<ClientMetadataInterceptor>();

        var iamUrl = config["Grpc:IamTenant:Url"]
            ?? throw new InvalidOperationException("Grpc:IamTenant:Url is required");

        services.AddGrpcClient<IamTenant.Grpc.IamService.IamServiceClient>(o => o.Address = new Uri(iamUrl))
            .AddInterceptor<ClientMetadataInterceptor>(InterceptorScope.Client)
            .AddStandardResilienceHandler(ConfigureIamResilience);

        services.AddGrpcClient<Auth.Grpc.AuthService.AuthServiceClient>(o => o.Address = new Uri(iamUrl))
            .AddInterceptor<ClientMetadataInterceptor>(InterceptorScope.Client)
            .AddStandardResilienceHandler(ConfigureIamResilience);

        var routePlanningUrl = config["Grpc:RoutePlanning:Url"];
        if (!string.IsNullOrWhiteSpace(routePlanningUrl))
        {
            services.AddGrpcClient<RoutePlanningAgent.Grpc.RoutePlanningService.RoutePlanningServiceClient>(
                    o => o.Address = new Uri(routePlanningUrl))
                .AddInterceptor<ClientMetadataInterceptor>(InterceptorScope.Client)
                .AddStandardResilienceHandler(ConfigureBusinessResilience);
        }

        var documentOcrUrl = config["Grpc:DocumentOcr:Url"] ?? "http://localhost:5005";
        services.AddGrpcClient<DocumentOcr.Grpc.DocumentOcrService.DocumentOcrServiceClient>(
                o => o.Address = new Uri(documentOcrUrl))
            .AddInterceptor<ClientMetadataInterceptor>(InterceptorScope.Client)
            .AddStandardResilienceHandler(ConfigureBusinessResilience);

        var regulatoryUrl = config["Grpc:RegulatoryCompliance:Url"] ?? "http://localhost:5006";
        services.AddGrpcClient<RegulatoryCompliance.Grpc.RegulatoryComplianceService.RegulatoryComplianceServiceClient>(
                o => o.Address = new Uri(regulatoryUrl))
            .AddInterceptor<ClientMetadataInterceptor>(InterceptorScope.Client)
            .AddStandardResilienceHandler(ConfigureBusinessResilience);

        var mailServiceUrl = config["Grpc:MailService:Url"] ?? "http://localhost:5003";
        services.AddGrpcClient<MailService.GrpcServices.MailManagement.MailManagementClient>(o =>
            {
                o.Address = new Uri(mailServiceUrl);
                o.ChannelOptionsActions.Add(opt =>
                {
                    opt.MaxReceiveMessageSize = (int)BuildingBlocks.BFF.Mail.Models.MailLimits.MaxGrpcMessageBytes;
                    opt.MaxSendMessageSize = (int)BuildingBlocks.BFF.Mail.Models.MailLimits.MaxGrpcMessageBytes;
                });
            })
            .AddInterceptor<ClientMetadataInterceptor>(InterceptorScope.Client)
            .AddStandardResilienceHandler(ConfigureBusinessResilience);

        services.AddGrpcClient<MailService.GrpcServices.MailSecurity.MailSecurityClient>(o =>
            {
                o.Address = new Uri(mailServiceUrl);
                o.ChannelOptionsActions.Add(opt =>
                {
                    opt.MaxReceiveMessageSize = (int)BuildingBlocks.BFF.Mail.Models.MailLimits.MaxGrpcMessageBytes;
                    opt.MaxSendMessageSize = (int)BuildingBlocks.BFF.Mail.Models.MailLimits.MaxGrpcMessageBytes;
                });
            })
            .AddInterceptor<ClientMetadataInterceptor>(InterceptorScope.Client)
            .AddStandardResilienceHandler(ConfigureBusinessResilience);

        services.AddScoped<BuildingBlocks.BFF.Mail.Clients.IMailServiceClient, BuildingBlocks.BFF.Mail.Clients.GrpcMailServiceClient>();

        var shipmentUrl = config["Grpc:ShipmentWorkflow:Url"] ?? "http://localhost:5001";
        services.AddGrpcClient<ShipmentWorkflow.Grpc.ShipmentWorkflowService.ShipmentWorkflowServiceClient>(
                o => o.Address = new Uri(shipmentUrl))
            .AddInterceptor<ClientMetadataInterceptor>(InterceptorScope.Client)
            .AddStandardResilienceHandler(ConfigureBusinessResilience);

        var gpsUrl = config["Grpc:GpsTracking:Url"] ?? "http://localhost:5004";
        services.AddGrpcClient<GpsTracking.Grpc.GpsTrackingService.GpsTrackingServiceClient>(
                o => o.Address = new Uri(gpsUrl))
            .AddInterceptor<ClientMetadataInterceptor>(InterceptorScope.Client)
            .AddStandardResilienceHandler(ConfigureBusinessResilience);

        var notificationUrl = config["Grpc:Notification:Url"] ?? "http://localhost:5008";
        services.AddGrpcClient<Notification.Grpc.NotificationService.NotificationServiceClient>(
                o => o.Address = new Uri(notificationUrl))
            .AddInterceptor<ClientMetadataInterceptor>(InterceptorScope.Client)
            .AddStandardResilienceHandler(ConfigureBusinessResilience);

        var billingUrl = config["Grpc:BillingService:Url"] ?? "http://localhost:5009";
        services.AddGrpcClient<BillingService.Grpc.BillingService.BillingServiceClient>(
                o => o.Address = new Uri(billingUrl))
            .AddInterceptor<ClientMetadataInterceptor>(InterceptorScope.Client)
            .AddStandardResilienceHandler(ConfigureBusinessResilience);

        var financialUrl = config["Grpc:FinancialService:Url"] ?? "http://localhost:5010";
        services.AddGrpcClient<FinancialService.Grpc.FinancialService.FinancialServiceClient>(
                o => o.Address = new Uri(financialUrl))
            .AddInterceptor<ClientMetadataInterceptor>(InterceptorScope.Client)
            .AddStandardResilienceHandler(ConfigureBusinessResilience);

        var negotiationUrl = config["Grpc:NegotiationService:Url"] ?? "http://localhost:5006";
        services.AddGrpcClient<Negotiation.Grpc.NegotiationService.NegotiationServiceClient>(
                o => o.Address = new Uri(negotiationUrl))
            .AddInterceptor<ClientMetadataInterceptor>(InterceptorScope.Client)
            .AddStandardResilienceHandler(ConfigureBusinessResilience);

        return services;
    }

    // ── Resilience profiles ──────────────────────────────────────────────────

    /// <summary>IAM: retry nhanh hơn (auth critical path).</summary>
    private static void ConfigureIamResilience(
        Microsoft.Extensions.Http.Resilience.HttpStandardResilienceOptions r)
    {
        r.Retry.MaxRetryAttempts = 2;
        r.Retry.Delay = TimeSpan.FromMilliseconds(200);
        r.Retry.UseJitter = true;
        r.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
        r.CircuitBreaker.FailureRatio = 0.5;
        r.CircuitBreaker.MinimumThroughput = 5;
        r.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);
        r.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(12);
    }

    /// <summary>Business services: timeout rộng hơn cho heavy operations (optimize/LLM).</summary>
    private static void ConfigureBusinessResilience(
        Microsoft.Extensions.Http.Resilience.HttpStandardResilienceOptions r)
    {
        r.Retry.MaxRetryAttempts = 2;
        r.Retry.Delay = TimeSpan.FromMilliseconds(200);
        r.Retry.UseJitter = true;
        r.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
        r.CircuitBreaker.FailureRatio = 0.5;
        r.CircuitBreaker.MinimumThroughput = 5;
        r.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);
        r.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(60);
    }
}
