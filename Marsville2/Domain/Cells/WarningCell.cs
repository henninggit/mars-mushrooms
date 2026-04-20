namespace Marsville2.Domain.Cells;

/// <summary>
/// A floor cell that is about to become a wall on the next border shrink.
/// Still walkable, but players should evacuate before the next shrink fires.
/// Used exclusively in level 12 (battle royale) to give advance warning.
/// </summary>
public class WarningCell : CellBase
{
    public override bool IsWalkable => true;
    public override bool IsJumpable => false;
    public override bool IsCrawlable => true;
    public override bool CanPlaceItems => true;
    public override CellType CellType => CellType.Warning;

    public WarningCell(int x, int y) : base(x, y) { }
}
