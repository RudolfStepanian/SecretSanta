using Microsoft.AspNetCore.Identity;
using SecretSanta.Data;

namespace SecretSanta.Services;

public enum LoginResult { Ok, Wrong, Locked, NoPassword }

// Participants sign in with their name and password inside a game. Five wrong passwords in a row lock the
// participant for 15 minutes; the organizer can reset the password from the panel.
public static class Passwords
{
    public const int MaxAttempts = 5;
    public static readonly TimeSpan LockFor = TimeSpan.FromMinutes(15);

    private static readonly PasswordHasher<Participant> Hasher = new();

    // Null when the new password is acceptable, otherwise the key of the error text.
    public static string? Problem(string? password, string? repeat) =>
        password is null || password.Length < Limits.PasswordMin ? "PasswordShort"
        : password != repeat ? "PasswordMismatch"
        : null;

    public static void Set(Participant person, string password)
    {
        person.PasswordHash = Hasher.HashPassword(person, password);
        person.FailedLogins = 0;
        person.LockedUntil = null;
    }

    // For a sign-up that becomes a participant later; the hash doesn't depend on the participant.
    public static string Hash(string password) => Hasher.HashPassword(new Participant(), password);

    public static LoginResult Check(Participant person, string password, DateTime now)
    {
        if (person.LockedUntil > now) return LoginResult.Locked;
        if (person.PasswordHash is null) return LoginResult.NoPassword;

        var result = Hasher.VerifyHashedPassword(person, person.PasswordHash, password);
        if (result != PasswordVerificationResult.Failed)
        {
            if (result == PasswordVerificationResult.SuccessRehashNeeded) person.PasswordHash = Hasher.HashPassword(person, password);
            person.FailedLogins = 0;
            person.LockedUntil = null;
            return LoginResult.Ok;
        }

        if (++person.FailedLogins < MaxAttempts) return LoginResult.Wrong;
        person.FailedLogins = 0;
        person.LockedUntil = now + LockFor;
        return LoginResult.Locked;
    }
}
