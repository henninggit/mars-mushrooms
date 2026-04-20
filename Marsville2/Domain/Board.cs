using Marsville2.Domain.Cells;
using Marsville2.Domain.Entities;
using Marsville2.Domain.Items;

namespace Marsville2.Domain;

/// <summary>
/// Represents the play area for a single player's instance of a level
/// (or the shared board for levels 11 and 12).
/// The grid is stored as a flat array indexed by [y * Width + x].
/// </summary>
public class Board
{
    public int Width { get; }
    public int Height { get; }
    public int Level { get; }
    public int VisionRadius { get; }   // 0 = full visibility
    public int StartX { get; }
    public int StartY { get; }
    public bool IsShared { get; }

    private readonly CellBase[] _cells;
    private readonly List<Player> _players = new();
    private readonly List<Enemy> _enemies = new();

    public IReadOnlyList<Player> Players => _players.AsReadOnly();
    public IReadOnlyList<Enemy> Enemies => _enemies.AsReadOnly();
    public IEnumerable<CellBase> AllCells => _cells;

    public Board(int width, int height, int level, CellBase[] cells,
        int visionRadius = 0, int startX = 0, int startY = 0, bool isShared = false)
    {
        Width = width;
        Height = height;
        Level = level;
        VisionRadius = visionRadius;
        StartX = startX;
        StartY = startY;
        IsShared = isShared;
        _cells = cells;
    }

    public CellBase GetCell(int x, int y) => _cells[y * Width + x];

    public bool InBounds(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;

    public void SetCell(int x, int y, CellBase cell) => _cells[y * Width + x] = cell;

    public void AddPlayer(Player player) => _players.Add(player);
    public void RemovePlayer(Player player) => _players.Remove(player);

    public void AddEnemy(Enemy enemy) => _enemies.Add(enemy);
    public void RemoveEnemy(Enemy enemy) => _enemies.Remove(enemy);

    public IEntity? GetEntityAt(int x, int y)
    {
        IEntity? p = _players.FirstOrDefault(p => p.X == x && p.Y == y);
        if (p is not null) return p;
        return _enemies.FirstOrDefault(e => e.X == x && e.Y == y);
    }

    public Player? GetPlayerAt(int x, int y) => _players.FirstOrDefault(p => p.X == x && p.Y == y);
    public Enemy? GetEnemyAt(int x, int y) => _enemies.FirstOrDefault(e => e.X == x && e.Y == y);

    /// <summary>
    /// Shrinks the board by converting any existing <see cref="WarningCell"/>s to
    /// <see cref="WallCell"/>s, then marks the new outermost ring as warning cells.
    /// Used in level 12 (battle royale). Call <see cref="MarkWarningRing"/> once at
    /// round start so players see the first warning before the initial shrink fires.
    /// </summary>
    public bool ShrinkBorder()
    {
        // Step 1: Convert existing warning cells to walls (actual shrink).
        foreach (var cell in _cells.OfType<WarningCell>().ToList())
            SetCell(cell.X, cell.Y, new WallCell(cell.X, cell.Y));

        // Step 2: Mark the new outermost non-wall ring as warning.
        return MarkWarningRing();
    }

    /// <summary>
    /// Marks the outermost non-wall ring as <see cref="WarningCell"/>s without
    /// immediately shrinking. Use this at round start to give players advance notice
    /// of the first upcoming shrink.
    /// </summary>
    public bool MarkWarningRing()
    {
        // Locate the outermost ring that hasn't been walled yet.
        int minX = 0, minY = 0, maxX = Width - 1, maxY = Height - 1;
        while (minX <= maxX && _cells[minY * Width + minX] is WallCell)
        {
            minX++; minY++; maxX--; maxY--;
            if (minX > maxX || minY > maxY) return false;
        }

        for (int x = minX; x <= maxX; x++)
        {
            if (_cells[minY * Width + x] is not WallCell)
                SetCell(x, minY, new WarningCell(x, minY));
            if (_cells[maxY * Width + x] is not WallCell)
                SetCell(x, maxY, new WarningCell(x, maxY));
        }
        for (int y = minY + 1; y <= maxY - 1; y++)
        {
            if (_cells[y * Width + minX] is not WallCell)
                SetCell(minX, y, new WarningCell(minX, y));
            if (_cells[y * Width + maxX] is not WallCell)
                SetCell(maxX, y, new WarningCell(maxX, y));
        }
        return true;
    }

    /// <summary>
    /// Returns cells visible to the given player, respecting VisionRadius.
    /// If VisionRadius == 0 all cells are returned.
    /// </summary>
    public IEnumerable<CellBase> GetVisibleCells(Player player)
    {
        if (VisionRadius == 0)
            return _cells;

        return _cells.Where(c =>
            Math.Abs(c.X - player.X) <= VisionRadius &&
            Math.Abs(c.Y - player.Y) <= VisionRadius);
    }

    /// <summary>Collect any mushrooms on the cell the player just moved onto.</summary>
    public void CollectMushroomsAt(Player player)
    {
        var cell = GetCell(player.X, player.Y);
        var mushrooms = cell.Items.OfType<Mushroom>().ToList();
        foreach (var mush in mushrooms)
        {
            cell.Items.Remove(mush);
            player.CollectMushroom();
        }
    }

    /// <summary>Damage all enemies adjacent (Manhattan distance 1) to the given position.</summary>
    public IEnumerable<Enemy> DamageAdjacentEnemies(int x, int y, int direction)
    {
        var (tx, ty) = DirectionOffset(direction);
        var target = GetEnemyAt(x + tx, y + ty);
        if (target is not null && target.TakeDamage(1))
        {
            _enemies.Remove(target);
            yield return target;
        }
    }

    /// <summary>Damage the player adjacent to an enemy position in a given direction.</summary>
    public Player? DamageAdjacentPlayer(int ex, int ey)
    {
        var adjacent = _players.Where(p =>
            Math.Abs(p.X - ex) + Math.Abs(p.Y - ey) == 1);
        foreach (var p in adjacent)
        {
            p.TakeDamage(1);
            if (!p.IsAlive)
                _players.Remove(p);
            return p;
        }
        return null;
    }

    public static (int dx, int dy) DirectionOffset(int direction) => direction switch
    {
        0 => (1, 0),   // East
        1 => (-1, 0),  // West
        2 => (0, -1),  // North
        3 => (0, 1),   // South
        _ => throw new ArgumentOutOfRangeException(nameof(direction))
    };
}
