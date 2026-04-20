namespace Marsville2.Domain.Cells;

/// <summary>
/// The goal cell — reaching this wins the board.
/// Walkable like a floor cell.
/// </summary>
public class GoalCell : CellBase
{
    public override bool IsWalkable => true;
    public override bool IsJumpable => false;
    public override bool IsCrawlable => true;
    public override bool CanPlaceItems => true;
    public override CellType CellType => CellType.Goal;

    public GoalCell(int x, int y) : base(x, y) { }
}
