namespace MarsvilleStarter.Model;

/// <summary>
/// The four cardinal directions the agent can act in.
/// The integer values match the server's encoding (0=East, 1=West, 2=North, 3=South).
/// </summary>
public enum Direction
{
    East  = 0,
    West  = 1,
    North = 2,
    South = 3,
}

/// <summary>Extension helpers for <see cref="Direction"/>.</summary>
public static class DirectionExtensions
{
    /// <summary>Returns the (dx, dy) grid offset for this direction.</summary>
    public static (int dx, int dy) ToOffset(this Direction dir) => dir switch
    {
        Direction.East  => ( 1,  0),
        Direction.West  => (-1,  0),
        Direction.North => ( 0, -1),
        Direction.South => ( 0,  1),
        _               => throw new ArgumentOutOfRangeException(nameof(dir))
    };

    /// <summary>Returns a human-readable label (e.g. "East").</summary>
    public static string Label(this Direction dir) => dir.ToString();
}
