namespace EfMigrationManager.Core.Models;

public enum NotificationSeverity { Informational, Success, Warning, Error }

public sealed class AppNotification
{
    public required string Title    { get; init; }
    public required string Message  { get; init; }
    public required NotificationSeverity Severity { get; init; }
    public string? ActionLabel      { get; init; }
    public string? ActionClipboardText { get; init; }

    public static AppNotification Info(string title, string msg)
        => new() { Title = title, Message = msg, Severity = NotificationSeverity.Informational };
    public static AppNotification Warn(string title, string msg)
        => new() { Title = title, Message = msg, Severity = NotificationSeverity.Warning };
    public static AppNotification Error(string title, string msg, string? actionLabel = null, string? actionClip = null)
        => new() { Title = title, Message = msg, Severity = NotificationSeverity.Error, ActionLabel = actionLabel, ActionClipboardText = actionClip };
}
