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

                // For level 11 seed mushrooms equal to player count
                if (round.Level == 11)
                    SeedLevel11Mushrooms(board, _session.Players.Count);

                round.SetSharedBoard(board);
                var svc = new BoardService(board);

                // Place players at spread-out start positions
                int idx = 0;
                foreach (var (playerId, player) in _session.Players)
                {
                    var maxHealth = round.Level == 11 ? 3 : 2;
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

            if (round.Level == 11)
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

        // Award completion point on goal reached (levels 3+)
        if (result == ActionResult.GoalReached && round.Level >= 3)
        {
            round.RoundScores[sessionPlayer.Id] =
                round.RoundScores.GetValueOrDefault(sessionPlayer.Id) + 1;
        }

        // Check level 11 kill / survivor scoring
        if (round.Level == 11 && !boardPlayer.IsAlive)
            HandleLevel11Death(round, sessionPlayer.Id);

        // Broadcast updated state
        var stateDto = BuildStateDto(board, boardPlayer);
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

        return BuildStateDto(board, boardPlayer);
    }

    // ------------------------------------------------------------------ Level 10 logic

    private void SeedLevel11Mushrooms(Board board, int playerCount)
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

    private void HandleLevel11Death(GameRound round, string deadPlayerId)
    {
        int aliveCount = round.SharedBoard?.Players.Count(p => p.IsAlive) ?? 0;
        var scores = round.RoundScores;

        // Scoring for remaining survivors when someone dies
        if (aliveCount == 0) scores[deadPlayerId] = scores.GetValueOrDefault(deadPlayerId); // last one
        if (aliveCount == 1)
        {
            var lastAlive = round.SharedBoard!.Players.First(p => p.IsAlive);
            scores[lastAlive.Id] = scores.GetValueOrDefault(lastAlive.Id) + 4; // last survivor
        }
        else if (aliveCount == 2)
        {
            // The one that just died is 3rd-to-last
            scores[deadPlayerId] = scores.GetValueOrDefault(deadPlayerId) + 1;
        }

        // Shrink the border when a player dies
        round.SharedBoard?.ShrinkBorder();
    }

    private void StartShrinkTimer()
    {
        _shrinkTimer = new Timer(_ =>
        {
            var round = _session.CurrentRound;
            if (round?.Level == 11 && round.Phase == RoundPhase.Playing)
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
                all.Add(BuildStateDto(round.SharedBoard, player));
        }
        else
        {
            foreach (var (playerId, board) in round.PlayerBoards)
            {
                var p = board.Players.FirstOrDefault(pl => pl.Id == playerId);
                if (p is not null) all.Add(BuildStateDto(board, p));
            }
        }

        await _hub.Clients.All.SendAsync("AllBoardsSnapshot", all);
    }

    // ------------------------------------------------------------------ Score finalization

    private void FinalizeScores(GameRound round)
    {
        if (round.Level == 11) return; // scored dynamically during play

        foreach (var (playerId, board) in round.PlayerBoards)
        {
            var player = board.Players.FirstOrDefault(p => p.Id == playerId);
            if (player is not null)
            {
                // Mushroom points accumulate via player.CollectMushroom() already
                round.RoundScores[playerId] =
                    round.RoundScores.GetValueOrDefault(playerId) +
                    player.MushroomsCollected;
            }
        }
    }

    // ------------------------------------------------------------------ DTO builders

    private object BuildStateDto(Board board, Player player)
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
            player.IsCrawling,
            player.MushroomsCollected,
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
        Scores = round.RoundScores,
        Cumulative = _session.CumulativeScores
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
        }).ToList(),
        CurrentRound = _session.CurrentRound is { } cr ? MapRoundInfo(cr) : null
    };
}
