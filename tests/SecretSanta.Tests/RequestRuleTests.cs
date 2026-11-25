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
    public void Language_link_keeps_the_address_except_lang_and_handler()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/play/abc";
        ctx.Request.QueryString = new QueryString("?new=true&lang=hy&handler=Credentials");

        Assert.Equal("/play/abc?new=true&lang=ru", Languages.Switch(ctx.Request, "ru"));
    }

    [Fact]
    public void Language_link_on_a_status_page_keeps_the_address_that_failed()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/not-found/404";
        ctx.Features.Set<IStatusCodeReExecuteFeature>(new StatusCodeReExecuteFeature
        {
            OriginalPathBase = "",
            OriginalPath = "/join/old",
            OriginalQueryString = "?mode=register",
        });

        Assert.Equal("/join/old?mode=register&lang=en", Languages.Switch(ctx.Request, "en"));
    }
}
