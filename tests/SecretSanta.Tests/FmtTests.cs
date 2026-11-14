using SecretSanta.Services;

namespace SecretSanta.Tests;

public class FmtTests
{
    [Fact]
    public void Dates_are_shown_in_yerevan_time()
    {
        Assert.True(Fmt.HasLocalZone, $"{Fmt.ZoneId} is missing from the OS time zone data.");
        Assert.Equal("20.12.2026 12:00", Fmt.Local(new DateTime(2026, 12, 20, 8, 0, 0, DateTimeKind.Utc)));
    }
}
