namespace WinToastRelay.Models;

public sealed class PendingDelivery
{
    public string DeliveryId { get; set; } = string.Empty;
    public WebhookPayload Payload { get; set; } = null!;
    public DateTimeOffset EnqueuedAt { get; set; } = DateTimeOffset.UtcNow;
    public int Attempts { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public string LastError { get; set; } = string.Empty;

    public PendingDelivery Clone() => new()
    {
        DeliveryId = DeliveryId,
        Payload = Payload,
        EnqueuedAt = EnqueuedAt,
        Attempts = Attempts,
        NextAttemptAt = NextAttemptAt,
        LastError = LastError
    };
}

public sealed class DeadLetterDelivery
{
    public string DeliveryId { get; set; } = string.Empty;
    public WebhookPayload Payload { get; set; } = null!;
    public int Attempts { get; set; }
    public DateTimeOffset FailedAt { get; set; }
    public string Error { get; set; } = string.Empty;

    public DeadLetterDelivery Clone() => new()
    {
        DeliveryId = DeliveryId,
        Payload = Payload,
        Attempts = Attempts,
        FailedAt = FailedAt,
        Error = Error
    };
}
