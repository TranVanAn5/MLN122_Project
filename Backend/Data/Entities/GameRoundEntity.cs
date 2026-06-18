namespace GameServer.Data.Entities;

public sealed class GameRoundEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RoomId { get; set; }
    public Guid QuestionId { get; set; }
    public int RoundNumber { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }

    public GameRoomEntity Room { get; set; } = null!;
    public QuestionEntity Question { get; set; } = null!;
    public List<PlayerAnswerEntity> PlayerAnswers { get; set; } = [];
    public List<ScoreEntity> Scores { get; set; } = [];
}
