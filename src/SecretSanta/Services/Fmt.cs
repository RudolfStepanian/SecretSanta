using System.Text;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Html;

namespace SecretSanta.Services;

public static partial class Fmt
{
    public const string ZoneId = "Asia/Yerevan";

    private static readonly TimeZoneInfo? LocalZone = FindZone();
    private static readonly TimeZoneInfo Zone = LocalZone ?? TimeZoneInfo.Utc;

    // False when the OS has no time zone data (e.g. an image without tzdata); then dates fall back to UTC.
    public static bool HasLocalZone => LocalZone is not null;

    public static string Local(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone).ToString("dd.MM.yyyy HH:mm");

    // HTML-encodes free text and turns http(s) links, and links pasted without a scheme from www., into anchors that
    // open in a new tab and do not leak the page URL. Punctuation right after a link stays outside it.
    public static IHtmlContent Linkify(string? text)
    {
        if (string.IsNullOrEmpty(text)) return HtmlString.Empty;
        var sb = new StringBuilder();
        var pos = 0;
        foreach (Match m in UrlRegex().Matches(text))
        {
            sb.Append(HtmlEncoder.Default.Encode(text[pos..m.Index]));
            var url = HtmlEncoder.Default.Encode(m.Value);
            var href = m.Value.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? "https://" + url : url;
            sb.Append($"<a href=\"{href}\" target=\"_blank\" rel=\"noopener noreferrer nofollow\">{url}</a>");
            pos = m.Index + m.Length;
        }
        sb.Append(HtmlEncoder.Default.Encode(text[pos..]));
        return new HtmlString(sb.ToString());
    }

    public static string Abs(this HttpRequest request, string path) =>
        $"{request.Scheme}://{request.Host}{request.PathBase}{path}";

    private static TimeZoneInfo? FindZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(ZoneId); }
        catch (TimeZoneNotFoundException) { return null; }
    }

    [GeneratedRegex(@"(?:https?://|\bwww\.)[^\s<>""]+[^\s<>"".,;:!?)\]]", RegexOptions.IgnoreCase)]
    private static partial Regex UrlRegex();
}
