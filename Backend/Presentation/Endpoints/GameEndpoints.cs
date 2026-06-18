using GameServer.Business.Services;
using GameServer.Presentation.Requests;

namespace GameServer.Presentation.Endpoints;

public static class GameEndpoints
{
    public static IEndpointRouteBuilder MapGameEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/rooms", async (CreateRoomRequest request, GameStateService games, CancellationToken cancellationToken) =>
        {
            var room = await games.CreateRoomAsync(request.HostName, cancellationToken);
            return Results.Ok(room);
        });

        app.MapPost("/api/rooms/{roomCode}/players", async (string roomCode, JoinRoomRequest request, GameStateService games, CancellationToken cancellationToken) =>
        {
            var result = await games.JoinRoomAsync(roomCode, request.Name, cancellationToken);
            return result is null ? Results.NotFound("Room not found or already full.") : Results.Ok(result);
        });

        app.MapGet("/api/rooms/{roomCode}", async (string roomCode, GameStateService games, CancellationToken cancellationToken) =>
        {
            var room = await games.GetRoomAsync(roomCode, cancellationToken);
            return room is null ? Results.NotFound() : Results.Ok(room);
        });

        app.MapGet("/api/rooms/{roomCode}/events", async (string roomCode, GameStateService games, HttpContext context, CancellationToken cancellationToken) =>
        {
            context.Response.Headers.CacheControl = "no-cache";
            context.Response.Headers.Connection = "keep-alive";
            context.Response.ContentType = "text/event-stream";

            string? lastPayload = null;
            while (!cancellationToken.IsCancellationRequested)
            {
                var room = await games.GetRoomAsync(roomCode, cancellationToken);
                if (room is null)
                {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }

                var payload = System.Text.Json.JsonSerializer.Serialize(room, System.Text.Json.JsonSerializerOptions.Web);
                if (payload != lastPayload)
                {
                    await context.Response.WriteAsync($"data: {payload}\n\n", cancellationToken);
                    await context.Response.Body.FlushAsync(cancellationToken);
                    lastPayload = payload;
                }

                await Task.Delay(1000, cancellationToken);
            }
        });

        app.MapPost("/api/rooms/{roomCode}/start-question", async (string roomCode, GameStateService games, CancellationToken cancellationToken) =>
        {
            var room = await games.StartQuestionAsync(roomCode, cancellationToken);
            return room is null ? Results.NotFound() : Results.Ok(room);
        });

        app.MapPost("/api/rooms/{roomCode}/answers", async (string roomCode, SubmitAnswerRequest request, GameStateService games, CancellationToken cancellationToken) =>
        {
            var room = await games.SubmitAnswerAsync(roomCode, request.PlayerId, request.Answer, cancellationToken);
            return room is null ? Results.NotFound() : Results.Ok(room);
        });

        app.MapPost("/api/rooms/{roomCode}/lock-round", async (string roomCode, GameStateService games, CancellationToken cancellationToken) =>
        {
            var room = await games.LockRoundAsync(roomCode, cancellationToken);
            return room is null ? Results.NotFound() : Results.Ok(room);
        });

        app.MapPost("/api/rooms/{roomCode}/eliminate", async (string roomCode, EliminatePlayersRequest request, GameStateService games, CancellationToken cancellationToken) =>
        {
            var room = await games.EliminatePlayersAsync(roomCode, request.PlayerIds, cancellationToken);
            return room is null ? Results.NotFound() : Results.Ok(room);
        });

        app.MapPost("/api/rooms/{roomCode}/investments", async (string roomCode, InvestRequest request, GameStateService games, CancellationToken cancellationToken) =>
        {
            var room = await games.InvestAsync(roomCode, request.InvestorPlayerId, request.TargetPlayerId, request.Amount, cancellationToken);
            return room is null ? Results.NotFound() : Results.Ok(room);
        });

        app.MapPost("/api/rooms/{roomCode}/start-final", async (string roomCode, GameStateService games, CancellationToken cancellationToken) =>
        {
            var room = await games.StartFinalAsync(roomCode, cancellationToken);
            return room is null ? Results.NotFound() : Results.Ok(room);
        });

        app.MapPost("/api/rooms/{roomCode}/start-final-question", async (string roomCode, GameStateService games, CancellationToken cancellationToken) =>
        {
            var room = await games.StartFinalQuestionAsync(roomCode, cancellationToken);
            return room is null ? Results.NotFound() : Results.Ok(room);
        });

        app.MapPost("/api/rooms/{roomCode}/finish", async (string roomCode, FinishGameRequest request, GameStateService games, CancellationToken cancellationToken) =>
        {
            var room = await games.FinishGameAsync(roomCode, request.WinnerPlayerId, cancellationToken);
            return room is null ? Results.NotFound() : Results.Ok(room);
        });

        return app;
    }
}
