namespace Marsville2.Domain.Items;

/// <summary>
/// A toxic Martian mushroom that is indistinguishable from a regular mushroom by sight.
/// Safe to step on — it is NOT auto-collected on movement.
/// Lethal only when explicitly picked up via the pickup action (bypasses shields).
/// Other items on the same cell can still be picked up safely; the poison mushroom
/// is only consumed when no other pickup-eligible item is present.
/// </summary>
public class PoisonMushroom : IItem
{
    public ItemType ItemType => ItemType.PoisonMushroom;
}
