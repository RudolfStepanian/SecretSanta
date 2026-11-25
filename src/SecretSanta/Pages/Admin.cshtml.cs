using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SecretSanta.Data;
using SecretSanta.Services;

namespace SecretSanta.Pages;

public class AdminModel(AppDb db, Notifier notifier) : PageModel
{
    public Game Game { get; private set; } = null!;
    public List<Participant> People { get; private set; } = [];
    public HashSet<int> Notified { get; private set; } = [];
    public List<(int Id, string A, string B)> Pairs { get; private set; } = [];
    public bool Created { get; private set; }
    [TempData] public string? ErrorKey { get; set; }
    [TempData] public string? ResetName { get; set; }
    [TempData] public string? ResetUrl { get; set; }
    [TempData] public bool InfoSaved { get; set; }

    public async Task<IActionResult> OnGetAsync(string token, bool created = false)
    {
        if (!await LoadAsync(token)) return NotFound();
        Created = created;
        return Page();
    }

    // Budget and note can change at any time, also after the draw; participants see the new text right away.
    public async Task<IActionResult> OnPostInfoAsync(string token, string? budget, string? note)
    {
        if (!await LoadAsync(token)) return NotFound();

        Game.Budget = Limits.Clean(budget, Limits.Budget);
        Game.Note = Limits.Clean(note, Limits.Note);
        await db.SaveChangesAsync();
        InfoSaved = true;
        return Redirect($"/admin/{token}#info");
    }

    public async Task<IActionResult> OnPostRemoveAsync(string token, int id)
    {
        if (!await LoadAsync(token)) return NotFound();
        if (Locked()) return Back(token);

        var person = Game.Participants.FirstOrDefault(p => p.Id == id);
        if (person is not null)
        {
            db.Exclusions.RemoveRange(Game.Exclusions.Where(x => x.FirstId == id || x.SecondId == id));
            db.Participants.Remove(person);
            await db.SaveChangesAsync();
        }
        return Back(token);
    }

    public async Task<IActionResult> OnPostExcludeAsync(string token, int a, int b)
    {
        if (!await LoadAsync(token)) return NotFound();
        if (Locked()) return Back(token);

        var ids = Game.Participants.Select(p => p.Id).ToHashSet();
        var exists = Game.Exclusions.Any(x => (x.FirstId == a && x.SecondId == b) || (x.FirstId == b && x.SecondId == a));
        if (a != b && ids.Contains(a) && ids.Contains(b) && !exists)
        {
            db.Exclusions.Add(new Exclusion { GameId = Game.Id, FirstId = a, SecondId = b });
            await db.SaveChangesAsync();
        }
        return Back(token);
    }

    public async Task<IActionResult> OnPostUnexcludeAsync(string token, int id)
    {
        if (!await LoadAsync(token)) return NotFound();
        if (Locked()) return Back(token);

        var exclusion = Game.Exclusions.FirstOrDefault(x => x.Id == id);
        if (exclusion is not null)
        {
            db.Exclusions.Remove(exclusion);
            await db.SaveChangesAsync();
        }
        return Back(token);
    }

    public async Task<IActionResult> OnPostDrawAsync(string token)
    {
        if (!await LoadAsync(token)) return NotFound();
        if (Game.DrawnAt is not null) return Back(token);

        if (Game.Participants.Count < Services.Draw.MinParticipants)
        {
            ErrorKey = "DrawNeedMore";
            return Back(token);
        }

        var pairs = Services.Draw.Assign(
            Game.Participants.Select(p => p.Id),
            Game.Exclusions.Select(x => (x.FirstId, x.SecondId)),
            Random.Shared);
        if (pairs is null)
        {
            ErrorKey = "DrawImpossible";
            return Back(token);
        }

        foreach (var p in Game.Participants) p.RecipientId = pairs[p.Id];
        Game.DrawnAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await notifier.NotifyAsync(Game.Participants.Select(p => p.Id).ToList(), "PushDrawDone", Request, $"/play/{Game.InviteToken}");
        return Back(token);
    }

    public async Task<IActionResult> OnPostUndrawAsync(string token)
    {
        if (!await LoadAsync(token)) return NotFound();
        var wasDrawn = Game.DrawnAt is not null;

        await db.Messages.Where(m => m.Participant.GameId == Game.Id).ExecuteDeleteAsync();
        foreach (var p in Game.Participants)
        {
            p.RecipientId = null;
            p.RevealedAt = null;
            p.GiftReadyAt = null;
        }
        Game.DrawnAt = null;
        await db.SaveChangesAsync();
        if (wasDrawn) await notifier.NotifyAsync(Game.Participants.Select(p => p.Id).ToList(), "PushDrawCancelled", Request, $"/play/{Game.InviteToken}");
        return Back(token);
    }

    // Signs the participant out everywhere and gives the organizer a one-time link for setting a new password.
    public async Task<IActionResult> OnPostResetAsync(string token, int id)
    {
        if (!await LoadAsync(token)) return NotFound();

        var person = Game.Participants.FirstOrDefault(p => p.Id == id);
        if (person is not null)
        {
            person.Token = Tokens.New(); // signs the participant out on every device
            person.ResetToken = Tokens.New();
            person.ResetUsedAt = null;
            // The card closes again and the reset is counted, so whoever signs in next leaves a trace the participant sees.
            person.RevealedAt = null;
            person.ResetCount++;
            person.ResetAt = DateTime.UtcNow;
            person.PasswordHash = null;
            person.FailedLogins = 0;
            person.LockedUntil = null;
            await db.SaveChangesAsync();
            ResetName = person.Name;
            ResetUrl = Request.Abs($"/me/{person.ResetToken}");
        }
        return Back(token);
    }

    private async Task<bool> LoadAsync(string token)
    {
        var game = await db.Games
            .Include(g => g.Participants)
            .Include(g => g.Exclusions)
            .SingleOrDefaultAsync(g => g.AdminToken == token);
        if (game is null) return false;

        Game = game;
        People = game.Participants.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        var names = game.Participants.ToDictionary(p => p.Id, p => p.Name);
        Pairs = game.Exclusions
            .Select(x => (x.Id, names.GetValueOrDefault(x.FirstId, "?"), names.GetValueOrDefault(x.SecondId, "?")))
            .ToList();
        Notified = (await db.PushSubscriptions.Where(s => s.Participant.GameId == game.Id).Select(s => s.ParticipantId).ToListAsync()).ToHashSet();
        return true;
    }

    private bool Locked()
    {
        if (Game.DrawnAt is null) return false;
        ErrorKey = "LockedAfterDraw";
        return true;
    }

    private RedirectResult Back(string token) => Redirect($"/admin/{token}");
}
