namespace GameServer.Data.Entities;

public sealed class UserEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DisplayName { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<PlayerEntity> Players { get; set; } = [];
}
