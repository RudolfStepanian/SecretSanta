using System.Diagnostics;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using SecretSanta.Data;
using SecretSanta.Localization;
using SecretSanta.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDb>(o => o.UseNpgsql(Database.ConnectionString(builder.Configuration)));
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<AppDb>()
    .SetApplicationName("SecretSanta");
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<Text>();
builder.Services.AddRazorPages();
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

var app = builder.Build();

// The first connection to the database is part of this, so on a cold start it shows how long the database took to wake up.
var migration = Stopwatch.StartNew();
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AppDb>().Database.Migrate();
}
migration.Stop();

if (!Fmt.HasLocalZone) app.Logger.LogWarning("Time zone {Zone} was not found; dates are shown in UTC.", Fmt.ZoneId);

app.Lifetime.ApplicationStarted.Register(() =>
{
    using var process = Process.GetCurrentProcess();
    var startup = DateTime.UtcNow - process.StartTime.ToUniversalTime();
    app.Logger.LogInformation(
        "Started in {StartupMs} ms from process start; Database.Migrate() took {MigrateMs} ms.",
        (long)startup.TotalMilliseconds, migration.ElapsedMilliseconds);
});

app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/error");
app.UseStatusCodePagesWithReExecute("/not-found");
app.Use(async (ctx, next) =>
{
    ctx.Items[Languages.ItemKey] = Languages.Resolve(ctx);
    ctx.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
    ctx.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next();
});
app.UseStaticFiles();
app.UseRouting();

// Liveness probe for Render and the keep-awake pinger: never touches the database and is never cached.
app.MapMethods("/healthz", [HttpMethods.Get, HttpMethods.Head], (HttpResponse response) =>
{
    response.Headers.CacheControl = "no-store";
    return Results.Text("ok");
}).ShortCircuit();

app.MapRazorPages();

app.Run();
