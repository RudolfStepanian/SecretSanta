using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecretSanta.Data;
using SecretSanta.Services;

namespace SecretSanta.Tests;

// Each test class has its own app and database, so the expiry test sees only the games it made.
public sealed class DeleteTests(SantaApp app) : IClassFixture<SantaApp>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_organizer_deletes_the_game_with_everything_in_it()
    {
        SantaApp.SkipWithoutDatabase();
        var game = await Games.NewAsync(app, ["Vahe", "Zara", "Ani", "Levon"], draw: false);
        var ids = new List<int>();
        foreach (var login in new[] { "vahe", "zara", "ani", "levon" }) ids.Add((await Games.IdAsync(app, login))!.Value);
        Assert.Equal(HttpStatusCode.Redirect, (await game.Organizer.PostAsync(game.Admin, game.Admin + "?handler=Exclude", new() { ["a"] = $"{ids[0]}", ["b"] = $"{ids[1]}" })).StatusCode);
        await game.Organizer.PostAsync(game.Admin, game.Admin + "?handler=Draw", []);
        var play = $"/play/{game.Invite}";
        var vahe = game.People["Vahe"];
        await vahe.PostAsync(play, play + "?handler=Reveal", []);
        await vahe.PostAsync(play, play + "?handler=Ask", new() { ["Text"] = "Hi!" });
        await vahe.PostAsync(play, play + "?handler=Answer", new() { ["Text"] = "Hello!" });
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            db.PushSubscriptions.Add(new PushSubscription
            {
                ParticipantId = ids[2], Endpoint = "https://fcm.googleapis.com/fcm/send/delete-test", P256dh = "key", Auth = "secret", Lang = "ru", CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync(Ct);
            Assert.Equal(2, await db.Messages.CountAsync(m => ids.Contains(m.ParticipantId), Ct));
        }
        Assert.Contains("The game will be deleted automatically on", await game.Organizer.PageAsync(game.Admin));

        var deleted = await game.Organizer.PostAsync(game.Admin, game.Admin + "?handler=Delete", []);

        Assert.Equal(HttpStatusCode.Redirect, deleted.StatusCode);
        Assert.Equal("/?deleted=true", deleted.Headers.Location!.OriginalString);
        Assert.Contains("The game has been deleted.", await game.Organizer.PageAsync("/?deleted=true"));
        Assert.Equal(HttpStatusCode.NotFound, (await game.Organizer.GetAsync(game.Admin)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await game.People["Zara"].GetAsync(play)).StatusCode);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            Assert.False(await db.Games.AnyAsync(g => g.InviteToken == game.Invite, Ct));
            Assert.False(await db.Participants.AnyAsync(p => ids.Contains(p.Id), Ct));
            Assert.False(await db.Exclusions.AnyAsync(x => ids.Contains(x.FirstId) || ids.Contains(x.SecondId), Ct));
            Assert.False(await db.Messages.AnyAsync(m => ids.Contains(m.ParticipantId), Ct));
            Assert.False(await db.PushSubscriptions.AnyAsync(s => ids.Contains(s.ParticipantId), Ct));
        }
    }

    [Fact]
    public async Task Games_expire_60_days_after_the_draw_or_120_days_after_creation_without_one()
    {
        SantaApp.SkipWithoutDatabase();
        var now = DateTime.UtcNow;
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        Game Made(string note, int createdDaysAgo, int? drawnDaysAgo) => new()
        {
            Note = note,
            AdminToken = Tokens.New(),
            InviteToken = Tokens.New(),
            CreatedAt = now.AddDays(-createdDaysAgo),
            DrawnAt = drawnDaysAgo is { } days ? now.AddDays(-days) : null,
        };
        db.Games.AddRange(
            Made("drawn 61 days ago", 90, 61),
            Made("drawn 59 days ago", 90, 59),
            Made("created 121 days ago, no draw", 121, null),
            Made("created 119 days ago, no draw", 119, null),
            Made("created 200 days ago, drawn 10 days ago", 200, 10));
        await db.SaveChangesAsync(Ct);
        var before = await db.Games.Select(g => g.Note!).ToListAsync(Ct);

        var deleted = await GameCleanup.DeleteExpiredAsync(db, now, Ct);

        Assert.Equal(2, deleted);
        var left = await db.Games.Select(g => g.Note!).ToListAsync(Ct);
        Assert.Equal(
            before.Except(["drawn 61 days ago", "created 121 days ago, no draw"]).Order(StringComparer.Ordinal),
            left.Order(StringComparer.Ordinal));
    }
}
