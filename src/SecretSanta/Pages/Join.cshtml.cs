using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SecretSanta.Data;
using SecretSanta.Services;

namespace SecretSanta.Pages;

// The invite link: a sign-in page with a sign-up tab (?mode=register) while the draw hasn't happened yet.
public class JoinModel(AppDb db, IMailSender mailer) : PageModel
{
    public Game Game { get; private set; } = null!;
    public Participant? Existing { get; private set; }
    public bool Registering { get; private set; }
    public bool Full { get; private set; }
    public bool MailEnabled => mailer.Enabled;
    public bool ForgotSent { get; private set; }
    public string? LoginErrorKey { get; private set; }
    public string? PasswordErrorKey { get; private set; }
    public string? NameErrorKey { get; private set; }
    public string? SignInErrorKey { get; private set; }
    [BindProperty] public string? Login { get; set; }
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

        var login = Logins.Normalize(Login);
        LoginErrorKey = login is null ? "LoginInvalid" : Game.Participants.Any(p => p.Login == login) ? "LoginTaken" : null;
        PasswordErrorKey = Passwords.Problem(Password, PasswordRepeat);
        var name = Limits.Clean(Name, Limits.PersonName);
        NameErrorKey = name is null ? "Required" : Game.Participants.Any(p => SameName(p.Name, name)) ? "NameTaken" : null;
        if (LoginErrorKey is not null || PasswordErrorKey is not null || NameErrorKey is not null) return Page();

        var person = new Participant
        {
            GameId = Game.Id,
            Name = name!,
            Login = login,
            Wishlist = Limits.Clean(Wishlist, Limits.Wishlist),
            NotWanted = Limits.Clean(NotWanted, Limits.NotWanted),
            Token = Tokens.New(),
            CreatedAt = DateTime.UtcNow,
        };
        Passwords.Set(person, Password!);
        db.Participants.Add(person);
        await db.SaveChangesAsync();

        ParticipantCookie.Set(Response, Game.Id, person.Token);
        return Redirect($"/play/{Game.InviteToken}?new=true");
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
            if (!await mailer.SendAsync(new Mail("reset", email, Request.Abs($"/me/{person.ResetToken}")), HttpContext.RequestAborted))
            {
                // Nothing went out: the earlier link, if any, keeps working and the participant can ask again right away.
                (person.ResetToken, person.ResetUsedAt, person.ResetSentAt) = (oldToken, oldUsedAt, oldSentAt);
                await db.SaveChangesAsync();
            }
        }
        return Redirect($"/join/{token}?forgot=true");
    }

    // By login, or by name for those who joined before logins existed and haven't chosen one yet.
    private Participant? Find(string? typed)
    {
        var clean = Limits.Clean(typed, Limits.PersonName);
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
