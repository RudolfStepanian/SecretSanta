namespace SecretSanta.Services;

// Render's free instance spins down after 15 minutes without inbound requests, and waking it takes about a minute.
// Render sets RENDER_EXTERNAL_URL; when it is present, the app requests its own public /healthz every 10 minutes
// (KeepAwakeInterval). The request goes through Render's proxy, so it counts as inbound traffic. Elsewhere it does nothing.
public sealed class KeepAwake(IHttpClientFactory http, IConfiguration config, ILogger<KeepAwake> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        if (config["RENDER_EXTERNAL_URL"] is not { Length: > 0 } baseUrl) return;

        var url = new Uri(new Uri(baseUrl), "/healthz");
        var interval = config.GetValue("KeepAwakeInterval", TimeSpan.FromMinutes(10));
        logger.LogInformation("Keep-awake: requesting {Url} every {Interval}.", url, interval);

        using var timer = new PeriodicTimer(interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stop))
            {
                try
                {
                    using var response = await http.CreateClient().GetAsync(url, stop);
                    if (response.IsSuccessStatusCode) logger.LogDebug("Keep-awake: {Url} answered {Status}.", url, (int)response.StatusCode);
                    else logger.LogWarning("Keep-awake: {Url} answered {Status}.", url, (int)response.StatusCode);
                }
                catch (Exception e) when (e is HttpRequestException || (e is TaskCanceledException && !stop.IsCancellationRequested))
                {
                    logger.LogWarning(e, "Keep-awake: request to {Url} failed.", url);
                }
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
        }
    }
}
