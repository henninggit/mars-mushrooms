using Marsville2.Domain;
using Marsville2.Domain.Cells;
using Marsville2.Domain.Entities;
using Marsville2.Domain.Items;
using Marsville2.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Marsville2.Services;

/// <summary>
/// Singleton orchestrator.  Owns the GameSession, one BoardService per active board,
/// and level 10 world-shrink logic.  Broadcasts SignalR events after every mutation.
/// </summary>
public class GameService
{
    private readonly GameSession _session;
    private readonly IHubContext<GameHub> _hub;
    private readonly ILogger<GameService> _logger;

    // playerId -> BoardService (level 1-8: one per player; level 9-10: all point to the same one)
    private readonly Dictionary<string, BoardService> _boardServices = new();
    private readonly object _globalLock = new();

    // Level 10 shrink timer
    private Timer? _shrinkTimer;
    private readonly TimeSpan _shrinkInterval = TimeSpan.FromSeconds(30);

    public GameService(GameSession session, IHubContext<GameHub> hub, ILogger<GameService> logger)
    {
        _session = session;
        _hub = hub;
        _logger = logger;
    }

    // ------------------------------------------------------------------ Admin: create/start/end round

    public GameRound CreateRound(int level, int timeoutSeconds, int? seed = null)
    {
        lock (_globalLock)
        {
            var actualSeed = seed ?? Random.Shared.Next();
            var round = new GameRound(level, actualSeed, timeoutSeconds);
            _session.SetCurrentRound(round);
            _boardServices.Clear();
            return round;
        }
    }

    public void StartRound()
    {
        lock (_globalLock)
        {
            var round = _session.CurrentRound
                ?? throw new InvalidOperationException("No active round to start.");

            if (round.Phase != RoundPhase.Registration)
                throw new InvalidOperationException("Round is not in Registration phase.");

            // Create boards for registered players
            if (round.IsSharedBoard)
            {
                var board = LevelFactory.CreateBoard(round.Level, round.Seed);

                // For level 12 seed mushrooms equal to player count
                if (round.Level == 12)
                    SeedLevel12Mushrooms(board, _session.Players.Count);

                round.SetSharedBoard(board);
                var svc = new BoardService(board);

                // Place players at spread-out start positions
                int idx = 0;
                foreach (var (playerId, player) in _session.Players)
                {
                    var maxHealth = round.Level == 12 ? 3 : 2;
                    var boardPlayer = new Player(playerId, player.TeamName, player.Token,
                        1 + idx * 2, 1, maxHealth);
                    board.AddPlayer(boardPlayer);
                    _boardServices[playerId] = svc;
                    idx++;
                }
            }
            else
            {
                foreach (var (playerId, player) in _session.Players)
                {
                    var board = LevelFactory.CreateBoard(round.Level, round.Seed);
                    var boardPlayer = new Player(playerId, player.TeamName, player.Token,
                        board.StartX, board.StartY);
                    board.AddPlayer(boardPlayer);
                    round.AssignPlayerBoard(playerId, board);
                    _boardServices[playerId] = new BoardService(board);
                }
            }

            round.Start();

            if (round.Level == 12)
                StartShrinkTimer();

            // Setup auto-end on timeout
            _ = Task.Delay(TimeSpan.FromSeconds(round.TimeoutSeconds))
                .ContinueWith(_ => AutoEndRound());

            _hub.Clients.All.SendAsync("RoundStarted", MapRoundInfo(round));
        }
    }

    public void EndRound()
    {
        lock (_globalLock)
        {
            var round = _session.CurrentRound
                ?? throw new InvalidOperationException("No active round.");

            FinalizeScores(round);
            _session.FinalizeCurrentRound();
            _boardServices.Clear();
            _shrinkTimer?.Dispose();
            _shrinkTimer = null;

            _hub.Clients.All.SendAsync("RoundEnded", MapScores(round));
        }
    }

    private void AutoEndRound()
    {
        lock (_globalLock)
        {
            if (_session.CurrentRound is { Phase: RoundPhase.Playing } round && round.HasTimedOut)
            {
                _logger.LogInformation("Round {RoundId} timed out; ending automatically.", round.RoundId);
                FinalizeScores(round);
                _session.FinalizeCurrentRound();
                _boardServices.Clear();
                _shrinkTimer?.Dispose();
                _shrinkTimer = null;
                _hub.Clients.All.SendAsync("RoundEnded", MapScores(round));
            }
        }
    }

    // ------------------------------------------------------------------ Player actions

