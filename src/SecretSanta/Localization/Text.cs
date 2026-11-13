namespace SecretSanta.Localization;

public static class Languages
{
    public const string ItemKey = "lang";
    public const string Default = "hy";

    // Order matches the value arrays in Strings.
    public static readonly string[] All = ["ru", "en", "hy"];

    public static readonly string[] Menu = ["hy", "ru", "en"];

    public static string Label(string lang) => lang switch
    {
        "ru" => "Рус",
        "en" => "Eng",
        "hy" => "Հայ",
        _ => lang,
    };

    // ?lang=xx wins and is remembered in a cookie; then the cookie; otherwise Armenian.
    public static string Resolve(HttpContext ctx)
    {
        var fromQuery = ctx.Request.Query["lang"].ToString();
        if (All.Contains(fromQuery))
        {
            ctx.Response.Cookies.Append("lang", fromQuery, new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                IsEssential = true,
            });
            return fromQuery;
        }

        var fromCookie = ctx.Request.Cookies["lang"];
        return fromCookie is not null && All.Contains(fromCookie) ? fromCookie : Default;
    }
}

public sealed class Text(IHttpContextAccessor http)
{
    public string Lang { get; } = http.HttpContext?.Items[Languages.ItemKey] as string ?? Languages.Default;

    public string this[string key] => Strings.Get(key, Lang);

    public string F(string key, params object[] args) => string.Format(this[key], args);
}
