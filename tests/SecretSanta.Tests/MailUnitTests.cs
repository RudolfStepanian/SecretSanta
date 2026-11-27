using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SecretSanta.Localization;
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

public class FirebaseMailSenderTests
{
    private static readonly Mail Letter = new("confirm", "ani@example.com", "https://santa.example/mail/token");
    private static readonly FirebaseAccounts Accounts = new(Enumerable.Range(1, 32).Select(i => (byte)i).ToArray());

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void Each_address_has_its_own_password_that_stays_the_same()
    {
        var password = Accounts.PasswordFor("ani@example.com");

        Assert.Equal(password, Accounts.PasswordFor("ani@example.com"));
        Assert.NotEqual(password, Accounts.PasswordFor("bob@example.com"));
        Assert.NotEqual(password, new FirebaseAccounts(new byte[32]).PasswordFor("ani@example.com"));
        Assert.True(password.Length >= 32);
    }

    [Fact]
    public async Task A_new_address_gets_an_account_and_Firebase_mails_it_our_link()
    {
        var calls = new List<Call>();
        var sender = Sender(calls, "ru", request => request.Path.EndsWith(":signUp")
            ? Answer(HttpStatusCode.OK, """{"idToken":"token-1","email":"ani@example.com"}""")
            : Answer(HttpStatusCode.OK, """{"email":"ani@example.com"}"""));

        Assert.True(await sender.SendAsync(Letter, Ct));

        Assert.Equal(new[] { "/v1/accounts:signUp", "/v1/accounts:sendOobCode" }, calls.Select(c => c.Path));
        Assert.All(calls, c => Assert.Equal("firebase-key", c.Key));
        Assert.Equal("ani@example.com", calls[0].Body.GetProperty("email").GetString());
        Assert.Equal(Accounts.PasswordFor("ani@example.com"), calls[0].Body.GetProperty("password").GetString());
        Assert.Equal("VERIFY_EMAIL", calls[1].Body.GetProperty("requestType").GetString());
        Assert.Equal("token-1", calls[1].Body.GetProperty("idToken").GetString());
        Assert.Equal("https://santa.example/mail/token", calls[1].Body.GetProperty("continueUrl").GetString());
        Assert.Equal("ru", calls[1].Locale);
    }

    [Fact]
    public async Task An_address_that_has_an_account_signs_in_to_it_with_the_same_password()
    {
        var calls = new List<Call>();
        var sender = Sender(calls, "hy", request => request.Path switch
        {
            _ when request.Path.EndsWith(":signUp") => Answer(HttpStatusCode.BadRequest, """{"error":{"code":400,"message":"EMAIL_EXISTS"}}"""),
            _ when request.Path.EndsWith(":signInWithPassword") => Answer(HttpStatusCode.OK, """{"idToken":"token-2"}"""),
            _ => Answer(HttpStatusCode.OK, "{}"),
        });

        Assert.True(await sender.SendAsync(Letter, Ct));

        Assert.Equal(new[] { "/v1/accounts:signUp", "/v1/accounts:signInWithPassword", "/v1/accounts:sendOobCode" }, calls.Select(c => c.Path));
        Assert.Equal(calls[0].Body.GetProperty("password").GetString(), calls[1].Body.GetProperty("password").GetString());
        Assert.Equal("token-2", calls[2].Body.GetProperty("idToken").GetString());
        Assert.Equal("hy", calls[2].Locale);
    }

    [Theory]
    [InlineData(":signUp", HttpStatusCode.BadRequest, """{"error":{"code":400,"message":"OPERATION_NOT_ALLOWED : Password sign-in is disabled for this project."}}""")]
    [InlineData(":signInWithPassword", HttpStatusCode.BadRequest, """{"error":{"code":400,"message":"INVALID_LOGIN_CREDENTIALS"}}""")]
    [InlineData(":sendOobCode", HttpStatusCode.BadRequest, """{"error":{"code":400,"message":"UNAUTHORIZED_DOMAIN : Domain not allowlisted by project"}}""")]
    [InlineData(":sendOobCode", HttpStatusCode.BadRequest, """{"error":{"code":400,"message":"TOO_MANY_ATTEMPTS_TRY_LATER"}}""")]
    [InlineData(":sendOobCode", HttpStatusCode.BadGateway, "<html>Bad gateway</html>")]
    public async Task A_refusal_from_Firebase_is_a_failure(string step, HttpStatusCode status, string body)
    {
        var sender = Sender([], "en", request => request.Path.EndsWith(step)
            ? Answer(status, body)
            : request.Path.EndsWith(":signUp")
                ? Answer(HttpStatusCode.BadRequest, """{"error":{"code":400,"message":"EMAIL_EXISTS"}}""")
                : Answer(HttpStatusCode.OK, """{"idToken":"token-3"}"""));

        Assert.False(await sender.SendAsync(Letter, Ct));
    }

    [Fact]
    public async Task Firebase_that_does_not_answer_is_a_failure()
    {
        Assert.False(await Sender([], "en", _ => throw new HttpRequestException("No route to host")).SendAsync(Letter, Ct));
    }

    private sealed record Call(string Path, string Key, string? Locale, JsonElement Body);

    private static FirebaseMailSender Sender(List<Call> calls, string lang, Func<Call, HttpResponseMessage> firebase)
    {
        var http = new DefaultHttpContext();
        http.Items[Languages.ItemKey] = lang;
        return new(
            new HttpClient(new Firebase(calls, firebase)),
            Options.Create(new FirebaseOptions { ApiKey = "firebase-key" }),
            Accounts,
            new HttpContextAccessor { HttpContext = http },
            NullLogger<FirebaseMailSender>.Instance);
    }

    private static HttpResponseMessage Answer(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class Firebase(List<Call> calls, Func<Call, HttpResponseMessage> answer) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(request.RequestUri!.Query);
            var call = new Call(
                request.RequestUri.AbsolutePath,
                query["key"].ToString(),
                request.Headers.TryGetValues("X-Firebase-Locale", out var locale) ? locale.Single() : null,
                body.RootElement.Clone());
            calls.Add(call);
            return answer(call);
        }
    }
}
