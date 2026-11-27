using SecretSanta.Data;

namespace SecretSanta.Services;

// Signing up with an e-mail address: the person joins the game only through the link mailed to that address.
public static class Signups
{
    // Whether someone in the game already signs in with the address: an address belongs to one participant.
    public static bool Taken(Game game, string email) => game.Participants.Any(p => p.Login == email);

    // Null when the sign-up can still join, otherwise the key of the reason: since the e-mail went out, the draw may
    // have happened, the game may have filled up, or someone else may have taken the address or the name.
    public static string? Problem(Game game, Signup signup) =>
        game.DrawnAt is not null ? "JoinClosed"
        : game.Participants.Count >= Limits.MaxParticipants ? "GameFull"
        : Taken(game, signup.Email) ? "MailTaken"
        : game.Participants.Any(p => string.Equals(p.Name, signup.Name, StringComparison.CurrentCultureIgnoreCase)) ? "SignupNameTaken"
        : null;

    // The participant a confirmed sign-up becomes: the address is both the login and the confirmed e-mail.
    public static Participant Admit(Signup signup) => new()
    {
        GameId = signup.GameId,
        Name = signup.Name,
        Login = signup.Email,
        Email = signup.Email,
        PasswordHash = signup.PasswordHash,
        Wishlist = signup.Wishlist,
        NotWanted = signup.NotWanted,
        Lang = signup.Lang,
        Token = Tokens.New(),
        CreatedAt = DateTime.UtcNow,
    };
}
