using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SecretSanta.Data;
using SecretSanta.Services;

namespace SecretSanta.Pages;

// Personal links from before passwords and the one-time links from a password reset: they sign this device in
// and lead to the participant's page. Setting a password changes the token, after which the link is dead.
public class EnterModel(AppDb db) : PageModel
{
    public async Task<IActionResult> OnGetAsync(string token)
    {
        var person = await db.Participants.Include(p => p.Game).SingleOrDefaultAsync(p => p.Token == token);
        if (person is null) return NotFound();

        ParticipantCookie.Set(Response, person.GameId, person.Token);
        return Redirect($"/play/{person.Game.InviteToken}");
    }
}
