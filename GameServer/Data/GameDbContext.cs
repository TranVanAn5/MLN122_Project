using GameServer.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace GameServer.Data;

public sealed class GameDbContext(DbContextOptions<GameDbContext> options) : DbContext(options)
{
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<GameRoomEntity> GameRooms => Set<GameRoomEntity>();
    public DbSet<PlayerEntity> Players => Set<PlayerEntity>();
    public DbSet<QuestionEntity> Questions => Set<QuestionEntity>();
    public DbSet<AnswerEntity> Answers => Set<AnswerEntity>();
    public DbSet<GameRoundEntity> GameRounds => Set<GameRoundEntity>();
    public DbSet<PlayerAnswerEntity> PlayerAnswers => Set<PlayerAnswerEntity>();
    public DbSet<InvestmentEntity> Investments => Set<InvestmentEntity>();
    public DbSet<ScoreEntity> Scores => Set<ScoreEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserEntity>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(user => user.Id);
            entity.Property(user => user.DisplayName).HasMaxLength(120).IsRequired();
            entity.Property(user => user.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        });

        modelBuilder.Entity<GameRoomEntity>(entity =>
        {
            entity.ToTable("GameRooms");
            entity.HasKey(room => room.Id);
            entity.HasIndex(room => room.RoomCode).IsUnique();
            entity.Property(room => room.RoomCode).HasMaxLength(12).IsRequired();
            entity.Property(room => room.HostName).HasMaxLength(120).IsRequired();
            entity.Property(room => room.Status).HasMaxLength(40).IsRequired();
            entity.Property(room => room.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        });

        modelBuilder.Entity<PlayerEntity>(entity =>
        {
            entity.ToTable("Players");
            entity.HasKey(player => player.Id);
            entity.Property(player => player.Name).HasMaxLength(120).IsRequired();
            entity.Property(player => player.Role).HasMaxLength(40).IsRequired();
            entity.Property(player => player.ConnectionId).HasMaxLength(160);
            entity.HasOne(player => player.Room)
                .WithMany(room => room.Players)
                .HasForeignKey(player => player.RoomId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(player => player.User)
                .WithMany(user => user.Players)
                .HasForeignKey(player => player.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<QuestionEntity>(entity =>
        {
            entity.ToTable("Questions");
            entity.HasKey(question => question.Id);
            entity.Property(question => question.Content).HasMaxLength(1000).IsRequired();
            entity.Property(question => question.OptionA).HasMaxLength(500).IsRequired();
            entity.Property(question => question.OptionB).HasMaxLength(500).IsRequired();
            entity.Property(question => question.OptionC).HasMaxLength(500).IsRequired();
            entity.Property(question => question.OptionD).HasMaxLength(500).IsRequired();
            entity.Property(question => question.CorrectAnswer).HasMaxLength(1).IsRequired();
            entity.Property(question => question.Explanation).HasMaxLength(1000);
            entity.Property(question => question.Difficulty).HasMaxLength(40).IsRequired();
            entity.Property(question => question.Topic).HasMaxLength(120);
        });

        modelBuilder.Entity<AnswerEntity>(entity =>
        {
            entity.ToTable("Answers");
            entity.HasKey(answer => answer.Id);
            entity.Property(answer => answer.Label).HasMaxLength(1).IsRequired();
            entity.Property(answer => answer.Content).HasMaxLength(500).IsRequired();
            entity.HasOne(answer => answer.Question)
                .WithMany(question => question.Answers)
                .HasForeignKey(answer => answer.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GameRoundEntity>(entity =>
        {
            entity.ToTable("GameRounds");
            entity.HasKey(round => round.Id);
            entity.Property(round => round.Status).HasMaxLength(40).IsRequired();
            entity.HasOne(round => round.Room)
                .WithMany(room => room.Rounds)
                .HasForeignKey(round => round.RoomId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(round => round.Question)
                .WithMany(question => question.Rounds)
                .HasForeignKey(round => round.QuestionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PlayerAnswerEntity>(entity =>
        {
            entity.ToTable("PlayerAnswers");
            entity.HasKey(answer => answer.Id);
            entity.Property(answer => answer.Answer).HasMaxLength(1).IsRequired();
            entity.HasIndex(answer => new { answer.RoundId, answer.PlayerId }).IsUnique();
            entity.HasOne(answer => answer.Round)
                .WithMany(round => round.PlayerAnswers)
                .HasForeignKey(answer => answer.RoundId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(answer => answer.Player)
                .WithMany(player => player.PlayerAnswers)
                .HasForeignKey(answer => answer.PlayerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<InvestmentEntity>(entity =>
        {
            entity.ToTable("Investments");
            entity.HasKey(investment => investment.Id);
            entity.Property(investment => investment.ProfitRate).HasPrecision(5, 2);
            entity.Property(investment => investment.Status).HasMaxLength(40).IsRequired();
            entity.HasOne(investment => investment.Room)
                .WithMany(room => room.Investments)
                .HasForeignKey(investment => investment.RoomId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(investment => investment.InvestorPlayer)
                .WithMany(player => player.InvestmentsMade)
                .HasForeignKey(investment => investment.InvestorPlayerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(investment => investment.TargetPlayer)
                .WithMany(player => player.InvestmentsReceived)
                .HasForeignKey(investment => investment.TargetPlayerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ScoreEntity>(entity =>
        {
            entity.ToTable("Scores");
            entity.HasKey(score => score.Id);
            entity.Property(score => score.Reason).HasMaxLength(120).IsRequired();
            entity.Property(score => score.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasOne(score => score.Player)
                .WithMany(player => player.Scores)
                .HasForeignKey(score => score.PlayerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(score => score.Round)
                .WithMany(round => round.Scores)
                .HasForeignKey(score => score.RoundId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
