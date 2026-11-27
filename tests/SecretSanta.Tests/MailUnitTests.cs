using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SecretSanta.Services;

namespace SecretSanta.Tests;

public class EmailAddressTests
{
    [Theory]
    [InlineData("ani@example.com", "ani@example.com")]
    [InlineData("  Ani.K@Mail.Example.AM ", "ani.k@mail.example.am")]
    [InlineData("ani+santa@example.co", "ani+santa@example.co")]
    public void Valid_addresses_are_stored_trimmed_in_lower_case(string typed, string stored)
    {
        Assert.Equal(stored, Emails.Normalize(typed));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ani")]
    [InlineData("ani@")]
    [InlineData("@example.com")]
    [InlineData("ani@example")]
    [InlineData("ani@example.com.")]
    [InlineData("ani@.example.com")]
    [InlineData("Ani <ani@example.com>")]
    [InlineData("ani@example.com, bob@example.com")]
    [InlineData("ani bob@example.com")]
    public void Anything_but_one_plain_address_is_rejected(string? typed)
    {
        Assert.Null(Emails.Normalize(typed));
    }

    [Fact]
    public void An_address_longer_than_254_characters_is_rejected()
    {
        Assert.Null(Emails.Normalize(new string('a', 243) + "@example.com"));
    }
}

public class MailLinkTests
{
    [Theory]
    [InlineData("https://santa.example/mail/Ab_c-1", "/mail/Ab_c-1")]
    [InlineData("https://santa.example/me/Xyz", "/me/Xyz")]
    [InlineData("https://elsewhere.example/me/Xyz", "/me/Xyz")]
    public void A_continue_url_leads_to_the_path_of_our_link_on_this_site(string continueUrl, string path)
    {
        Assert.Equal(path, MailLinks.PathOf(continueUrl));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://santa.example/admin/Xyz")]
    [InlineData("https://santa.example/mail/")]
    [InlineData("https://santa.example/mail/Xyz/more")]
    [InlineData("https://santa.example/me/X%20y")]
    [InlineData("javascript:alert(1)")]
    public void Anything_else_leads_nowhere(string? continueUrl)
    {
        Assert.Null(MailLinks.PathOf(continueUrl));
    }
}

public class FirebaseMailSenderTests
{
    private static readonly Mail Letter = new("confirm", "ani@example.com", "https://santa.example/mail/token");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_address_gets_an_account_and_Firebase_mails_it_our_link()
    {
        var calls = new List<(string Path, string Key, JsonElement Body)>();
        var sender = Sender(async request =>
        {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(request.RequestUri!.Query);
            calls.Add((request.RequestUri.AbsolutePath, query["key"].ToString(), body.RootElement.Clone()));
            return Answer(HttpStatusCode.OK, "{}");
        });

        Assert.True(await sender.SendAsync(Letter, Ct));

        Assert.Equal(new[] { "/v1/accounts:signUp", "/v1/accounts:sendOobCode" }, calls.Select(c => c.Path));
        Assert.All(calls, c => Assert.Equal("firebase-key", c.Key));
        Assert.Equal("ani@example.com", calls[0].Body.GetProperty("email").GetString());
        Assert.True(calls[0].Body.GetProperty("password").GetString()!.Length >= 32);
        Assert.Equal("PASSWORD_RESET", calls[1].Body.GetProperty("requestType").GetString());
        Assert.Equal("ani@example.com", calls[1].Body.GetProperty("email").GetString());
        Assert.Equal("https://santa.example/mail/token", calls[1].Body.GetProperty("continueUrl").GetString());
        Assert.Equal("203.0.113.7", calls[1].Body.GetProperty("userIp").GetString());
    }

    [Fact]
    public async Task An_address_that_already_has_an_account_is_mailed_too()
    {
        var sender = Sender(request => Task.FromResult(request.RequestUri!.AbsolutePath.EndsWith(":signUp")
            ? Answer(HttpStatusCode.BadRequest, """{"error":{"code":400,"message":"EMAIL_EXISTS"}}""")
            : Answer(HttpStatusCode.OK, """{"email":"ani@example.com"}""")));

        Assert.True(await sender.SendAsync(Letter, Ct));
    }

    [Theory]
    [InlineData(":signUp", HttpStatusCode.BadRequest, """{"error":{"code":400,"message":"OPERATION_NOT_ALLOWED : Password sign-in is disabled for this project."}}""")]
    [InlineData(":sendOobCode", HttpStatusCode.BadRequest, """{"error":{"code":400,"message":"UNAUTHORIZED_DOMAIN : Domain not allowlisted by project"}}""")]
    [InlineData(":sendOobCode", HttpStatusCode.BadRequest, """{"error":{"code":400,"message":"RESET_PASSWORD_EXCEED_LIMIT"}}""")]
    [InlineData(":sendOobCode", HttpStatusCode.BadGateway, "<html>Bad gateway</html>")]
    public async Task A_refusal_from_Firebase_is_a_failure(string step, HttpStatusCode status, string body)
    {
        var sender = Sender(request => Task.FromResult(request.RequestUri!.AbsolutePath.EndsWith(step)
            ? Answer(status, body)
            : Answer(HttpStatusCode.OK, "{}")));

        Assert.False(await sender.SendAsync(Letter, Ct));
    }

    [Fact]
    public async Task Firebase_that_does_not_answer_is_a_failure()
    {
        Assert.False(await Sender(_ => throw new HttpRequestException("No route to host")).SendAsync(Letter, Ct));
    }

    private static FirebaseMailSender Sender(Func<HttpRequestMessage, Task<HttpResponseMessage>> firebase)
    {
        var http = new DefaultHttpContext();
        http.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");
        return new(
            new HttpClient(new Firebase(firebase)),
            Options.Create(new FirebaseOptions { ApiKey = "firebase-key" }),
            new HttpContextAccessor { HttpContext = http },
            NullLogger<FirebaseMailSender>.Instance);
    }

    private static HttpResponseMessage Answer(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class Firebase(Func<HttpRequestMessage, Task<HttpResponseMessage>> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => answer(request);
    }
}
