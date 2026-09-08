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

        async Task<(string PoolId, string ClientId)> CreatePoolAndClientAsync(string poolSuffix, string clientSuffix)
        {
            var poolReq = new CreateUserPoolRequest
            {
                PoolName = $"{sanitizedCode}_{poolSuffix}",
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

            var poolRes = await cognito.CreateUserPoolAsync(poolReq, ct);
            var poolId = poolRes.UserPool.Id;

            var clientReq = new CreateUserPoolClientRequest
            {
                UserPoolId = poolId,
                ClientName = $"{sanitizedCode}_{clientSuffix}",
                GenerateSecret = false,
                ExplicitAuthFlows = new List<string>
                {
                    "ALLOW_ADMIN_USER_PASSWORD_AUTH",
                    "ALLOW_REFRESH_TOKEN_AUTH",
                    "ALLOW_USER_PASSWORD_AUTH"
                },
                AllowedOAuthFlows = new List<string> { "code", "implicit" },
                AllowedOAuthScopes = new List<string> { "phone", "email", "openid", "profile", "aws.cognito.signin.user.admin" },
                AllowedOAuthFlowsUserPoolClient = true,
                SupportedIdentityProviders = new List<string> { "COGNITO" },
                CallbackURLs = _options.CallbackUrls,
                LogoutURLs = _options.LogoutUrls
            };

            var clientRes = await cognito.CreateUserPoolClientAsync(clientReq, ct);
            return (poolId, clientRes.UserPoolClient.ClientId);
        }

        var adminTask = CreatePoolAndClientAsync("Admin_UserPool", "Admin_AppClient");
        var staffTask = CreatePoolAndClientAsync("User_UserPool", "User_AppClient");

        await Task.WhenAll(adminTask, staffTask);

        var (adminPoolId, adminClientId) = await adminTask;
        var (staffPoolId, staffClientId) = await staffTask;

        // Provision default groups in Admin User Pool & Staff User Pool
        async Task EnsureGroupAsync(string poolId, string groupName, string description)
        {
            try
            {
                await cognito.CreateGroupAsync(new CreateGroupRequest
                {
                    UserPoolId = poolId,
                    GroupName = groupName,
                    Description = description
                }, ct);
            }
            catch (GroupExistsException) { }
            catch { /* non-blocking */ }
        }

        await Task.WhenAll(
            EnsureGroupAsync(adminPoolId, "TENANT_ADMIN", "Tenant Administrator Group"),
            EnsureGroupAsync(staffPoolId, "STAFF", "Tenant Staff Group"),
            EnsureGroupAsync(staffPoolId, "MANAGER", "Tenant Manager Group")
        );

        return new TenantCognitoPoolsResult
        {
            AdminUserPoolId = adminPoolId,
            AdminUserPoolClientId = adminClientId,
            StaffUserPoolId = staffPoolId,
            StaffUserPoolClientId = staffClientId
        };
    }

    public async Task<string> AdminCreateUserInPoolAsync(
        string userPoolId,
        string email,
        string tempPassword,
        string? firstName = null,
        string? lastName = null,
        string? role = null,
        CancellationToken ct = default)
    {
        var attributes = new List<AttributeType>
        {
            new() { Name = "email", Value = email },
            new() { Name = "email_verified", Value = "true" }
        };

        if (!string.IsNullOrWhiteSpace(firstName))
            attributes.Add(new() { Name = "given_name", Value = firstName });

        if (!string.IsNullOrWhiteSpace(lastName))
            attributes.Add(new() { Name = "family_name", Value = lastName });

        var fullName = $"{firstName} {lastName}".Trim();
        if (!string.IsNullOrWhiteSpace(fullName))
            attributes.Add(new() { Name = "name", Value = fullName });

        var request = new AdminCreateUserRequest
        {
            UserPoolId = userPoolId,
            Username = email,
            DesiredDeliveryMediums = new List<string> { "EMAIL" },
            TemporaryPassword = tempPassword,
            UserAttributes = attributes
        };

        var response = await cognito.AdminCreateUserAsync(request, ct);

        // Add user to specified Cognito group if provided
        if (!string.IsNullOrWhiteSpace(role))
        {
            try
            {
                await cognito.AdminAddUserToGroupAsync(new AdminAddUserToGroupRequest
                {
                    UserPoolId = userPoolId,
                    Username = email,
                    GroupName = role
                }, ct);
            }
            catch
            {
                try
                {
                    await cognito.CreateGroupAsync(new CreateGroupRequest
                    {
                        UserPoolId = userPoolId,
                        GroupName = role,
                        Description = $"{role} Group"
                    }, ct);

                    await cognito.AdminAddUserToGroupAsync(new AdminAddUserToGroupRequest
                    {
                        UserPoolId = userPoolId,
                        Username = email,
                        GroupName = role
                    }, ct);
                }
                catch { /* non-blocking */ }
            }
        }

        var subAttribute = response.User.Attributes.FirstOrDefault(a => a.Name == "sub");
        return subAttribute?.Value ?? throw new Exception("Sub not found in Cognito response.");
    }

    public async Task<string> AdminCreateUserAsync(string email, string tempPassword, string? firstName = null, string? lastName = null, string? role = null, CancellationToken ct = default)
    {
        return await AdminCreateUserInPoolAsync(_options.UserPoolId, email, tempPassword, firstName, lastName, role, ct);
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
