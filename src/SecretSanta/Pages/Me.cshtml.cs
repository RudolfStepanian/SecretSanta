using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SecretSanta.Data;
using SecretSanta.Services;

namespace SecretSanta.Pages;

public record ThreadView(List<Message> Messages, bool ViewerIsSanta, string OtherName);

public class MeModel(AppDb db) : PageModel
{
    public Participant Me { get; private set; } = null!;
    public List<Message> SantaThread { get; private set; } = [];
    public List<Message> AskThread { get; private set; } = [];
    public bool IsNew { get; private set; }
    public bool IsSaved { get; private set; }
    public string? ErrorKey { get; private set; }
    [BindProperty] public string? Name { get; set; }
    [BindProperty] public string? Wishlist { get; set; }
    [BindProperty] public string? NotWanted { get; set; }

    public async Task<IActionResult> OnGetAsync(string token, bool @new = false, bool saved = false)
    {
        if (!await LoadAsync(token)) return NotFound();

        ParticipantCookie.Set(Response, Me.GameId, Me.Token);
        IsNew = @new;
        IsSaved = saved;
        Name = Me.Name;
        Wishlist = Me.Wishlist;
        NotWanted = Me.NotWanted;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string token)
    {
        if (!await LoadAsync(token)) return NotFound();

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
        return Redirect($"/me/{token}?saved=true");
    }

    public async Task<IActionResult> OnPostRevealAsync(string token)
    {
        if (!await LoadAsync(token)) return NotFound();

        if (Me.Game.DrawnAt is not null && Me.RevealedAt is null)
        {
            Me.RevealedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        return Redirect($"/me/{token}");
    }

    // Santa writes to the recipient drawn for them; asking also counts as having opened the card.
    public async Task<IActionResult> OnPostAskAsync(string token, string? text)
    {
        if (!await LoadAsync(token)) return NotFound();

        var clean = Limits.Clean(text, Limits.Message);
        if (Me.Game.DrawnAt is not null && Me.RecipientId is { } recipientId && clean is not null)
        {
            db.Messages.Add(new Message { ParticipantId = recipientId, FromSanta = true, Text = clean, CreatedAt = DateTime.UtcNow });
            Me.RevealedAt ??= DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        return Redirect($"/me/{token}#ask");
    }

    // The recipient answers into their own thread; who reads it is decided by the draw, never stored here.
    public async Task<IActionResult> OnPostAnswerAsync(string token, string? text)
    {
        if (!await LoadAsync(token)) return NotFound();

        var clean = Limits.Clean(text, Limits.Message);
        if (Me.Game.DrawnAt is not null && clean is not null)
        {
            db.Messages.Add(new Message { ParticipantId = Me.Id, FromSanta = false, Text = clean, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        return Redirect($"/me/{token}#santa");
    }

    private async Task<bool> LoadAsync(string token)
    {
        var me = await db.Participants
            .Include(p => p.Game).ThenInclude(g => g.Participants)
            .Include(p => p.Recipient)
            .SingleOrDefaultAsync(p => p.Token == token);
        if (me is null) return false;

        Me = me;
        if (me.Game.DrawnAt is not null)
        {
            var messages = await db.Messages
                .Where(m => m.ParticipantId == me.Id || m.ParticipantId == me.RecipientId)
                .OrderBy(m => m.Id)
                .ToListAsync();
            SantaThread = messages.Where(m => m.ParticipantId == me.Id).ToList();
            AskThread = messages.Where(m => m.ParticipantId == me.RecipientId).ToList();
        }
        return true;
    }
}
