using System.Text;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Html;

namespace SecretSatana.Services;

public static partial class Fmt
{
    private static readonly TimeZoneInfo Zone = FindZone();

    public static string Local(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone).ToString("dd.MM.yyyy HH:mm");

    // HTML-encodes free text and turns http(s) links into anchors that do not leak the page URL.
    public static IHtmlContent Linkify(string? text)
    {
        if (string.IsNullOrEmpty(text)) return HtmlString.Empty;
        var sb = new StringBuilder();
        var pos = 0;
        foreach (Match m in UrlRegex().Matches(text))
        {
            sb.Append(HtmlEncoder.Default.Encode(text[pos..m.Index]));
            var url = HtmlEncoder.Default.Encode(m.Value);
            sb.Append($"<a href=\"{url}\" target=\"_blank\" rel=\"noopener noreferrer nofollow\">{url}</a>");
            pos = m.Index + m.Length;
        }
        sb.Append(HtmlEncoder.Default.Encode(text[pos..]));
        return new HtmlString(sb.ToString());
    }

    public static string Abs(this HttpRequest request, string path) =>
        $"{request.Scheme}://{request.Host}{request.PathBase}{path}";

    private static TimeZoneInfo FindZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Yerevan"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.Utc; }
    }

    [GeneratedRegex(@"https?://[^\s<>""]+[^\s<>"".,;:!?)\]]", RegexOptions.IgnoreCase)]
    private static partial Regex UrlRegex();
}
