using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SecretSanta.Data;
using SecretSanta.Localization;
using SecretSanta.Services;

namespace SecretSanta.Pages;

// The invite link: a sign-in page with a sign-up tab (?mode=register) while the draw hasn't happened yet.
public class JoinModel(AppDb db, IMailSender mailer, Text text) : PageModel
{
    public Game Game { get; private set; } = null!;
    public Participant? Existing { get; private set; }
    public bool Registering { get; private set; }
    public bool Full { get; private set; }
    public bool MailEnabled => mailer.Enabled;
    public bool ForgotSent { get; private set; }
    public string? EmailErrorKey { get; private set; }
    public string? PasswordErrorKey { get; private set; }
    public string? NameErrorKey { get; private set; }
    public string? SignInErrorKey { get; private set; }
    // The address the sign-up e-mail just went to, shown once after the redirect.
    [TempData] public string? SignupSentTo { get; set; }
    [BindProperty] public string? Email { get; set; }
    [BindProperty] public string? Password { get; set; }
    [BindProperty] public string? PasswordRepeat { get; set; }
    [BindProperty] public string? Name { get; set; }
    [BindProperty] public string? Wishlist { get; set; }
    [BindProperty] public string? NotWanted { get; set; }
    [BindProperty] public string? SignInLogin { get; set; }
    [BindProperty] public string? SignInPassword { get; set; }
    [BindProperty] public string? ForgotLogin { get; set; }

    public async Task<IActionResult> OnGetAsync(string token, string? mode, bool forgot = false)
    {
        if (!await LoadAsync(token)) return NotFound();
        Registering = mode == "register" && Game.DrawnAt is null;
        ForgotSent = forgot && !Registering;
        return Page();
    }

    // Signs the person up with their e-mail address as the login. With mail on, they join only through the link mailed
    // to the address (Pages/Signup); without a relay nothing can confirm it, so they join right away.
    public async Task<IActionResult> OnPostAsync(string token)
    {
        if (!await LoadAsync(token)) return NotFound();
        if (Game.DrawnAt is not null) return Page();
        Registering = true;
        if (Game.Participants.Count >= Limits.MaxParticipants)
        {
            Full = true;
            return Page();
        }

        var email = Emails.Normalize(Email);
        EmailErrorKey = email is null ? "MailInvalid" : Signups.Taken(Game, email) ? "MailTaken" : null;
        PasswordErrorKey = Passwords.Problem(Password, PasswordRepeat);
        var name = Limits.Clean(Name, Limits.PersonName);
        NameErrorKey = name is null ? "Required" : Game.Participants.Any(p => SameName(p.Name, name)) ? "NameTaken" : null;
        if (EmailErrorKey is not null || PasswordErrorKey is not null || NameErrorKey is not null) return Page();

        var signup = new Signup
        {
            GameId = Game.Id,
            Email = email!,
            Name = name!,
            PasswordHash = Passwords.Hash(Password!),
            Wishlist = Limits.Clean(Wishlist, Limits.Wishlist),
            NotWanted = Limits.Clean(NotWanted, Limits.NotWanted),
            Lang = text.Lang,
            Token = Tokens.New(),
            SentAt = DateTime.UtcNow,
        };
        if (!mailer.Enabled)
        {
            var person = Signups.Admit(signup);
            person.Email = null; // nothing confirmed it
            db.Participants.Add(person);
            await db.SaveChangesAsync();
            ParticipantCookie.Set(Response, Game.Id, person.Token);
            return Redirect($"/play/{Game.InviteToken}?new=true");
        }

        // One sign-up per address and game: signing up again replaces it, at most once every few minutes.
        var earlier = await db.Signups.Where(s => s.GameId == Game.Id && s.Email == email).Select(s => (DateTime?)s.SentAt).SingleOrDefaultAsync();
        if (earlier > signup.SentAt - MailLinks.Pause)
        {
            EmailErrorKey = "MailWait";
            return Page();
        }
        await db.Signups.Where(s => s.GameId == Game.Id && s.Email == email).ExecuteDeleteAsync();
        db.Signups.Add(signup);
        await db.SaveChangesAsync();
        // The link carries the language of the sign-up, so its page opens in it on any device.
        var link = Request.Abs($"/signup/{signup.Token}?lang={text.Lang}");
        if (!await mailer.SendAsync(new Mail("signup", email!, text["MailConfirmSubject"], text.F("MailSignupBody", name!, link)), HttpContext.RequestAborted))
        {
            // Nothing went out, so nothing waits for confirmation, and the person can try again right away.
            db.Signups.Remove(signup);
            await db.SaveChangesAsync();
            EmailErrorKey = "MailFailed";
            return Page();
        }
        SignupSentTo = email;
        return Redirect($"/join/{Game.InviteToken}?mode=register");
    }

