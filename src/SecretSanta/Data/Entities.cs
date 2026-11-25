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
    // How many times the organizer reset the password, and when last: shown on the participant's page.
    public int ResetCount { get; set; }
    public DateTime? ResetAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? RecipientId { get; set; }
    public Participant? Recipient { get; set; }
    public DateTime? RevealedAt { get; set; }
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

public class Exclusion
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public int FirstId { get; set; }
    public int SecondId { get; set; }
}

// One browser that asked for push notifications for a participant; a participant can have several devices.
// Lang is the page language at the moment of subscribing, so notifications come in that language.
public class PushSubscription
{
    public int Id { get; set; }
    public int ParticipantId { get; set; }
    public Participant Participant { get; set; } = null!;
    public string Endpoint { get; set; } = "";
    public string P256dh { get; set; } = "";
    public string Auth { get; set; } = "";
    public string Lang { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

// Values the app creates for itself, such as the VAPID key pair for push notifications.
public class AppSetting
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}
