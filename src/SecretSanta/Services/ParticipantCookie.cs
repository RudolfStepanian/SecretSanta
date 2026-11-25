namespace SecretSanta.Services;

// Keeps a participant signed in on this device: one cookie per game, holding the participant's token.
// Resetting the password or setting a new one changes the token, which signs the participant out everywhere else.
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

    public static void Clear(HttpResponse response, int gameId) => response.Cookies.Delete(Name(gameId));
}
