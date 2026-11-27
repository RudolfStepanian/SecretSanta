using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecretSanta.Data;
using SecretSanta.Services;

namespace SecretSanta.Tests;

// Linking an address and resetting a forgotten password through it, on the real pages. Mail lands in app.Mailbox;
// each test uses its own names, so it finds its own mail there. Everyone signs up with name@example.com (Games).
public sealed class MailTests(SantaApp app) : IClassFixture<SantaApp>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_new_address_is_linked_only_by_the_button_behind_its_confirmation_link()
    {
        SantaApp.SkipWithoutDatabase();
        var game = await Games.NewAsync(app, ["Arsen", "Bella", "Gayane"], draw: false);
        var play = $"/play/{game.Invite}";
        var join = $"/join/{game.Invite}";
        var arsen = game.People["Arsen"];
        await SignsInWithLoginAsync("Arsen");
        var before = await arsen.PageAsync(play);
        Assert.Contains("Linked address: arsen@example.com", before);
        Assert.Contains("Only you can see the address.", before);

        var asked = await arsen.PostAsync(play, play + "?handler=Mail", new() { ["Email"] = "  Arsen.New@Example.COM " });

        Assert.Equal($"{play}#mail", asked.Headers.Location!.OriginalString);
        var mail = Assert.Single(app.Mailbox.Sent, m => m.To == "arsen.new@example.com");
        Assert.Equal("confirm", mail.Kind);
        Assert.Equal("Secret Santa: confirm your e-mail address", mail.Subject);
        Assert.StartsWith("Hi Arsen,", mail.Body);
        var link = app.Mailbox.LinkTo("arsen.new@example.com");
        Assert.StartsWith("/mail/", link);
        Assert.Contains("A confirmation e-mail went to arsen.new@example.com", await arsen.PageAsync(play));
        Assert.Equal("arsen@example.com", await EmailAsync("Arsen"));

        // Opening the link, as a mail scanner would, links nothing; the button does, on any device.
        var phone = await Browser.OpenAsync(app);
        Assert.Contains("Link arsen.new@example.com to &quot;Arsen&quot; in Secret Santa?", await phone.PageAsync(link));
        Assert.Equal("arsen@example.com", await EmailAsync("Arsen"));
        var confirmed = await phone.PostAsync(link, link, []);
        Assert.Contains("Done: arsen.new@example.com is linked.", await confirmed.Content.ReadAsStringAsync(Ct));
        Assert.Equal("arsen.new@example.com", await EmailAsync("Arsen"));
        var page = await arsen.PageAsync(play);
        Assert.Contains("Linked address: arsen.new@example.com", page);
        Assert.DoesNotContain("A confirmation e-mail went to", page);
        Assert.Equal(HttpStatusCode.NotFound, (await phone.GetAsync(link)).StatusCode);
        Assert.DoesNotContain("@example.com", await game.Organizer.PageAsync(game.Admin));

        // The linked address signs in too, the old one no longer does.
        Assert.Equal(HttpStatusCode.Redirect, (await SignInAsync(join, "arsen.new@example.com", "secret-1")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SignInAsync(join, "arsen@example.com", "secret-1")).StatusCode);
        // Bella signs in with her address, which is linked already: her page has no card for it.
        Assert.DoesNotContain("id=\"mail\"", await game.People["Bella"].PageAsync(play));
    }

    [Fact]
    public async Task A_wrong_a_repeated_a_used_or_the_same_address_sends_nothing()
    {
        SantaApp.SkipWithoutDatabase();
        var game = await Games.NewAsync(app, ["Davit", "Eva", "Gohar"], draw: false);
        var play = $"/play/{game.Invite}";
        var davit = game.People["Davit"];
        await SignsInWithLoginAsync("Davit");
        async Task<string> AskAsync(string address) =>
            (await davit.PostAsync(play, play + "?handler=Mail", new() { ["Email"] = address })).Headers.Location!.OriginalString;

        Assert.Equal($"{play}?mail=invalid#mail", await AskAsync("dk@example"));
        Assert.Contains("Please check the address", await davit.PageAsync($"{play}?mail=invalid"));
        Assert.Equal($"{play}?mail=same#mail", await AskAsync("Davit@Example.com"));
        Assert.Equal($"{play}?mail=taken#mail", await AskAsync("eva@example.com"));
        Assert.Contains("This address already belongs to another participant in the game.", await davit.PageAsync($"{play}?mail=taken"));
        Assert.Equal($"{play}#mail", await AskAsync("dk@example.com"));
        Assert.Equal($"{play}?mail=wait#mail", await AskAsync("dk@example.org"));
        Assert.Contains("You can ask for a new one in a few minutes.", await davit.PageAsync($"{play}?mail=wait"));
        Assert.Single(app.Mailbox.Sent, m => m.To.StartsWith("dk@", StringComparison.Ordinal));

        var link = app.Mailbox.LinkTo("dk@example.com");
        await davit.PostAsync(link, link, []);
        Assert.Equal($"{play}?mail=same#mail", await AskAsync("DK@Example.com"));
        Assert.Single(app.Mailbox.Sent, m => m.To.StartsWith("dk@", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_confirmation_link_works_for_a_day()
    {
        SantaApp.SkipWithoutDatabase();
        var game = await Games.NewAsync(app, ["Hayk", "Inga", "Karine"], draw: false);
        var play = $"/play/{game.Invite}";
        await SignsInWithLoginAsync("Hayk");
        await game.People["Hayk"].PostAsync(play, play + "?handler=Mail", new() { ["Email"] = "hayk.new@example.com" });
        var link = app.Mailbox.LinkTo("hayk.new@example.com");
        await UpdateAsync("Hayk", p => p.EmailSentAt = DateTime.UtcNow.AddHours(-25));

        var phone = await Browser.OpenAsync(app);
        Assert.Contains("This link has expired", await phone.PageAsync(link));
        // The expired page has no button; a page opened before the day ran out still posts it.
        var late = await phone.PostAsync("/", link, []);
        Assert.Contains("This link has expired", await late.Content.ReadAsStringAsync(Ct));
        Assert.Equal("hayk@example.com", await EmailAsync("Hayk"));
        Assert.DoesNotContain("A confirmation e-mail went to", await game.People["Hayk"].PageAsync(play));
    }

    [Fact]
    public async Task A_forgotten_password_is_reset_through_the_linked_address()
    {
        SantaApp.SkipWithoutDatabase();
        var game = await Games.NewAsync(app, ["Levon", "Mariam", "Narek"], draw: true);
        // Mariam stands for someone who signed up with a login before addresses and never linked one.
        await UpdateAsync("Mariam", p =>
        {
            p.Login = "mariam";
            p.Email = null;
        });
        var join = $"/join/{game.Invite}";
        var play = $"/play/{game.Invite}";
        var phone = await Browser.OpenAsync(app);
        Assert.Contains("Forgot your password?", await phone.PageAsync(join));

        // An unknown address, a login without an address and the right address, twice, all get the same answer.
        foreach (var typed in new[] { "nobody@example.com", "mariam", "Levon@Example.com", "levon@example.com" })
        {
            var asked = await phone.PostAsync(join, join + "?handler=Forgot", new() { ["ForgotLogin"] = typed });
            Assert.Equal($"{join}?forgot=true", asked.Headers.Location!.OriginalString);
        }
        Assert.Contains("If there is such a participant with a linked address", await phone.PageAsync($"{join}?forgot=true"));
        var mail = Assert.Single(app.Mailbox.Sent, m => m.To == "levon@example.com" && m.Kind == "reset");
        Assert.Equal("Secret Santa: a new password", mail.Subject);
        var link = app.Mailbox.LinkTo("levon@example.com");

        // Asking changed nothing: the old password still signs in, and opening the link only shows the button.
        Assert.Equal(HttpStatusCode.Redirect, (await SignInAsync(join, "levon@example.com", "secret-1")).StatusCode);
        Assert.Contains("Sign in and set a password", await phone.PageAsync(link));

        var used = await phone.PostAsync(link, link, []);
        Assert.Equal(play, used.Headers.Location!.OriginalString);
        var page = await phone.PageAsync(play);
        Assert.Contains("Sign-in details", page);
        Assert.DoesNotContain("Password resets:", page);
        Assert.Equal(HttpStatusCode.Redirect, (await game.People["Levon"].GetAsync(play)).StatusCode);
        Assert.Contains("No password is set for this participant yet", await (await SignInAsync(join, "levon@example.com", "secret-1")).Content.ReadAsStringAsync(Ct));
        Assert.Contains("This link has already been used", await phone.PageAsync(link));

        var saved = await phone.PostAsync(play, play + "?handler=Credentials", new() { ["Password"] = "new-secret", ["PasswordRepeat"] = "new-secret" });
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await SignInAsync(join, "levon@example.com", "new-secret")).StatusCode);
    }

    [Fact]
    public async Task When_mail_cannot_be_sent_nothing_is_left_waiting()
    {
        SantaApp.SkipWithoutDatabase();
        var game = await Games.NewAsync(app, ["Ovsanna", "Petros", "Ruzanna"], draw: false);
        var play = $"/play/{game.Invite}";
        var join = $"/join/{game.Invite}";
        var ovsanna = game.People["Ovsanna"];
        await SignsInWithLoginAsync("Ovsanna");

        app.Mailbox.Fail = true;
        try
        {
            var asked = await ovsanna.PostAsync(play, play + "?handler=Mail", new() { ["Email"] = "ovsanna.k@example.com" });
            Assert.Equal($"{play}?mail=failed#mail", asked.Headers.Location!.OriginalString);
            Assert.Contains("be sent. Please try again a bit later.", await ovsanna.PageAsync($"{play}?mail=failed"));
            await ovsanna.PostAsync(join, join + "?handler=Forgot", new() { ["ForgotLogin"] = "petros@example.com" });
            var signup = await (await Browser.OpenAsync(app)).PostAsync(join, join, Games.JoinFields("Sevak"));
            Assert.Contains("be sent. Please try again a bit later.", await signup.Content.ReadAsStringAsync(Ct));
        }
        finally
        {
            app.Mailbox.Fail = false;
        }
        Assert.DoesNotContain("A confirmation e-mail went to", await ovsanna.PageAsync(play));
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var petros = await db.Participants.SingleAsync(p => p.Name == "Petros", Ct);
            Assert.Null(petros.ResetToken);
            Assert.Null(petros.ResetSentAt);
            Assert.False(await db.Signups.AnyAsync(s => s.Email == "sevak@example.com", Ct));
        }

        // Nothing went out, so asking again right away is not "too soon".
        Assert.Equal($"{play}#mail", (await ovsanna.PostAsync(play, play + "?handler=Mail", new() { ["Email"] = "ovsanna.k@example.com" })).Headers.Location!.OriginalString);
        await ovsanna.PostAsync(join, join + "?handler=Forgot", new() { ["ForgotLogin"] = "petros@example.com" });
        Assert.Equal(HttpStatusCode.Redirect, (await (await Browser.OpenAsync(app)).PostAsync(join, join, Games.JoinFields("Sevak"))).StatusCode);
        Assert.Single(app.Mailbox.Sent, m => m.To == "ovsanna.k@example.com");
        Assert.Single(app.Mailbox.Sent, m => m.To == "petros@example.com" && m.Kind == "reset");
        Assert.Single(app.Mailbox.Sent, m => m.To == "sevak@example.com");
    }

    [Fact]
    public async Task Unlinking_the_address_also_stops_a_mailed_reset_link()
    {
        SantaApp.SkipWithoutDatabase();
        var game = await Games.NewAsync(app, ["Sona", "Tigran", "Vardan"], draw: false);
        var play = $"/play/{game.Invite}";
        var join = $"/join/{game.Invite}";
        var sona = game.People["Sona"];
        await SignsInWithLoginAsync("Sona");
        Assert.Contains("Linked address: sona@example.com", await sona.PageAsync(play));
        await (await Browser.OpenAsync(app)).PostAsync(join, join + "?handler=Forgot", new() { ["ForgotLogin"] = "sona" });
        var link = app.Mailbox.LinkTo("sona@example.com");

        var removed = await sona.PostAsync(play, play + "?handler=MailRemove", []);

        Assert.Equal($"{play}#mail", removed.Headers.Location!.OriginalString);
        Assert.Null(await EmailAsync("Sona"));
        Assert.DoesNotContain("Linked address", await sona.PageAsync(play));
        Assert.Equal(HttpStatusCode.NotFound, (await (await Browser.OpenAsync(app)).GetAsync(link)).StatusCode);

        // An address that is also the login stays.
        var tigran = game.People["Tigran"];
        Assert.DoesNotContain("Unlink the address", await tigran.PageAsync(play));
        Assert.Equal(HttpStatusCode.NotFound, (await tigran.PostAsync(play, play + "?handler=MailRemove", [])).StatusCode);
        Assert.Equal("tigran@example.com", await EmailAsync("Tigran"));
    }

    private async Task<HttpResponseMessage> SignInAsync(string join, string login, string password)
    {
        var device = await Browser.OpenAsync(app);
        return await device.PostAsync(join, join + "?handler=SignIn", new() { ["SignInLogin"] = login, ["SignInPassword"] = password });
    }

    private async Task<string?> EmailAsync(string name)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        return await db.Participants.Where(p => p.Name == name).Select(p => p.Email).SingleAsync(Ct);
    }

    // As if the participant had signed up with a login before addresses and linked their address later.
    private Task SignsInWithLoginAsync(string name) => UpdateAsync(name, p => p.Login = name.ToLowerInvariant());

    private async Task UpdateAsync(string name, Action<Participant> change)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        change(await db.Participants.SingleAsync(p => p.Name == name, Ct));
        await db.SaveChangesAsync(Ct);
    }
}

