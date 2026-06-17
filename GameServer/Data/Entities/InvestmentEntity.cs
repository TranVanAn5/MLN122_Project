namespace GameServer.Data.Entities;

public sealed class InvestmentEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RoomId { get; set; }
    public Guid InvestorPlayerId { get; set; }
    public Guid TargetPlayerId { get; set; }
    public int Amount { get; set; }
    public decimal ProfitRate { get; set; } = 0.3m;
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public GameRoomEntity Room { get; set; } = null!;
    public PlayerEntity InvestorPlayer { get; set; } = null!;
    public PlayerEntity TargetPlayer { get; set; } = null!;
}
