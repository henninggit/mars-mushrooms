namespace Marsville2.Domain.Cells;

public interface ICell
{
    int X { get; }
    int Y { get; }
    bool IsWalkable { get; }
    bool IsJumpable { get; }
    bool IsCrawlable { get; }
    bool CanPlaceItems { get; }
    string CellType { get; }
}
