using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SecretSanta.Data;
using SecretSanta.Services;

namespace SecretSanta.Pages;

public class MeModel(AppDb db) : PageModel
{
    public Participant Me { get; private set; } = null!;
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

    private async Task<bool> LoadAsync(string token)
    {
        var me = await db.Participants
            .Include(p => p.Game).ThenInclude(g => g.Participants)
            .Include(p => p.Recipient)
            .SingleOrDefaultAsync(p => p.Token == token);
        if (me is null) return false;

        Me = me;
        return true;
    }
}
