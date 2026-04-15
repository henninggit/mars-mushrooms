namespace MarsvilleStarter.Model;

/// <summary>
/// Describes a single action the agent could take, either from its current position
/// or hypothetically from any visible cell.
/// </summary>
public sealed record PossibleAction
{
    /// <summary>The action to perform.</summary>
    public ActionType ActionType { get; init; }

    /// <summary>
    /// The direction of the action, or <c>null</c> for direction-less actions
    /// (<c>pickup</c>, <c>wait</c>).
    /// </summary>
    public Direction? Direction { get; init; }

    /// <summary>
    /// The grid X coordinate of the cell the action targets / moves toward,
    /// or <c>null</c> for non-directional actions.
    /// </summary>
    public int? TargetX { get; init; }

    /// <summary>
    /// The grid Y coordinate of the cell the action targets / moves toward,
    /// or <c>null</c> for non-directional actions.
    /// </summary>
    public int? TargetY { get; init; }

    /// <summary>Human-readable summary of the action (e.g. "Move East to (3, 2)").</summary>
    public string Description { get; init; }

    public PossibleAction(ActionType actionType, Direction? direction, int? targetX, int? targetY, string description)
    {
        ActionType  = actionType;
        Direction   = direction;
        TargetX     = targetX;
        TargetY     = targetY;
        Description = description;
    }

    public override string ToString() => Description;
}
