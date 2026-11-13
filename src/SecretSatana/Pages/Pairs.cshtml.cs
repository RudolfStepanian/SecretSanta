using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SecretSatana.Data;

namespace SecretSatana.Pages;

// Organizer's private view of all pairs: needs the admin token and ?key= equal to the PairsKey setting; without PairsKey it is off.
public class PairsModel(AppDb db, IConfiguration config) : PageModel
{
    public Game Game { get; private set; } = null!;
    public List<(string Giver, string Recipient)> Pairs { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(string token, string? key)
    {
        if (!KeyMatches(key)) return NotFound();

        var game = await db.Games.Include(g => g.Participants).SingleOrDefaultAsync(g => g.AdminToken == token);
        if (game is null) return NotFound();

        Game = game;
        var names = game.Participants.ToDictionary(p => p.Id, p => p.Name);
        Pairs = game.Participants
            .Where(p => p.RecipientId is not null)
            .Select(p => (p.Name, names.GetValueOrDefault(p.RecipientId!.Value, "?")))
            .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        return Page();
    }

    private bool KeyMatches(string? key) =>
        config["PairsKey"] is { Length: > 0 } expected
        && key is not null
        && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(key));
}
