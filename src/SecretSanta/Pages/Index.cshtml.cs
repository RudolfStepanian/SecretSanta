using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SecretSanta.Data;
using SecretSanta.Services;

namespace SecretSanta.Pages;

public class IndexModel(AppDb db) : PageModel
{
    [BindProperty] public string? Budget { get; set; }
    [BindProperty] public string? Note { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var game = new Game
        {
            Budget = Limits.Clean(Budget, Limits.Budget),
            Note = Limits.Clean(Note, Limits.Note),
            AdminToken = Tokens.New(),
            InviteToken = Tokens.New(),
            CreatedAt = DateTime.UtcNow,
        };
        db.Games.Add(game);
        await db.SaveChangesAsync();

        return Redirect($"/admin/{game.AdminToken}?created=true");
    }
}
