using SecretSanta.Services;

namespace SecretSanta.Tests;

public class LoginTests
{
    [Theory]
    [InlineData("ani", "ani")]
    [InlineData("  Ani.K ", "ani.k")]
    [InlineData("david_2-x", "david_2-x")]
    [InlineData("abcdefghijklmnopqrstuvwxyz012345", "abcdefghijklmnopqrstuvwxyz012345")]
    public void Valid_logins_are_stored_trimmed_in_lower_case(string typed, string stored)
    {
        Assert.Equal(stored, Logins.Normalize(typed));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("an")]
    [InlineData("ani k")]
    [InlineData("ани")]
    [InlineData("անի")]
    [InlineData("ani@bank")]
    [InlineData("abcdefghijklmnopqrstuvwxyz0123456")]
    public void Invalid_logins_are_rejected(string? typed)
    {
        Assert.Null(Logins.Normalize(typed));
    }
}
