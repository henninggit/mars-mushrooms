namespace Marsville2.Domain.Cells;

/// <summary>An impassable solid Martian wall. Cannot be walked, crawled, or jumped.</summary>
public class WallCell : CellBase
{
    public override bool IsWalkable => false;
    public override bool IsJumpable => false;
    public override bool IsCrawlable => false;
    public override bool CanPlaceItems => false;
    public override string CellType => "wall";

    public WallCell(int x, int y) : base(x, y) { }
}
