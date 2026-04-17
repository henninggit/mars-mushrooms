using Marsville2.Domain;
using Marsville2.Domain.Cells;
using Marsville2.Domain.Entities;
using Marsville2.Domain.Items;

namespace Marsville2.Services;

public enum ActionResult
{
    Ok,
    InvalidDirection,
    CellBlocked,
    CannotJump,
    CannotCrawl,
    NoItemToPickUp,
    BackpackFull,
    MissingBridgeMaterials,
    NoBrokenBridgeAdjacent,
    NothingToAttack,
    NotPlaying,
    PlayerDead,
    GoalReached,
    KilledEnemy
}

/// <summary>
/// Encapsulates all mutation logic for a single board.
/// All public methods are thread-safe via a per-board lock.
/// After each successful action, enemy AI runs and the result is returned.
/// </summary>
public class BoardService
{
    private readonly Board _board;
    private readonly object _lock = new();
    private readonly EnemyAiService _enemyAi;

    public BoardService(Board board)
    {
        _board = board;
        _enemyAi = new EnemyAiService(board);
    }

    // ------------------------------------------------------------------ Move
    public ActionResult Move(Player player, int direction)
    {
        lock (_lock)
        {
            if (!player.IsAlive) return ActionResult.PlayerDead;
            if (player.HasReachedGoal) return ActionResult.GoalReached;

            var (dx, dy) = Board.DirectionOffset(direction);
            int nx = player.X + dx;
            int ny = player.Y + dy;

            if (!_board.InBounds(nx, ny)) return ActionResult.CellBlocked;

            var cell = _board.GetCell(nx, ny);

            if (player.IsCrawling)
            {
                // Once crawling through a low obstacle, can move forward; exit crawl when cell is normal
                if (!cell.IsCrawlable) return ActionResult.CellBlocked;
                if (cell is not LowObstacleCell) player.SetCrawling(false);
            }
            else
            {
                if (!cell.IsWalkable) return ActionResult.CellBlocked;
            }

            player.X = nx;
            player.Y = ny;
            ApplyTeleporter(player);
            _board.CollectMushroomsAt(player);
            player.RecordAction();
            _enemyAi.MoveEnemies(player);

            if (_board.GetCell(player.X, player.Y) is GoalCell) { player.MarkGoalReached(); return ActionResult.GoalReached; }
            return ActionResult.Ok;
        }
    }

    // ------------------------------------------------------------------ Jump
    // Jump moves the player 2 cells in the direction over a single HoleCell.
    public ActionResult Jump(Player player, int direction)
    {
        lock (_lock)
        {
            if (!player.IsAlive) return ActionResult.PlayerDead;
            if (player.HasReachedGoal) return ActionResult.GoalReached;

            var (dx, dy) = Board.DirectionOffset(direction);
            int midX = player.X + dx;
            int midY = player.Y + dy;
            int landX = player.X + dx * 2;
            int landY = player.Y + dy * 2;

            if (!_board.InBounds(midX, midY) || !_board.InBounds(landX, landY))
                return ActionResult.CannotJump;

            var midCell = _board.GetCell(midX, midY);
            var landCell = _board.GetCell(landX, landY);

            // Must be jumping OVER a jumpable cell (hole), landing on a walkable cell
            if (!midCell.IsJumpable) return ActionResult.CannotJump;
            if (!landCell.IsWalkable) return ActionResult.CannotJump;

            player.X = landX;
            player.Y = landY;
            ApplyTeleporter(player);
            _board.CollectMushroomsAt(player);
            player.RecordAction();
            _enemyAi.MoveEnemies(player);

            if (_board.GetCell(player.X, player.Y) is GoalCell) { player.MarkGoalReached(); return ActionResult.GoalReached; }
            return ActionResult.Ok;
        }
    }

    // ------------------------------------------------------------------ Crawl
    // Initiates crawling into an adjacent LowObstacleCell
    public ActionResult Crawl(Player player, int direction)
    {
        lock (_lock)
        {
            if (!player.IsAlive) return ActionResult.PlayerDead;
            if (player.HasReachedGoal) return ActionResult.GoalReached;

            var (dx, dy) = Board.DirectionOffset(direction);
            int nx = player.X + dx;
            int ny = player.Y + dy;

            if (!_board.InBounds(nx, ny)) return ActionResult.CellBlocked;

            var cell = _board.GetCell(nx, ny);
            if (!cell.IsCrawlable) return ActionResult.CannotCrawl;

            player.X = nx;
            player.Y = ny;
            player.SetCrawling(cell is LowObstacleCell);
            ApplyTeleporter(player);
            _board.CollectMushroomsAt(player);
            player.RecordAction();
            _enemyAi.MoveEnemies(player);

            if (_board.GetCell(player.X, player.Y) is GoalCell) { player.MarkGoalReached(); return ActionResult.GoalReached; }
            return ActionResult.Ok;
        }
    }

