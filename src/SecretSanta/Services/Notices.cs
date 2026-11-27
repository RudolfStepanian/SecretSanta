using System.Threading.Channels;
using SecretSanta.Data;
using SecretSanta.Localization;

namespace SecretSanta.Services;

// E-mails about game events wait here, so pages never wait for the relay; MailDelivery sends them in the background.
public sealed class MailQueue
{
    private readonly Channel<Mail> channel = Channel.CreateUnbounded<Mail>();

    public ChannelReader<Mail> Reader => channel.Reader;

    public void Enqueue(Mail mail) => channel.Writer.TryWrite(mail);
}

// What the relay said last about the day's sending limit. Notices stop while fewer than Reserve e-mails are left, so
// confirmations and password resets still go out. A figure counts for an hour; after that the next notice is tried,
// and the relay's answer to it brings a new figure.
public sealed class MailQuota
{
    public const int Reserve = 20;
    private volatile Reading? last;

    public void Report(int left, DateTime at) => last = new Reading(left, at);

    public bool LowAt(DateTime now) => last is { } r && r.Left < Reserve && now - r.At < TimeSpan.FromHours(1);

    private sealed record Reading(int Left, DateTime At);
}

// Turns game events into e-mails to the participants with a linked address, each in the language they last opened
// their page in (until then, in the language of whoever caused the event). The texts name nobody.
public sealed class Notifier(AppDb db, MailQueue queue, IMailSender mailer)
{
    public bool Enabled => mailer.Enabled;

    public void DrawDone(Game game, HttpRequest request) => ToEveryone(game, "draw", "MailDrawSubject", "MailDrawBody", request);

    public void DrawCancelled(Game game, HttpRequest request) => ToEveryone(game, "undraw", "MailUndrawSubject", "MailUndrawBody", request);

    // A new message is mailed only if the reader opened their page since the last such e-mail, so a run of messages
    // makes one e-mail.
    public async Task MessageAsync(Game game, Participant reader, bool fromSanta, HttpRequest request)
    {
        if (!mailer.Enabled || reader.Email is null) return;
        if (reader.MessageMailAt is { } mailed && !(reader.SeenAt > mailed)) return;

        reader.MessageMailAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        var (subject, body, part) = fromSanta
            ? ("MailSantaWroteSubject", "MailSantaWroteBody", "santa")
            : ("MailRecipientWroteSubject", "MailRecipientWroteBody", "ask");
        Enqueue(reader, "message", subject, body, request.Abs($"/play/{game.InviteToken}#{part}"), request);
    }

    private void ToEveryone(Game game, string kind, string subject, string body, HttpRequest request)
    {
        if (!mailer.Enabled) return;

        var link = request.Abs($"/play/{game.InviteToken}");
        foreach (var person in game.Participants.Where(p => p.Email is not null)) Enqueue(person, kind, subject, body, link, request);
    }

    private void Enqueue(Participant to, string kind, string subject, string body, string link, HttpRequest request)
    {
        var lang = to.Lang ?? request.HttpContext.Items[Languages.ItemKey] as string ?? Languages.Default;
        var text = string.Format(Strings.Get(body, lang), to.Name, link) + "\n\n" + Strings.Get("MailNoticeFooter", lang);
        queue.Enqueue(new Mail(kind, to.Email!, Strings.Get(subject, lang), text));
    }
}

// Sends the queued e-mails one at a time. A failure is logged and the e-mail dropped: a missed notice is not worth a
// retry queue.
public sealed class MailDelivery(MailQueue queue, MailQuota quota, IServiceScopeFactory scopes, ILogger<MailDelivery> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        try
        {
            await foreach (var mail in queue.Reader.ReadAllAsync(stop))
            {
                try
                {
                    await DeliverAsync(mail, stop);
                }
                catch (Exception e) when (e is not OperationCanceledException || !stop.IsCancellationRequested)
                {
                    logger.LogWarning(e, "E-mail {Kind} not sent.", mail.Kind);
                }
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
        }
    }

    // False when the e-mail was skipped or not sent.
    public async Task<bool> DeliverAsync(Mail mail, CancellationToken stop)
    {
        if (quota.LowAt(DateTime.UtcNow))
        {
            logger.LogWarning("E-mail {Kind} skipped: the relay has fewer than {Reserve} e-mails left today, kept for confirmations and password resets.", mail.Kind, MailQuota.Reserve);
            return false;
        }

        using var scope = scopes.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IMailSender>().SendAsync(mail, stop);
    }
}
