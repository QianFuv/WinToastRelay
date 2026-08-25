using System.Net;
using System.Text;
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
    public async Task DeliverAsync_BarkMode_PostsJsonToPushEndpointWithTemplatesAndCustomParameters()
    {
        using var listener = new HttpListener();
        listener.Prefixes.Add("http://127.0.0.1:18766/");
        listener.Start();

        var requestTask = listener.GetContextAsync();
        var payload = new WebhookPayload(
            "notification.added",
            "delivery-bark",
            new RelayNotification(9, "Mail", "Build passed", new string('长', 3000), DateTimeOffset.Parse("2026-08-20T00:00:00Z")));
        var target = new RelayDeliveryTarget(
            RelayDeliveryTarget.BarkMode,
            string.Empty,
            string.Empty,
            "http://127.0.0.1:18766",
            "device-key",
            "{app}: {title}",
            "{body}",
            "sound=bell\ngroup=builds\nbadge=3");

        var deliveryTask = new WebhookClient().DeliverAsync(target, payload);
        var context = await requestTask;
        var method = context.Request.HttpMethod;
        var path = context.Request.RawUrl?.Split('?')[0];
        var deliveryId = context.Request.Headers["X-WinToastRelay-Delivery"];
        using var reader = new StreamReader(context.Request.InputStream);
        var body = await reader.ReadToEndAsync();
        context.Response.StatusCode = (int)HttpStatusCode.OK;
        context.Response.Close();

        var result = await deliveryTask;

        Assert.True(result.Succeeded);
        Assert.Equal("POST", method);
        Assert.Equal("/push", path);
        Assert.Equal("delivery-bark", deliveryId);
        using var json = JsonDocument.Parse(body);
        Assert.Equal("device-key", json.RootElement.GetProperty("device_key").GetString());
        Assert.Equal("Mail: Build passed", json.RootElement.GetProperty("title").GetString());
        var barkBody = json.RootElement.GetProperty("body").GetString();
        Assert.NotNull(barkBody);
        Assert.StartsWith(new string('长', 100), barkBody);
        Assert.EndsWith("[truncated by WinToastRelay]", barkBody);
        Assert.True(Encoding.UTF8.GetByteCount(body) <= 3500);
        Assert.Equal("bell", json.RootElement.GetProperty("sound").GetString());
        Assert.Equal("builds", json.RootElement.GetProperty("group").GetString());
        Assert.Equal(3, json.RootElement.GetProperty("badge").GetInt32());
    }
}
