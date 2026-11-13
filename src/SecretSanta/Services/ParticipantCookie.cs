namespace SecretSanta.Services;

// Remembers the participant on this device so a lost personal link can be recovered from the invite link.
public static class ParticipantCookie
{
    private static string Name(int gameId) => $"ss_{gameId}";

    public static string? Get(HttpRequest request, int gameId) => request.Cookies[Name(gameId)];

    public static void Set(HttpResponse response, int gameId, string token) =>
        response.Cookies.Append(Name(gameId), token, new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
            Secure = response.HttpContext.Request.IsHttps,
        });
}
