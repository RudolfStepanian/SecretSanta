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
    [BindProperty] public string? Name { get; set; }
    [BindProperty] public string? Wishlist { get; set; }
    [BindProperty] public string? NotWanted { get; set; }

    public async Task<IActionResult> OnGetAsync(string token)
    {
        return await LoadAsync(token) ? Page() : NotFound();
    }

    public async Task<IActionResult> OnPostAsync(string token)
    {
        if (!await LoadAsync(token)) return NotFound();
        if (Game.DrawnAt is not null) return Page();

        var name = Limits.Clean(Name, Limits.PersonName);
        if (name is null)
        {
            ErrorKey = "Required";
            return Page();
        }
        if (Game.Participants.Any(p => string.Equals(p.Name, name, StringComparison.CurrentCultureIgnoreCase)))
        {
            ErrorKey = "NameTaken";
            return Page();
        }

        var person = new Participant
        {
            GameId = Game.Id,
            Name = name,
            Wishlist = Limits.Clean(Wishlist, Limits.Wishlist),
            NotWanted = Limits.Clean(NotWanted, Limits.NotWanted),
            Token = Tokens.New(),
            CreatedAt = DateTime.UtcNow,
        };
        db.Participants.Add(person);
        await db.SaveChangesAsync();

        ParticipantCookie.Set(Response, Game.Id, person.Token);
        return Redirect($"/me/{person.Token}?new=true");
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
}
