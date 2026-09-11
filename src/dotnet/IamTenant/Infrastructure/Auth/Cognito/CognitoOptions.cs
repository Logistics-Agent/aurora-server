namespace IamTenant.Infrastructure.Auth.Cognito;

public class CognitoOptions
{
    public string UserPoolId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;

    public List<string> CallbackUrls { get; set; } =
    [
        "http://localhost:3000/callback",
        "https://admin.humanak.cyou/callback",
        "https://api.humanak.cyou/api/v1/auth/callback",
        "https://humanak.cyou/callback",
        "https://system.humanak.cyou/callback"
    ];

    public List<string> LogoutUrls { get; set; } =
    [
        "http://127.0.0.1:3000/",
        "https://admin.humanak.cyou/",
        "https://humanak.cyou/",
        "https://system.humanak.cyou/"
    ];
}
