using System.Net.Mail;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using SecretSanta.Data;

namespace SecretSanta.Services;

// One e-mail with a link to this site: Kind "confirm" links an address, "reset" sets a new password.
// The log gets the kind, never the address.
public sealed record Mail(string Kind, string To, string Link);

public interface IMailSender
{
    // False when the site has no way to send mail: the pages then offer nothing that needs it.
    bool Enabled { get; }

    // True once the mail has been handed over for delivery.
    Task<bool> SendAsync(Mail mail, CancellationToken ct);
}

// Firebase:ApiKey (Firebase__ApiKey in the environment): the Web API key of the Firebase project that sends the mail.
public sealed class FirebaseOptions
{
    public string? ApiKey { get; set; }

    public bool Configured => !string.IsNullOrWhiteSpace(ApiKey);
}

// A link sent by e-mail works for a day, and a participant gets at most one e-mail of each kind per five minutes,
// so nobody can spend the day's sending limit on one person.
public static partial class MailLinks
{
    public static readonly TimeSpan Life = TimeSpan.FromDays(1);
    public static readonly TimeSpan Pause = TimeSpan.FromMinutes(5);

    // The path of a link this site mails (/mail/{token} or /me/{token}) inside Firebase's continueUrl, or null for
    // anything else. Only the path is kept, so the link in an e-mail can't lead off this site.
    public static string? PathOf(string? continueUrl) =>
        Uri.TryCreate(continueUrl, UriKind.Absolute, out var url) && MailedPath().IsMatch(url.AbsolutePath) ? url.AbsolutePath : null;

    [GeneratedRegex("^/(mail|me)/[A-Za-z0-9_-]+$")]
    private static partial Regex MailedPath();
}

// Firebase Authentication sends the mail, free and without a mailbox of our own: its password reset e-mail, whose text
// is set in the Firebase console (README, E-mail), carries our link as the continue URL. The link in the e-mail opens
// /auth/action, which goes on to that page of ours; Firebase's own code in it is not used.
// Firebase mails only addresses that have a Firebase account, so the address first gets one, with a random password
// that nobody uses: signing in stays on this site.
public sealed class FirebaseMailSender(HttpClient http, IOptions<FirebaseOptions> options, IHttpContextAccessor context, ILogger<FirebaseMailSender> logger) : IMailSender
{
    private const string Accounts = "https://identitytoolkit.googleapis.com/v1/accounts";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    public bool Enabled => true;

    public async Task<bool> SendAsync(Mail mail, CancellationToken ct)
    {
        var key = Uri.EscapeDataString(options.Value.ApiKey!);
        try
        {
            var account = await PostAsync($"{Accounts}:signUp?key={key}", new { email = mail.To, password = Tokens.New() + Tokens.New() }, ct);
            if (account is not (null or "EMAIL_EXISTS"))
            {
                logger.LogWarning("E-mail {Kind} not sent: Firebase refused the account, {Error}.", mail.Kind, account);
                return false;
            }

            // The participant's IP, so Firebase's abuse limits count people rather than this server.
            var ip = context.HttpContext?.Connection.RemoteIpAddress?.ToString();
            var sent = await PostAsync($"{Accounts}:sendOobCode?key={key}", new { requestType = "PASSWORD_RESET", email = mail.To, continueUrl = mail.Link, userIp = ip }, ct);
            if (sent is null)
            {
                logger.LogInformation("E-mail {Kind} sent through Firebase.", mail.Kind);
                return true;
            }
            logger.LogWarning("E-mail {Kind} not sent: Firebase answered {Error}.", mail.Kind, sent);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            logger.LogWarning(e, "E-mail {Kind} not sent: Firebase did not answer.", mail.Kind);
        }
        return false;
    }

    // Null when Firebase accepted the request; otherwise its error code, such as EMAIL_EXISTS.
    private async Task<string?> PostAsync(string url, object body, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync(url, body, Json, ct);
        if (response.IsSuccessStatusCode) return null;
        var text = await response.Content.ReadAsStringAsync(ct);
        try
        {
            using var reply = JsonDocument.Parse(text);
            var message = reply.RootElement.GetProperty("error").GetProperty("message").GetString() ?? "";
            return message.Split(" : ")[0];
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return $"HTTP {(int)response.StatusCode}";
        }
    }
}

// Development without Firebase: mail goes to the log, so its links can be followed by hand.
public sealed class LogMailSender(ILogger<LogMailSender> logger) : IMailSender
{
    public bool Enabled => true;

    public Task<bool> SendAsync(Mail mail, CancellationToken ct)
    {
        logger.LogInformation("E-mail {Kind} to {To}: {Link}", mail.Kind, mail.To, mail.Link);
        return Task.FromResult(true);
    }
}

// Firebase not configured: the site works as before, without e-mail.
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
