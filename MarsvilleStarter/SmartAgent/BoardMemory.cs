using MarsvilleStarter.Model;

namespace SmartAgent;

/// <summary>
/// Stores what the agent has seen across all turns in a round.
/// Updated every turn from the current GameStateView's visible cells.
/// </summary>
public sealed class BoardMemory
{
    private readonly Dictionary<(int, int), CellView> _cells = new();
    private readonly HashSet<(int, int)> _visitedCells = new();

    public int Level { get; private set; } = -1;

    // Known goal position (if seen)
    public (int X, int Y)? GoalPosition { get; private set; }

    /// <summary>Returns the memorized cell at (x,y), or null if never seen.</summary>
    public CellView? GetCell(int x, int y) =>
        _cells.GetValueOrDefault((x, y));

    /// <summary>
    /// Integrates all visible cells from the current turn into memory.
    /// Resets memory if the level has changed (new round).
    /// </summary>
    public void UpdateFromState(GameStateView state)
    {
        if (state.Level != Level)
        {
            _cells.Clear();
            _visitedCells.Clear();
            GoalPosition = null;
            Level = state.Level;
        }

        _visitedCells.Add((state.X, state.Y));

        foreach (var cell in state.VisibleCells)
        {
            var dto = new CellDto
            (
                cell.X,
                cell.Y,
                cell.CellType,
                cell.Items.ToList(),
                null // dont memorize entities
            );
            var memorized = new CellView(dto, cell.IsPartlyVisible);

            _cells[(cell.X, cell.Y)] = memorized;

            if (cell.IsGoal)
                GoalPosition = (cell.X, cell.Y);
        }
    }

    /// <summary>
    /// Returns all memorized cells that have at least one item (plank, nail, mushroom).
    /// </summary>
    public IEnumerable<CellView> GetCellsWithItems() =>
        _cells.Values.Where(c => c.Items.Count > 0);

    /// <summary>
    /// Returns all memorized cells that have a mushroom.
    /// </summary>
    public IEnumerable<CellView> GetMushroomCells() =>
        _cells.Values.Where(c => c.Items.Contains(ItemType.Mushroom));

    /// <summary>
    /// Returns all memorized cells with plank or nail.
    /// </summary>
    public IEnumerable<CellView> GetHammerNailCells() =>
        _cells.Values.Where(c => c.Items.Any(i => i is ItemType.Plank or ItemType.Nail));

    public bool HasSeenCell(int x, int y) => _cells.ContainsKey((x, y));

    /// <summary>
    /// Returns memorized non-wall cells that have at least one in-bounds, unseen cardinal neighbor.
    /// These are the "frontier" — places the agent should move toward to reveal new territory.
    /// </summary>
    public IReadOnlyList<(int x, int y)> GetFrontierCells(int boardWidth, int boardHeight)
    {
        var unvisitedPossibleVisits = _cells.Where(x => !_visitedCells.Contains((x.Key.Item1, x.Key.Item2)) && !x.Value.IsWall).Select(x => (x.Key.Item1, x.Key.Item2)).ToList();
        return unvisitedPossibleVisits;
    }
}
