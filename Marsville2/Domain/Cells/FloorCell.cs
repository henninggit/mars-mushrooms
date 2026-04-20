namespace Marsville2.Domain.Cells;

/// <summary>Normal Martian regolith — fully walkable, items can be placed here.</summary>
public class FloorCell : CellBase
{
    public override bool IsWalkable => true;
    public override bool IsJumpable => false;
    public override bool IsCrawlable => true;
    public override bool CanPlaceItems => true;
    public override CellType CellType => CellType.Floor;

    public FloorCell(int x, int y) : base(x, y) { }
}
