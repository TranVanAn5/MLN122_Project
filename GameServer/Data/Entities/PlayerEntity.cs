namespace GameServer.Data.Entities;

public sealed class PlayerEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public Guid RoomId { get; set; }
    public Guid? UserId { get; set; }
    public int Score { get; set; } = 1000;
    public bool IsEliminated { get; set; }
    public string Role { get; set; } = "Contestant";
    public string? ConnectionId { get; set; }

    public GameRoomEntity Room { get; set; } = null!;
    public UserEntity? User { get; set; }
    public List<PlayerAnswerEntity> PlayerAnswers { get; set; } = [];
    public List<InvestmentEntity> InvestmentsMade { get; set; } = [];
    public List<InvestmentEntity> InvestmentsReceived { get; set; } = [];
    public List<ScoreEntity> Scores { get; set; } = [];
}
