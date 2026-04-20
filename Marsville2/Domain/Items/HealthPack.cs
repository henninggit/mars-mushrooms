namespace Marsville2.Domain.Items;

/// <summary>
/// A health pack that restores the player's HP to their maximum when picked up.
/// If the player is already at full health, the pack is consumed with no effect.
/// Not stored in the backpack — consumed immediately on pickup.
/// </summary>
public class HealthPack : IItem
{
    public ItemType ItemType => ItemType.Health;
}
