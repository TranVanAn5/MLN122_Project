namespace GameServer.Presentation.Requests;

public sealed record CreateRoomRequest(string HostName);
public sealed record JoinRoomRequest(string Name);
public sealed record SubmitAnswerRequest(string PlayerId, string Answer);
public sealed record EliminatePlayersRequest(IReadOnlyList<string> PlayerIds);
public sealed record InvestRequest(string InvestorPlayerId, string TargetPlayerId, int Amount);
public sealed record FinishGameRequest(string WinnerPlayerId);
