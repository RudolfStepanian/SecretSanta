using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecretSanta.Data;
using SecretSanta.Services;

namespace SecretSanta.Tests;

// Resetting a forgotten password by e-mail, on the real pages. Mail lands in app.Mailbox; each test uses its own
// names, so it finds its own mail there. Everyone signs up with name@example.com (Games).
public sealed class MailTests(SantaApp app) : IClassFixture<SantaApp>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_forgotten_password_is_reset_through_the_address()
    {
        SantaApp.SkipWithoutDatabase();
        var game = await Games.NewAsync(app, ["Levon", "Mariam", "Narek"], draw: true);
        // Mariam stands for someone who signed up with a login before addresses.
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
        Assert.Contains("If this address is in the game", await phone.PageAsync($"{join}?forgot=true"));
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
        var join = $"/join/{game.Invite}";
        var device = await Browser.OpenAsync(app);

        app.Mailbox.Fail = true;
        try
        {
            await device.PostAsync(join, join + "?handler=Forgot", new() { ["ForgotLogin"] = "petros@example.com" });
            var signup = await device.PostAsync(join, join, Games.JoinFields("Sevak"));
            Assert.Contains("be sent. Please try again a bit later.", await signup.Content.ReadAsStringAsync(Ct));
        }
        finally
        {
            app.Mailbox.Fail = false;
        }
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var petros = await db.Participants.SingleAsync(p => p.Name == "Petros", Ct);
            Assert.Null(petros.ResetToken);
            Assert.Null(petros.ResetSentAt);
            Assert.False(await db.Signups.AnyAsync(s => s.Email == "sevak@example.com", Ct));
        }

        // Nothing went out, so asking again right away is not "too soon".
        await device.PostAsync(join, join + "?handler=Forgot", new() { ["ForgotLogin"] = "petros@example.com" });
        Assert.Equal(HttpStatusCode.Redirect, (await device.PostAsync(join, join, Games.JoinFields("Sevak"))).StatusCode);
        Assert.Single(app.Mailbox.Sent, m => m.To == "petros@example.com" && m.Kind == "reset");
        Assert.Single(app.Mailbox.Sent, m => m.To == "sevak@example.com");
    }

    private async Task<HttpResponseMessage> SignInAsync(string join, string login, string password)
    {
        var device = await Browser.OpenAsync(app);
        return await device.PostAsync(join, join + "?handler=SignIn", new() { ["SignInLogin"] = login, ["SignInPassword"] = password });
    }

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
        var join = $"/join/{game.Invite}";
        var device = await Browser.OpenAsync(app);

        Assert.DoesNotContain("Forgot your password?", await device.PageAsync(join));
        Assert.Equal(HttpStatusCode.NotFound, (await device.PostAsync(join, join + "?handler=Forgot", new() { ["ForgotLogin"] = "anahit@example.com" })).StatusCode);
        Assert.DoesNotContain("no e-mail", await game.Organizer.PageAsync(game.Admin));
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