    // ------------------------------------------------------------------ Pickup
    public ActionResult Pickup(Player player)
    {
        lock (_lock)
        {
            if (!player.IsAlive) return ActionResult.PlayerDead;
            if (player.HasReachedGoal) return ActionResult.GoalReached;

            var cell = _board.GetCell(player.X, player.Y);
            var item = cell.Items.FirstOrDefault(i => i is not Mushroom); // mushrooms auto-collect on move
            if (item is null) return ActionResult.NoItemToPickUp;

            cell.Items.Remove(item);

            // Immediately-consumed items — do not go into backpack
            if (item is HealthPack)
            {
                player.HealToFull();
                player.RecordAction();
                _enemyAi.MoveEnemies(player);
                return ActionResult.Ok;
            }

            if (item is Shield)
            {
                player.AddShield();
                player.RecordAction();
                _enemyAi.MoveEnemies(player);
                return ActionResult.Ok;
            }

            if (player.Backpack.IsFull)
            {
                // Put the item back and report backpack full
                cell.Items.Add(item);
                return ActionResult.BackpackFull;
            }

            player.Backpack.TryAdd(item);
            player.RecordAction();
            _enemyAi.MoveEnemies(player);
            return ActionResult.Ok;
        }
    }

    // ------------------------------------------------------------------ Build (repair bridge)
    public ActionResult Build(Player player, int direction)
    {
        lock (_lock)
        {
            if (!player.IsAlive) return ActionResult.PlayerDead;
            if (player.HasReachedGoal) return ActionResult.GoalReached;

            if (!player.Backpack.HasPlankAndNail()) return ActionResult.MissingBridgeMaterials;

            var (dx, dy) = Board.DirectionOffset(direction);
            int tx = player.X + dx;
            int ty = player.Y + dy;

            if (!_board.InBounds(tx, ty)) return ActionResult.NoBrokenBridgeAdjacent;
            var cell = _board.GetCell(tx, ty);
            if (cell is not BrokenBridgeCell) return ActionResult.NoBrokenBridgeAdjacent;

            player.Backpack.ConsumePlankAndNail();
            _board.SetCell(tx, ty, new BridgeCell(tx, ty));
            player.RecordAction();
            _enemyAi.MoveEnemies(player);
            return ActionResult.Ok;
        }
    }

    // ------------------------------------------------------------------ Attack
    public ActionResult Attack(Player player, int direction)
    {
        lock (_lock)
        {
            if (!player.IsAlive) return ActionResult.PlayerDead;
            if (player.HasReachedGoal) return ActionResult.GoalReached;

            var killed = _board.DamageAdjacentEnemies(player.X, player.Y, direction).ToList();
            player.RecordAction();
            _enemyAi.MoveEnemies(player);

            if (killed.Count == 0 && _board.GetEnemyAt(
                player.X + Board.DirectionOffset(direction).dx,
                player.Y + Board.DirectionOffset(direction).dy) is null)
                return ActionResult.NothingToAttack;

            return killed.Count > 0 ? ActionResult.KilledEnemy : ActionResult.Ok;
        }
    }

    // ------------------------------------------------------------------ Wait
    public ActionResult Wait(Player player)
    {
        lock (_lock)
        {
            if (!player.IsAlive) return ActionResult.PlayerDead;
            if (player.HasReachedGoal) return ActionResult.GoalReached;
            player.RecordAction();
            _enemyAi.MoveEnemies(player);
            return ActionResult.Ok;
        }
    }

    // ------------------------------------------------------------------ Teleporter helper

    /// <summary>
    /// If the player's current cell is a <see cref="TeleporterCell"/>, warps them to
    /// the target position. One-shot: landing on a teleporter via warp does not chain.
    /// </summary>
    private void ApplyTeleporter(Player player)
    {
        if (_board.GetCell(player.X, player.Y) is TeleporterCell teleporter)
        {
            player.X = teleporter.TargetX;
            player.Y = teleporter.TargetY;
        }
    }
}
