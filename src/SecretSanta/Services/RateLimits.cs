using System.Threading.RateLimiting;

namespace SecretSanta.Services;

// Limits on form posts per client IP and kind of action; reading pages is never limited. A whole office can sit behind
// one IP (and behind a proxy the IP may even be shared by everyone using this site), so the limits are generous:
// thirty colleagues joining within five minutes is normal and stays well below them.
public static class RateLimits
{
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(5);
    public const int CreateGame = 10;
    public const int Join = 60;
    public const int SignIn = 100;
    public const int Messages = 120;
    public const int OtherPosts = 300;

    public static PartitionedRateLimiter<HttpContext> Create() => PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
    {
        if (Kind(ctx.Request) is not { } kind) return RateLimitPartition.GetNoLimiter("");
        return RateLimitPartition.GetFixedWindowLimiter($"{kind}:{ctx.Connection.RemoteIpAddress}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = Limit(kind),
            Window = Window,
            QueueLimit = 0,
        });
    });

    // The kind of action a request is counted as, or null when it is not limited.
    public static string? Kind(HttpRequest request)
    {
        if (!HttpMethods.IsPost(request.Method)) return null;
        var path = request.Path.Value ?? "";
        var handler = request.Query["handler"].ToString();
        if (path.StartsWith("/not-found", StringComparison.Ordinal)) return null; // the "too many requests" page itself
        if (path == "/") return "create";
        if (path.StartsWith("/join/", StringComparison.Ordinal))
            return handler.Equals("SignIn", StringComparison.OrdinalIgnoreCase) ? "signin" : "join";
        if (path.StartsWith("/play/", StringComparison.Ordinal)
            && (handler.Equals("Ask", StringComparison.OrdinalIgnoreCase) || handler.Equals("Answer", StringComparison.OrdinalIgnoreCase)))
            return "message";
        return "post";
    }

    public static int Limit(string kind) => kind switch
    {
        "create" => CreateGame,
        "join" => Join,
        "signin" => SignIn,
        "message" => Messages,
        _ => OtherPosts,
    };
}