    public (ActionResult result, object? state) PerformAction(
        Player sessionPlayer, string action, int direction = 0)
    {
        var round = _session.CurrentRound;
        if (round is null || round.Phase != RoundPhase.Playing)
            return (ActionResult.NotPlaying, null);

        if (!_boardServices.TryGetValue(sessionPlayer.Id, out var svc))
            return (ActionResult.NotPlaying, null);

        var board = round.GetBoardForPlayer(sessionPlayer.Id);
        if (board is null) return (ActionResult.NotPlaying, null);

        var boardPlayer = board.Players.FirstOrDefault(p => p.Id == sessionPlayer.Id);
        if (boardPlayer is null) return (ActionResult.PlayerDead, null);

        var result = action switch
        {
            "move" => svc.Move(boardPlayer, direction),
            "jump" => svc.Jump(boardPlayer, direction),
            "crawl" => svc.Crawl(boardPlayer, direction),
            "pickup" => svc.Pickup(boardPlayer),
            "build" => svc.Build(boardPlayer, direction),
            "attack" => svc.Attack(boardPlayer, direction),
            "wait" => svc.Wait(boardPlayer),
            _ => ActionResult.InvalidDirection
        };

        // Award level completion bonus on goal reached
        if (result == ActionResult.GoalReached)
        {
            round.RoundScores[sessionPlayer.Id] =
                round.RoundScores.GetValueOrDefault(sessionPlayer.Id) + 100;
        }

        // Award kill bonus in battle royale
        if (result == ActionResult.KilledEnemy && round.Level == 12)
        {
            round.RoundScores[sessionPlayer.Id] =
                round.RoundScores.GetValueOrDefault(sessionPlayer.Id) + 20;
        }

        // Check level 12 kill / survivor scoring
        if (round.Level == 12 && !boardPlayer.IsAlive)
            HandleLevel12Death(round, sessionPlayer.Id);

        // Broadcast updated state
        var stateDto = BuildStateDto(board, boardPlayer,
            round.RoundScores.GetValueOrDefault(sessionPlayer.Id));
        _ = _hub.Clients.All.SendAsync("BoardUpdated", sessionPlayer.Id, stateDto);
        _ = BroadcastAllBoards();

        // Auto-end when all players done
        if (round.AllPlayersFinished())
            Task.Run(() => EndRound());

        return (result, stateDto);
    }

    public object? GetState(Player sessionPlayer)
    {
        var round = _session.CurrentRound;
        if (round is null) return null;

        var board = round.GetBoardForPlayer(sessionPlayer.Id);
        if (board is null) return null;

        var boardPlayer = board.Players.FirstOrDefault(p => p.Id == sessionPlayer.Id);
        if (boardPlayer is null) return null;

        return BuildStateDto(board, boardPlayer,
            round.RoundScores.GetValueOrDefault(sessionPlayer.Id));
    }

    // ------------------------------------------------------------------ Level 10 logic

    private void SeedLevel12Mushrooms(Board board, int playerCount)
    {
        var rng = new Random();
        int placed = 0;
        int attempts = 0;
        while (placed < playerCount && attempts < 1000)
        {
            int x = rng.Next(1, board.Width - 1);
            int y = rng.Next(1, board.Height - 1);
            var cell = board.GetCell(x, y);
            if (cell.CanPlaceItems && !cell.Items.OfType<Mushroom>().Any())
            {
                cell.Items.Add(new Mushroom());
                placed++;
            }
            attempts++;
        }
    }

    private void HandleLevel12Death(GameRound round, string deadPlayerId)
    {
        int aliveCount = round.SharedBoard?.Players.Count(p => p.IsAlive) ?? 0;
        var scores = round.RoundScores;

        if (aliveCount == 1)
        {
            // The dying player finished 2nd place
            scores[deadPlayerId] = scores.GetValueOrDefault(deadPlayerId) + 50;
            // The last survivor gets the top prize
            var lastAlive = round.SharedBoard!.Players.First(p => p.IsAlive);
            scores[lastAlive.Id] = scores.GetValueOrDefault(lastAlive.Id) + 100;
        }
        else if (aliveCount == 2)
        {
            // The dying player finished 3rd place
            scores[deadPlayerId] = scores.GetValueOrDefault(deadPlayerId) + 25;
        }

        // Shrink the border when a player dies
        round.SharedBoard?.ShrinkBorder();
    }

    private void StartShrinkTimer()
    {
        _shrinkTimer = new Timer(_ =>
        {
            var round = _session.CurrentRound;
            if (round?.Level == 12 && round.Phase == RoundPhase.Playing)
            {
                round.SharedBoard?.ShrinkBorder();
                _ = BroadcastAllBoards();
            }
        }, null, _shrinkInterval, _shrinkInterval);
    }

    // ------------------------------------------------------------------ SignalR broadcasts

