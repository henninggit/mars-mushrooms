namespace MarsvilleStarter.Model;

/// <summary>
/// Enriched view of the agent's current game state, built from the server's response.
/// Use this as the main entry point for reading board information and computing actions.
/// </summary>
public sealed class GameStateView
{
    // ---------------------------------------------------------------- identity / vitals

    /// <summary>Your player ID assigned by the server.</summary>
    public string PlayerId { get; }

    /// <summary>Your team name.</summary>
    public string TeamName { get; }

    /// <summary>Current level number (1–12).</summary>
    public int Level { get; }

    /// <summary>Your current X position (column, 0-based).</summary>
    public int X { get; }

    /// <summary>Your current Y position (row, 0-based).</summary>
    public int Y { get; }

    /// <summary>Your current health points.</summary>
    public int Health { get; }

    /// <summary>Your maximum health points (base + any collected shields).</summary>
    public int MaxHealth { get; }

    /// <summary>
    /// Number of shields collected this round. Each shield adds +1 to <see cref="MaxHealth"/>.
    /// </summary>
    public int ShieldHealth { get; }

    /// <summary>Whether the agent is currently crawling through a low obstacle.</summary>
    public bool IsCrawling { get; }

    /// <summary>Number of mushrooms collected so far this round.</summary>
    public int MushroomsCollected { get; }

    // ---------------------------------------------------------------- board dimensions

    /// <summary>Total width of the board (number of columns).</summary>
    public int BoardWidth { get; }

    /// <summary>Total height of the board (number of rows).</summary>
    public int BoardHeight { get; }

    // ---------------------------------------------------------------- inventory

    /// <summary>
    /// Contents of the agent's backpack (planks, nails, etc.).
    /// Mushrooms are <em>not</em> stored here — they are auto-collected and tracked via
    /// <see cref="MushroomsCollected"/>.
    /// </summary>
    public IReadOnlyList<string> Inventory { get; }

    /// <summary><c>true</c> when the backpack contains at least one "plank" and one "nail".</summary>
    public bool HasPlankAndNail =>
        Inventory.Contains("plank") && Inventory.Contains("nail");

    // ---------------------------------------------------------------- vision

    /// <summary>
    /// All cells within the agent's current vision radius.
    /// Cells outside this set are hidden by fog of war.
    /// </summary>
    public IReadOnlyList<CellView> VisibleCells { get; }

    // ---------------------------------------------------------------- construction

    internal GameStateView(GameStateDto dto)
    {
        PlayerId           = dto.PlayerId;
        TeamName           = dto.TeamName;
        Level              = dto.Level;
        X                  = dto.X;
        Y                  = dto.Y;
        Health             = dto.Health;
        MaxHealth          = dto.MaxHealth;
        ShieldHealth       = dto.ShieldHealth;
        IsCrawling         = dto.IsCrawling;
        MushroomsCollected = dto.MushroomsCollected;
        BoardWidth         = dto.BoardWidth;
        BoardHeight        = dto.BoardHeight;
        Inventory          = dto.Backpack.AsReadOnly();

        // Build the visible cell lookup first so IsPartlyVisible can be computed
        var visibleSet = dto.VisibleCells.ToDictionary(c => (c.X, c.Y));
        VisibleCells = dto.VisibleCells
            .Select(c => new CellView(c, IsPartlyVisible(c.X, c.Y, visibleSet, dto.BoardWidth, dto.BoardHeight)))
            .ToList()
            .AsReadOnly();

        _cellLookup = VisibleCells.ToDictionary(c => (c.X, c.Y));
    }

    // ---------------------------------------------------------------- cell lookup

    private readonly Dictionary<(int, int), CellView> _cellLookup;

    /// <summary>
    /// Returns the visible cell at (x, y), or <c>null</c> if the cell is
    /// out-of-bounds or hidden by fog of war.
    /// </summary>
    public CellView? GetCell(int x, int y) =>
        _cellLookup.GetValueOrDefault((x, y));

    /// <summary>Returns the cell the agent is currently standing on, or <c>null</c>.</summary>
    public CellView? GetCurrentCell() => GetCell(X, Y);

    // ---------------------------------------------------------------- action helpers

    /// <summary>
    /// Returns all actions the agent can perform from its current position, based on the
    /// currently visible board. Does not include actions toward fog-of-war cells.
    /// </summary>
    public IReadOnlyList<PossibleAction> GetPossibleActions()
    {
        var currentCell = GetCurrentCell();
        if (currentCell is null) return Array.Empty<PossibleAction>();
        return currentCell.GetPossibleActions(this, isCurrentPosition: true);
    }

    /// <summary>
    /// Returns the actions that would be possible from a visible cell at (x, y),
    /// given the current board state. Only considers visible neighbours — no actions
    /// toward fog-of-war cells are included.
    /// <para>
    /// This is useful for planning ahead: e.g., "if I were at (5,3) what could I do?"
    /// </para>
    /// <para>
    /// Returns an empty list when (x, y) is not in the visible cell set.
    /// </para>
    /// </summary>
    public IReadOnlyList<PossibleAction> GetPossibleActions(int x, int y)
    {
        var cell = GetCell(x, y);
        if (cell is null) return Array.Empty<PossibleAction>();
        bool isCurrentPos = x == X && y == Y;
        return cell.GetPossibleActions(this, isCurrentPosition: isCurrentPos);
    }

    // ---------------------------------------------------------------- helpers

    private static bool IsPartlyVisible(
        int cx, int cy,
        Dictionary<(int, int), CellDto> visibleSet,
        int boardWidth, int boardHeight)
    {
        foreach (Direction dir in Enum.GetValues<Direction>())
        {
            var (dx, dy) = dir.ToOffset();
            int nx = cx + dx;
            int ny = cy + dy;
            // An in-bounds neighbour that is NOT in the visible set means fog hides it
            if (nx >= 0 && nx < boardWidth && ny >= 0 && ny < boardHeight
                && !visibleSet.ContainsKey((nx, ny)))
                return true;
        }
        return false;
    }
}
