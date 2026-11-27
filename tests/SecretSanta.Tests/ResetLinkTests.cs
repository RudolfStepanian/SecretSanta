using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SecretSanta.Data;
using SecretSanta.Pages;
using SecretSanta.Services;

namespace SecretSanta.Tests;

// Runs against a real PostgreSQL, in a throwaway database, when SECRETSANTA_TEST_DB holds an Npgsql connection string
// to a server (CI starts one); skipped otherwise. Each page call gets its own DbContext, as each request would.
public sealed class ResetLinkTests : IAsyncDisposable
{
    private string? connectionString;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Reset_link_signs_in_once_then_says_when_it_was_used()
    {
        await CreateDatabaseAsync();
        var (game, person) = await SeedAsync(withPassword: true);

        var link = await ResetAsync(game, person);
        // Opening the link, as a mail scanner or a chat preview can, only shows the button.
        var viewed = await ViewAsync(link);
        Assert.IsType<PageResult>(viewed.Result);
        Assert.Null(viewed.Page.UsedAt);
        Assert.False(viewed.Page.Expired);
        Assert.Empty(viewed.SetCookie);

        var first = await UseAsync(link);
        Assert.Equal($"/play/{game.InviteToken}", Assert.IsType<RedirectResult>(first.Result).Url);
        await using (var db = NewDb())
        {
            var token = (await db.Participants.SingleAsync(p => p.Id == person.Id, Ct)).Token;
            Assert.NotEqual(person.Token, token); // the reset signed the old sessions out
            Assert.Contains($"ss_{game.Id}={token}", first.SetCookie);
        }

        var again = await UseAsync(link);
        Assert.IsType<PageResult>(again.Result);
        Assert.NotNull(again.Page.UsedAt);
        Assert.Empty(again.SetCookie);
        Assert.NotNull((await ViewAsync(link)).Page.UsedAt);
    }

    [Fact]
    public async Task A_newer_reset_replaces_the_unused_link()
    {
        await CreateDatabaseAsync();
        var (game, person) = await SeedAsync(withPassword: true);

        var old = await ResetAsync(game, person);
        var current = await ResetAsync(game, person);

        Assert.IsType<NotFoundResult>((await UseAsync(old)).Result);
        Assert.IsType<RedirectResult>((await UseAsync(current)).Result);
    }

    [Fact]
    public async Task Personal_link_from_before_passwords_keeps_working()
    {
        await CreateDatabaseAsync();
        var (_, person) = await SeedAsync(withPassword: false);

        Assert.IsType<RedirectResult>((await ViewAsync(person.Token)).Result);
        Assert.IsType<RedirectResult>((await ViewAsync(person.Token)).Result);
    }

    [Fact]
    public async Task A_mailed_link_works_for_a_day_and_then_changes_nothing()
    {
        await CreateDatabaseAsync();
        var (_, early) = await SeedAsync(withPassword: true);
        var (_, late) = await SeedAsync(withPassword: true);
        var fresh = await MailedLinkAsync(early, sentHoursAgo: 23);
        var stale = await MailedLinkAsync(late, sentHoursAgo: 25);

        Assert.True((await ViewAsync(stale)).Page.Expired);
        var refused = await UseAsync(stale);
        Assert.IsType<PageResult>(refused.Result);
        Assert.True(refused.Page.Expired);
        Assert.Empty(refused.SetCookie);
        Assert.False((await ViewAsync(fresh)).Page.Expired);
        Assert.IsType<RedirectResult>((await UseAsync(fresh)).Result);

        await using var db = NewDb();
        var stalePerson = await db.Participants.SingleAsync(p => p.Id == late.Id, Ct);
        Assert.NotNull(stalePerson.PasswordHash);
        Assert.Equal(late.Token, stalePerson.Token);
        // The used link signed the other devices out and asks for a new password, as the organizer's link does.
        var freshPerson = await db.Participants.SingleAsync(p => p.Id == early.Id, Ct);
        Assert.Null(freshPerson.PasswordHash);
        Assert.NotEqual(early.Token, freshPerson.Token);
    }

    private async Task<string> ResetAsync(Game game, Participant person)
    {
        await using var db = NewDb();
        var admin = WithContext(new AdminModel(db, new Notifier(db, new PushQueue())));
        await admin.OnPostResetAsync(game.AdminToken, person.Id);
        var path = new Uri(admin.ResetUrl!).AbsolutePath;
        Assert.StartsWith("/me/", path);
        return path["/me/".Length..];
    }

    // What "Forgot your password?" leaves behind: a link token and when it was mailed.
    private async Task<string> MailedLinkAsync(Participant person, int sentHoursAgo)
    {
        await using var db = NewDb();
        var token = Tokens.New();
        await db.Participants.Where(p => p.Id == person.Id).ExecuteUpdateAsync(s => s
            .SetProperty(p => p.ResetToken, token)
            .SetProperty(p => p.ResetSentAt, DateTime.UtcNow.AddHours(-sentHoursAgo)), Ct);
        return token;
    }

    private Task<(IActionResult Result, EnterModel Page, string SetCookie)> ViewAsync(string token) =>
        CallAsync(page => page.OnGetAsync(token));

    private Task<(IActionResult Result, EnterModel Page, string SetCookie)> UseAsync(string token) =>
        CallAsync(page => page.OnPostAsync(token));

    private async Task<(IActionResult Result, EnterModel Page, string SetCookie)> CallAsync(Func<EnterModel, Task<IActionResult>> call)
    {
        await using var db = NewDb();
        var page = WithContext(new EnterModel(db));
        var result = await call(page);
        return (result, page, page.Response.Headers.SetCookie.ToString());
    }

    private async Task<(Game Game, Participant Person)> SeedAsync(bool withPassword)
    {
        await using var db = NewDb();
        var game = new Game { AdminToken = Tokens.New(), InviteToken = Tokens.New(), CreatedAt = DateTime.UtcNow };
        var person = new Participant { Game = game, Name = "Anna", Login = withPassword ? "anna" : null, Token = Tokens.New(), CreatedAt = DateTime.UtcNow };
        if (withPassword) Passwords.Set(person, "secret-1");
        db.Participants.Add(person);
        await db.SaveChangesAsync(Ct);
        return (game, person);
    }

    private static T WithContext<T>(T page) where T : PageModel
    {
        var http = new DefaultHttpContext();
        http.Request.Scheme = "https";
        http.Request.Host = new HostString("santa.test");
        page.PageContext = new PageContext { HttpContext = http };
        return page;
    }

    private async Task CreateDatabaseAsync()
    {
        var server = Environment.GetEnvironmentVariable("SECRETSANTA_TEST_DB") ?? "";
        Assert.SkipWhen(server.Length == 0, "SECRETSANTA_TEST_DB is not set, so there is no PostgreSQL to test against.");
        connectionString = new NpgsqlConnectionStringBuilder(server)
        {
            Database = $"santa_test_{Guid.NewGuid():N}",
            GssEncryptionMode = GssEncryptionMode.Disable,
        }.ConnectionString;
        await using var db = NewDb();
        await db.Database.MigrateAsync(Ct);
    }

    private AppDb NewDb() => new(new DbContextOptionsBuilder<AppDb>().UseNpgsql(connectionString).Options);

    public async ValueTask DisposeAsync()
    {
        if (connectionString is null) return;
        await using var db = NewDb();
        await db.Database.EnsureDeletedAsync(Ct);
    }
}
