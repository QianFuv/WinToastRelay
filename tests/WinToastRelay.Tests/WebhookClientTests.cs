using System.Net;
using System.Text.Json;
using WinToastRelay.Models;
using WinToastRelay.Services;

namespace WinToastRelay.Tests;

public sealed class WebhookClientTests
{
    [Theory]
    [InlineData("https://hooks.example.com/relay", true)]
    [InlineData("http://127.0.0.1:8080/relay", true)]
    [InlineData("http://localhost:8080/relay", true)]
    [InlineData("http://hooks.example.com/relay", false)]
    [InlineData("ftp://hooks.example.com/relay", false)]
    [InlineData("not a URL", false)]
    public void EndpointValidation_OnlyAllowsHttpsOrLoopbackHttp(string endpoint, bool expected) =>
        Assert.Equal(expected, WebhookClient.IsValidEndpoint(endpoint));

    [Fact]
    public async Task DeliverAsync_PostsJsonAndDeliveryHeader()
    {
        using var listener = new HttpListener();
        listener.Prefixes.Add("http://127.0.0.1:18765/");
        listener.Start();

        var requestTask = listener.GetContextAsync();
        var payload = new WebhookPayload(
            "relay.test",
            "delivery-123",
            new RelayNotification(7, "Calendar", "Reminder", "Meeting in 10 minutes", DateTimeOffset.Parse("2026-08-20T00:00:00Z")));

        var deliveryTask = new WebhookClient().DeliverAsync("http://127.0.0.1:18765/hooks", "token-abc", payload);
        var context = await requestTask;
        using var reader = new StreamReader(context.Request.InputStream);
        var body = await reader.ReadToEndAsync();
        context.Response.StatusCode = (int)HttpStatusCode.Accepted;
        context.Response.Close();

        var result = await deliveryTask;

        Assert.True(result.Succeeded);
        Assert.Equal("delivery-123", context.Request.Headers["X-WinToastRelay-Delivery"]);
        Assert.Equal("Bearer", context.Request.Headers["Authorization"]?.Split(' ')[0]);
        using var json = JsonDocument.Parse(body);
        Assert.Equal("relay.test", json.RootElement.GetProperty("eventType").GetString());
        Assert.Equal("Calendar", json.RootElement.GetProperty("notification").GetProperty("app").GetString());
    }

    [Fact]
    public async Task DeliverAsync_BarkMode_UsesPathTemplatesAndCustomParameters()
    {
        using var listener = new HttpListener();
        listener.Prefixes.Add("http://127.0.0.1:18766/");
        listener.Start();

        var requestTask = listener.GetContextAsync();
        var payload = new WebhookPayload(
            "notification.added",
            "delivery-bark",
            new RelayNotification(9, "Mail", "Build passed", "Ship it", DateTimeOffset.Parse("2026-08-20T00:00:00Z")));
        var target = new RelayDeliveryTarget(
            RelayDeliveryTarget.BarkMode,
            string.Empty,
            string.Empty,
            "http://127.0.0.1:18766",
            "device-key",
            "{app}: {title}",
            "{body}",
            "sound=bell\ngroup=builds");

        var deliveryTask = new WebhookClient().DeliverAsync(target, payload);
        var context = await requestTask;
        var method = context.Request.HttpMethod;
        var path = context.Request.RawUrl?.Split('?')[0];
        var sound = context.Request.QueryString["sound"];
        var group = context.Request.QueryString["group"];
        var deliveryId = context.Request.Headers["X-WinToastRelay-Delivery"];
        context.Response.StatusCode = (int)HttpStatusCode.OK;
        context.Response.Close();

        var result = await deliveryTask;

        Assert.True(result.Succeeded);
        Assert.Equal("GET", method);
        Assert.Equal("/device-key/Mail%3A%20Build%20passed/Ship%20it", path);
        Assert.Equal("bell", sound);
        Assert.Equal("builds", group);
        Assert.Equal("delivery-bark", deliveryId);
    }
}
