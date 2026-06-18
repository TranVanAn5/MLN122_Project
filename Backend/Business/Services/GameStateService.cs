using GameServer.Data;
using GameServer.Data.Entities;
using GameServer.Business.Models;
using Microsoft.EntityFrameworkCore;

namespace GameServer.Business.Services;

public sealed class GameStateService(GameDbContext db)
{
    public async Task<RoomSnapshot> CreateRoomAsync(string? hostName, CancellationToken cancellationToken = default)
    {
        await SeedQuestionsAsync(cancellationToken);

        var code = await CreateCodeAsync(cancellationToken);
        var room = new GameRoomEntity
        {
            RoomCode = code,
            HostName = string.IsNullOrWhiteSpace(hostName) ? "Host" : hostName.Trim(),
            Status = RoomStatus.Lobby.ToString()
        };

        db.GameRooms.Add(room);
        await db.SaveChangesAsync(cancellationToken);
        return await SnapshotAsync(code, cancellationToken) ?? throw new InvalidOperationException("Room was not created.");
    }

    public async Task<JoinRoomResult?> JoinRoomAsync(string roomCode, string playerName, CancellationToken cancellationToken = default)
    {
        var normalizedCode = roomCode.Trim().ToUpperInvariant();
        var room = await db.GameRooms
            .Include(item => item.Players)
            .FirstOrDefaultAsync(item => item.RoomCode == normalizedCode, cancellationToken);

        if (room is null || room.Players.Count >= 10 || room.Status != RoomStatus.Lobby.ToString())
        {
            return null;
        }

        var player = new PlayerEntity
        {
            RoomId = room.Id,
            Name = string.IsNullOrWhiteSpace(playerName) ? $"Player {room.Players.Count + 1}" : playerName.Trim(),
            Role = PlayerRole.Contestant.ToString()
        };

        db.Players.Add(player);
        await db.SaveChangesAsync(cancellationToken);

        var snapshot = await SnapshotAsync(normalizedCode, cancellationToken);
        return snapshot is null ? null : new JoinRoomResult(player.Id.ToString("N"), snapshot);
    }

    public Task<RoomSnapshot?> GetRoomAsync(string roomCode, CancellationToken cancellationToken = default) =>
        SnapshotAsync(roomCode, cancellationToken);