    // Works before and after the draw. Participants who joined before logins existed sign in with their name
    // until they choose a login on their page.
    public async Task<IActionResult> OnPostSignInAsync(string token)
    {
        if (!await LoadAsync(token)) return NotFound();

        var person = Find(SignInLogin);
        var result = person is null || string.IsNullOrEmpty(SignInPassword)
            ? LoginResult.Wrong
            : Passwords.Check(person, SignInPassword, DateTime.UtcNow);
        await db.SaveChangesAsync();

        if (result != LoginResult.Ok)
        {
            SignInErrorKey = result switch
            {
                LoginResult.Locked => "LoginLocked",
                LoginResult.NoPassword => "LoginNoPassword",
                _ => "LoginFailed",
            };
            if (person is null && Emails.Normalize(SignInLogin) is { } email
                && await db.Signups.AnyAsync(s => s.GameId == Game.Id && s.Email == email))
                SignInErrorKey = "SignupPending";
            return Page();
        }

        ParticipantCookie.Set(Response, Game.Id, person!.Token);
        return Redirect($"/play/{Game.InviteToken}");
    }

    // Mails a one-time link for a new password to the address linked to the login. Asking changes nothing else: the old
    // password works until the link is used. The answer is the same whether or not the login exists or has an address,
    // so the form tells nobody which logins have one.
    public async Task<IActionResult> OnPostForgotAsync(string token)
    {
        if (!await LoadAsync(token)) return NotFound();
        if (!mailer.Enabled) return NotFound();

        var person = Find(ForgotLogin);
        var now = DateTime.UtcNow;
        if (person?.Email is { } email && !(person.ResetSentAt > now - MailLinks.Pause))
        {
            var (oldToken, oldUsedAt, oldSentAt) = (person.ResetToken, person.ResetUsedAt, person.ResetSentAt);
            person.ResetToken = Tokens.New();
            person.ResetUsedAt = null;
            person.ResetSentAt = now;
            await db.SaveChangesAsync();
            var link = Request.Abs($"/me/{person.ResetToken}");
            if (!await mailer.SendAsync(new Mail("reset", email, text["MailResetSubject"], text.F("MailResetBody", person.Name, link)), HttpContext.RequestAborted))
            {
                // Nothing went out: the earlier link, if any, keeps working and the participant can ask again right away.
                (person.ResetToken, person.ResetUsedAt, person.ResetSentAt) = (oldToken, oldUsedAt, oldSentAt);
                await db.SaveChangesAsync();
            }
        }
        return Redirect($"/join/{token}?forgot=true");
    }

    // By login (the e-mail address for everyone who signed up with one), or by name for those who joined before logins
    // existed and haven't chosen one yet.
    private Participant? Find(string? typed)
    {
        var clean = Limits.Clean(typed, Limits.Email);
        if (clean is null) return null;
        var login = clean.ToLowerInvariant();
        return Game.Participants.FirstOrDefault(p => p.Login == login)
            ?? Game.Participants.FirstOrDefault(p => p.Login is null && SameName(p.Name, clean));
    }

    private async Task<bool> LoadAsync(string token)
    {
        var game = await db.Games.Include(g => g.Participants).SingleOrDefaultAsync(g => g.InviteToken == token);
        if (game is null) return false;

        Game = game;
        var mine = ParticipantCookie.Get(Request, game.Id);
        Existing = mine is null ? null : game.Participants.FirstOrDefault(p => p.Token == mine);
        return true;
    }

    private static bool SameName(string a, string b) => string.Equals(a, b, StringComparison.CurrentCultureIgnoreCase);
}
