using System.Net.Mail;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SecretSanta.Data;

namespace SecretSanta.Services;

// One plain-text e-mail. The log gets its kind ("confirm", "reset", "draw", "undraw" or "message"), never the address.
public sealed record Mail(string Kind, string To, string Subject, string Body);

public interface IMailSender
{
    // False when the site has no way to send mail: the pages then offer nothing that needs it.
    bool Enabled { get; }

    // True once the mail has been handed over for delivery.
    Task<bool> SendAsync(Mail mail, CancellationToken ct);
}

// Mail:ScriptUrl and Mail:Key (Mail__ScriptUrl and Mail__Key in the environment): the relay's web app URL and the key
// it checks.
public sealed class MailOptions
{
    public string? ScriptUrl { get; set; }
    public string? Key { get; set; }

    public bool Configured => !string.IsNullOrWhiteSpace(ScriptUrl) && !string.IsNullOrWhiteSpace(Key);
}

// A link sent by e-mail works for a day, and a participant gets at most one e-mail of each kind per five minutes,
// so nobody can spend the day's sending limit on one person.
public static class MailLinks
{
    public static readonly TimeSpan Life = TimeSpan.FromDays(1);
    public static readonly TimeSpan Pause = TimeSpan.FromMinutes(5);
}

// Sends through the Apps Script web app of the site's own Gmail account (tools/mail-relay.gs): an HTTPS post, because
// a free Render instance can't open SMTP connections. Gmail lets such a script mail 100 recipients a day.
public sealed class ScriptMailSender(HttpClient http, IOptions<MailOptions> options, MailQuota quota, ILogger<ScriptMailSender> logger) : IMailSender
{
    public bool Enabled => true;

    public async Task<bool> SendAsync(Mail mail, CancellationToken ct)
    {
        var settings = options.Value;
        try
        {
            // The web app answers with a redirect to its result, which the client follows.
            using var response = await http.PostAsJsonAsync(settings.ScriptUrl, new { key = settings.Key, to = mail.To, subject = mail.Subject, body = mail.Body }, ct);
            var reply = Read(await response.Content.ReadAsStringAsync(ct));
            if (response.IsSuccessStatusCode && reply is { Ok: true })
            {
                if (reply.Left is { } left) quota.Report(left, DateTime.UtcNow);
                logger.LogInformation("E-mail {Kind} sent; the relay can send {Left} more today.", mail.Kind, reply.Left);
                return true;
            }
            logger.LogWarning("E-mail {Kind} not sent: the relay answered {Status}, {Error}.", mail.Kind, (int)response.StatusCode, reply?.Error ?? "not with a result");
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            logger.LogWarning(e, "E-mail {Kind} not sent: the relay did not answer.", mail.Kind);
        }
        return false;
    }

    private static RelayReply? Read(string text)
    {
        try
        {
            return JsonSerializer.Deserialize<RelayReply>(text, JsonSerializerOptions.Web);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record RelayReply(bool Ok, string? Error, int? Left);
}

// Development without a relay: mail goes to the log, so its links can be followed by hand.
public sealed class LogMailSender(ILogger<LogMailSender> logger) : IMailSender
{
    public bool Enabled => true;

    public Task<bool> SendAsync(Mail mail, CancellationToken ct)
    {
        logger.LogInformation("E-mail {Kind} to {To}: {Subject}\n{Body}", mail.Kind, mail.To, mail.Subject, mail.Body);
        return Task.FromResult(true);
    }
}

// No relay configured: the site works as before, without e-mail.
public sealed class NoMailSender : IMailSender
{
    public bool Enabled => false;

    public Task<bool> SendAsync(Mail mail, CancellationToken ct) => Task.FromResult(false);
}

// An address is stored trimmed and in lower case, so "Ani@Mail.am" and "ani@mail.am" are the same address. Accepted is
// a single plain address with a dot in the domain: no display name, no list.
public static class Emails
{
    // The address as stored, or null when the value is not an acceptable address.
    public static string? Normalize(string? value)
    {
        var email = value?.Trim().ToLowerInvariant();
        if (email is not { Length: > 0 and <= Limits.Email }) return null;
        if (!MailAddress.TryCreate(email, out var parsed) || parsed.Address != email || parsed.DisplayName.Length > 0) return null;
        var domain = parsed.Host;
        return domain.Contains('.') && !domain.StartsWith('.') && !domain.EndsWith('.') ? email : null;
    }
}
