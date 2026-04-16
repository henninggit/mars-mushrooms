namespace MarsvilleStarter.Model;

/// <summary>
/// An enriched view of a single board cell within the player's vision radius.
/// </summary>
public class CellView
{
    /// <summary>Grid X coordinate (column, 0-based).</summary>
    public int X { get; }

    /// <summary>Grid Y coordinate (row, 0-based).</summary>
    public int Y { get; }

    /// <summary>
    /// The type of this cell.
    /// </summary>
    public CellType CellType { get; }

    /// <summary>Items resting on this cell (e.g. "plank", "nail").</summary>
    public IReadOnlyList<string> Items { get; }

    /// <summary>
    /// The entity standing on this cell, if any.
    /// Check <see cref="EntityView.IsEnemy"/> / <see cref="EntityView.IsPlayer"/>.
    /// </summary>
    public EntityView? Entity { get; }

    /// <summary>
    /// <c>true</c> when at least one cardinal neighbour of this cell is hidden by fog of war.
    /// When <c>true</c>, the list returned by <see cref="GetPossibleActions"/> may be
    /// incomplete — there could be valid moves or interactions toward the hidden neighbours.
    /// </summary>
    public bool IsPartlyVisible { get; }

    // ---------------------------------------------------------------- shorthands

    public bool IsFloor          => CellType == CellType.Floor;
    public bool IsHole           => CellType == CellType.Hole;
    public bool IsWall           => CellType == CellType.Wall;
    public bool IsBrokenBridge   => CellType == CellType.BrokenBridge;
    public bool IsBridge         => CellType == CellType.Bridge;
    public bool IsLowObstacle    => CellType == CellType.LowObstacle;
    public bool IsGoal           => CellType == CellType.Goal;
    public bool IsTeleporter     => CellType == CellType.Teleporter;
    public bool IsWalkable       => CellType is CellType.Floor or CellType.Bridge or CellType.Goal or CellType.Teleporter;
    public bool IsJumpable       => CellType == CellType.Hole;
    public bool IsCrawlable      => CellType is CellType.Floor or CellType.Bridge or CellType.Goal or CellType.LowObstacle or CellType.Teleporter;
    public bool HasItems         => Items.Count > 0;
    public bool HasEnemy         => Entity?.IsEnemy == true;

    // ---------------------------------------------------------------- construction

    public CellView(CellDto dto, bool isPartlyVisible)
    {
        X               = dto.X;
        Y               = dto.Y;
        CellType        = dto.CellType;
        Items           = dto.Items.AsReadOnly();
        Entity          = dto.Entity is not null ? new EntityView(dto.Entity) : null;
        IsPartlyVisible = isPartlyVisible;
    }

    // ---------------------------------------------------------------- possible actions

    /// <summary>
    /// Returns the actions that would be available from this cell given the current
    /// game state. Only considers cells that are actually visible — no actions are
    /// listed toward hidden fog-of-war neighbours.
    /// <para>
    /// If <see cref="IsPartlyVisible"/> is <c>true</c> the list may be incomplete.
    /// </para>
    /// </summary>
    /// <param name="state">The current game state (used for inventory checks and fog boundary).</param>
    /// <param name="isCurrentPosition">
    /// Pass <c>true</c> when this cell is the player's current position. This enables
    /// <c>pickup</c> and <c>wait</c>, which only make sense from the player's actual location.
    /// </param>
    public IReadOnlyList<PossibleAction> GetPossibleActions(GameStateView state, bool isCurrentPosition = false)
    {
        var actions = new List<PossibleAction>();

        foreach (Direction dir in Enum.GetValues<Direction>())
        {
            var (dx, dy) = dir.ToOffset();
            int tx = X + dx;
            int ty = Y + dy;

            var target = state.GetCell(tx, ty);
            if (target is null) continue; // hidden by fog — skip

            // move
            if (target.IsWalkable)
                actions.Add(new PossibleAction(ActionType.Move, dir, tx, ty, $"Move {dir.Label()} to ({tx},{ty})"));

            // crawl into low obstacle
            if (target.IsCrawlable && target.IsLowObstacle)
                actions.Add(new PossibleAction(ActionType.Crawl, dir, tx, ty, $"Crawl {dir.Label()} into obstacle at ({tx},{ty})"));

            // jump over hole
            if (target.IsJumpable)
            {
                int lx = X + dx * 2;
                int ly = Y + dy * 2;
                // all holes when jumped over should land on land
                actions.Add(new PossibleAction(ActionType.Jump, dir, lx, ly, $"Jump {dir.Label()} over hole, landing at ({lx},{ly})"));
            }

            // build (repair broken bridge)
            if (target.IsBrokenBridge && state.HasPlankAndNail)
                actions.Add(new PossibleAction(ActionType.Build, dir, tx, ty, $"Repair broken bridge {dir.Label()} at ({tx},{ty})"));

            // attack enemy
            if (target.HasEnemy)
                actions.Add(new PossibleAction(ActionType.Attack, dir, tx, ty, $"Attack enemy {dir.Label()} at ({tx},{ty})"));
        }

        if (isCurrentPosition)
        {
            // pickup — only non-mushroom items (mushrooms are collected automatically on step)
            if (Items.Any(i => i != "mushroom"))
                actions.Add(new PossibleAction(ActionType.Pickup, null, X, Y, $"Pick up item at ({X},{Y})"));

            // wait — always available at current position
            actions.Add(new PossibleAction(ActionType.Wait, null, null, null, "Wait (skip turn)"));
        }

        return actions.AsReadOnly();
    }

    public override string ToString() => $"[{CellType}]({X},{Y}){(IsPartlyVisible ? "~" : "")}";
}
