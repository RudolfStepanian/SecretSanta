using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecretSanta.Data;

namespace SecretSanta.Tests;

public sealed record TestGame(Browser Organizer, string Admin, string Invite, Dictionary<string, Browser> People);

// Sets games up through the real pages: the organizer creates one, each name signs up from its own browser with
// name@example.com (Address) and, with mail on, joins through the link mailed there.
public static class Games
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static async Task<TestGame> NewAsync(SantaApp app, string[] names, bool draw)
    {
        var organizer = await Browser.OpenAsync(app);
        var created = await organizer.PostAsync("/", "/", new() { ["Budget"] = "100", ["Note"] = "Party" });
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        var admin = created.Headers.Location!.OriginalString.Split('?')[0];
        var invite = Regex.Match(await organizer.PageAsync(admin), "/join/([A-Za-z0-9_-]+)").Groups[1].Value;

        var people = new Dictionary<string, Browser>();
        foreach (var name in names)
        {
            var person = await Browser.OpenAsync(app);
            var join = $"/join/{invite}";
            var joined = await person.PostAsync(join, join, JoinFields(name));
            Assert.Equal(HttpStatusCode.Redirect, joined.StatusCode);
            if (!joined.Headers.Location!.OriginalString.StartsWith("/play/", StringComparison.Ordinal))
            {
                var link = app.Mailbox.LinkTo(Address(name));
                Assert.Equal($"/play/{invite}?new=true", (await person.PostAsync(link, link, [])).Headers.Location!.OriginalString);
            }
            people[name] = person;
        }
        if (draw) Assert.Equal(HttpStatusCode.Redirect, (await organizer.PostAsync(admin, admin + "?handler=Draw", [])).StatusCode);
        return new TestGame(organizer, admin, invite, people);
    }

    public static string Address(string name) => $"{name.ToLowerInvariant()}@example.com";

    public static Dictionary<string, string> JoinFields(string name) => new()
    {
        ["Name"] = name,
        ["Email"] = Address(name),
        ["Password"] = "secret-1",
        ["PasswordRepeat"] = "secret-1",
        ["Wishlist"] = $"{name} wants a scarf",
        ["NotWanted"] = "",
    };

    // The participant who signed up with the name's address, if they are in a game.
    public static async Task<int?> IdAsync(SantaApp app, string name)
    {
        var address = Address(name);
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        return await db.Participants.Where(p => p.Login == address).Select(p => (int?)p.Id).SingleOrDefaultAsync(Ct);
    }
}
