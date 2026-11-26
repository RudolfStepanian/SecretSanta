using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using SecretSanta.Localization;
using SecretSanta.Services;

namespace SecretSanta.Tests;

public class RequestRuleTests
{
    [Theory]
    [InlineData("POST", "/", "", "create")]
    [InlineData("POST", "/join/abc", "", "join")]
    [InlineData("POST", "/join/abc", "SignIn", "signin")]
    [InlineData("POST", "/play/abc", "Ask", "message")]
    [InlineData("POST", "/play/abc", "Answer", "message")]
    [InlineData("POST", "/play/abc", "Reveal", "post")]
    [InlineData("POST", "/admin/abc", "Draw", "post")]
    [InlineData("POST", "/not-found/429", "", null)]
    [InlineData("GET", "/", "", null)]
    [InlineData("GET", "/join/abc", "", null)]
    public void Posts_are_limited_by_kind_and_pages_are_not(string method, string path, string handler, string? kind)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = method;
        ctx.Request.Path = path;
        if (handler.Length > 0) ctx.Request.QueryString = new QueryString($"?handler={handler}");

        Assert.Equal(kind, RateLimits.Kind(ctx.Request));
    }

    [Fact]
    public void Language_link_keeps_the_query_except_lang_and_handler()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/play/abc";
        ctx.Request.QueryString = new QueryString("?new=true&lang=hy&handler=Credentials");

        Assert.Equal("?new=true&lang=ru", Languages.Switch(ctx.Request, "ru"));
    }

    [Fact]
    public void Language_link_on_a_status_page_keeps_the_query_that_failed()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/not-found/404";
        ctx.Features.Set<IStatusCodeReExecuteFeature>(new StatusCodeReExecuteFeature
        {
            OriginalPathBase = "",
            OriginalPath = "/join/old",
            OriginalQueryString = "?mode=register",
        });

        Assert.Equal("?mode=register&lang=en", Languages.Switch(ctx.Request, "en"));
    }

    // A browser reads //host and /\host as another site; the link has no path, so the address can't lead there.
    [Theory]
    [InlineData("//evil.example/x")]
    [InlineData("/\\evil.example")]
    public void Language_link_never_leads_to_another_site(string path)
    {
        var page = new DefaultHttpContext();
        page.Request.Path = path;
        var statusPage = new DefaultHttpContext();
        statusPage.Request.Path = "/not-found/404";
        statusPage.Features.Set<IStatusCodeReExecuteFeature>(new StatusCodeReExecuteFeature
        {
            OriginalPathBase = "",
            OriginalPath = path,
            OriginalQueryString = "",
        });

        Assert.Equal("?lang=ru", Languages.Switch(page.Request, "ru"));
        Assert.Equal("?lang=ru", Languages.Switch(statusPage.Request, "ru"));
    }
}
