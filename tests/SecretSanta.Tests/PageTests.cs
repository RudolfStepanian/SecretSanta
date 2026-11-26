using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecretSanta.Data;
using SecretSanta.Services;

namespace SecretSanta.Tests;

// The stage 2 rules through the whole app: real pages, cookies, antiforgery and PostgreSQL.
public sealed class PageTests(SantaApp app) : IClassFixture<SantaApp>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Pages_and_static_files_send_the_security_headers()
    {
        SantaApp.SkipWithoutDatabase();
        var visitor = await Browser.OpenAsync(app);

        foreach (var url in new[] { "/", "/css/site.css", "/no-such-page" })
        {
            var response = await visitor.GetAsync(url);
            Assert.Equal(SecurityHeaders.ContentSecurityPolicy, response.Headers.GetValues("Content-Security-Policy").Single());
            Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        }
    }

    [Fact]
    public async Task The_recipient_is_in_the_page_only_after_the_card_is_opened()
    {
        SantaApp.SkipWithoutDatabase();
        var game = await Games.NewAsync(app, ["Arpi", "Babken", "Chamich"], draw: true);
        var play = $"/play/{game.Invite}";

        // With JavaScript: the card animation fetches the recipient's part of the page.
        var arpi = game.People["Arpi"];
        var before = await arpi.PageAsync(play);
        Assert.Contains("class=\"deck\"", before);
        Assert.DoesNotContain("Babken", before);
        Assert.DoesNotContain("Chamich", before);
        var reveal = await arpi.PostAsync(play, play + "?handler=Reveal", [], header: "X-Reveal");
        Assert.Equal(HttpStatusCode.OK, reveal.StatusCode);
        Assert.Equal("1", reveal.Headers.GetValues("X-Reveal").Single());
        var part = await reveal.Content.ReadAsStringAsync(Ct);
        var recipient = Regex.Match(part, "<p class=\"recipient\">([^<]+)</p>").Groups[1].Value;
        Assert.Contains(recipient, new[] { "Babken", "Chamich" });
        Assert.Contains($"{recipient} wants a scarf", part);
        Assert.Contains("Card opened on", part);
        var after = await arpi.PageAsync(play);
        Assert.Contains($"<p class=\"recipient\">{recipient}</p>", after);
        Assert.DoesNotContain("class=\"deck\"", after);

        // Without JavaScript: the card is a submit button and the post lands on the opened page.
        var babken = game.People["Babken"];
        Assert.DoesNotContain("class=\"recipient\"", await babken.PageAsync(play));
        var post = await babken.PostAsync(play, play + "?handler=Reveal", []);
        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);
        Assert.Contains("class=\"recipient\"", await babken.PageAsync(play));
    }

    [Fact]
    public async Task A_reset_closes_the_card_and_the_participant_sees_the_resets()
    {
        SantaApp.SkipWithoutDatabase();
        var game = await Games.NewAsync(app, ["Davit", "Eva", "Gor"], draw: true);
        var play = $"/play/{game.Invite}";
        await game.People["Davit"].PostAsync(play, play + "?handler=Reveal", []);

        var reset = await game.Organizer.PostAsync(game.Admin, $"{game.Admin}?handler=Reset&id={await Games.IdAsync(app, "davit")}", []);
        Assert.Equal(HttpStatusCode.Redirect, reset.StatusCode);
        var link = Regex.Match(await game.Organizer.PageAsync(game.Admin), "/me/[A-Za-z0-9_-]+").Value;
        var device = await Browser.OpenAsync(app);
        Assert.Equal(HttpStatusCode.Redirect, (await device.GetAsync(link)).StatusCode);

        var page = await device.PageAsync(play);
        Assert.Contains("class=\"deck\"", page);
        Assert.Contains("Password resets: 1, the last one on", page);
        Assert.Contains("Cards opened: 0 of 3", await game.Organizer.PageAsync(game.Admin));
    }

    [Fact]
    public async Task Santa_marks_the_gift_ready_once_the_card_is_open()
    {
        SantaApp.SkipWithoutDatabase();
        var game = await Games.NewAsync(app, ["Ruben", "Seda", "Tatev"], draw: true);
        var play = $"/play/{game.Invite}";
        var ruben = game.People["Ruben"];
        async Task<string> PanelAsync() => await game.Organizer.PageAsync(game.Admin);

        await ruben.PostAsync(play, play + "?handler=Gift", new() { ["ready"] = "true" });
        Assert.Contains("Gifts ready: 0 of 3", await PanelAsync());

        await ruben.PostAsync(play, play + "?handler=Reveal", []);
        var marked = await ruben.PostAsync(play, play + "?handler=Gift", new() { ["ready"] = "true" });
        Assert.Equal(HttpStatusCode.Redirect, marked.StatusCode);
        Assert.Contains("Gift ready: you marked it on", await ruben.PageAsync(play));
        var panel = await PanelAsync();
        Assert.Contains("Gifts ready: 1 of 3", panel);
        Assert.Single(Regex.Matches(panel, "· gift ready"));
        Assert.Equal(2, Regex.Matches(panel, "· gift not ready yet").Count);

        await ruben.PostAsync(play, play + "?handler=Gift", new() { ["ready"] = "false" });
        Assert.Contains("Gifts ready: 0 of 3", await PanelAsync());

        await ruben.PostAsync(play, play + "?handler=Gift", new() { ["ready"] = "true" });
        await game.Organizer.PostAsync(game.Admin, game.Admin + "?handler=Undraw", []);
        await game.Organizer.PostAsync(game.Admin, game.Admin + "?handler=Draw", []);
        Assert.Contains("Gifts ready: 0 of 3", await PanelAsync());
    }

    [Fact]
    public async Task The_organizer_changes_budget_and_note_after_the_draw()
    {
        SantaApp.SkipWithoutDatabase();
        var game = await Games.NewAsync(app, ["Hayk", "Iren", "Karen"], draw: true);

        var save = await game.Organizer.PostAsync(game.Admin, game.Admin + "?handler=Info", new() { ["budget"] = "20000 AMD", ["note"] = "Moved to Friday" });
        Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);

        var page = await game.People["Hayk"].PageAsync($"/play/{game.Invite}");
        Assert.Contains("20000 AMD", page);
        Assert.Contains("Moved to Friday", page);
    }

    [Fact]
    public async Task The_language_menu_keeps_the_address()
    {
        SantaApp.SkipWithoutDatabase();
        var visitor = await Browser.OpenAsync(app);

        var page = await visitor.PageAsync("/?created=true");

        Assert.Contains("href=\"?created=true&amp;lang=ru\"", page);
    }

    // //evil.example/x is a path on this site, but a link to that path would take the browser to evil.example.
    [Fact]
    public async Task The_language_menu_on_a_double_slash_address_stays_on_the_site()
    {
        SantaApp.SkipWithoutDatabase();
        var visitor = await Browser.OpenAsync(app);

        var response = await visitor.GetAsync("http://localhost//evil.example/x");
        var page = await response.Content.ReadAsStringAsync(Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("href=\"?lang=ru\"", page);
        Assert.DoesNotContain("evil.example", page);
    }

    // The visitor's cookie says English; the language menu on the "not found" page asks for Russian.
    [Fact]
    public async Task A_status_page_comes_in_the_language_its_address_asks_for()
    {
        SantaApp.SkipWithoutDatabase();
        var visitor = await Browser.OpenAsync(app);

        var response = await visitor.GetAsync("/no-such-page?lang=ru");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("<html lang=\"ru\"", await response.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task A_game_takes_at_most_200_participants()
    {
        SantaApp.SkipWithoutDatabase();
        var game = await Games.NewAsync(app, [], draw: false);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var id = await db.Games.Where(g => g.InviteToken == game.Invite).Select(g => g.Id).SingleAsync(Ct);
            db.Participants.AddRange(Enumerable.Range(0, Limits.MaxParticipants).Select(i => new Participant
            {
                GameId = id, Name = $"Person {i}", Login = $"person{i}", Token = Tokens.New(), CreatedAt = DateTime.UtcNow,
            }));
            await db.SaveChangesAsync(Ct);
        }

        var late = await Browser.OpenAsync(app);
        var join = $"/join/{game.Invite}";
        var response = await late.PostAsync(join, join, Games.JoinFields("Latecomer"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("The game already has 200 participants", await response.Content.ReadAsStringAsync(Ct));
        Assert.Null(await Games.IdAsync(app, "latecomer"));
    }
}

// Its own app, so the posts it spends do not count against the other tests.
public sealed class RateLimitPageTests(SantaApp app) : IClassFixture<SantaApp>
{
    [Fact]
    public async Task Too_many_new_games_from_one_address_get_a_friendly_page()
    {
        SantaApp.SkipWithoutDatabase();
        var organizer = await Browser.OpenAsync(app);
        Dictionary<string, string> fields = new() { ["Budget"] = "", ["Note"] = "" };

        for (var i = 0; i < RateLimits.CreateGame; i++)
            Assert.Equal(HttpStatusCode.Redirect, (await organizer.PostAsync("/", "/", fields)).StatusCode);
        var refused = await organizer.PostAsync("/", "/", fields);

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.Contains("Too many requests", await refused.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(HttpStatusCode.OK, (await organizer.GetAsync("/")).StatusCode);
    }
}
