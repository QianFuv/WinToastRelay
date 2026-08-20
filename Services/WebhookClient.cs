using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using WinToastRelay.Models;

namespace WinToastRelay.Services;

public sealed class WebhookClient
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(15) };

    public static bool IsValidEndpoint(string endpoint) =>
        Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttps ||
         (uri.Scheme == Uri.UriSchemeHttp && (uri.IsLoopback || string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))));

    public static bool IsValidConfiguration(RelayDeliveryTarget target) => target.IsBark
        ? !string.IsNullOrWhiteSpace(target.BarkDeviceKey) && IsValidEndpoint(target.BarkServerUrl)
        : IsValidEndpoint(target.WebhookUrl);

    public async Task<DeliveryResult> DeliverAsync(string endpoint, string bearerToken, WebhookPayload payload)
        => await DeliverAsync(new RelayDeliveryTarget(
            RelayDeliveryTarget.JsonWebhookMode,
            endpoint,
            bearerToken,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty), payload);

    public async Task<DeliveryResult> DeliverAsync(RelayDeliveryTarget target, WebhookPayload payload)
    {
        if (!IsValidConfiguration(target))
            return new DeliveryResult(false, target.IsBark ? "Invalid Bark configuration" : "Invalid webhook URL", false);

        var uri = target.IsBark
            ? BuildBarkUri(target, payload)
            : new Uri(target.WebhookUrl, UriKind.Absolute);

        using var request = target.IsBark
            ? new HttpRequestMessage(HttpMethod.Get, uri)
            : new HttpRequestMessage(HttpMethod.Post, uri)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload, AppJsonContext.Default.WebhookPayload), Encoding.UTF8, "application/json")
            };
        request.Headers.Add("X-WinToastRelay-Delivery", payload.DeliveryId);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("WinToastRelay", "0.1"));
        if (!target.IsBark && !string.IsNullOrWhiteSpace(target.BearerToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", target.BearerToken);

        try
        {
            using var response = await HttpClient.SendAsync(request);
            var retryable = response.StatusCode == System.Net.HttpStatusCode.RequestTimeout ||
                            response.StatusCode == System.Net.HttpStatusCode.TooManyRequests ||
                            (int)response.StatusCode >= 500;
            return new DeliveryResult(response.IsSuccessStatusCode,
                $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}", retryable);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new DeliveryResult(false, ex.Message, true);
        }
    }

    private static Uri BuildBarkUri(RelayDeliveryTarget target, WebhookPayload payload)
    {
        var title = ApplyTemplate(target.BarkTitleTemplate, payload);
        var body = ApplyTemplate(target.BarkBodyTemplate, payload);
        var baseUri = new Uri(target.BarkServerUrl, UriKind.Absolute);
        var basePath = baseUri.GetLeftPart(UriPartial.Path).TrimEnd('/');
        var path = string.Join('/',
            Uri.EscapeDataString(target.BarkDeviceKey.Trim()),
            Uri.EscapeDataString(title),
            Uri.EscapeDataString(body));
        var query = ParseBarkParameters(target.BarkParameters);
        return new Uri(query.Length == 0 ? $"{basePath}/{path}" : $"{basePath}/{path}?{query}", UriKind.Absolute);
    }

    private static string ApplyTemplate(string template, WebhookPayload payload)
    {
        var notification = payload.Notification;
        var value = string.IsNullOrWhiteSpace(template) ? "{body}" : template;
        return value
            .Replace("{app}", notification.App, StringComparison.OrdinalIgnoreCase)
            .Replace("{title}", notification.Title, StringComparison.OrdinalIgnoreCase)
            .Replace("{body}", notification.Body, StringComparison.OrdinalIgnoreCase)
            .Replace("{id}", notification.Id.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("{eventType}", payload.EventType, StringComparison.OrdinalIgnoreCase)
            .Replace("{createdAt}", notification.CreatedAt.ToString("O"), StringComparison.OrdinalIgnoreCase);
    }

    private static string ParseBarkParameters(string parameters)
    {
        var pairs = new List<string>();
        foreach (var line in parameters.Split(['\r', '\n', '&'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (line.StartsWith('#')) continue;
            var separator = line.IndexOf('=');
            var key = separator < 0 ? line : line[..separator];
            var value = separator < 0 ? string.Empty : line[(separator + 1)..];
            if (key.Length == 0) continue;
            pairs.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}");
        }
        return string.Join('&', pairs);
    }
}
