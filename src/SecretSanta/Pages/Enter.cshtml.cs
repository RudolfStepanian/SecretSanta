using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SecretSanta.Data;
using SecretSanta.Services;

namespace SecretSanta.Pages;

// /me/{token} is either the one-time link for a new password (from the organizer, or mailed after "Forgot your
// password?") or a personal link from before passwords.
// Opening a one-time link only shows a button: mail scanners and chat link previews open links too, and the link must
// still work for its owner. The button signs one device in, signs every other device out and asks for a new password;
// the link is then used up. Opened again, it says when it was used, so a participant whose link someone else used first
// can tell. A mailed link works for a day, the organizer's has no time limit.
// A personal link signs the device in until the participant sets a password, which changes the token.
public class EnterModel(AppDb db) : PageModel
{
    public DateTime? UsedAt { get; private set; }
    public bool Expired { get; private set; }
    public string Invite { get; private set; } = "";

    public async Task<IActionResult> OnGetAsync(string token)
    {
        var reset = await db.Participants
            .Where(p => p.ResetToken == token)
            .Select(p => new { p.ResetUsedAt, p.ResetSentAt, p.Game.InviteToken })
            .SingleOrDefaultAsync();
        if (reset is not null)
        {
            Invite = reset.InviteToken;
            UsedAt = reset.ResetUsedAt;
            Expired = UsedAt is null && reset.ResetSentAt <= DateTime.UtcNow - MailLinks.Life;
            return Page();
        }

        var person = await db.Participants.Include(p => p.Game).SingleOrDefaultAsync(p => p.Token == token);
        return person is null ? NotFound() : SignIn(person);
    }

    public async Task<IActionResult> OnPostAsync(string token)
    {
        // Using up the link is one conditional update, so of two simultaneous posts only one signs in.
        var now = DateTime.UtcNow;
        var since = now - MailLinks.Life;
        var fresh = Tokens.New();
        var claimed = await db.Participants
            .Where(p => p.ResetToken == token && p.ResetUsedAt == null && (p.ResetSentAt == null || p.ResetSentAt > since))
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.ResetUsedAt, now)
                .SetProperty(p => p.Token, fresh)
                .SetProperty(p => p.PasswordHash, (string?)null)
                .SetProperty(p => p.FailedLogins, 0)
                .SetProperty(p => p.LockedUntil, (DateTime?)null));
        if (claimed == 1) return SignIn(await db.Participants.Include(p => p.Game).SingleAsync(p => p.ResetToken == token));
        return await OnGetAsync(token);
    }

    private RedirectResult SignIn(Participant person)
    {
        ParticipantCookie.Set(Response, person.GameId, person.Token);
        return Redirect($"/play/{person.Game.InviteToken}");
    }
}
