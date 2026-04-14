namespace Marsville2.Domain.Cells;

/// <summary>A repaired bridge — fully walkable. Mushrooms (coins) may also be placed here.</summary>
public class BridgeCell : CellBase
{
    public override bool IsWalkable => true;
    public override bool IsJumpable => false;
    public override bool IsCrawlable => true;
    public override bool CanPlaceItems => true;
    public override string CellType => "bridge";

    public BridgeCell(int x, int y) : base(x, y) { }
}
