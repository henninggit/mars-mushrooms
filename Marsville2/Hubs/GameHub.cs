using Microsoft.AspNetCore.SignalR;

namespace Marsville2.Hubs;

/// <summary>
/// SignalR hub for real-time spectator and admin updates.
/// Server-to-client events:
///   BoardUpdated(playerId, boardState)
///   AllBoardsSnapshot(allBoards)
///   RoundStarted(roundInfo)
///   RoundEnded(scores)
/// </summary>
public class GameHub : Hub
{
    public override Task OnConnectedAsync()
    {
        return base.OnConnectedAsync();
    }
}
