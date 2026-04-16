namespace Marsville2.Domain;

public enum RoundPhase
{
    Registration,
    Playing,
    Ended
}

/// <summary>
/// Represents one game round (one level) across all registered players.
/// In levels 1-8 each player gets their own Board instance.
/// In levels 9-10 all players share a single Board instance.
/// </summary>
public class GameRound
{
    public string RoundId { get; } = Guid.NewGuid().ToString();
    public int Level { get; }
    public int Seed { get; }
    public RoundPhase Phase { get; private set; } = RoundPhase.Registration;
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? EndedAt { get; private set; }
    public int TimeoutSeconds { get; }

    public bool IsSharedBoard => LevelFactory.IsSharedLevel(Level);
    private readonly Dictionary<string, Board> _playerBoards = new(); // playerId -> board
    private Board? _sharedBoard;

    public IReadOnlyDictionary<string, Board> PlayerBoards => _playerBoards;
    public Board? SharedBoard => _sharedBoard;

    // Scores awarded in this round: playerId -> points
    public Dictionary<string, int> RoundScores { get; } = new();

    public GameRound(int level, int seed, int timeoutSeconds = 300)
    {
        Level = level;
        Seed = seed;
        TimeoutSeconds = timeoutSeconds;
    }

    public void Start()
    {
        Phase = RoundPhase.Playing;
        StartedAt = DateTimeOffset.UtcNow;
    }

    public void End()
    {
        Phase = RoundPhase.Ended;
        EndedAt = DateTimeOffset.UtcNow;
    }

    public bool HasTimedOut =>
        Phase == RoundPhase.Playing &&
        StartedAt.HasValue &&
        (DateTimeOffset.UtcNow - StartedAt.Value).TotalSeconds >= TimeoutSeconds;

    public void AssignPlayerBoard(string playerId, Board board)
    {
        _playerBoards[playerId] = board;
    }

    public void SetSharedBoard(Board board) => _sharedBoard = board;

    public Board? GetBoardForPlayer(string playerId)
    {
        if (IsSharedBoard) return _sharedBoard;
        _playerBoards.TryGetValue(playerId, out var board);
        return board;
    }

    public bool IsPlayerFinished(string playerId)
    {
        var board = GetBoardForPlayer(playerId);
        if (board is null) return true; // never joined
        var player = board.Players.FirstOrDefault(p => p.Id == playerId);
        return player is null || !player.IsAlive;
    }

    public bool AllPlayersFinished()
    {
        if (!IsSharedBoard)
            return _playerBoards.Keys.All(IsPlayerFinished);

        // Shared board: all players dead or goal reached
        return _sharedBoard is null ||
               !_sharedBoard.Players.Any(p => p.IsAlive);
    }
}
