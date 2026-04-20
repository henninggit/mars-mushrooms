namespace Marsville2.Domain.Cells;

/// <summary>
/// A teleporter pad. When a player steps onto this cell they are instantly
/// warped to the paired teleporter at (<see cref="TargetX"/>, <see cref="TargetY"/>).
/// Teleportation is one-shot per action — landing on a teleporter via teleport
/// does not trigger a second warp.
/// </summary>
public class TeleporterCell : CellBase
{
    public int TargetX { get; }
    public int TargetY { get; }

    public override bool IsWalkable => true;
    public override bool IsJumpable => false;
    public override bool IsCrawlable => true;
    public override bool CanPlaceItems => true;
    public override CellType CellType => CellType.Teleporter;

    public TeleporterCell(int x, int y, int targetX, int targetY) : base(x, y)
    {
        TargetX = targetX;
        TargetY = targetY;
    }
}
