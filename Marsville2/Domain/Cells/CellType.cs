namespace Marsville2.Domain.Cells;

/// <summary>Discriminator enum for all cell types in the game.</summary>
public enum CellType
{
    Floor,
    Hole,
    Wall,
    BrokenBridge,
    Bridge,
    LowObstacle,
    Goal,
    Teleporter,
    Warning
}
