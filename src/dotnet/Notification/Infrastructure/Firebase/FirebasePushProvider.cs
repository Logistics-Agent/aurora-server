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
        if (string.IsNullOrWhiteSpace(settings.ProjectId) || string.IsNullOrWhiteSpace(settings.ClientEmail) || string.IsNullOrWhiteSpace(settings.PrivateKey))
            throw new InvalidOperationException("Firebase is enabled but server credentials are incomplete.");

        var credentialJson = $$"""{"type":"service_account","project_id":"{{settings.ProjectId}}","client_email":"{{settings.ClientEmail}}","private_key":"{{settings.PrivateKey.Replace("\\n", "\n")}}"}""";
        app = FirebaseApp.Create(new AppOptions { Credential = GoogleCredential.FromJson(credentialJson), ProjectId = settings.ProjectId });
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
        catch (FirebaseMessagingException ex) when (ex.MessagingErrorCode is MessagingErrorCode.Unregistered or MessagingErrorCode.InvalidArgument)
        {
            logger.LogWarning("FCM rejected a device token with code {ErrorCode}", ex.MessagingErrorCode);
            return new(FcmSendStatus.InvalidToken, ErrorCode: ex.MessagingErrorCode.ToString());
        }
        catch (FirebaseMessagingException ex)
        {
            logger.LogWarning("FCM transient/provider failure with code {ErrorCode}", ex.MessagingErrorCode);
            return new(FcmSendStatus.TransientFailure, ErrorCode: ex.MessagingErrorCode.ToString());
        }
    }
}
