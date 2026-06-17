namespace GameServer.Data.Entities;

public sealed class PlayerAnswerEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RoundId { get; set; }
    public Guid PlayerId { get; set; }
    public string Answer { get; set; } = "";
    public bool IsCorrect { get; set; }
    public TimeSpan ResponseTime { get; set; }
    public DateTime AnsweredAt { get; set; } = DateTime.UtcNow;

    public GameRoundEntity Round { get; set; } = null!;
    public PlayerEntity Player { get; set; } = null!;
}
