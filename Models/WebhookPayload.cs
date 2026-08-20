namespace WinToastRelay.Models;

public sealed record WebhookPayload(string EventType, string DeliveryId, RelayNotification Notification);

public sealed record RelayNotification(uint Id, string App, string Title, string Body, DateTimeOffset CreatedAt);

public sealed record DeliveryResult(bool Succeeded, string Detail, bool Retryable = false);

public sealed record DeliveryOutcome(
    string DeliveryId,
    RelayNotification Notification,
    DeliveryResult Result,
    int Attempts,
    bool DeadLettered)
{
    public DateTimeOffset QueuedAt { get; init; }
    public DateTimeOffset CompletedAt { get; init; }
}

public sealed record ActivityEntry(DateTimeOffset Time, string App, string Summary, bool Succeeded, string Detail)
{
    public string Body { get; init; } = string.Empty;
}