    private async Task BroadcastAllBoards()
    {
        var round = _session.CurrentRound;
        if (round is null) return;

        var all = new List<object>();

        if (round.IsSharedBoard && round.SharedBoard is not null)
        {
            foreach (var player in round.SharedBoard.Players)
                all.Add(BuildStateDto(round.SharedBoard, player,
                    round.RoundScores.GetValueOrDefault(player.Id)));
        }
        else
        {
            foreach (var (playerId, board) in round.PlayerBoards)
            {
                var p = board.Players.FirstOrDefault(pl => pl.Id == playerId);
                if (p is not null)
                    all.Add(BuildStateDto(board, p,
                        round.RoundScores.GetValueOrDefault(playerId)));
            }
        }

        await _hub.Clients.All.SendAsync("AllBoardsSnapshot", all);
    }

    // ------------------------------------------------------------------ Score finalization

    private void FinalizeScores(GameRound round)
    {
        // Collect all players from whichever board layout this level uses
        IEnumerable<(string playerId, Player player)> AllBoardPlayers()
        {
            if (round.IsSharedBoard && round.SharedBoard is not null)
            {
                foreach (var p in round.SharedBoard.Players)
                    yield return (p.Id, p);
            }
            else
            {
                foreach (var (playerId, board) in round.PlayerBoards)
                {
                    var p = board.Players.FirstOrDefault(pl => pl.Id == playerId);
                    if (p is not null) yield return (playerId, p);
                }
            }
        }

        foreach (var (playerId, player) in AllBoardPlayers())
        {
            int pts = round.RoundScores.GetValueOrDefault(playerId);

            // Mushrooms are worth 10 points each
            pts += player.MushroomsCollected * 10;

            // Each turn costs 1 point
            pts -= player.TurnCount;

            round.RoundScores[playerId] = pts;
        }
    }

    // ------------------------------------------------------------------ DTO builders

    private object BuildStateDto(Board board, Player player, int roundScore = 0)
    {
        var visibleCells = board.GetVisibleCells(player).Select(c => new
        {
            c.X,
            c.Y,
            c.CellType,
            Items = c.Items.Select(i => i.ItemType).ToList(),
            Entity = board.GetEntityAt(c.X, c.Y) is { } e
                ? new { e.EntityType, e.Id, e.Health }
                : null
        }).ToList();

        return new
        {
            PlayerId = player.Id,
            TeamName = player.TeamName,
            player.X,
            player.Y,
            player.Health,
            player.MaxHealth,
            player.ShieldHealth,
            player.IsCrawling,
            player.MushroomsCollected,
            player.HasReachedGoal,
            RoundScore = roundScore,
            Backpack = player.Backpack.Items.Select(i => i.ItemType).ToList(),
            VisibleCells = visibleCells,
            BoardWidth = board.Width,
            BoardHeight = board.Height,
            Level = board.Level,
            LevelName = LevelFactory.GetLevelName(board.Level)
        };
    }

    private object MapRoundInfo(GameRound round) => new
    {
        round.RoundId,
        round.Level,
        LevelName = LevelFactory.GetLevelName(round.Level),
        round.Seed,
        round.TimeoutSeconds,
        Phase = round.Phase.ToString()
    };

    private object MapScores(GameRound round) => new
    {
        round.RoundId,
        round.Level,
        LevelName = LevelFactory.GetLevelName(round.Level),
        Scores = round.RoundScores
            .Select(kv => new
            {
                PlayerId = kv.Key,
                TeamName = _session.GetPlayer(kv.Key)?.TeamName ?? kv.Key,
                Score = kv.Value
            })
            .OrderByDescending(s => s.Score)
            .ToList(),
        Cumulative = _session.CumulativeScores
            .OrderByDescending(kv => kv.Value)
            .Select(kv => new { Team = kv.Key, Score = kv.Value })
            .ToList()
    };

    // ------------------------------------------------------------------ Public read helpers

    public object GetLeaderboard() => new
    {
        Cumulative = _session.CumulativeScores
            .OrderByDescending(kv => kv.Value)
            .Select(kv => new { Team = kv.Key, Score = kv.Value })
            .ToList(),
        Rounds = _session.CompletedRounds.Select(r => new
        {
            r.RoundId,
            r.Level,
            LevelName = LevelFactory.GetLevelName(r.Level),
            r.StartedAt,
            r.EndedAt,
            Scores = r.RoundScores
                .Select(kv => new
                {
                    PlayerId = kv.Key,
                    TeamName = _session.GetPlayer(kv.Key)?.TeamName ?? kv.Key,
                    Score = kv.Value
                })
                .OrderByDescending(s => s.Score)
                .ToList()
        }).ToList(),
        CurrentRound = _session.CurrentRound is { } cr ? MapRoundInfo(cr) : null
    };
}
