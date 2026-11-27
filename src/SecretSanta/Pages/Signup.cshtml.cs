using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SecretSanta.Data;
using SecretSanta.Services;

namespace SecretSanta.Pages;

// /signup/{token}: the link from a sign-up e-mail. Opening it only shows a button, because mail scanners open links
// too; the button makes the person a participant and signs this device in. The link works for a day and only once.
public class SignupModel(AppDb db) : PageModel
{
    public Signup Pending { get; private set; } = null!;
    public bool Expired { get; private set; }
    public string? ProblemKey { get; private set; }

    public async Task<IActionResult> OnGetAsync(string token) => await LoadAsync(token) ? Page() : NotFound();

    public async Task<IActionResult> OnPostAsync(string token)
    {
        if (!await LoadAsync(token)) return NotFound();
        if (Expired) return Page();

        ProblemKey = Signups.Problem(Pending.Game, Pending);
        if (ProblemKey is not null) return Page();

        var person = Signups.Admit(Pending);
        db.Participants.Add(person);
        db.Signups.Remove(Pending);
        await db.SaveChangesAsync();
        ParticipantCookie.Set(Response, person.GameId, person.Token);
        return Redirect($"/play/{Pending.Game.InviteToken}?new=true");
    }

    private async Task<bool> LoadAsync(string token)
    {
        var signup = await db.Signups.Include(s => s.Game).ThenInclude(g => g.Participants).SingleOrDefaultAsync(s => s.Token == token);
        if (signup is null) return false;

        Pending = signup;
        Expired = !(signup.SentAt > DateTime.UtcNow - MailLinks.Life);
        return true;
    }
}
