using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SecretSanta.Data;
using SecretSanta.Services;

namespace SecretSanta.Pages;

public class JoinModel(AppDb db) : PageModel
{
    public Game Game { get; private set; } = null!;
    public Participant? Existing { get; private set; }
    public string? ErrorKey { get; private set; }
    public string? PasswordErrorKey { get; private set; }
    public string? LoginErrorKey { get; private set; }
    [BindProperty] public string? Name { get; set; }
    [BindProperty] public string? Password { get; set; }
    [BindProperty] public string? PasswordRepeat { get; set; }
    [BindProperty] public string? Wishlist { get; set; }
    [BindProperty] public string? NotWanted { get; set; }
    [BindProperty] public string? LoginName { get; set; }
    [BindProperty] public string? LoginPassword { get; set; }

    public async Task<IActionResult> OnGetAsync(string token)
    {
        return await LoadAsync(token) ? Page() : NotFound();
    }

    public async Task<IActionResult> OnPostAsync(string token)
    {
        if (!await LoadAsync(token)) return NotFound();
        if (Game.DrawnAt is not null) return Page();

        var name = Limits.Clean(Name, Limits.PersonName);
        if (name is null) ErrorKey = "Required";
        else if (Game.Participants.Any(p => SameName(p.Name, name))) ErrorKey = "NameTaken";
        PasswordErrorKey = Passwords.Problem(Password, PasswordRepeat);
        if (ErrorKey is not null || PasswordErrorKey is not null) return Page();

        var person = new Participant
        {
            GameId = Game.Id,
            Name = name!,
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

    // Works before and after the draw: the name finds the participant in this game, the password lets them in.
    public async Task<IActionResult> OnPostLoginAsync(string token)
    {
        if (!await LoadAsync(token)) return NotFound();

        var name = Limits.Clean(LoginName, Limits.PersonName);
        var person = name is null ? null : Game.Participants.FirstOrDefault(p => SameName(p.Name, name));
        var result = person is null || string.IsNullOrEmpty(LoginPassword)
            ? LoginResult.Wrong
            : Passwords.Check(person, LoginPassword, DateTime.UtcNow);
        await db.SaveChangesAsync();

        if (result != LoginResult.Ok)
        {
            LoginErrorKey = result switch
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
