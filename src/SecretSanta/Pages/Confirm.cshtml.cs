using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SecretSanta.Data;
using SecretSanta.Services;

namespace SecretSanta.Pages;

// /mail/{token}: the link from a confirmation e-mail. Opening it only shows a button, because mail scanners open links
// too; the button links the address. The link works for a day and only once.
public class ConfirmModel(AppDb db) : PageModel
{
    public string Address { get; private set; } = "";
    public string Name { get; private set; } = "";
    public string Invite { get; private set; } = "";
    public bool Expired { get; private set; }
    public bool Taken { get; private set; }
    public bool Done { get; private set; }

    public async Task<IActionResult> OnGetAsync(string token) => await LoadAsync(token) is null ? NotFound() : Page();

    public async Task<IActionResult> OnPostAsync(string token)
    {
        var person = await LoadAsync(token);
        if (person is null) return NotFound();
        if (Expired) return Page();
        Taken = Signups.Taken(person.Game, person.PendingEmail!, except: person.Id);
        if (Taken) return Page();

        person.Email = person.PendingEmail;
        person.PendingEmail = null;
        person.EmailToken = null;
        await db.SaveChangesAsync();
        Done = true;
        return Page();
    }

    private async Task<Participant?> LoadAsync(string token)
    {
        var person = await db.Participants.Include(p => p.Game).ThenInclude(g => g.Participants).SingleOrDefaultAsync(p => p.EmailToken == token);
        if (person?.PendingEmail is null) return null;

        Address = person.PendingEmail;
        Name = person.Name;
        Invite = person.Game.InviteToken;
        Expired = !(person.EmailSentAt > DateTime.UtcNow - MailLinks.Life);
        return person;
    }
}
