namespace Notification.Domain.Enums;

public enum NotificationChannel { Push, InApp }
public enum NotificationStatus { Pending, Sent, Failed, Read }
public enum NotificationPriority { Info, Warning, Critical }
public enum DevicePlatform { Web, Android, Ios }
public enum DeliveryAttemptStatus { Pending, Sent, Retrying, Failed, InvalidToken }
public enum ProcessedNotificationEventOutcome { AudienceResolved, NoRecipient }
