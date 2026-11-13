namespace SecretSatana.Data;

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
    public string? Wishlist { get; set; }
    public string? NotWanted { get; set; }
    public string Token { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public int? RecipientId { get; set; }
    public Participant? Recipient { get; set; }
}

public class Exclusion
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public int FirstId { get; set; }
    public int SecondId { get; set; }
}