    public async Task<RoomSnapshot?> StartQuestionAsync(string roomCode, CancellationToken cancellationToken = default)
    {
        await SeedQuestionsAsync(cancellationToken);

        var room = await FindRoomAsync(roomCode, cancellationToken);
        if (room is null)
        {
            return null;
        }

        var questions = await db.Questions
            .Where(question => question.Topic == null || question.Topic != "Final")
            .OrderBy(question => question.Content)
            .ToListAsync(cancellationToken);
        if (questions.Count == 0)
        {
            return await SnapshotAsync(room.RoomCode, cancellationToken);
        }

        var nextIndex = Math.Min(room.CurrentQuestionIndex + 1, questions.Count - 1);
        var question = questions[nextIndex];
        var nextRound = Math.Min(room.CurrentRound + 1, 3);

        room.CurrentQuestionIndex = nextIndex;
        room.CurrentRound = nextRound;
        room.Status = RoomStatus.Question.ToString();
        room.QuestionEndsAt = DateTime.UtcNow.AddSeconds(30);

        var oldRound = await CurrentRoundAsync(room.Id, room.CurrentRound, cancellationToken);
        if (oldRound is not null)
        {
            db.PlayerAnswers.RemoveRange(db.PlayerAnswers.Where(answer => answer.RoundId == oldRound.Id));
        }

        db.GameRounds.Add(new GameRoundEntity
        {
            RoomId = room.Id,
            QuestionId = question.Id,
            RoundNumber = nextRound,
            Status = RoomStatus.Question.ToString(),
            StartedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(cancellationToken);
        return await SnapshotAsync(room.RoomCode, cancellationToken);
    }

    public async Task<RoomSnapshot?> SubmitAnswerAsync(string roomCode, string playerId, string answer, CancellationToken cancellationToken = default)
    {
        var room = await FindRoomAsync(roomCode, cancellationToken);
        if (room is null || (room.Status != RoomStatus.Question.ToString() && room.Status != RoomStatus.Final.ToString()) || !Guid.TryParse(playerId, out var playerGuid))
        {
            return room is null ? null : await SnapshotAsync(room.RoomCode, cancellationToken);
        }

        var player = await db.Players.FirstOrDefaultAsync(
            item => item.Id == playerGuid && item.RoomId == room.Id && !item.IsEliminated,
            cancellationToken);
        var round = await CurrentRoundAsync(room.Id, room.CurrentRound, cancellationToken);
        if (player is null || round is null || player.Role == PlayerRole.Investor.ToString())
        {
            return await SnapshotAsync(room.RoomCode, cancellationToken);
        }

        var alreadyAnswered = await db.PlayerAnswers.AnyAsync(
            item => item.PlayerId == player.Id && item.RoundId == round.Id,
            cancellationToken);
        if (alreadyAnswered)
        {
            return await SnapshotAsync(room.RoomCode, cancellationToken);
        }

        var normalized = answer.Trim().ToUpperInvariant();
        var question = await db.Questions.FirstAsync(item => item.Id == round.QuestionId, cancellationToken);
        db.PlayerAnswers.Add(new PlayerAnswerEntity
        {
            PlayerId = player.Id,
            RoundId = round.Id,
            Answer = normalized,
            IsCorrect = normalized == question.CorrectAnswer,
            ResponseTime = room.QuestionEndsAt.HasValue ? room.QuestionEndsAt.Value - DateTime.UtcNow : TimeSpan.Zero,
            AnsweredAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(cancellationToken);
        return await SnapshotAsync(room.RoomCode, cancellationToken);
    }

    public async Task<RoomSnapshot?> LockRoundAsync(string roomCode, CancellationToken cancellationToken = default)
    {
        var room = await FindRoomAsync(roomCode, cancellationToken);
        if (room is null)
        {
            return null;
        }

        var round = await CurrentRoundAsync(room.Id, room.CurrentRound, cancellationToken);
        if (round is null)
        {
            return await SnapshotAsync(room.RoomCode, cancellationToken);
        }

        if (room.Status == RoomStatus.Final.ToString())
        {
            await LockFinalRoundAsync(room, round, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return await SnapshotAsync(room.RoomCode, cancellationToken);
        }

        var players = await db.Players
            .Where(player => player.RoomId == room.Id && !player.IsEliminated && player.Role != PlayerRole.Investor.ToString())
            .ToListAsync(cancellationToken);
        var answers = await db.PlayerAnswers
            .Where(answer => answer.RoundId == round.Id)
            .ToListAsync(cancellationToken);

        foreach (var player in players)
        {
            var correct = answers.FirstOrDefault(answer => answer.PlayerId == player.Id)?.IsCorrect == true;
            var delta = ScoreDelta(room.CurrentRound, correct);
            player.Score += delta;
            db.Scores.Add(new ScoreEntity
            {
                PlayerId = player.Id,
                RoundId = round.Id,
                Delta = delta,
                TotalAfter = player.Score,
                Reason = correct ? "CorrectAnswer" : "WrongAnswer"
            });
        }

        round.Status = RoomStatus.RoundResults.ToString();
        round.EndedAt = DateTime.UtcNow;
        if (room.Status == RoomStatus.Question.ToString())
        {
            room.Status = RoomStatus.RoundResults.ToString();
        }
        room.QuestionEndsAt = null;

        await db.SaveChangesAsync(cancellationToken);
        return await SnapshotAsync(room.RoomCode, cancellationToken);
    }

    public async Task<RoomSnapshot?> EliminatePlayersAsync(string roomCode, IReadOnlyCollection<string> playerIds, CancellationToken cancellationToken = default)
    {
        var room = await FindRoomAsync(roomCode, cancellationToken);
        if (room is null)
        {
            return null;
        }

        if (room.Status != RoomStatus.RoundResults.ToString())
        {
            return await SnapshotAsync(room.RoomCode, cancellationToken);
        }

        var selectedIds = playerIds
            .Select(id => Guid.TryParse(id, out var playerId) ? playerId : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .ToHashSet();
        var active = await db.Players
            .Where(player => player.RoomId == room.Id && !player.IsEliminated && player.Role != PlayerRole.Investor.ToString())
            .ToListAsync(cancellationToken);
        var selectedPlayers = active.Where(player => selectedIds.Contains(player.Id)).ToList();
        if (selectedPlayers.Count == 0)
        {
            return await SnapshotAsync(room.RoomCode, cancellationToken);
        }

        foreach (var player in selectedPlayers)
        {
            player.IsEliminated = true;
            player.Role = PlayerRole.Investor.ToString();
        }

        var remaining = active.Where(player => !selectedIds.Contains(player.Id)).ToList();
        if (remaining.Count == 1)
        {
            var finalist = remaining[0];
            finalist.Role = PlayerRole.Finalist.ToString();
            room.Status = RoomStatus.Investment.ToString();
            room.CurrentQuestionIndex = -1;
            room.QuestionEndsAt = null;
        }

        await db.SaveChangesAsync(cancellationToken);
        return await SnapshotAsync(room.RoomCode, cancellationToken);
    }

    public async Task<RoomSnapshot?> StartFinalQuestionAsync(string roomCode, CancellationToken cancellationToken = default)
    {
        await SeedQuestionsAsync(cancellationToken);

        var room = await FindRoomAsync(roomCode, cancellationToken);
        if (room is null)
        {
            return null;
        }

        if (room.Status != RoomStatus.Final.ToString())
        {
            return await SnapshotAsync(room.RoomCode, cancellationToken);
        }

        var finalists = await db.Players
            .Where(player => player.RoomId == room.Id && !player.IsEliminated && player.Role == PlayerRole.Finalist.ToString())
            .ToListAsync(cancellationToken);
        if (finalists.Count != 1)
        {
            return await SnapshotAsync(room.RoomCode, cancellationToken);
        }

        var finalQuestions = await db.Questions
            .Where(question => question.Topic == "Final")
            .OrderBy(question => question.Content)
            .ToListAsync(cancellationToken);
        if (finalQuestions.Count == 0)
        {
            return await SnapshotAsync(room.RoomCode, cancellationToken);
        }

        var nextIndex = Math.Min(room.CurrentQuestionIndex + 1, finalQuestions.Count - 1);
        var question = finalQuestions[nextIndex];
        var nextRound = room.CurrentRound + 1;

        room.CurrentQuestionIndex = nextIndex;
        room.CurrentRound = nextRound;
        room.QuestionEndsAt = DateTime.UtcNow.AddSeconds(30);

        db.GameRounds.Add(new GameRoundEntity
        {
            RoomId = room.Id,
            QuestionId = question.Id,
            RoundNumber = nextRound,
            Status = RoomStatus.Question.ToString(),
            StartedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(cancellationToken);
        return await SnapshotAsync(room.RoomCode, cancellationToken);
    }

    public async Task<RoomSnapshot?> InvestAsync(string roomCode, string investorPlayerId, string targetPlayerId, int amount, CancellationToken cancellationToken = default)
    {
        var room = await FindRoomAsync(roomCode, cancellationToken);
        if (room is null || room.Status != RoomStatus.Investment.ToString() || !Guid.TryParse(investorPlayerId, out var investorGuid) || !Guid.TryParse(targetPlayerId, out var targetGuid))
        {
            return room is null ? null : await SnapshotAsync(room.RoomCode, cancellationToken);
        }

        var investor = await db.Players.FirstOrDefaultAsync(
            player => player.Id == investorGuid && player.RoomId == room.Id && player.Role == PlayerRole.Investor.ToString(),
            cancellationToken);
        var target = await db.Players.FirstOrDefaultAsync(
            player => player.Id == targetGuid && player.RoomId == room.Id && player.Role == PlayerRole.Finalist.ToString(),
            cancellationToken);
        if (investor is null || target is null)
        {
            return await SnapshotAsync(room.RoomCode, cancellationToken);
        }

        var investedAmount = Math.Clamp(amount, 0, Math.Max(0, investor.Score));
        if (investedAmount <= 0)
        {
            return await SnapshotAsync(room.RoomCode, cancellationToken);
        }

        investor.Score -= investedAmount;
        target.Score += investedAmount;
        db.Investments.Add(new InvestmentEntity
        {
            RoomId = room.Id,
            InvestorPlayerId = investor.Id,
            TargetPlayerId = target.Id,
            Amount = investedAmount
        });

        await db.SaveChangesAsync(cancellationToken);
        return await SnapshotAsync(room.RoomCode, cancellationToken);
    }

    public async Task<RoomSnapshot?> StartFinalAsync(string roomCode, CancellationToken cancellationToken = default)
    {
        var room = await FindRoomAsync(roomCode, cancellationToken);
        if (room is null)
        {
            return null;
        }

        var finalists = await db.Players
            .Where(player => player.RoomId == room.Id && !player.IsEliminated && player.Role != PlayerRole.Investor.ToString())
            .ToListAsync(cancellationToken);

        if (finalists.Count != 1)
        {
            return await SnapshotAsync(room.RoomCode, cancellationToken);
        }

        foreach (var finalist in finalists)
        {
            finalist.Role = PlayerRole.Finalist.ToString();
        }

        var hasFinalRound = await db.GameRounds.AnyAsync(
            round => round.RoomId == room.Id && round.Question.Topic == "Final",
            cancellationToken);

        room.Status = RoomStatus.Final.ToString();
        if (!hasFinalRound)
        {
            room.CurrentQuestionIndex = -1;
        }

        await db.SaveChangesAsync(cancellationToken);
        return await SnapshotAsync(room.RoomCode, cancellationToken);
    }

    public async Task<RoomSnapshot?> FinishGameAsync(string roomCode, string winnerPlayerId, CancellationToken cancellationToken = default)
    {
        var room = await FindRoomAsync(roomCode, cancellationToken);
        if (room is null || !Guid.TryParse(winnerPlayerId, out var winnerGuid))
        {
            return room is null ? null : await SnapshotAsync(room.RoomCode, cancellationToken);
        }

        room.WinnerPlayerId = winnerGuid;
        await SettleInvestmentsAsync(room.Id, winnerGuid, cancellationToken);

        room.Status = RoomStatus.Finished.ToString();
        await db.SaveChangesAsync(cancellationToken);
        return await SnapshotAsync(room.RoomCode, cancellationToken);
    }

    public async Task<bool> AttachConnectionAsync(string roomCode, string playerId, string connectionId, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(playerId, out var playerGuid))
        {
            return false;
        }

        var room = await FindRoomAsync(roomCode, cancellationToken);
        var player = room is null
            ? null
            : await db.Players.FirstOrDefaultAsync(item => item.RoomId == room.Id && item.Id == playerGuid, cancellationToken);
        if (player is null)
        {
            return false;
        }

        player.ConnectionId = connectionId;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<RoomSnapshot?> SnapshotAsync(string roomCode, CancellationToken cancellationToken)
    {
        var normalizedCode = roomCode.Trim().ToUpperInvariant();
        var room = await db.GameRooms
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.RoomCode == normalizedCode, cancellationToken);
        if (room is null)
        {
            return null;
        }

        var round = await db.GameRounds
            .AsNoTracking()
            .Include(item => item.Question)
            .FirstOrDefaultAsync(item => item.RoomId == room.Id && item.RoundNumber == room.CurrentRound, cancellationToken);
        var answers = round is null
            ? new List<PlayerAnswerEntity>()
            : await db.PlayerAnswers.AsNoTracking().Where(answer => answer.RoundId == round.Id).ToListAsync(cancellationToken);
        var players = await db.Players
            .AsNoTracking()
            .Where(player => player.RoomId == room.Id)
            .OrderByDescending(player => player.Score)
            .ToListAsync(cancellationToken);
        var investments = await db.Investments
            .AsNoTracking()
            .Where(investment => investment.RoomId == room.Id)
            .ToListAsync(cancellationToken);
        var revealAnswer = ParseRoomStatus(room.Status) is RoomStatus.RoundResults or RoomStatus.Finished || round?.Status == RoomStatus.RoundResults.ToString();
        var question = round?.Question;

        return new RoomSnapshot
        {
            RoomCode = room.RoomCode,
            Status = ParseRoomStatus(room.Status),
            CurrentRound = room.CurrentRound,
            QuestionEndsAt = room.QuestionEndsAt,
            CurrentQuestion = question is null ? null : new QuestionView
            {
                Id = question.Id.ToString("N"),
                Content = question.Content,
                OptionA = question.OptionA,
                OptionB = question.OptionB,
                OptionC = question.OptionC,
                OptionD = question.OptionD,
                Difficulty = question.Difficulty,
                CorrectAnswer = revealAnswer ? question.CorrectAnswer : null,
                Explanation = revealAnswer ? question.Explanation : null
            },
            Players = players.Select(player => new PlayerView
            {
                Id = player.Id.ToString("N"),
                Name = player.Name,
                Score = player.Score,
                IsEliminated = player.IsEliminated,
                Role = ParsePlayerRole(player.Role),
                HasAnswered = answers.Any(answer => answer.PlayerId == player.Id)
            }).ToList(),
            Investments = investments.Select(investment => new Investment
            {
                Id = investment.Id.ToString("N"),
                InvestorPlayerId = investment.InvestorPlayerId.ToString("N"),
                TargetPlayerId = investment.TargetPlayerId.ToString("N"),
                Amount = investment.Amount,
                ProfitRate = investment.ProfitRate,
                Status = investment.Status
            }).ToList(),
            WinnerPlayerId = room.WinnerPlayerId?.ToString("N")
        };
    }

    private async Task<GameRoomEntity?> FindRoomAsync(string roomCode, CancellationToken cancellationToken) =>
        await db.GameRooms.FirstOrDefaultAsync(item => item.RoomCode == roomCode.Trim().ToUpperInvariant(), cancellationToken);

    private async Task<GameRoundEntity?> CurrentRoundAsync(Guid roomId, int roundNumber, CancellationToken cancellationToken) =>
        await db.GameRounds.FirstOrDefaultAsync(item => item.RoomId == roomId && item.RoundNumber == roundNumber, cancellationToken);

    private async Task LockFinalRoundAsync(GameRoomEntity room, GameRoundEntity round, CancellationToken cancellationToken)
    {
        var finalist = await db.Players.FirstOrDefaultAsync(
            player => player.RoomId == room.Id && player.Role == PlayerRole.Finalist.ToString() && !player.IsEliminated,
            cancellationToken);

        round.Status = RoomStatus.RoundResults.ToString();
        round.EndedAt = DateTime.UtcNow;
        room.QuestionEndsAt = null;

        if (finalist is null)
        {
            room.WinnerPlayerId = null;
            room.Status = RoomStatus.Finished.ToString();
            return;
        }

        var finalistAnswer = await db.PlayerAnswers.FirstOrDefaultAsync(
            answer => answer.RoundId == round.Id && answer.PlayerId == finalist.Id,
            cancellationToken);
        var pendingInvestments = await db.Investments
            .Where(investment => investment.RoomId == room.Id && investment.TargetPlayerId == finalist.Id && investment.Status == "Pending")
            .ToListAsync(cancellationToken);
        var investorIds = pendingInvestments.Select(investment => investment.InvestorPlayerId).Distinct().ToList();
        var investors = await db.Players
            .Where(player => investorIds.Contains(player.Id))
            .ToListAsync(cancellationToken);

        var investmentTotal = pendingInvestments.Sum(investment => investment.Amount);

        if (finalistAnswer?.IsCorrect == true)
        {
            var investorProfitTotal = 0;
            foreach (var investment in pendingInvestments)
            {
                var investor = investors.FirstOrDefault(player => player.Id == investment.InvestorPlayerId);
                if (investor is null)
                {
                    continue;
                }

                var profit = (int)Math.Round(investment.Amount * investment.ProfitRate);
                investorProfitTotal += profit;
                investor.Score += investment.Amount + profit;
                investment.Status = "Won";
            }

            var finalistDelta = 200 - investorProfitTotal;
            finalist.Score += finalistDelta;
            db.Scores.Add(new ScoreEntity
            {
                PlayerId = finalist.Id,
                RoundId = round.Id,
                Delta = finalistDelta,
                TotalAfter = finalist.Score,
                Reason = "FinalCorrect"
            });

            room.Status = RoomStatus.Investment.ToString();
            return;
        }

        foreach (var investment in pendingInvestments)
        {
            investment.Status = "Lost";
        }

        finalist.Score -= investmentTotal;
        db.Scores.Add(new ScoreEntity
        {
            PlayerId = finalist.Id,
            RoundId = round.Id,
            Delta = -investmentTotal,
            TotalAfter = finalist.Score,
            Reason = "FinalWrong"
        });

        room.WinnerPlayerId = null;
        room.Status = RoomStatus.Finished.ToString();
    }

    private async Task SettleInvestmentsAsync(Guid roomId, Guid? winnerPlayerId, CancellationToken cancellationToken)
    {
        var investments = await db.Investments.Where(investment => investment.RoomId == roomId).ToListAsync(cancellationToken);
        var investorIds = investments.Select(investment => investment.InvestorPlayerId).Distinct().ToList();
        var investors = await db.Players.Where(player => investorIds.Contains(player.Id)).ToListAsync(cancellationToken);

        foreach (var investment in investments.Where(investment => investment.Status == "Pending"))
        {
            var investor = investors.FirstOrDefault(player => player.Id == investment.InvestorPlayerId);
            if (investor is null)
            {
                continue;
            }

            if (winnerPlayerId.HasValue && investment.TargetPlayerId == winnerPlayerId.Value)
            {
                investor.Score += investment.Amount + (int)Math.Round(investment.Amount * investment.ProfitRate);
                investment.Status = "Won";
            }
            else
            {
                investor.Score += investment.Amount / 2;
                investment.Status = "Lost";
            }
        }
    }

    private async Task SeedQuestionsAsync(CancellationToken cancellationToken)
    {
        if (await db.Questions.AnyAsync(cancellationToken) && await db.Questions.AnyAsync(question => question.Topic == "Final", cancellationToken))
        {
            return;
        }

        if (!await db.Questions.AnyAsync(cancellationToken))
        {
            db.Questions.AddRange(QuestionBank.Create().Select(question => new QuestionEntity
            {
                Content = question.Content,
                OptionA = question.OptionA,
                OptionB = question.OptionB,
                OptionC = question.OptionC,
                OptionD = question.OptionD,
                CorrectAnswer = question.CorrectAnswer,
                Explanation = question.Explanation,
                Difficulty = question.Difficulty,
                Topic = "Main"
            }));
        }

        if (!await db.Questions.AnyAsync(question => question.Topic == "Final", cancellationToken))
        {
            db.Questions.AddRange(FinalQuestionBank.Create().Select(question => new QuestionEntity
            {
                Content = question.Content,
                OptionA = question.OptionA,
                OptionB = question.OptionB,
                OptionC = question.OptionC,
                OptionD = question.OptionD,
                CorrectAnswer = question.CorrectAnswer,
                Explanation = question.Explanation,
                Difficulty = question.Difficulty,
                Topic = "Final"
            }));
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<string> CreateCodeAsync(CancellationToken cancellationToken)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        string code;
        do
        {
            code = new string(Enumerable.Range(0, 5).Select(_ => chars[Random.Shared.Next(chars.Length)]).ToArray());
        }
        while (await db.GameRooms.AnyAsync(room => room.RoomCode == code, cancellationToken));

        return code;
    }

    private static int ScoreDelta(int round, bool correct) => round switch
    {
        1 => correct ? 100 : 0,
        2 => correct ? 200 : -50,
        _ => correct ? 300 : -100
    };

    private static RoomStatus ParseRoomStatus(string value) =>
        Enum.TryParse<RoomStatus>(value, out var status) ? status : RoomStatus.Lobby;

    private static PlayerRole ParsePlayerRole(string value) =>
        Enum.TryParse<PlayerRole>(value, out var role) ? role : PlayerRole.Contestant;
}

public sealed record JoinRoomResult(string PlayerId, RoomSnapshot Room);
