using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecretSanta.Data;
using SecretSanta.Localization;
using SecretSanta.Services;

namespace SecretSanta.Tests;

// Which e-mails the game events queue. Delivery is switched off in SantaApp, so the e-mails wait in the queue.
public sealed class NoticeTests(SantaApp app) : IClassFixture<SantaApp>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_draw_and_its_cancelling_are_mailed_to_everyone_with_an_address_in_their_language()
    {
        SantaApp.SkipWithoutDatabase();
        var game = await Games.NewAsync(app, ["Hasmik", "Karen", "Lusine"], draw: false);
        await LinkAsync("hasmik", "hasmik@example.com", lang: "hy");
        await LinkAsync("karen", "karen@example.com");
        Drain();

        await PostAsync(game.Organizer, game.Admin, "Draw");

        var mails = Drain();
        Assert.Equal(new[] { "hasmik@example.com", "karen@example.com" }, mails.Select(m => m.To).Order());
        var hasmik = mails.Single(m => m.To == "hasmik@example.com");
        Assert.Equal("draw", hasmik.Kind);
        Assert.Equal(Strings.Get("MailDrawSubject", "hy"), hasmik.Subject);
        Assert.StartsWith("Բարև ձեզ, Hasmik։", hasmik.Body);
        // Karen hasn't opened her page yet, so her e-mail is in the organizer's language.
        var karen = mails.Single(m => m.To == "karen@example.com");
        Assert.Equal("Secret Santa: the draw is done", karen.Subject);
        Assert.StartsWith("Hi Karen,", karen.Body);
        Assert.Contains($"/play/{game.Invite}\n", karen.Body);
        Assert.All(mails, m => Assert.Single(game.People.Keys, name => (m.Subject + m.Body).Contains(name)));
        Assert.Equal(1, Count(await game.Organizer.PageAsync(game.Admin), "no e-mail"));

        await PostAsync(game.Organizer, game.Admin, "Undraw");
        var cancelled = Drain();
        Assert.Equal(2, cancelled.Count);
        Assert.All(cancelled, m => Assert.Equal("undraw", m.Kind));
        Assert.Equal("Secret Santa: the draw was cancelled", cancelled.Single(m => m.To == "karen@example.com").Subject);

        // Cancelling when there is no draw mails nothing.
        await PostAsync(game.Organizer, game.Admin, "Undraw");
        Assert.Empty(Drain());
    }

    [Fact]
    public async Task A_run_of_messages_makes_one_email_until_the_reader_opens_their_page()
    {
        SantaApp.SkipWithoutDatabase();
        var game = await Games.NewAsync(app, ["Mariam", "Narek", "Ofelia"], draw: true);
        var play = $"/play/{game.Invite}";
        var recipient = await RecipientOfAsync("mariam");
        await LinkAsync("mariam", "mariam@example.com");
        await LinkAsync(recipient.ToLowerInvariant(), "recipient@example.com");
        var mariam = game.People["Mariam"];
        var reader = game.People[recipient];
        Drain();

        await PostAsync(mariam, play, "Ask", "What would you like?");
        var asked = Assert.Single(Drain());
        Assert.Equal(("message", "recipient@example.com", "Secret Santa: your Santa wrote to you"), (asked.Kind, asked.To, asked.Subject));
        Assert.Contains($"/play/{game.Invite}#santa\n", asked.Body);
        Assert.DoesNotContain("Mariam", asked.Body);

        await PostAsync(mariam, play, "Ask", "And your size?");
        Assert.Empty(Drain());

        await reader.PageAsync(play);
        await PostAsync(mariam, play, "Ask", "Thanks!");
        Assert.Equal("recipient@example.com", Assert.Single(Drain()).To);

        await PostAsync(reader, play, "Answer", "A scarf, size M");
        var answered = Assert.Single(Drain());
        Assert.Equal(("mariam@example.com", "Secret Santa: your recipient wrote to you"), (answered.To, answered.Subject));
        Assert.Contains($"/play/{game.Invite}#ask\n", answered.Body);
        await PostAsync(reader, play, "Answer", "Blue, please");
        Assert.Empty(Drain());
    }

    private static async Task PostAsync(Browser who, string page, string handler, string? text = null)
    {
        Dictionary<string, string> fields = text is null ? [] : new() { ["Text"] = text };
        Assert.Equal(HttpStatusCode.Redirect, (await who.PostAsync(page, $"{page}?handler={handler}", fields)).StatusCode);
    }

    private async Task LinkAsync(string login, string email, string? lang = null)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var updated = await db.Participants.Where(p => p.Login == login)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Email, email).SetProperty(p => p.Lang, lang), Ct);
        Assert.Equal(1, updated);
    }

    private async Task<string> RecipientOfAsync(string login)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        return await db.Participants.Where(p => p.Login == login).Select(p => p.Recipient!.Name).SingleAsync(Ct);
    }

    private List<Mail> Drain()
    {
        var queue = app.Services.GetRequiredService<MailQueue>();
        var mails = new List<Mail>();
        while (queue.Reader.TryRead(out var mail)) mails.Add(mail);
        return mails;
    }

    private static int Count(string text, string part) => text.Split(part).Length - 1;
}
