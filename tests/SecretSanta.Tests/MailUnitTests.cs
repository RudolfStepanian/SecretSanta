using System.Net;
using System.Text;
using System.Text.Json;
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

public class MailTextTests
{
    // The link stands on a line of its own, so mail apps make it clickable.
    [Theory]
    [InlineData("MailConfirmBody")]
    [InlineData("MailResetBody")]
    public void Every_language_greets_by_name_and_carries_the_link(string key)
    {
        Assert.All(Languages.All, lang =>
        {
            var body = string.Format(Strings.Get(key, lang), "Ani", "https://santa.test/me/token");
            Assert.Contains("Ani", body);
            Assert.Contains("\nhttps://santa.test/me/token\n", body);
        });
    }
}

public class ScriptMailSenderTests
{
    private static readonly Mail Letter = new("confirm", "ani@example.com", "Subject", "Line one\nԲարև ձեզ, «Ани»");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Posts_the_key_and_the_mail_and_trusts_an_ok_answer()
    {
        string? posted = null;
        var sender = Sender(async request =>
        {
            posted = await request.Content!.ReadAsStringAsync();
            return Answer(HttpStatusCode.OK, """{"ok":true,"left":99}""");
        });

        Assert.True(await sender.SendAsync(Letter, Ct));
        using var json = JsonDocument.Parse(posted!);
        Assert.Equal("relay-key", json.RootElement.GetProperty("key").GetString());
        Assert.Equal("ani@example.com", json.RootElement.GetProperty("to").GetString());
        Assert.Equal("Subject", json.RootElement.GetProperty("subject").GetString());
        Assert.Equal("Line one\nԲարև ձեզ, «Ани»", json.RootElement.GetProperty("body").GetString());
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, """{"ok":false,"error":"wrong key"}""")]
    [InlineData(HttpStatusCode.OK, "<html><body>Sign in</body></html>")]
    [InlineData(HttpStatusCode.InternalServerError, """{"ok":true}""")]
    public async Task Any_other_answer_is_a_failure(HttpStatusCode status, string body)
    {
        Assert.False(await Sender(_ => Task.FromResult(Answer(status, body))).SendAsync(Letter, Ct));
    }

    [Fact]
    public async Task A_relay_that_does_not_answer_is_a_failure()
    {
        Assert.False(await Sender(_ => throw new HttpRequestException("No route to host")).SendAsync(Letter, Ct));
    }

    private static ScriptMailSender Sender(Func<HttpRequestMessage, Task<HttpResponseMessage>> relay) => new(
        new HttpClient(new Relay(relay)),
        Options.Create(new MailOptions { ScriptUrl = "https://script.example/exec", Key = "relay-key" }),
        NullLogger<ScriptMailSender>.Instance);

    private static HttpResponseMessage Answer(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class Relay(Func<HttpRequestMessage, Task<HttpResponseMessage>> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => answer(request);
    }
}
