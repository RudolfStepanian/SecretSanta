using SecretSanta.Services;

namespace SecretSanta.Tests;

public class FmtTests
{
    [Fact]
    public void Dates_are_shown_in_yerevan_time()
    {
        Assert.True(Fmt.HasLocalZone, $"{Fmt.ZoneId} is missing from the OS time zone data.");
        Assert.Equal("20.12.2026 12:00", Fmt.Local(new DateTime(2026, 12, 20, 8, 0, 0, DateTimeKind.Utc)));
    }

    private const string Anchor = "target=\"_blank\" rel=\"noopener noreferrer nofollow\"";

    [Theory]
    [InlineData("A scarf: https://shop.example/scarf?size=m&color=red.",
        "A scarf: <a href=\"https://shop.example/scarf?size=m&amp;color=red\" " + Anchor + ">https://shop.example/scarf?size=m&amp;color=red</a>.")]
    [InlineData("(see www.shop.am/item)",
        "(see <a href=\"https://www.shop.am/item\" " + Anchor + ">www.shop.am/item</a>)")]
    [InlineData("HTTP://SHOP.EXAMPLE/A, then",
        "<a href=\"HTTP://SHOP.EXAMPLE/A\" " + Anchor + ">HTTP://SHOP.EXAMPLE/A</a>, then")]
    [InlineData("Line one\nhttps://a.example/x\nLine three",
        "Line one&#xA;<a href=\"https://a.example/x\" " + Anchor + ">https://a.example/x</a>&#xA;Line three")]
    public void Links_in_free_text_become_anchors(string text, string html)
    {
        Assert.Equal(html, Fmt.Linkify(text).ToString());
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://files.example/a")]
    [InlineData("awww.example")]
    [InlineData("just text, no link")]
    public void Anything_else_stays_plain_text(string text)
    {
        Assert.DoesNotContain("<a ", Fmt.Linkify(text).ToString());
    }

    [Fact]
    public void Markup_in_the_text_is_shown_as_text()
    {
        var html = Fmt.Linkify("<script>alert(1)</script> https://x.example/\"onmouseover=alert(1)").ToString();

        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;script&gt;", html);
        Assert.Contains("<a href=\"https://x.example/\" ", html);
        Assert.DoesNotContain("\"onmouseover", html);
    }
}
