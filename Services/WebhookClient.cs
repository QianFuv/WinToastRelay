using System.Net.Http.Headers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using WinToastRelay.Models;

namespace WinToastRelay.Services;

public sealed class WebhookClient
{
    // Keep a conservative margin below the payload size accepted by Bark/APNs.
    // This limit applies to the serialized JSON body, not the notification text
    // alone, so the device key and configured Bark fields are included as well.
    private const int BarkPayloadByteLimit = 3500;
    private const string BarkTruncationSuffix = "\n[truncated by WinToastRelay]";
    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
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
            ? BuildBarkPushUri(target.BarkServerUrl)
            : new Uri(target.WebhookUrl, UriKind.Absolute);

        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = target.IsBark
                ? new StringContent(CreateBarkJson(target, payload), Encoding.UTF8, "application/json")
                : new StringContent(JsonSerializer.Serialize(payload, AppJsonContext.Default.WebhookPayload), Encoding.UTF8, "application/json")
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

    private static Uri BuildBarkPushUri(string serverUrl)
    {
        var builder = new UriBuilder(new Uri(serverUrl, UriKind.Absolute))
        {
            Query = string.Empty,
        };
        var path = builder.Path.TrimEnd('/');
        if (!path.EndsWith("/push", StringComparison.OrdinalIgnoreCase))
            path += "/push";
        builder.Path = path;
        return builder.Uri;
    }

    private static string CreateBarkJson(RelayDeliveryTarget target, WebhookPayload payload)
    {
        var json = new JsonObject();
        foreach (var (key, value) in ParseBarkParameters(target.BarkParameters))
        {
            // These fields are controlled by the existing dedicated settings and
            // templates. Do not let an old query-style parameter silently replace them.
            if (key.Equals("device_key", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("title", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("body", StringComparison.OrdinalIgnoreCase))
                continue;

            json[key] = CreateBarkValue(key, value);
        }

        // Bark's JSON endpoint accepts the device key in the request body, which
        // keeps long titles and bodies out of the request URL entirely.
        json["device_key"] = target.BarkDeviceKey.Trim();
        json["title"] = ApplyTemplate(target.BarkTitleTemplate, payload);
        json["body"] = ApplyTemplate(target.BarkBodyTemplate, payload);

        if (Utf8.GetByteCount(json.ToJsonString()) > BarkPayloadByteLimit)
        {
            // Body is the usual source of oversized payloads. If a custom title
            // template also contains {body}, fit it separately after the body.
            FitBarkTextProperty(json, "body", json["body"]?.GetValue<string>() ?? string.Empty);
            if (Utf8.GetByteCount(json.ToJsonString()) > BarkPayloadByteLimit)
                FitBarkTextProperty(json, "title", json["title"]?.GetValue<string>() ?? string.Empty);
        }

        return json.ToJsonString();
    }

    private static void FitBarkTextProperty(JsonObject json, string propertyName, string originalValue)
    {
        var low = 0;
        var high = Utf8.GetByteCount(originalValue);
        var best = string.Empty;

        while (low <= high)
        {
            var candidateBudget = low + ((high - low) / 2);
            var candidate = TruncateUtf8(originalValue, candidateBudget);
            json[propertyName] = candidate;
            if (Utf8.GetByteCount(json.ToJsonString()) <= BarkPayloadByteLimit)
            {
                best = candidate;
                low = candidateBudget + 1;
            }
            else
            {
                high = candidateBudget - 1;
            }
        }

        json[propertyName] = best;
    }

    private static string TruncateUtf8(string value, int maxBytes)
    {
        if (maxBytes <= 0) return string.Empty;
        if (Utf8.GetByteCount(value) <= maxBytes) return value;

        var suffixBytes = Utf8.GetByteCount(BarkTruncationSuffix);
        if (suffixBytes >= maxBytes) return string.Empty;

        var contentBudget = maxBytes - suffixBytes;
        var builder = new StringBuilder();
        var usedBytes = 0;
        foreach (var rune in value.EnumerateRunes())
        {
            var runeText = rune.ToString();
            var runeBytes = Utf8.GetByteCount(runeText);
            if (usedBytes + runeBytes > contentBudget) break;
            builder.Append(runeText);
            usedBytes += runeBytes;
        }

        return builder.Append(BarkTruncationSuffix).ToString();
    }

    private static JsonNode CreateBarkValue(string key, string value)
    {
        // Bark documents badge and ttl as integer JSON values. Keep the other
        // legacy key=value options as strings so existing configurations retain
        // their exact meaning (for example call=1 and isArchive=1).
        if ((key.Equals("badge", StringComparison.OrdinalIgnoreCase) ||
             key.Equals("ttl", StringComparison.OrdinalIgnoreCase)) &&
            long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
            return JsonValue.Create(number)!;

        return JsonValue.Create(value)!;
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

    private static IEnumerable<(string Key, string Value)> ParseBarkParameters(string parameters)
    {
        foreach (var line in parameters.Split(['\r', '\n', '&'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (line.StartsWith('#')) continue;
            var separator = line.IndexOf('=');
            var key = (separator < 0 ? line : line[..separator]).Trim();
            var value = (separator < 0 ? string.Empty : line[(separator + 1)..]).Trim();
            if (key.Length == 0) continue;
            yield return (key, value);
        }
    }
}
