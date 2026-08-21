using System.Security.Cryptography;
using System.Text;
using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using IamTenant.Application.Interfaces;
using IamTenant.Domain.Enums;
using Microsoft.Extensions.Options;

namespace IamTenant.Infrastructure.Auth.Cognito;

public class CognitoAuthService(
    IAmazonCognitoIdentityProvider cognito,
    IOptions<CognitoOptions> options) : ICognitoAuthService
{
    private readonly CognitoOptions _options = options.Value;

    public async Task<TenantCognitoPoolsResult> CreateTenantUserPoolsAsync(string tenantCode, CancellationToken ct = default)
    {
        var sanitizedCode = tenantCode.Replace("-", "_").ToUpperInvariant();

        // 1. Create Admin User Pool & App Client
        var adminPoolReq = new CreateUserPoolRequest
        {
            PoolName = $"{sanitizedCode}_Admin_UserPool",
            AutoVerifiedAttributes = new List<string> { "email" },
            UsernameAttributes = new List<string> { "email" },
            Policies = new UserPoolPolicyType
            {
                PasswordPolicy = new PasswordPolicyType
                {
                    MinimumLength = 8,
                    RequireUppercase = true,
                    RequireLowercase = true,
                    RequireNumbers = true,
                    RequireSymbols = true
                }
            }
        };

        var adminPoolRes = await cognito.CreateUserPoolAsync(adminPoolReq, ct);
        var adminUserPoolId = adminPoolRes.UserPool.Id;

        var adminClientReq = new CreateUserPoolClientRequest
        {
            UserPoolId = adminUserPoolId,
            ClientName = $"{sanitizedCode}_Admin_AppClient",
            GenerateSecret = false,
            ExplicitAuthFlows = new List<string>
            {
                "ALLOW_ADMIN_USER_PASSWORD_AUTH",
                "ALLOW_REFRESH_TOKEN_AUTH",
                "ALLOW_USER_PASSWORD_AUTH"
            }
        };

        var adminClientRes = await cognito.CreateUserPoolClientAsync(adminClientReq, ct);
        var adminClientId = adminClientRes.UserPoolClient.ClientId;

        // 2. Create User User Pool & App Client
        var userPoolReq = new CreateUserPoolRequest
        {
            PoolName = $"{sanitizedCode}_User_UserPool",
            AutoVerifiedAttributes = new List<string> { "email" },
            UsernameAttributes = new List<string> { "email" },
            Policies = new UserPoolPolicyType
            {
                PasswordPolicy = new PasswordPolicyType
                {
                    MinimumLength = 8,
                    RequireUppercase = true,
                    RequireLowercase = true,
                    RequireNumbers = true,
                    RequireSymbols = true
                }
            }
        };

        var userPoolRes = await cognito.CreateUserPoolAsync(userPoolReq, ct);
        var userUserPoolId = userPoolRes.UserPool.Id;

        var userClientReq = new CreateUserPoolClientRequest
        {
            UserPoolId = userUserPoolId,
            ClientName = $"{sanitizedCode}_User_AppClient",
            GenerateSecret = false,
            ExplicitAuthFlows = new List<string>
            {
                "ALLOW_ADMIN_USER_PASSWORD_AUTH",
                "ALLOW_REFRESH_TOKEN_AUTH",
                "ALLOW_USER_PASSWORD_AUTH"
            }
        };

        var userClientRes = await cognito.CreateUserPoolClientAsync(userClientReq, ct);
        var userClientId = userClientRes.UserPoolClient.ClientId;

        return new TenantCognitoPoolsResult
        {
            AdminUserPoolId = adminUserPoolId,
            AdminUserPoolClientId = adminClientId,
            UserUserPoolId = userUserPoolId,
            UserUserPoolClientId = userClientId
        };
    }

    public async Task<string> AdminCreateUserInPoolAsync(string userPoolId, string email, string tempPassword, CancellationToken ct = default)
    {
        var request = new AdminCreateUserRequest
        {
            UserPoolId = userPoolId,
            Username = email,
            MessageAction = MessageActionType.SUPPRESS,
            TemporaryPassword = tempPassword,
            UserAttributes = new List<AttributeType>
            {
                new() { Name = "email", Value = email },
                new() { Name = "email_verified", Value = "true" }
            }
        };

        var response = await cognito.AdminCreateUserAsync(request, ct);

        var subAttribute = response.User.Attributes.FirstOrDefault(a => a.Name == "sub");
        return subAttribute?.Value ?? throw new Exception("Sub not found in Cognito response.");
    }

    public async Task<string> AdminCreateUserAsync(string email, string tempPassword, CancellationToken ct = default)
    {
        return await AdminCreateUserInPoolAsync(_options.UserPoolId, email, tempPassword, ct);
    }

    public async Task<AuthResult> InitiateAuthAsync(string email, string password, CancellationToken ct = default)
    {
        return await InitiateAuthAsync(_options.ClientId, email, password, ct);
    }

    public async Task<AuthResult> InitiateAuthAsync(string clientId, string email, string password, CancellationToken ct = default)
    {
        var request = new InitiateAuthRequest
        {
            ClientId = clientId,
            AuthFlow = AuthFlowType.USER_PASSWORD_AUTH,
            AuthParameters = new Dictionary<string, string>
            {
                ["USERNAME"] = email,
                ["PASSWORD"] = password
            }
        };

        var response = await cognito.InitiateAuthAsync(request, ct);

        if (response.ChallengeName == ChallengeNameType.NEW_PASSWORD_REQUIRED)
        {
            return new AuthResult { Session = response.Session };
        }

        var result = response.AuthenticationResult;
        if (result is null || string.IsNullOrWhiteSpace(result.AccessToken))
        {
            throw new Exception("Cognito did not return a valid access token.");
        }

        return new AuthResult
        {
            AccessToken = result.AccessToken,
            RefreshToken = result.RefreshToken,
            ExpiresIn = (int)result.ExpiresIn!
        };
    }

    public async Task<AuthResult> CompleteNewPasswordChallengeAsync(string email, string newPassword, string session, CancellationToken ct = default)
    {
        return await CompleteNewPasswordChallengeAsync(_options.ClientId, email, newPassword, session, ct);
    }

    public async Task<AuthResult> CompleteNewPasswordChallengeAsync(string clientId, string email, string newPassword, string session, CancellationToken ct = default)
    {
        var request = new RespondToAuthChallengeRequest
        {
            ClientId = clientId,
            ChallengeName = ChallengeNameType.NEW_PASSWORD_REQUIRED,
            Session = session,
            ChallengeResponses = new Dictionary<string, string>
            {
                ["USERNAME"] = email,
                ["NEW_PASSWORD"] = newPassword
            }
        };

        var response = await cognito.RespondToAuthChallengeAsync(request, ct);
        var result = response.AuthenticationResult;

        if (result is null || string.IsNullOrWhiteSpace(result.AccessToken))
        {
            throw new Exception("Cognito did not return a valid access token after challenge.");
        }

        return new AuthResult
        {
            AccessToken = result.AccessToken,
            RefreshToken = result.RefreshToken,
            ExpiresIn = (int)result.ExpiresIn!
        };
    }

    public async Task<AuthResult> RefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        return await RefreshTokenAsync(_options.ClientId, refreshToken, ct);
    }

    public async Task<AuthResult> RefreshTokenAsync(string? clientId, string refreshToken, CancellationToken ct = default)
    {
        var targetClientId = string.IsNullOrWhiteSpace(clientId) ? _options.ClientId : clientId;
        var request = new InitiateAuthRequest
        {
            ClientId = targetClientId,
            AuthFlow = AuthFlowType.REFRESH_TOKEN_AUTH,
            AuthParameters = new Dictionary<string, string>
            {
                ["REFRESH_TOKEN"] = refreshToken
            }
        };

        var response = await cognito.InitiateAuthAsync(request, ct);
        var result = response.AuthenticationResult;

        return new AuthResult
        {
            AccessToken = result.AccessToken,
            RefreshToken = string.IsNullOrWhiteSpace(result.RefreshToken) ? refreshToken : result.RefreshToken,
            ExpiresIn = (int)result.ExpiresIn!
        };
    }


    public async Task ForgotPasswordAsync(string email, CancellationToken ct = default)
    {
        await ForgotPasswordAsync(_options.ClientId, email, ct);
    }

    public async Task ForgotPasswordAsync(string clientId, string email, CancellationToken ct = default)
    {
        var request = new ForgotPasswordRequest
        {
            ClientId = clientId,
            Username = email,
        };

        await cognito.ForgotPasswordAsync(request, ct);
    }

    public async Task ConfirmForgotPasswordAsync(string email, string newPassword, string confirmationCode, CancellationToken ct = default)
    {
        await ConfirmForgotPasswordAsync(_options.ClientId, email, newPassword, confirmationCode, ct);
    }

    public async Task ConfirmForgotPasswordAsync(string clientId, string email, string newPassword, string confirmationCode, CancellationToken ct = default)
    {
        var request = new ConfirmForgotPasswordRequest
        {
            ClientId = clientId,
            Username = email,
            Password = newPassword,
            ConfirmationCode = confirmationCode,
        };

        await cognito.ConfirmForgotPasswordAsync(request, ct);
    }
}
