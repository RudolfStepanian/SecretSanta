using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SecretSanta.Data;
using SecretSanta.Localization;
using SecretSanta.Services;

namespace SecretSanta.Pages;

public record ThreadView(List<Message> Messages, bool ViewerIsSanta, string OtherName);

// The participant's own page in a game. It shows whoever is signed in to that game on this device;
// anyone else is sent to the sign-in form on the invite page.
public class MeModel(AppDb db, IMailSender mailer, Notifier notifier, Text text) : PageModel
{
    // The outcomes of the e-mail form, passed back as ?mail=… so the page opens at the form after the post.
    private static readonly Dictionary<string, string> MailErrors = new()
    {
        ["invalid"] = "MailInvalid",
        ["same"] = "MailSame",
        ["taken"] = "MailUsed",
        ["wait"] = "MailWait",
        ["failed"] = "MailFailed",
    };

    public bool MailEnabled => mailer.Enabled;
    // Whoever signed up with their address signs in with it: it is linked already and can't be changed or unlinked.
    public bool SignsInWithEmail => Me.Email is not null && Me.Login == Me.Email;
    public bool MailPending => Me.PendingEmail is not null && Me.EmailSentAt > DateTime.UtcNow - MailLinks.Life;
    public string? MailErrorKey { get; private set; }
    [BindProperty] public string? Email { get; set; }
    public Participant Me { get; private set; } = null!;
    public List<Message> SantaThread { get; private set; } = [];
    public List<Message> AskThread { get; private set; } = [];
    public bool IsNew { get; private set; }
    public bool IsSaved { get; private set; }
    public bool PasswordSaved { get; private set; }
    public string? ErrorKey { get; private set; }
    public string? LoginErrorKey { get; private set; }
    public string? PasswordErrorKey { get; private set; }
    [BindProperty] public string? Name { get; set; }
    [BindProperty] public string? Wishlist { get; set; }
    [BindProperty] public string? NotWanted { get; set; }
    [BindProperty] public string? Login { get; set; }
    [BindProperty] public string? Password { get; set; }
    [BindProperty] public string? PasswordRepeat { get; set; }

    public async Task<IActionResult> OnGetAsync(string invite, bool @new = false, bool saved = false, bool password = false, string? mail = null)
    {
        if (await LoadAsync(invite) is { } elsewhere) return elsewhere;

        // The next message is mailed again (Notifier), in the language of this visit.
        Me.SeenAt = DateTime.UtcNow;
        Me.Lang = text.Lang;
        await db.SaveChangesAsync();

        IsNew = @new;
        IsSaved = saved;
        PasswordSaved = password;
        MailErrorKey = mail is not null ? MailErrors.GetValueOrDefault(mail) : null;
        FillForm();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string invite)
    {
        if (await LoadAsync(invite) is { } elsewhere) return elsewhere;

        var name = Limits.Clean(Name, Limits.PersonName);
        if (name is null)
        {
            ErrorKey = "Required";
            return Page();
        }
        if (Me.Game.Participants.Any(p => p.Id != Me.Id && string.Equals(p.Name, name, StringComparison.CurrentCultureIgnoreCase)))
        {
            ErrorKey = "NameTaken";
            return Page();
        }

        Me.Name = name;
        Me.Wishlist = Limits.Clean(Wishlist, Limits.Wishlist);
        Me.NotWanted = Limits.Clean(NotWanted, Limits.NotWanted);
        await db.SaveChangesAsync();
        return Redirect($"/play/{invite}?saved=true");
    }

    // Fills in whatever is missing for signing in: a login for those who joined before logins existed, a password
    // after an old personal link or a reset link. A new password replaces the token, so that link stops working.
    public async Task<IActionResult> OnPostCredentialsAsync(string invite)
    {
        if (await LoadAsync(invite) is { } elsewhere) return elsewhere;
        var needsLogin = Me.Login is null;
        var needsPassword = Me.PasswordHash is null;
        if (!needsLogin && !needsPassword) return Redirect($"/play/{invite}");

        var login = needsLogin ? Logins.Normalize(Login) : Me.Login;
        if (needsLogin) LoginErrorKey = login is null ? "LoginInvalid" : Me.Game.Participants.Any(p => p.Id != Me.Id && p.Login == login) ? "LoginTaken" : null;
        if (needsPassword) PasswordErrorKey = Passwords.Problem(Password, PasswordRepeat);
        if (LoginErrorKey is not null || PasswordErrorKey is not null)
        {
            FillForm();
            return Page();
        }

        Me.Login = login;
        if (needsPassword)
        {
            Passwords.Set(Me, Password!);
            Me.Token = Tokens.New();
        }
        await db.SaveChangesAsync();
        ParticipantCookie.Set(Response, Me.GameId, Me.Token);
        return Redirect($"/play/{invite}?password=true");
    }

    // Sends a confirmation link to the address. It is linked only once that link is used; until then a previously
    // linked address stays.
    public async Task<IActionResult> OnPostMailAsync(string invite)
    {
        if (await LoadAsync(invite) is { } elsewhere) return elsewhere;
        if (!mailer.Enabled || SignsInWithEmail) return NotFound();

        var email = Emails.Normalize(Email);
        var now = DateTime.UtcNow;
        var problem = email is null ? "invalid"
            : email == Me.Email ? "same"
            : Signups.Taken(Me.Game, email, except: Me.Id) ? "taken"
            : Me.EmailSentAt > now - MailLinks.Pause ? "wait"
            : null;
        if (problem is not null) return Redirect($"/play/{invite}?mail={problem}#mail");

        Me.PendingEmail = email;
        Me.EmailToken = Tokens.New();
        Me.EmailSentAt = now;
        await db.SaveChangesAsync();
        var link = Request.Abs($"/mail/{Me.EmailToken}");
        if (await mailer.SendAsync(new Mail("confirm", email!, text["MailConfirmSubject"], text.F("MailConfirmBody", Me.Name, link)), HttpContext.RequestAborted))
            return Redirect($"/play/{invite}#mail");

        // Nothing went out, so nothing waits for confirmation, and the participant can try again right away.
        Me.PendingEmail = null;
        Me.EmailToken = null;
        Me.EmailSentAt = null;
        await db.SaveChangesAsync();
        return Redirect($"/play/{invite}?mail=failed#mail");
    }

