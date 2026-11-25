using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SecretSanta.Data;
using SecretSanta.Services;

namespace SecretSanta.Pages;

// /me/{token} is either the one-time link from a password reset or a personal link from before passwords.
// A reset link signs one device in and is then used up: opened again, it says when it was opened, so a participant
// whose link someone else (the organizer, say) opened first can tell. A personal link signs the device in until the
// participant sets a password, which changes the token.
public class EnterModel(AppDb db) : PageModel
{
    public DateTime? UsedAt { get; private set; }

    public async Task<IActionResult> OnGetAsync(string token)
    {
        // Using up the link is one conditional update, so of two simultaneous opens only one signs in.
        var now = DateTime.UtcNow;
        var claimed = await db.Participants
            .Where(p => p.ResetToken == token && p.ResetUsedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.ResetUsedAt, now));
        if (claimed == 1) return SignIn(await db.Participants.Include(p => p.Game).SingleAsync(p => p.ResetToken == token));

        UsedAt = await db.Participants.Where(p => p.ResetToken == token).Select(p => p.ResetUsedAt).SingleOrDefaultAsync();
        if (UsedAt is not null) return Page();

        var person = await db.Participants.Include(p => p.Game).SingleOrDefaultAsync(p => p.Token == token);
        return person is null ? NotFound() : SignIn(person);
    }

    private RedirectResult SignIn(Participant person)
    {
        ParticipantCookie.Set(Response, person.GameId, person.Token);
        return Redirect($"/play/{person.Game.InviteToken}");
    }
}
