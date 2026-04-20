namespace Marsville2.Domain.Items;

/// <summary>Discriminator enum for all item types in the game.</summary>
public enum ItemType
{
    Mushroom,
    Nail,
    Plank,
    /// <summary>Health pack — serialises as "health" on the wire.</summary>
    Health,
    Shield,
    PoisonMushroom
}
