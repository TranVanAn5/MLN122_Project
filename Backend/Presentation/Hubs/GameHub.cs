using GameServer.Business.Services;
using Microsoft.AspNetCore.SignalR;

namespace GameServer.Presentation.Hubs;

public sealed class GameHub(GameStateService games) : Hub
{
    public async Task JoinRoomGroup(string roomCode, string playerId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, roomCode);
        await games.AttachConnectionAsync(roomCode, playerId, Context.ConnectionId);

        var snapshot = await games.GetRoomAsync(roomCode);
        if (snapshot is not null)
        {
            await Clients.Group(roomCode).SendAsync("RoomUpdated", snapshot);
        }
    }

    public async Task StartQuestion(string roomCode) => await Broadcast(roomCode, await games.StartQuestionAsync(roomCode));

    public Task SubmitAnswer(string roomCode, string playerId, string answer) =>
        SubmitAnswerAsync(roomCode, playerId, answer);

    public async Task LockRound(string roomCode) => await Broadcast(roomCode, await games.LockRoundAsync(roomCode));

    public async Task EliminatePlayers(string roomCode, IReadOnlyList<string> playerIds) =>
        await Broadcast(roomCode, await games.EliminatePlayersAsync(roomCode, playerIds));

    public Task Invest(string roomCode, string investorPlayerId, string targetPlayerId, int amount) =>
        InvestAsync(roomCode, investorPlayerId, targetPlayerId, amount);

    public async Task StartFinal(string roomCode) => await Broadcast(roomCode, await games.StartFinalAsync(roomCode));

    public async Task StartFinalQuestion(string roomCode) => await Broadcast(roomCode, await games.StartFinalQuestionAsync(roomCode));

    public Task FinishGame(string roomCode, string winnerPlayerId) =>
        FinishGameAsync(roomCode, winnerPlayerId);

    private async Task SubmitAnswerAsync(string roomCode, string playerId, string answer) =>
        await Broadcast(roomCode, await games.SubmitAnswerAsync(roomCode, playerId, answer));

    private async Task InvestAsync(string roomCode, string investorPlayerId, string targetPlayerId, int amount) =>
        await Broadcast(roomCode, await games.InvestAsync(roomCode, investorPlayerId, targetPlayerId, amount));

    private async Task FinishGameAsync(string roomCode, string winnerPlayerId) =>
        await Broadcast(roomCode, await games.FinishGameAsync(roomCode, winnerPlayerId));

    private async Task Broadcast(string roomCode, object? snapshot)
    {
        if (snapshot is not null)
        {
            await Clients.Group(roomCode).SendAsync("RoomUpdated", snapshot);
        }
    }
}
