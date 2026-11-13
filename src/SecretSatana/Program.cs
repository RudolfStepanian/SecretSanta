using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using SecretSatana.Data;
using SecretSatana.Localization;
using SecretSatana.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDb>(o => o.UseNpgsql(Database.ConnectionString(builder.Configuration)));
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<AppDb>()
    .SetApplicationName("SecretSatana");
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<Text>();
builder.Services.AddRazorPages();
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AppDb>().Database.Migrate();
}

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
app.MapRazorPages();

app.Run();
