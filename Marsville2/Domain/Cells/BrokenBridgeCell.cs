namespace Marsville2.Domain.Cells;

/// <summary>
/// A damaged section of bridge that agents cannot stand on.
/// Can be repaired by spending 1 Plank + 1 Nail from the backpack.
/// Repair transforms this into a BridgeCell at the same coordinates.
/// </summary>
public class BrokenBridgeCell : CellBase
{
    public override bool IsWalkable => false;
    public override bool IsJumpable => false;
    public override bool IsCrawlable => false;
    public override bool CanPlaceItems => false;
    public override CellType CellType => CellType.BrokenBridge;

    public BrokenBridgeCell(int x, int y) : base(x, y) { }
}
