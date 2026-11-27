using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecretSanta.Data;

namespace SecretSanta.Tests;

// Signing up with an e-mail address: nobody is in the game until they open the link mailed to the address.
public sealed class SignupTests(SantaApp app) : IClassFixture<SantaApp>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_sign_up_joins_the_game_only_through_the_link_in_its_email()
    {
        SantaApp.SkipWithoutDatabase();
        var game = await Games.NewAsync(app, ["Zaven", "Anush"], draw: false);
        var join = $"/join/{game.Invite}";
        var register = $"{join}?mode=register";
        var gevorg = await Browser.OpenAsync(app);

        var signedUp = await gevorg.PostAsync(join, join, Games.JoinFields("Gevorg"));

        Assert.Equal(register, signedUp.Headers.Location!.OriginalString);
        Assert.Contains("An e-mail went to gevorg@example.com.", await gevorg.PageAsync(register));
        Assert.DoesNotContain("An e-mail went to", await gevorg.PageAsync(register));
        var repeated = await (await Browser.OpenAsync(app)).PostAsync(join, join, Games.JoinFields("Gevorg"));
        Assert.Contains("An e-mail was sent a moment ago.", await repeated.Content.ReadAsStringAsync(Ct));
        var mail = Assert.Single(app.Mailbox.Sent, m => m.To == "gevorg@example.com");
        Assert.Equal(("signup", "Secret Santa: confirm your e-mail address"), (mail.Kind, mail.Subject));
        Assert.StartsWith("Hi Gevorg,", mail.Body);
        var link = app.Mailbox.LinkTo("gevorg@example.com");
        Assert.Matches("^/signup/[A-Za-z0-9_-]+\\?lang=en$", link);

        // Until then Gevorg is not in the game: the panel only lists him as waiting, and he can't sign in.
        Assert.Null(await Games.IdAsync(app, "gevorg"));
        var panel = await game.Organizer.PageAsync(game.Admin);
        Assert.Contains("Waiting for their e-mail confirmation: Gevorg", panel);
        Assert.DoesNotContain("@example.com", panel);
        Assert.Contains("This address isn&#x27;t confirmed yet", await (await SignInAsync(join, "gevorg@example.com")).Content.ReadAsStringAsync(Ct));

        // Opening the link, as a mail scanner would, only shows the button; the button joins, on any device.
        var phone = await Browser.OpenAsync(app);
        Assert.Contains("Join Secret Santa as &quot;Gevorg&quot; with the address gevorg@example.com?", await phone.PageAsync(link));
        Assert.Null(await Games.IdAsync(app, "gevorg"));
        var joined = await phone.PostAsync(link, link, []);
        Assert.Equal($"/play/{game.Invite}?new=true", joined.Headers.Location!.OriginalString);
        Assert.Contains("You&#x27;re in!", await phone.PageAsync($"/play/{game.Invite}?new=true"));
        Assert.NotNull(await Games.IdAsync(app, "gevorg"));
        Assert.DoesNotContain("Waiting for their e-mail confirmation", await game.Organizer.PageAsync(game.Admin));
        Assert.Equal(HttpStatusCode.NotFound, (await phone.GetAsync(link)).StatusCode);

        // From now on the address and the password sign in, and the address can't sign up again.
        Assert.Equal(HttpStatusCode.Redirect, (await SignInAsync(join, "Gevorg@Example.com")).StatusCode);
        var again = await (await Browser.OpenAsync(app)).PostAsync(join, join, Games.JoinFields("Gevorg"));
        Assert.Contains("This address is already in the game.", await again.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task A_stale_or_overtaken_sign_up_link_lets_nobody_in()
    {
        SantaApp.SkipWithoutDatabase();
        var game = await Games.NewAsync(app, ["Hrach", "Ishkhan"], draw: false);
        var join = $"/join/{game.Invite}";
        var device = await Browser.OpenAsync(app);

        // A day later the link has expired.
        await SignUpAsync(join, Games.JoinFields("Lernik"));
        var stale = app.Mailbox.LinkTo("lernik@example.com");
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            await db.Signups.Where(s => s.Email == "lernik@example.com").ExecuteUpdateAsync(s => s.SetProperty(x => x.SentAt, DateTime.UtcNow.AddHours(-25)), Ct);
        }
        Assert.Contains("This link has expired", await device.PageAsync(stale));
        Assert.Contains("This link has expired", await (await device.PostAsync("/", stale, [])).Content.ReadAsStringAsync(Ct));
        Assert.Null(await Games.IdAsync(app, "lernik"));

        // Two sign-ups with the same name: whoever opens their link first gets it.
        var other = Games.JoinFields("Mane");
        other["Email"] = "mane.k@example.com";
        await SignUpAsync(join, Games.JoinFields("Mane"));
        await SignUpAsync(join, other);
        var late = app.Mailbox.LinkTo("mane@example.com");
        var early = app.Mailbox.LinkTo("mane.k@example.com");
        Assert.Equal(HttpStatusCode.Redirect, (await device.PostAsync(early, early, [])).StatusCode);
        var refused = await (await Browser.OpenAsync(app)).PostAsync(late, late, []);
        Assert.Contains("someone else took the name &quot;Mane&quot;", await refused.Content.ReadAsStringAsync(Ct));

        // After the draw no link lets anyone in.
        await SignUpAsync(join, Games.JoinFields("Nairi"));
        var closed = app.Mailbox.LinkTo("nairi@example.com");
        Assert.Equal(HttpStatusCode.Redirect, (await game.Organizer.PostAsync(game.Admin, game.Admin + "?handler=Draw", [])).StatusCode);
        var shut = await (await Browser.OpenAsync(app)).PostAsync(closed, closed, []);
        Assert.Contains("joining is closed", await shut.Content.ReadAsStringAsync(Ct));
        Assert.Null(await Games.IdAsync(app, "nairi"));
    }

    private async Task SignUpAsync(string join, Dictionary<string, string> fields)
    {
        var response = await (await Browser.OpenAsync(app)).PostAsync(join, join, fields);
        Assert.Equal($"{join}?mode=register", response.Headers.Location!.OriginalString);
    }

    private async Task<HttpResponseMessage> SignInAsync(string join, string login)
    {
        var device = await Browser.OpenAsync(app);
        return await device.PostAsync(join, join + "?handler=SignIn", new() { ["SignInLogin"] = login, ["SignInPassword"] = "secret-1" });
    }
}
