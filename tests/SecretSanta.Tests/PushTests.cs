using System.Net;
using Microsoft.Extensions.DependencyInjection;
using SecretSanta.Data;
using SecretSanta.Localization;
using SecretSanta.Services;

namespace SecretSanta.Tests;

// Which notifications the game events queue. Delivery is switched off in SantaApp, so the messages wait in the queue.
public sealed class PushTests(SantaApp app) : IClassFixture<SantaApp>
{
    [Fact]
    public async Task Cancelling_the_draw_notifies_subscribers_in_their_language_and_names_nobody()
    {
        SantaApp.SkipWithoutDatabase();
        var game = await Games.NewAsync(app, ["Lilit", "Mher", "Nare"], draw: true);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            db.PushSubscriptions.Add(new PushSubscription
            {
                ParticipantId = (await Games.IdAsync(app, "lilit"))!.Value,
                Endpoint = "https://fcm.googleapis.com/fcm/send/test",
                P256dh = "key",
                Auth = "secret",
                Lang = "hy",
                CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        var queue = app.Services.GetRequiredService<PushQueue>();
        while (queue.Reader.TryRead(out _)) { }

        var cancel = await game.Organizer.PostAsync(game.Admin, game.Admin + "?handler=Undraw", []);

        Assert.Equal(HttpStatusCode.Redirect, cancel.StatusCode);
        Assert.True(queue.Reader.TryRead(out var message));
        Assert.Equal(Strings.Get("PushDrawCancelled", "hy"), message.Body);
        Assert.Equal($"/play/{game.Invite}", message.Url);
        Assert.All(game.People.Keys, name => Assert.DoesNotContain(name, message.Title + message.Body));
        Assert.False(queue.Reader.TryRead(out _));

        // Cancelling when there is no draw sends nothing.
        await game.Organizer.PostAsync(game.Admin, game.Admin + "?handler=Undraw", []);
        Assert.False(queue.Reader.TryRead(out _));
    }
}
