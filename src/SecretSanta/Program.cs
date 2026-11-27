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
builder.Services.AddHttpClient();
builder.Services.AddHostedService<KeepAwake>();
builder.Services.AddSingleton(services =>
{
    using var scope = services.CreateScope();
    return VapidKeys.LoadOrCreate(scope.ServiceProvider.GetRequiredService<AppDb>());
});
builder.Services.AddSingleton<PushQueue>();
builder.Services.AddScoped<Notifier>();
builder.Services.AddHostedService<PushDelivery>();
builder.Services.AddHostedService<GameCleanupService>();
// E-mail goes through the relay once it is configured; in Development without one, to the log; otherwise it is off.
var mail = builder.Configuration.GetSection("Mail");
builder.Services.Configure<MailOptions>(mail);
string mailMode;
if (mail.Get<MailOptions>() is { Configured: true })
{
    builder.Services.AddHttpClient<IMailSender, ScriptMailSender>(c => c.Timeout = TimeSpan.FromSeconds(20));
    mailMode = "on, through the relay";
}
else if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<IMailSender, LogMailSender>();
    mailMode = "written to the log";
}
else
{
    builder.Services.AddSingleton<IMailSender, NoMailSender>();
    mailMode = "off: Mail__ScriptUrl and Mail__Key are not set";
}
builder.Services.AddScoped<Text>();
builder.Services.AddRazorPages();
builder.Services.AddRateLimiter(o =>
{
    o.GlobalLimiter = RateLimits.Create();
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

var app = builder.Build();

// Timed separately: building the EF model is CPU only (slow on a 0.1 CPU instance), the first connection includes
// waking a suspended Neon compute, and Migrate is the migrations check itself.
long modelMs, connectMs, migrateMs;
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    var step = Stopwatch.StartNew();
    _ = db.Model;
    modelMs = step.ElapsedMilliseconds;
    step.Restart();
    db.Database.OpenConnection();
    connectMs = step.ElapsedMilliseconds;
    step.Restart();
    db.Database.Migrate();
    migrateMs = step.ElapsedMilliseconds;
}

if (!Fmt.HasLocalZone) app.Logger.LogWarning("Time zone {Zone} was not found; dates are shown in UTC.", Fmt.ZoneId);
app.Logger.LogInformation("E-mail is {Mode}.", mailMode);

app.Lifetime.ApplicationStarted.Register(() =>
{
    using var process = Process.GetCurrentProcess();
    var startup = DateTime.UtcNow - process.StartTime.ToUniversalTime();
    app.Logger.LogInformation(
        "Started in {StartupMs} ms from process start; EF model {ModelMs} ms, first database connection {ConnectMs} ms, Database.Migrate() {MigrateMs} ms.",
        (long)startup.TotalMilliseconds, modelMs, connectMs, migrateMs);
});

app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/error");
app.UseStatusCodePagesWithReExecute("/not-found/{0}");
app.Use(async (ctx, next) =>
{
    // A status code page runs the pipeline again for the same request, without its query: it keeps the language the
    // first run resolved, so the ?lang of the address that failed still counts.
    if (!ctx.Items.ContainsKey(Languages.ItemKey)) ctx.Items[Languages.ItemKey] = Languages.Resolve(ctx);
    ctx.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
    ctx.Response.Headers["Referrer-Policy"] = "no-referrer";
    SecurityHeaders.Apply(ctx.Response);
    await next();
});
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();

// Liveness probe for Render and the keep-awake pinger: never touches the database and is never cached.
app.MapMethods("/healthz", [HttpMethods.Get, HttpMethods.Head], (HttpResponse response) =>
{
    response.Headers.CacheControl = "no-store";
    return Results.Text("ok");
}).ShortCircuit();

app.MapRazorPages();

app.Run();
