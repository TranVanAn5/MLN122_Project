namespace GameServer.Data.Entities;

public sealed class ScoreEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlayerId { get; set; }
    public Guid? RoundId { get; set; }
    public int Delta { get; set; }
    public int TotalAfter { get; set; }
    public string Reason { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public PlayerEntity Player { get; set; } = null!;
    public GameRoundEntity? Round { get; set; }
}
