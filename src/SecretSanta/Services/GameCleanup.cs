using Microsoft.EntityFrameworkCore;
using SecretSanta.Data;

namespace SecretSanta.Services;

// A game is deleted with everything in it: participants, exclusions and messages all cascade from the game row in the
// database. The organizer can delete it at any time; otherwise it goes DaysAfterDraw days after
// the draw, or DaysWithoutDraw days after it was created when there was no draw.
public static class GameCleanup
{
    public const int DaysAfterDraw = 60;
    public const int DaysWithoutDraw = 120;

    public static DateTime DeleteAt(Game game) =>
        game.DrawnAt is { } drawn ? drawn.AddDays(DaysAfterDraw) : game.CreatedAt.AddDays(DaysWithoutDraw);

    public static Task<int> DeleteAsync(AppDb db, int gameId, CancellationToken ct = default) =>
        db.Games.Where(g => g.Id == gameId).ExecuteDeleteAsync(ct);

    public static Task<int> DeleteExpiredAsync(AppDb db, DateTime now, CancellationToken ct = default)
    {
        var drawnBefore = now.AddDays(-DaysAfterDraw);
        var createdBefore = now.AddDays(-DaysWithoutDraw);
        return db.Games
            .Where(g => (g.DrawnAt != null && g.DrawnAt < drawnBefore) || (g.DrawnAt == null && g.CreatedAt < createdBefore))
            .ExecuteDeleteAsync(ct);
    }
}

// Deletes expired games shortly after the app starts and then once a day. The free instance may sleep for long
// stretches; then the timer does not run, but the next start deletes whatever has expired in the meantime.
public sealed class GameCleanupService(IServiceScopeFactory scopes, ILogger<GameCleanupService> logger) : BackgroundService
{
    // A cold start first serves the request that woke the instance.
    private static readonly TimeSpan FirstRunDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan Interval = TimeSpan.FromDays(1);

    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        try
        {
            await Task.Delay(FirstRunDelay, stop);
            using var timer = new PeriodicTimer(Interval);
            do
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    var deleted = await GameCleanup.DeleteExpiredAsync(scope.ServiceProvider.GetRequiredService<AppDb>(), DateTime.UtcNow, stop);
                    if (deleted > 0) logger.LogInformation("Deleted {Count} expired games.", deleted);
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    logger.LogWarning(e, "Deleting expired games failed; trying again in a day.");
                }
            }
            while (await timer.WaitForNextTickAsync(stop));
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
        }
    }
}