    // Unlinks the address. A reset link already mailed to it and not used yet stops working too.
    public async Task<IActionResult> OnPostMailRemoveAsync(string invite)
    {
        if (await LoadAsync(invite) is { } elsewhere) return elsewhere;
        if (SignsInWithEmail) return NotFound();

        Me.Email = null;
        Me.PendingEmail = null;
        Me.EmailToken = null;
        if (Me.ResetSentAt is not null && Me.ResetUsedAt is null) Me.ResetToken = null;
        await db.SaveChangesAsync();
        return Redirect($"/play/{invite}#mail");
    }

    public async Task<IActionResult> OnPostLogoutAsync(string invite)
    {
        if (await LoadAsync(invite) is { } elsewhere) return elsewhere;

        ParticipantCookie.Clear(Response, Me.GameId);
        return Redirect($"/join/{invite}");
    }

    public async Task<IActionResult> OnPostRevealAsync(string invite)
    {
        if (await LoadAsync(invite) is { } elsewhere) return elsewhere;

        if (Me.Game.DrawnAt is not null && Me.RevealedAt is null)
        {
            Me.RevealedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        // The card animation asks for the recipient's part of the page only now (X-Reveal), so the page holds none of it
        // before the card is opened. Without JavaScript the form post lands on the page with the card open.
        if (Request.Headers["X-Reveal"] == "1" && Me.RevealedAt is not null && Me.Recipient is not null)
        {
            Response.Headers["X-Reveal"] = "1";
            return Partial("_Reveal", this);
        }
        return Redirect($"/play/{invite}");
    }

    // Santa marks the gift for their recipient as ready or takes the mark back; only once the card is open.
    public async Task<IActionResult> OnPostGiftAsync(string invite, bool ready)
    {
        if (await LoadAsync(invite) is { } elsewhere) return elsewhere;

        if (Me.Game.DrawnAt is not null && Me.RevealedAt is not null)
        {
            Me.GiftReadyAt = ready ? Me.GiftReadyAt ?? DateTime.UtcNow : null;
            await db.SaveChangesAsync();
        }
        return Redirect($"/play/{invite}#gift");
    }

    // Santa writes to the recipient drawn for them; asking also counts as having opened the card.
    public async Task<IActionResult> OnPostAskAsync(string invite, string? text)
    {
        if (await LoadAsync(invite) is { } elsewhere) return elsewhere;

        var clean = Limits.Clean(text, Limits.Message);
        if (Me.Game.DrawnAt is not null && Me.RecipientId is { } recipientId && clean is not null)
        {
            db.Messages.Add(new Message { ParticipantId = recipientId, FromSanta = true, Text = clean, CreatedAt = DateTime.UtcNow });
            Me.RevealedAt ??= DateTime.UtcNow;
            await db.SaveChangesAsync();
            await notifier.MessageAsync(Me.Game, Me.Game.Participants.Single(p => p.Id == recipientId), fromSanta: true, Request);
        }
        return Redirect($"/play/{invite}#ask");
    }

    // The recipient answers into their own thread; who reads it is decided by the draw, never stored here.
    public async Task<IActionResult> OnPostAnswerAsync(string invite, string? text)
    {
        if (await LoadAsync(invite) is { } elsewhere) return elsewhere;

        var clean = Limits.Clean(text, Limits.Message);
        if (Me.Game.DrawnAt is not null && clean is not null)
        {
            db.Messages.Add(new Message { ParticipantId = Me.Id, FromSanta = false, Text = clean, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
            if (Me.Game.Participants.FirstOrDefault(p => p.RecipientId == Me.Id) is { } santa)
                await notifier.MessageAsync(Me.Game, santa, fromSanta: false, Request);
        }
        return Redirect($"/play/{invite}#santa");
    }

    // Null when this device is signed in to the game; otherwise where to go instead.
    private async Task<IActionResult?> LoadAsync(string invite)
    {
        var game = await db.Games.Include(g => g.Participants).SingleOrDefaultAsync(g => g.InviteToken == invite);
        if (game is null) return NotFound();

        var token = ParticipantCookie.Get(Request, game.Id);
        var me = token is null ? null : game.Participants.FirstOrDefault(p => p.Token == token);
        if (me is null) return Redirect($"/join/{invite}");

        Me = me;
        if (game.DrawnAt is not null)
        {
            var messages = await db.Messages
                .Where(m => m.ParticipantId == me.Id || m.ParticipantId == me.RecipientId)
                .OrderBy(m => m.Id)
                .ToListAsync();
            SantaThread = messages.Where(m => m.ParticipantId == me.Id).ToList();
            AskThread = messages.Where(m => m.ParticipantId == me.RecipientId).ToList();
        }
        return null;
    }

    private void FillForm()
    {
        Name = Me.Name;
        Wishlist = Me.Wishlist;
        NotWanted = Me.NotWanted;
        Email = MailPending ? Me.PendingEmail : Me.Email;
    }
}
