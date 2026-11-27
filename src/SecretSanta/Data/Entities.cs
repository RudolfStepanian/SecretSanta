namespace SecretSanta.Data;

public class Game
{
    public int Id { get; set; }
    public string? Budget { get; set; }
    public string? Note { get; set; }
    public string AdminToken { get; set; } = "";
    public string InviteToken { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? DrawnAt { get; set; }
    public List<Participant> Participants { get; set; } = [];
    public List<Exclusion> Exclusions { get; set; } = [];
}

public class Participant
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public Game Game { get; set; } = null!;
    public string Name { get; set; } = "";
    public string? Login { get; set; }
    public string? Wishlist { get; set; }
    public string? NotWanted { get; set; }
    public string Token { get; set; } = "";
    public string? PasswordHash { get; set; }
    public int FailedLogins { get; set; }
    public DateTime? LockedUntil { get; set; }
    // The one-time link from a password reset, and when it was opened: opened again, the link shows that time.
    public string? ResetToken { get; set; }
    public DateTime? ResetUsedAt { get; set; }
    // When the reset link went out by e-mail: such a link works for a day. The organizer's link has none and doesn't expire.
    public DateTime? ResetSentAt { get; set; }
    // How many times the organizer reset the password, and when last: shown on the participant's page.
    public int ResetCount { get; set; }
    public DateTime? ResetAt { get; set; }
    // The confirmed address that a forgotten password can be reset through; only the participant sees it.
    public string? Email { get; set; }
    // An address waiting for confirmation, the token of the link sent to it and when that link was sent.
    public string? PendingEmail { get; set; }
    public string? EmailToken { get; set; }
    public DateTime? EmailSentAt { get; set; }
    // The language the participant last opened their page in: e-mails to them are written in it.
    public string? Lang { get; set; }
    // When the participant last opened their page, and when the last e-mail about new messages went to them: the next
    // message is mailed only if the page was opened since.
    public DateTime? SeenAt { get; set; }
    public DateTime? MessageMailAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? RecipientId { get; set; }
    public Participant? Recipient { get; set; }
    public DateTime? RevealedAt { get; set; }
    // Santa's own mark that the gift for their recipient is ready; the organizer sees the marks, not who they are for.
    public DateTime? GiftReadyAt { get; set; }
}

// One thread per recipient: their Santa asks, they answer. The sender is stored only as a side, never as a person.
public class Message
{
    public int Id { get; set; }
    public int ParticipantId { get; set; }
    public Participant Participant { get; set; } = null!;
    public bool FromSanta { get; set; }
    public string Text { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

// A sign-up waiting for its address to be confirmed. It becomes a participant only through the link mailed to the
// address, so until then the person is not in the game: not on the list, not in the draw, and can't sign in.
public class Signup
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public Game Game { get; set; } = null!;
    public string Email { get; set; } = "";
    public string Name { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string? Wishlist { get; set; }
    public string? NotWanted { get; set; }
    public string Token { get; set; } = "";
    public string Lang { get; set; } = "";
    public DateTime SentAt { get; set; }
}

public class Exclusion
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public int FirstId { get; set; }
    public int SecondId { get; set; }
}
