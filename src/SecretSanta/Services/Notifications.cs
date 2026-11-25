using System.Buffers.Text;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using SecretSanta.Data;
using SecretSanta.Localization;

namespace SecretSanta.Services;

public sealed record PushMessage(int SubscriptionId, string Endpoint, string P256dh, string Auth, string Title, string Body, string Url, string Origin);

// Push messages wait here, so pages never wait for push services; PushDelivery sends them in the background.
public sealed class PushQueue
{
    private readonly Channel<PushMessage> channel = Channel.CreateUnbounded<PushMessage>();

    public ChannelReader<PushMessage> Reader => channel.Reader;

    public void Enqueue(PushMessage message) => channel.Writer.TryWrite(message);
}

// Turns a game event into one push message per subscribed device, in the language the device subscribed with.
public sealed class Notifier(AppDb db, PushQueue queue)
{
    public async Task NotifyAsync(IReadOnlyCollection<int> participantIds, string textKey, HttpRequest request, string path)
    {
        if (participantIds.Count == 0) return;

        var subscriptions = await db.PushSubscriptions.Where(s => participantIds.Contains(s.ParticipantId)).ToListAsync();
        foreach (var s in subscriptions) Enqueue(s, textKey, request, path);
    }

    public void Enqueue(PushSubscription s, string textKey, HttpRequest request, string path) =>
        queue.Enqueue(new PushMessage(s.Id, s.Endpoint, s.P256dh, s.Auth, Strings.Get("PushTitle", s.Lang), Strings.Get(textKey, s.Lang), path, request.Abs("/")));
}

// Encrypts and sends the queued messages. A subscription the push service reports as gone (404 or 410) is deleted;
// other failures are logged and the message is dropped: a missed notification is not worth a retry queue.
public sealed class PushDelivery(PushQueue queue, VapidKeys keys, IHttpClientFactory http, IServiceScopeFactory scopes, ILogger<PushDelivery> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        try
        {
            await foreach (var message in queue.Reader.ReadAllAsync(stop))
            {
                try
                {
                    await SendAsync(message, stop);
                }
                catch (Exception e) when (e is not OperationCanceledException || !stop.IsCancellationRequested)
                {
                    logger.LogWarning(e, "Push to {Host} failed.", new Uri(message.Endpoint).Host);
                }
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
        }
    }

    private async Task SendAsync(PushMessage message, CancellationToken stop)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(new { title = message.Title, body = message.Body, url = message.Url });
        using var serverKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var body = WebPush.Encrypt(payload, Base64Url.DecodeFromChars(message.P256dh), Base64Url.DecodeFromChars(message.Auth), serverKey, RandomNumberGenerator.GetBytes(16));

        using var signer = keys.CreateSigner();
        using var request = new HttpRequestMessage(HttpMethod.Post, message.Endpoint) { Content = new ByteArrayContent(body) };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        request.Content.Headers.ContentEncoding.Add("aes128gcm");
        request.Headers.Add("TTL", "86400");
        request.Headers.TryAddWithoutValidation("Authorization", WebPush.VapidAuthorization(message.Endpoint, message.Origin, signer, DateTimeOffset.UtcNow.AddHours(12)));

        using var response = await http.CreateClient().SendAsync(request, stop);
        var host = new Uri(message.Endpoint).Host;
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
        {
            using var scope = scopes.CreateScope();
            await scope.ServiceProvider.GetRequiredService<AppDb>().PushSubscriptions.Where(s => s.Id == message.SubscriptionId).ExecuteDeleteAsync(stop);
            logger.LogInformation("Push subscription {Id} at {Host} is gone; deleted.", message.SubscriptionId, host);
        }
        else if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Push to {Host} answered {Status}: {Reason}", host, (int)response.StatusCode, await response.Content.ReadAsStringAsync(stop));
        }
        else
        {
            logger.LogDebug("Push to {Host} accepted with {Status}.", host, (int)response.StatusCode);
        }
    }
}
