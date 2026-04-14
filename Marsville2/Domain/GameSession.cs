using Marsville2.Domain.Entities;

namespace Marsville2.Domain;

/// <summary>
/// Top-level singleton that owns the list of registered players (persists across rounds)
/// and the history of completed rounds.
/// </summary>
public class GameSession
{
    // playerId -> Player (carries cumulative score across rounds)
    private readonly Dictionary<string, Player> _players = new();
    // token -> playerId  (for authentication)
    private readonly Dictionary<string, string> _tokenIndex = new();

    public GameRound? CurrentRound { get; private set; }
    public List<GameRound> CompletedRounds { get; } = new();

    // Cumulative scores per team: teamName -> total points
    public Dictionary<string, int> CumulativeScores { get; } = new();

    public IReadOnlyDictionary<string, Player> Players => _players;

    public Player RegisterPlayer(string teamName)
    {
        var id = Guid.NewGuid().ToString();
        var token = Guid.NewGuid().ToString("N");
        var player = new Player(id, teamName, token, 0, 0);
        _players[id] = player;
        _tokenIndex[token] = id;
        CumulativeScores.TryAdd(teamName, 0);
        return player;
    }

    public Player? GetPlayerByToken(string token)
    {
        if (!_tokenIndex.TryGetValue(token, out var id)) return null;
        _players.TryGetValue(id, out var player);
        return player;
    }

    public Player? GetPlayer(string playerId)
    {
        _players.TryGetValue(playerId, out var player);
        return player;
    }

    public void SetCurrentRound(GameRound round)
    {
        CurrentRound = round;
    }

    public void FinalizeCurrentRound()
    {
        if (CurrentRound is null) return;
        CurrentRound.End();

        foreach (var (playerId, pts) in CurrentRound.RoundScores)
        {
            if (_players.TryGetValue(playerId, out var player))
            {
                player.TotalScore += pts;
                CumulativeScores[player.TeamName] =
                    CumulativeScores.GetValueOrDefault(player.TeamName) + pts;
            }
        }

        CompletedRounds.Add(CurrentRound);
        CurrentRound = null;
    }

    public bool ValidateAdminPassword(string password, IConfiguration configuration)
    {
        var expected = configuration["AdminPassword"];
        return !string.IsNullOrWhiteSpace(expected) && expected == password;
    }
}