public sealed class MailOffTests(SantaAppWithoutMail app) : IClassFixture<SantaAppWithoutMail>
{
    [Fact]
    public async Task Without_a_relay_the_address_signs_in_and_nothing_is_mailed()
    {
        SantaApp.SkipWithoutDatabase();
        var game = await Games.NewAsync(app, ["Anahit", "Bagrat", "Gevorg"], draw: false);
        var play = $"/play/{game.Invite}";
        var join = $"/join/{game.Invite}";
        var anahit = game.People["Anahit"];

        Assert.DoesNotContain("id=\"mail\"", await anahit.PageAsync(play));
        Assert.DoesNotContain("Forgot your password?", await anahit.PageAsync(join));
        Assert.Equal(HttpStatusCode.NotFound, (await anahit.PostAsync(play, play + "?handler=Mail", new() { ["Email"] = "anahit@example.com" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anahit.PostAsync(join, join + "?handler=Forgot", new() { ["ForgotLogin"] = "anahit@example.com" })).StatusCode);
        Assert.DoesNotContain("no e-mail", await game.Organizer.PageAsync(game.Admin));
        var device = await Browser.OpenAsync(app);
        var signedIn = await device.PostAsync(join, join + "?handler=SignIn", new() { ["SignInLogin"] = "anahit@example.com", ["SignInPassword"] = "secret-1" });
        Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);

        // Nothing confirmed the addresses, and one confirmed while a relay was set up gets no notices once it is gone.
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            Assert.False(await db.Participants.AnyAsync(p => p.Email != null, TestContext.Current.CancellationToken));
            await db.Participants.Where(p => p.Name == "Anahit").ExecuteUpdateAsync(s => s.SetProperty(p => p.Email, "anahit@example.com"), TestContext.Current.CancellationToken);
        }
        Assert.Equal(HttpStatusCode.Redirect, (await game.Organizer.PostAsync(game.Admin, game.Admin + "?handler=Draw", [])).StatusCode);
        Assert.False(app.Services.GetRequiredService<MailQueue>().Reader.TryRead(out _));
        Assert.Empty(app.Mailbox.Sent);
    }
}
