using System.Security.Cryptography;
using System.Text;
using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using IamTenant.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace IamTenant.Infrastructure.Auth.Cognito;

public class CognitoAuthService(
    IAmazonCognitoIdentityProvider cognito,
    IOptions<CognitoOptions> options) : ICognitoAuthService
{
    private readonly CognitoOptions _options = options.Value;

    private string ComputeSecretHash(string username)
    {
        var key = Encoding.UTF8.GetBytes(_options.ClientSecret);
        var message = Encoding.UTF8.GetBytes(username + _options.ClientId);

        using var hmac = new HMACSHA256(key);
        var hash = hmac.ComputeHash(message);
        return Convert.ToBase64String(hash);
    }

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
            GenerateSecret = true,
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
            GenerateSecret = true,
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
        var request = new InitiateAuthRequest
        {
            ClientId = _options.ClientId,
            AuthFlow = AuthFlowType.USER_PASSWORD_AUTH,
            AuthParameters = new Dictionary<string, string>
            {
                ["USERNAME"] = email,
                ["PASSWORD"] = password,
                ["SECRET_HASH"] = ComputeSecretHash(email)
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
        var request = new RespondToAuthChallengeRequest
        {
            ClientId = _options.ClientId,
            ChallengeName = ChallengeNameType.NEW_PASSWORD_REQUIRED,
            Session = session,
            ChallengeResponses = new Dictionary<string, string>
            {
                ["USERNAME"] = email,
                ["NEW_PASSWORD"] = newPassword,
                ["SECRET_HASH"] = ComputeSecretHash(email)
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

    public async Task<AuthResult> RefreshTokenAsync(string email, string refreshToken, CancellationToken ct = default)
    {
        var request = new InitiateAuthRequest
        {
            ClientId = _options.ClientId,
            AuthFlow = AuthFlowType.REFRESH_TOKEN_AUTH,
            AuthParameters = new Dictionary<string, string>
            {
                ["REFRESH_TOKEN"] = refreshToken,
                ["SECRET_HASH"] = ComputeSecretHash(email)
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
        var request = new ForgotPasswordRequest
        {
            ClientId = _options.ClientId,
            Username = email,
            SecretHash = ComputeSecretHash(email)
        };

        await cognito.ForgotPasswordAsync(request, ct);
    }

    public async Task ConfirmForgotPasswordAsync(string email, string newPassword, string confirmationCode, CancellationToken ct = default)
    {
        var request = new ConfirmForgotPasswordRequest
        {
            ClientId = _options.ClientId,
            Username = email,
            Password = newPassword,
            ConfirmationCode = confirmationCode,
            SecretHash = ComputeSecretHash(email)
        };

        await cognito.ConfirmForgotPasswordAsync(request, ct);
    }
}
