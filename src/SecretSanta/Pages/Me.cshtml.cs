using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SecretSanta.Data;
using SecretSanta.Services;

namespace SecretSanta.Pages;

public record ThreadView(List<Message> Messages, bool ViewerIsSanta, string OtherName);

// The participant's own page in a game. It shows whoever is signed in to that game on this device;
// anyone else is sent to the sign-in form on the invite page.
public class MeModel(AppDb db) : PageModel
{
    public Participant Me { get; private set; } = null!;
    public List<Message> SantaThread { get; private set; } = [];
    public List<Message> AskThread { get; private set; } = [];
    public bool IsNew { get; private set; }
    public bool IsSaved { get; private set; }
    public bool PasswordSaved { get; private set; }
    public string? ErrorKey { get; private set; }
    public string? PasswordErrorKey { get; private set; }
    [BindProperty] public string? Name { get; set; }
    [BindProperty] public string? Wishlist { get; set; }
    [BindProperty] public string? NotWanted { get; set; }
    [BindProperty] public string? Password { get; set; }
    [BindProperty] public string? PasswordRepeat { get; set; }

    public async Task<IActionResult> OnGetAsync(string invite, bool @new = false, bool saved = false, bool password = false)
    {
        if (await LoadAsync(invite) is { } elsewhere) return elsewhere;

        IsNew = @new;
        IsSaved = saved;
        PasswordSaved = password;
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

    // For someone who came in by an old personal link or a reset link: the new password replaces the token,
    // so that link stops working and only the name and password (or this device) get them back in.
    public async Task<IActionResult> OnPostPasswordAsync(string invite)
    {
        if (await LoadAsync(invite) is { } elsewhere) return elsewhere;
        if (Me.PasswordHash is not null) return Redirect($"/play/{invite}");

        PasswordErrorKey = Passwords.Problem(Password, PasswordRepeat);
        if (PasswordErrorKey is not null)
        {
            FillForm();
            return Page();
        }

        Passwords.Set(Me, Password!);
        Me.Token = Tokens.New();
        await db.SaveChangesAsync();
        ParticipantCookie.Set(Response, Me.GameId, Me.Token);
        return Redirect($"/play/{invite}?password=true");
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
        return Redirect($"/play/{invite}");
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
    }
}
