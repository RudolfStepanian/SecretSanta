using System.Collections.Concurrent;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using SecretSanta.Services;

namespace SecretSanta.Tests;

// The whole app against a real PostgreSQL, in a throwaway database on the server from SECRETSANTA_TEST_DB
// (CI starts one). Tests that use it call SkipWithoutDatabase first, so without that server they are skipped.
// The mail the app sends lands in Mailbox; SantaAppWithoutMail is the site as it runs before a mail relay is set up.
public class SantaApp : WebApplicationFactory<Program>, IAsyncLifetime
{
    private static readonly string DbServer = Environment.GetEnvironmentVariable("SECRETSANTA_TEST_DB") ?? "";
    private string? database;

    public TestMailbox Mailbox { get; } = new();

    protected virtual bool WithMail => true;

    public static void SkipWithoutDatabase() =>
        Assert.SkipWhen(DbServer.Length == 0, "SECRETSANTA_TEST_DB is not set, so there is no PostgreSQL to test against.");

    public async ValueTask InitializeAsync()
    {
        if (DbServer.Length == 0) return;
        database = $"santa_test_{Guid.NewGuid():N}";
        await using var connection = new NpgsqlConnection(DbServer);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var create = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", connection);
        await create.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Not Development, so appsettings.Development.json can't point the tests at a developer's database.
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", new NpgsqlConnectionStringBuilder(DbServer)
        {
            Database = database,
            GssEncryptionMode = GssEncryptionMode.Disable,
        }.ConnectionString);
        builder.ConfigureTestServices(services =>
        {
            // E-mails about game events stay in the queue, where tests read them.
            services.Remove(services.Single(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(MailDelivery)));
            if (WithMail) services.AddSingleton<IMailSender>(Mailbox);
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (database is null) return;
        NpgsqlConnection.ClearAllPools();
        await using var connection = new NpgsqlConnection(DbServer);
        await connection.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)", connection);
        await drop.ExecuteNonQueryAsync();
    }
}

public sealed class SantaAppWithoutMail : SantaApp
{
    protected override bool WithMail => false;
}

// Keeps the mail the app sends. With Fail set, sending fails the way an unreachable relay does.
public sealed class TestMailbox : IMailSender
{
    private readonly ConcurrentQueue<Mail> sent = new();

    public bool Enabled => true;
    public bool Fail { get; set; }
    public IReadOnlyList<Mail> Sent => [.. sent];

    public Task<bool> SendAsync(Mail mail, CancellationToken ct)
    {
        if (Fail) return Task.FromResult(false);
        sent.Enqueue(mail);
        return Task.FromResult(true);
    }

    // The path of the first link to this site in the last mail to the address.
    public string LinkTo(string address)
    {
        var mail = sent.Last(m => m.To == address);
        var link = Regex.Match(mail.Body, @"https?://[^/\s]+(/[^\s]+)").Groups[1].Value;
        Assert.NotEmpty(link);
        return link;
    }
}

// One visitor: keeps its cookies, does not follow redirects, and posts forms with the antiforgery token of the page.
public sealed class Browser
{
    private readonly HttpClient client;

    private Browser(SantaApp app) =>
        client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Pages come in English, so the tests can look for plain text.
    public static async Task<Browser> OpenAsync(SantaApp app)
    {
        var browser = new Browser(app);
        await browser.PageAsync("/?lang=en");
        return browser;
    }

    public Task<HttpResponseMessage> GetAsync(string url) => client.GetAsync(url, Ct);

    public async Task<string> PageAsync(string url)
    {
        var response = await GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync(Ct);
    }

    public async Task<HttpResponseMessage> PostAsync(string formPage, string action, Dictionary<string, string> fields, string? header = null)
    {
        var page = await PageAsync(formPage);
        var token = Regex.Match(page, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        var request = new HttpRequestMessage(HttpMethod.Post, action)
        {
            Content = new FormUrlEncodedContent(fields.Append(KeyValuePair.Create("__RequestVerificationToken", token))),
        };
        if (header is not null) request.Headers.Add(header, "1");
        return await client.SendAsync(request, Ct);
    }
}
