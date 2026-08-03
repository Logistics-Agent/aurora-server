namespace BuildingBlocks.BFF.Options;

/// <summary>
/// Cấu hình Cognito Authentication (Hosted UI) cho BFF.
/// Bind từ section "Auth:Cognito" trong appsettings / environment variables.
/// </summary>
public class CognitoAuthOptions
{
    public const string SectionName = "Auth:Cognito";

    /// <summary>AWS Region, VD: "ap-southeast-1"</summary>
    public string Region { get; set; } = "ap-southeast-1";

    /// <summary>Cognito User Pool ID, VD: "ap-southeast-1_XXXXXXXXX"</summary>
    public string UserPoolId { get; set; } = string.Empty;

    /// <summary>App Client ID đã đăng ký trên Cognito.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>App Client Secret.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Cognito Hosted UI domain prefix hoặc full URL.
    /// VD: "aurora-auth" → https://aurora-auth.auth.ap-southeast-1.amazoncognito.com
    /// </summary>
    public string Domain { get; set; } = string.Empty;

    /// <summary>
    /// Base URL của ứng dụng BFF cho callback / logout redirects.
    /// VD: "https://localhost:8443"
    /// </summary>
    public string AppDomain { get; set; } = "https://localhost:8443";

    /// <summary>OIDC Authority URL — Cognito OIDC discovery endpoint.</summary>
    public string Authority =>
        $"https://cognito-idp.{Region}.amazonaws.com/{UserPoolId}";

    /// <summary>Cognito Hosted UI base URL cho login/logout redirects.</summary>
    public string CognitoDomainUrl => Domain.StartsWith("http", StringComparison.OrdinalIgnoreCase)
        ? Domain
        : $"https://{Domain}.auth.{Region}.amazoncognito.com";

    /// <summary>Token endpoint cho refresh token và code exchange.</summary>
    public string TokenEndpoint => $"{CognitoDomainUrl}/oauth2/token";

    /// <summary>Logout endpoint.</summary>
    public string LogoutEndpoint => $"{CognitoDomainUrl}/logout";
}
