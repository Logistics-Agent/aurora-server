using System.Security.Cryptography;
using System.Text;
using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using IamTenant.Infrastructure.Auth.Cognito;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace IamTenant.Tests.Infrastructure.Auth.Cognito;

public class CognitoAuthServiceTests
{
    [Fact]
    public async Task RefreshTokenAsync_AddsSecretHash_WhenClientHasSecret()
    {
        const string clientId = "client-id";
        const string clientSecret = "client-secret";
        const string refreshSubject = "cognito-subject";
        const string refreshToken = "refresh-token";

        var cognito = Substitute.For<IAmazonCognitoIdentityProvider>();
        InitiateAuthRequest? capturedRequest = null;
        cognito.InitiateAuthAsync(
                Arg.Do<InitiateAuthRequest>(request => capturedRequest = request),
                Arg.Any<CancellationToken>())
            .Returns(new InitiateAuthResponse
            {
                AuthenticationResult = new AuthenticationResultType
                {
                    AccessToken = "new-access-token",
                    ExpiresIn = 3600
                }
            });

        var service = new CognitoAuthService(
            cognito,
            Options.Create(new CognitoOptions
            {
                ClientId = clientId,
                ClientSecret = clientSecret
            }));

        await service.RefreshTokenAsync(clientId, refreshToken, refreshSubject);

        Assert.NotNull(capturedRequest);
        Assert.Equal(refreshToken, capturedRequest.AuthParameters["REFRESH_TOKEN"]);
        Assert.Equal(
            CalculateSecretHash(clientId, clientSecret, refreshSubject),
            capturedRequest.AuthParameters["SECRET_HASH"]);
    }

    [Fact]
    public async Task RefreshTokenAsync_OmitsSecretHash_WhenClientHasNoSecret()
    {
        const string tenantClientId = "tenant-client-id";
        var cognito = Substitute.For<IAmazonCognitoIdentityProvider>();
        InitiateAuthRequest? capturedRequest = null;
        cognito.InitiateAuthAsync(
                Arg.Do<InitiateAuthRequest>(request => capturedRequest = request),
                Arg.Any<CancellationToken>())
            .Returns(new InitiateAuthResponse
            {
                AuthenticationResult = new AuthenticationResultType
                {
                    AccessToken = "new-access-token",
                    ExpiresIn = 3600
                }
            });

        var service = new CognitoAuthService(
            cognito,
            Options.Create(new CognitoOptions { ClientId = "system-client-id" }));

        await service.RefreshTokenAsync(tenantClientId, "refresh-token", "cognito-subject");

        Assert.NotNull(capturedRequest);
        Assert.Equal("refresh-token", capturedRequest.AuthParameters["REFRESH_TOKEN"]);
        Assert.False(capturedRequest.AuthParameters.ContainsKey("SECRET_HASH"));
    }

    [Fact]
    public async Task InitiateAuthAsync_ReturnsSubjectFromAccessToken()
    {
        const string clientId = "client-id";
        const string accessToken = "eyJhbGciOiJub25lIn0.eyJzdWIiOiJjb2duaXRvLXN1YmplY3QifQ.";
        var cognito = Substitute.For<IAmazonCognitoIdentityProvider>();
        cognito.InitiateAuthAsync(
                Arg.Any<InitiateAuthRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(new InitiateAuthResponse
            {
                AuthenticationResult = new AuthenticationResultType
                {
                    AccessToken = accessToken,
                    RefreshToken = "refresh-token",
                    ExpiresIn = 3600
                }
            });

        var service = new CognitoAuthService(
            cognito,
            Options.Create(new CognitoOptions { ClientId = clientId }));

        var result = await service.InitiateAuthAsync(clientId, "user@example.com", "password");

        Assert.Equal("cognito-subject", result.RefreshTokenSubject);
    }

    private static string CalculateSecretHash(string clientId, string clientSecret, string username)
    {
        var data = Encoding.UTF8.GetBytes(username + clientId);
        var key = Encoding.UTF8.GetBytes(clientSecret);
        return Convert.ToBase64String(HMACSHA256.HashData(key, data));
    }
}
