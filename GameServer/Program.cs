using GameServer;
using GameServer.Data;
using GameServer.Hubs;
using GameServer.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .SetIsOriginAllowed(_ => true);
    });
});

builder.Services.AddSignalR();
builder.Services.AddScoped<GameStateService>();
builder.Services.AddDbContext<GameDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.EnableRetryOnFailure()));

var app = builder.Build();

if (app.Configuration.GetValue("Database:AutoMigrate", true))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
    db.Database.Migrate();
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors();

app.MapHub<GameHub>("/gameHub");

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

app.MapFallbackToFile("index.html");

app.Run();

public sealed record CreateRoomRequest(string HostName);
public sealed record JoinRoomRequest(string Name);
public sealed record SubmitAnswerRequest(string PlayerId, string Answer);
public sealed record EliminatePlayersRequest(IReadOnlyList<string> PlayerIds);
public sealed record InvestRequest(string InvestorPlayerId, string TargetPlayerId, int Amount);
public sealed record FinishGameRequest(string WinnerPlayerId);
