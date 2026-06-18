namespace GameServer.Data.Entities;

public sealed class GameRoomEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string RoomCode { get; set; } = "";
    public string HostName { get; set; } = "Host";
    public string Status { get; set; } = "Lobby";
    public int CurrentRound { get; set; }
    public int CurrentQuestionIndex { get; set; } = -1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? QuestionEndsAt { get; set; }
    public Guid? WinnerPlayerId { get; set; }

    public List<PlayerEntity> Players { get; set; } = [];
    public List<GameRoundEntity> Rounds { get; set; } = [];
    public List<InvestmentEntity> Investments { get; set; } = [];
}
