namespace Marsville2.Domain.Items;

/// <summary>
/// A shield that permanently increases the player's maximum health by 1
/// and also heals 1 HP when picked up.
/// Not stored in the backpack — consumed immediately on pickup.
/// </summary>
public class Shield : IItem
{
    public ItemType ItemType => ItemType.Shield;
}
