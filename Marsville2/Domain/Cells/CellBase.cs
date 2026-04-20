using Marsville2.Domain.Items;

namespace Marsville2.Domain.Cells;

public abstract class CellBase : ICell
{
    public int X { get; }
    public int Y { get; }
    public abstract bool IsWalkable { get; }
    public abstract bool IsJumpable { get; }
    public abstract bool IsCrawlable { get; }
    public abstract bool CanPlaceItems { get; }
    public abstract CellType CellType { get; }

    // Items resting on this cell (e.g. planks, nails, mushrooms)
    public List<IItem> Items { get; } = new();

    protected CellBase(int x, int y)
    {
        X = x;
        Y = y;
    }
}
