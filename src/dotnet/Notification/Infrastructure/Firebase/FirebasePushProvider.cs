using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Options;
using Notification.Application.Interfaces;
using Notification.Domain.Entities;

namespace Notification.Infrastructure.Firebase;

public sealed class FirebasePushProvider : IFcmPushProvider
{
    private readonly FirebaseApp? app;
    private readonly ILogger<FirebasePushProvider> logger;

    public FirebasePushProvider(IOptions<FirebaseOptions> options, ILogger<FirebasePushProvider> logger)
    {
        this.logger = logger;
        var settings = options.Value;
        if (!settings.Enabled) return;

        GoogleCredential credential;
        if (!string.IsNullOrWhiteSpace(settings.CredentialsPath))
        {
            if (!File.Exists(settings.CredentialsPath))
                throw new InvalidOperationException("Firebase credentials file was not found.");
            credential = GoogleCredential.FromFile(settings.CredentialsPath);
        }
        else if (settings.HasInlineCredentials)
        {
            var credentialJson = $$"""{"type":"service_account","project_id":"{{settings.ProjectId}}","client_email":"{{settings.ClientEmail}}","private_key":"{{settings.PrivateKey.Replace("\\n", "\n")}}"}""";
            credential = GoogleCredential.FromJson(credentialJson);
        }
        else
        {
            throw new InvalidOperationException("Firebase is enabled but credentials are incomplete.");
        }

        app = FirebaseApp.Create(new AppOptions
        {
            Credential = credential,
            ProjectId = settings.ProjectId
        });
    }

    public async Task<FcmSendResult> SendAsync(NotificationDevice device, FcmMessage message, CancellationToken cancellationToken)
    {
        if (app is null) return new(FcmSendStatus.PermanentFailure, ErrorCode: "firebase_disabled");
        try
        {
            var id = await FirebaseMessaging.GetMessaging(app).SendAsync(new Message
            {
                Token = device.FcmToken,
                Notification = new FirebaseAdmin.Messaging.Notification { Title = message.Title, Body = message.Body },
                Data = message.Data.ToDictionary(x => x.Key, x => x.Value)
            }, cancellationToken);
            return new(FcmSendStatus.Sent, id);
        }
        catch (FirebaseMessagingException ex) when (ex.MessagingErrorCode == MessagingErrorCode.Unregistered)
        {
            logger.LogWarning("FCM rejected a device token with code {ErrorCode}", ex.MessagingErrorCode);
            return new(FcmSendStatus.InvalidToken, ErrorCode: ex.MessagingErrorCode.ToString());
        }
        catch (FirebaseMessagingException ex) when (ex.MessagingErrorCode == MessagingErrorCode.InvalidArgument)
        {
            logger.LogWarning("FCM rejected a notification payload with code {ErrorCode}", ex.MessagingErrorCode);
            return new(FcmSendStatus.PermanentFailure, ErrorCode: ex.MessagingErrorCode.ToString());
        }
        catch (FirebaseMessagingException ex)
        {
            logger.LogWarning("FCM transient/provider failure with code {ErrorCode}", ex.MessagingErrorCode);
            return new(FcmSendStatus.TransientFailure, ErrorCode: ex.MessagingErrorCode.ToString());
        }
    }
}
