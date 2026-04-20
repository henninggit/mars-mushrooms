namespace Marsville2.Domain.Items;

/// <summary>
/// A Martian mushroom — the coin of Marsville.
/// Consumed immediately when a player steps onto the same cell.
/// Worth 1 point.
/// </summary>
public class Mushroom : IItem
{
    public ItemType ItemType => ItemType.Mushroom;
}
