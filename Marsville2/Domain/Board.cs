using Marsville2.Domain.Cells;
using Marsville2.Domain.Entities;
using Marsville2.Domain.Items;

namespace Marsville2.Domain;

/// <summary>
/// Represents the play area for a single player's instance of a level
/// (or the shared board for levels 9 and 10).
/// The grid is stored as a flat array indexed by [y * Width + x].
/// </summary>
public class Board
{
    public int Width { get; }
    public int Height { get; }
    public int Level { get; }
    public int VisionRadius { get; }   // 0 = full visibility

    private readonly CellBase[] _cells;
    private readonly List<Player> _players = new();
    private readonly List<Enemy> _enemies = new();

    public IReadOnlyList<Player> Players => _players.AsReadOnly();
    public IReadOnlyList<Enemy> Enemies => _enemies.AsReadOnly();

    public Board(int width, int height, int level, CellBase[] cells, int visionRadius = 0)
    {
        Width = width;
        Height = height;
        Level = level;
        VisionRadius = visionRadius;
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
    /// Shrinks the board by removing the outermost ring of cells (replacing with WallCells).
    /// Used in level 10 battle royale.
    /// </summary>
    public bool ShrinkBorder()
    {
        // Find the current active (non-wall) bounds
        int minX = 0, minY = 0, maxX = Width - 1, maxY = Height - 1;
        // Find filled border to determine how far we've shrunk
        while (minX <= maxX && _cells[minY * Width + minX] is WallCell)
        {
            minX++; minY++; maxX--; maxY--;
            if (minX > maxX || minY > maxY) return false;
        }

        for (int x = minX; x <= maxX; x++)
        {
            SetCell(x, minY, new WallCell(x, minY));
            SetCell(x, maxY, new WallCell(x, maxY));
        }
        for (int y = minY + 1; y <= maxY - 1; y++)
        {
            SetCell(minX, y, new WallCell(minX, y));
            SetCell(maxX, y, new WallCell(maxX, y));
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
        foreach (var mut in mushrooms)
        {
            cell.Items.Remove(mut);
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
