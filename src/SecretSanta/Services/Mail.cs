using System.Buffers.Text;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SecretSanta.Data;
using SecretSanta.Localization;

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
public static class MailLinks
{
    public static readonly TimeSpan Life = TimeSpan.FromDays(1);
    public static readonly TimeSpan Pause = TimeSpan.FromMinutes(5);
}

// Turns an address into the password of its Firebase account, so the site can sign in to that account again whenever
// Firebase is to mail it. The key is created on the first use and kept in the database, like the VAPID keys.
public sealed class FirebaseAccounts(byte[] key)
{
    private const string Name = "FirebaseAccountKey";

    public static FirebaseAccounts LoadOrCreate(AppDb db)
    {
        var saved = db.Settings.SingleOrDefault(s => s.Key == Name);
        if (saved is not null) return new FirebaseAccounts(Base64Url.DecodeFromChars(saved.Value));

        var key = RandomNumberGenerator.GetBytes(32);
        db.Settings.Add(new AppSetting { Key = Name, Value = Base64Url.EncodeToString(key) });
        db.SaveChanges();
        return new FirebaseAccounts(key);
    }

    public string PasswordFor(string email) => Base64Url.EncodeToString(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(email)));
}

// Firebase Authentication sends the mail, free and without a mailbox of our own. Its e-mail texts and the page its links
// open can't be changed for this project, so every e-mail is Firebase's standard "verify your e-mail address" one: the
// link opens Firebase's page, which confirms the address and offers Continue to our link (the continue URL). That
// e-mail asks for no password, unlike Firebase's own password reset.
// Firebase mails only addresses that have an account, and only when asked with that account's ID token, so the address
// gets an account first, with a password made by FirebaseAccounts that nobody types: signing in stays on this site.
public sealed class FirebaseMailSender(HttpClient http, IOptions<FirebaseOptions> options, FirebaseAccounts accounts, IHttpContextAccessor context, ILogger<FirebaseMailSender> logger) : IMailSender
{
    private const string Accounts = "https://identitytoolkit.googleapis.com/v1/accounts";

    public bool Enabled => true;

    public async Task<bool> SendAsync(Mail mail, CancellationToken ct)
    {
        var key = Uri.EscapeDataString(options.Value.ApiKey!);
        var account = new { email = mail.To, password = accounts.PasswordFor(mail.To), returnSecureToken = true };
        try
        {
            var (idToken, error) = await PostAsync($"{Accounts}:signUp?key={key}", account, null, ct);
            if (error == "EMAIL_EXISTS") (idToken, error) = await PostAsync($"{Accounts}:signInWithPassword?key={key}", account, null, ct);
            if (idToken is null)
            {
                logger.LogWarning("E-mail {Kind} not sent: no Firebase account for the address, {Error}.", mail.Kind, error ?? "no ID token");
                return false;
            }

            // Firebase's e-mail and its page in the language of the page the participant asked on.
            var locale = context.HttpContext?.Items[Languages.ItemKey] as string;
            var (_, sent) = await PostAsync($"{Accounts}:sendOobCode?key={key}", new { requestType = "VERIFY_EMAIL", idToken, continueUrl = mail.Link }, locale, ct);
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

    // Firebase's answer: the ID token it returned, if any, or the code of its error, such as EMAIL_EXISTS.
    private async Task<(string? IdToken, string? Error)> PostAsync(string url, object body, string? locale, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        if (locale is not null) request.Headers.Add("X-Firebase-Locale", locale);
        using var response = await http.SendAsync(request, ct);
        var text = await response.Content.ReadAsStringAsync(ct);
        try
        {
            using var reply = JsonDocument.Parse(text);
            if (response.IsSuccessStatusCode)
                return (reply.RootElement.TryGetProperty("idToken", out var token) ? token.GetString() : null, null);
            var message = reply.RootElement.GetProperty("error").GetProperty("message").GetString() ?? "";
            return (null, message.Split(" : ")[0]);
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return (null, response.IsSuccessStatusCode ? null : $"HTTP {(int)response.StatusCode}");
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
