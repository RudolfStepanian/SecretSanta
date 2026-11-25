using SecretSanta.Data;

namespace SecretSanta.Services;

// A login is 3 to 32 Latin letters, digits, dots, hyphens or underscores, unique within a game.
// It is stored and compared in lower case, so "Ani.K" and "ani.k" are the same login.
public static class Logins
{
    // The login as stored, or null when the value is not a valid login.
    public static string? Normalize(string? value)
    {
        var login = value?.Trim().ToLowerInvariant();
        return login is { Length: >= Limits.LoginMin and <= Limits.Login } && login.All(IsAllowed) ? login : null;
    }

    private static bool IsAllowed(char c) => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '-' or '_';
}
