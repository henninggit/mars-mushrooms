namespace Marsville2.Domain.Cells;

/// <summary>A gap in the Martian surface. Cannot be walked or crawled through; can be jumped over if exactly 1 cell wide.</summary>
public class HoleCell : CellBase
{
    public override bool IsWalkable => false;
    public override bool IsJumpable => true; // the hole itself is the thing being jumped over
    public override bool IsCrawlable => false;
    public override bool CanPlaceItems => false;
    public override string CellType => "hole";

    public HoleCell(int x, int y) : base(x, y) { }
}
