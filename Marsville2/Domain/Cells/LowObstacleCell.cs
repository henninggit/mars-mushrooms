namespace Marsville2.Domain.Cells;

/// <summary>
/// A low-hanging Martian rock formation. Cannot be walked through normally;
/// agents must use the Crawl action to pass through this cell.
/// </summary>
public class LowObstacleCell : CellBase
{
    public override bool IsWalkable => false;
    public override bool IsJumpable => false;
    public override bool IsCrawlable => true;
    public override bool CanPlaceItems => false;
    public override CellType CellType => CellType.LowObstacle;

    public LowObstacleCell(int x, int y) : base(x, y) { }
}
