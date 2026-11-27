using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecretSanta.Data;
using SecretSanta.Services;

namespace SecretSanta.Tests;

// Linking an address and resetting a forgotten password through it, on the real pages. Mail lands in app.Mailbox;
// each test uses its own names, so it finds its own mail there.
public sealed class MailTests(SantaApp app) : IClassFixture<SantaApp>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_address_is_linked_only_by_the_button_behind_its_confirmation_link()
    {
        SantaApp.SkipWithoutDatabase();
        var game = await Games.NewAsync(app, ["Arsen", "Bella", "Gayane"], draw: false);
        var play = $"/play/{game.Invite}";
        var arsen = game.People["Arsen"];
        Assert.Contains("Only you can see the address.", await arsen.PageAsync(play));

        var asked = await arsen.PostAsync(play, play + "?handler=Mail", new() { ["Email"] = "  Arsen@Example.COM " });

        Assert.Equal($"{play}#mail", asked.Headers.Location!.OriginalString);
        var mail = Assert.Single(app.Mailbox.Sent, m => m.To == "arsen@example.com");
        Assert.Equal("confirm", mail.Kind);
        Assert.StartsWith("http://localhost/mail/", mail.Link);
        var link = app.Mailbox.LinkTo("arsen@example.com");
        Assert.StartsWith("/mail/", link);
        Assert.Contains("An e-mail went to arsen@example.com", await arsen.PageAsync(play));
        Assert.Null(await EmailAsync("arsen"));

        // Opening the link, as a mail scanner would, links nothing; the button does, on any device.
        var phone = await Browser.OpenAsync(app);
        Assert.Contains("Link arsen@example.com to &quot;Arsen&quot; in Secret Santa?", await phone.PageAsync(link));
        Assert.Null(await EmailAsync("arsen"));
        var confirmed = await phone.PostAsync(link, link, []);
        Assert.Contains("Done: arsen@example.com is linked.", await confirmed.Content.ReadAsStringAsync(Ct));
        Assert.Equal("arsen@example.com", await EmailAsync("arsen"));
        var page = await arsen.PageAsync(play);
        Assert.Contains("Linked address: arsen@example.com", page);
        Assert.DoesNotContain("An e-mail went to", page);
        Assert.Equal(HttpStatusCode.NotFound, (await phone.GetAsync(link)).StatusCode);
        Assert.DoesNotContain("arsen@example.com", await game.Organizer.PageAsync(game.Admin));
    }

    [Fact]
    public async Task A_wrong_a_repeated_or_the_same_address_sends_nothing()
    {
        SantaApp.SkipWithoutDatabase();
        var game = await Games.NewAsync(app, ["Davit", "Eva", "Gohar"], draw: false);
        var play = $"/play/{game.Invite}";
        var davit = game.People["Davit"];
        async Task<string> AskAsync(string address) =>
            (await davit.PostAsync(play, play + "?handler=Mail", new() { ["Email"] = address })).Headers.Location!.OriginalString;

        Assert.Equal($"{play}?mail=invalid#mail", await AskAsync("davit@example"));
        Assert.Contains("Please check the address", await davit.PageAsync($"{play}?mail=invalid"));
        Assert.Equal($"{play}#mail", await AskAsync("davit@example.com"));
        Assert.Equal($"{play}?mail=wait#mail", await AskAsync("davit@example.org"));
        Assert.Contains("You can ask for a new one in a few minutes.", await davit.PageAsync($"{play}?mail=wait"));
        Assert.Single(app.Mailbox.Sent, m => m.To.StartsWith("davit@", StringComparison.Ordinal));

        var link = app.Mailbox.LinkTo("davit@example.com");
        await davit.PostAsync(link, link, []);
        Assert.Equal($"{play}?mail=same#mail", await AskAsync("Davit@Example.com"));
        Assert.Single(app.Mailbox.Sent, m => m.To.StartsWith("davit@", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_confirmation_link_works_for_a_day()
    {
        SantaApp.SkipWithoutDatabase();
        var game = await Games.NewAsync(app, ["Hayk", "Inga", "Karine"], draw: false);
        var play = $"/play/{game.Invite}";
        await game.People["Hayk"].PostAsync(play, play + "?handler=Mail", new() { ["Email"] = "hayk@example.com" });
        var link = app.Mailbox.LinkTo("hayk@example.com");
        await UpdateAsync("hayk", p => p.EmailSentAt = DateTime.UtcNow.AddHours(-25));

        var phone = await Browser.OpenAsync(app);
        Assert.Contains("This link has expired", await phone.PageAsync(link));
        // The expired page has no button; a page opened before the day ran out still posts it.
        var late = await phone.PostAsync("/", link, []);
        Assert.Contains("This link has expired", await late.Content.ReadAsStringAsync(Ct));
        Assert.Null(await EmailAsync("hayk"));
        Assert.DoesNotContain("An e-mail went to", await game.People["Hayk"].PageAsync(play));
    }

    [Fact]
    public async Task A_forgotten_password_is_reset_through_the_linked_address()
    {
        SantaApp.SkipWithoutDatabase();
        var game = await Games.NewAsync(app, ["Levon", "Mariam", "Narek"], draw: true);
        await UpdateAsync("levon", p => p.Email = "levon@example.com");
        var join = $"/join/{game.Invite}";
        var play = $"/play/{game.Invite}";
        var phone = await Browser.OpenAsync(app);
        Assert.Contains("Forgot your password?", await phone.PageAsync(join));

        // An unknown login, a login without an address and the right one, twice, all get the same answer.
        foreach (var login in new[] { "nobody", "mariam", "Levon", "levon" })
        {
            var asked = await phone.PostAsync(join, join + "?handler=Forgot", new() { ["ForgotLogin"] = login });
            Assert.Equal($"{join}?forgot=true", asked.Headers.Location!.OriginalString);
        }
        Assert.Contains("If an e-mail address is linked to this login", await phone.PageAsync($"{join}?forgot=true"));
        var mail = Assert.Single(app.Mailbox.Sent, m => m.To == "levon@example.com");
        Assert.Equal("reset", mail.Kind);
        var link = app.Mailbox.LinkTo("levon@example.com");

        // Asking changed nothing: the old password still signs in, and opening the link only shows the button.
        Assert.Equal(HttpStatusCode.Redirect, (await SignInAsync(join, "levon", "secret-1")).StatusCode);
        Assert.Contains("Sign in and set a password", await phone.PageAsync(link));

        var used = await phone.PostAsync(link, link, []);
        Assert.Equal(play, used.Headers.Location!.OriginalString);
        var page = await phone.PageAsync(play);
        Assert.Contains("Sign-in details", page);
        Assert.DoesNotContain("Password resets:", page);
        Assert.Equal(HttpStatusCode.Redirect, (await game.People["Levon"].GetAsync(play)).StatusCode);
        Assert.Contains("No password is set for this participant yet", await (await SignInAsync(join, "levon", "secret-1")).Content.ReadAsStringAsync(Ct));
        Assert.Contains("This link has already been used", await phone.PageAsync(link));

        var saved = await phone.PostAsync(play, play + "?handler=Credentials", new() { ["Password"] = "new-secret", ["PasswordRepeat"] = "new-secret" });
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await SignInAsync(join, "levon", "new-secret")).StatusCode);
    }

    [Fact]
    public async Task When_mail_cannot_be_sent_nothing_is_left_waiting()
    {
        SantaApp.SkipWithoutDatabase();
        var game = await Games.NewAsync(app, ["Ovsanna", "Petros", "Ruzanna"], draw: false);
        var play = $"/play/{game.Invite}";
        var join = $"/join/{game.Invite}";
        var ovsanna = game.People["Ovsanna"];
        await UpdateAsync("petros", p => p.Email = "petros@example.com");

        app.Mailbox.Fail = true;
        try
        {
            var asked = await ovsanna.PostAsync(play, play + "?handler=Mail", new() { ["Email"] = "ovsanna@example.com" });
            Assert.Equal($"{play}?mail=failed#mail", asked.Headers.Location!.OriginalString);
            Assert.Contains("be sent. Please try again a bit later.", await ovsanna.PageAsync($"{play}?mail=failed"));
            await ovsanna.PostAsync(join, join + "?handler=Forgot", new() { ["ForgotLogin"] = "petros" });
        }
        finally
        {
            app.Mailbox.Fail = false;
        }
        Assert.DoesNotContain("An e-mail went to", await ovsanna.PageAsync(play));
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var petros = await scope.ServiceProvider.GetRequiredService<AppDb>().Participants.SingleAsync(p => p.Login == "petros", Ct);
            Assert.Null(petros.ResetToken);
            Assert.Null(petros.ResetSentAt);
        }

        // Nothing went out, so asking again right away is not "too soon".
        Assert.Equal($"{play}#mail", (await ovsanna.PostAsync(play, play + "?handler=Mail", new() { ["Email"] = "ovsanna@example.com" })).Headers.Location!.OriginalString);
        await ovsanna.PostAsync(join, join + "?handler=Forgot", new() { ["ForgotLogin"] = "petros" });
        Assert.Single(app.Mailbox.Sent, m => m.To == "ovsanna@example.com");
        Assert.Single(app.Mailbox.Sent, m => m.To == "petros@example.com");
    }

    [Fact]
    public async Task Unlinking_the_address_also_stops_a_mailed_reset_link()
    {
        SantaApp.SkipWithoutDatabase();
        var game = await Games.NewAsync(app, ["Sona", "Tigran", "Vardan"], draw: false);
        var play = $"/play/{game.Invite}";
        var join = $"/join/{game.Invite}";
        var sona = game.People["Sona"];
        await UpdateAsync("sona", p => p.Email = "sona@example.com");
        Assert.Contains("Linked address: sona@example.com", await sona.PageAsync(play));
        await (await Browser.OpenAsync(app)).PostAsync(join, join + "?handler=Forgot", new() { ["ForgotLogin"] = "sona" });
        var link = app.Mailbox.LinkTo("sona@example.com");

        var removed = await sona.PostAsync(play, play + "?handler=MailRemove", []);

        Assert.Equal($"{play}#mail", removed.Headers.Location!.OriginalString);
        Assert.Null(await EmailAsync("sona"));
        Assert.DoesNotContain("Linked address", await sona.PageAsync(play));
        Assert.Equal(HttpStatusCode.NotFound, (await (await Browser.OpenAsync(app)).GetAsync(link)).StatusCode);
    }

    // The key behind the Firebase account passwords survives restarts, or the site couldn't sign in to the accounts again.
    [Fact]
    public async Task The_Firebase_account_key_is_made_once_and_kept()
    {
        SantaApp.SkipWithoutDatabase();
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();

        var first = FirebaseAccounts.LoadOrCreate(db).PasswordFor("ani@example.com");

        Assert.Equal(first, FirebaseAccounts.LoadOrCreate(db).PasswordFor("ani@example.com"));
        Assert.Equal(1, await db.Settings.CountAsync(s => s.Key == "FirebaseAccountKey", Ct));
    }

    private async Task<HttpResponseMessage> SignInAsync(string join, string login, string password)
    {
        var device = await Browser.OpenAsync(app);
        return await device.PostAsync(join, join + "?handler=SignIn", new() { ["SignInLogin"] = login, ["SignInPassword"] = password });
    }

    private async Task<string?> EmailAsync(string login)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        return await db.Participants.Where(p => p.Login == login).Select(p => p.Email).SingleAsync(Ct);
    }

    private async Task UpdateAsync(string login, Action<Participant> change)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        change(await db.Participants.SingleAsync(p => p.Login == login, Ct));
        await db.SaveChangesAsync(Ct);
    }
}

// Until a mail relay is set up, the site runs as it did before e-mail.
public sealed class MailOffTests(SantaAppWithoutMail app) : IClassFixture<SantaAppWithoutMail>
{
    [Fact]
    public async Task Without_a_relay_nothing_offers_e_mail()
    {
        SantaApp.SkipWithoutDatabase();
        var game = await Games.NewAsync(app, ["Anahit", "Bagrat", "Gevorg"], draw: false);
        var play = $"/play/{game.Invite}";
        var join = $"/join/{game.Invite}";
        var anahit = game.People["Anahit"];

        Assert.DoesNotContain("id=\"mail\"", await anahit.PageAsync(play));
        Assert.DoesNotContain("Forgot your password?", await anahit.PageAsync(join));
        Assert.Equal(HttpStatusCode.NotFound, (await anahit.PostAsync(play, play + "?handler=Mail", new() { ["Email"] = "anahit@example.com" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anahit.PostAsync(join, join + "?handler=Forgot", new() { ["ForgotLogin"] = "anahit" })).StatusCode);
        Assert.Empty(app.Mailbox.Sent);
    }
}
