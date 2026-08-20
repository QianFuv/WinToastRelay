namespace WinToastRelay.Models;

public sealed record RelayDeliveryTarget(
    string Mode,
    string WebhookUrl,
    string BearerToken,
    string BarkServerUrl,
    string BarkDeviceKey,
    string BarkTitleTemplate,
    string BarkBodyTemplate,
    string BarkParameters)
{
    public const string BarkMode = "bark";
    public const string JsonWebhookMode = "json";

    public bool IsBark => string.Equals(Mode, BarkMode, StringComparison.OrdinalIgnoreCase);
}
