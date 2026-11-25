using SecretSanta.Data;
using SecretSanta.Services;

namespace SecretSanta.Tests;

public class PasswordTests
{
    private static readonly DateTime Now = new(2026, 12, 1, 12, 0, 0, DateTimeKind.Utc);

    private static Participant WithPassword(string password)
    {
        var person = new Participant { Name = "Ani" };
        Passwords.Set(person, password);
        return person;
    }

    [Fact]
    public void Stored_hash_is_not_the_password()
    {
        var person = WithPassword("secret1");

        Assert.NotNull(person.PasswordHash);
        Assert.DoesNotContain("secret1", person.PasswordHash);
    }

    [Fact]
    public void Right_password_signs_in_and_clears_failures()
    {
        var person = WithPassword("secret1");

        Assert.Equal(LoginResult.Wrong, Passwords.Check(person, "Secret1", Now));
        Assert.Equal(LoginResult.Ok, Passwords.Check(person, "secret1", Now));
        Assert.Equal(0, person.FailedLogins);
    }

    [Fact]
    public void Five_wrong_passwords_lock_for_15_minutes()
    {
        var person = WithPassword("secret1");

        for (var i = 1; i < Passwords.MaxAttempts; i++) Assert.Equal(LoginResult.Wrong, Passwords.Check(person, "nope", Now));
        Assert.Equal(LoginResult.Locked, Passwords.Check(person, "nope", Now));
        Assert.Equal(LoginResult.Locked, Passwords.Check(person, "secret1", Now.AddMinutes(14)));
        Assert.Equal(LoginResult.Ok, Passwords.Check(person, "secret1", Now.AddMinutes(15)));
    }

    [Fact]
    public void Participant_without_password_cannot_sign_in()
    {
        Assert.Equal(LoginResult.NoPassword, Passwords.Check(new Participant { Name = "Ani" }, "anything", Now));
    }

    [Theory]
    [InlineData(null, null, "PasswordShort")]
    [InlineData("12345", "12345", "PasswordShort")]
    [InlineData("123456", "123457", "PasswordMismatch")]
    [InlineData("123456", "123456", null)]
    public void New_password_rules(string? password, string? repeat, string? problem)
    {
        Assert.Equal(problem, Passwords.Problem(password, repeat));
    }
}
