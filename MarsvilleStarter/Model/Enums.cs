using System.Text.Json.Serialization;

namespace MarsvilleStarter.Model;

/// <summary>
/// The type of a board cell, matching the server's snake_case string values.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CellType
{
    [JsonStringEnumMemberName("floor")]         Floor,
    [JsonStringEnumMemberName("hole")]          Hole,
    [JsonStringEnumMemberName("wall")]          Wall,
    [JsonStringEnumMemberName("broken_bridge")] BrokenBridge,
    [JsonStringEnumMemberName("bridge")]        Bridge,
    [JsonStringEnumMemberName("low_obstacle")]  LowObstacle,
    [JsonStringEnumMemberName("goal")]          Goal,
    [JsonStringEnumMemberName("teleporter")]    Teleporter,
    /// <summary>
    /// Outermost ring of the battle-royale board (level 12) that is about to
    /// become a wall on the next shrink. Still walkable — evacuate before the
    /// next shrink timer fires.
    /// </summary>
    [JsonStringEnumMemberName("warning")]       Warning,
}

/// <summary>
/// The type of an entity on the board, matching the server's string values.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EntityType
{
    [JsonStringEnumMemberName("player")] Player,
    [JsonStringEnumMemberName("enemy")]  Enemy,
}

/// <summary>
/// The type of an item, matching the server's snake_case string values.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ItemType
{
    [JsonStringEnumMemberName("mushroom")]        Mushroom,
    [JsonStringEnumMemberName("nail")]            Nail,
    [JsonStringEnumMemberName("plank")]           Plank,
    /// <summary>Restores HP to max on pickup. Not stored in the backpack.</summary>
    [JsonStringEnumMemberName("health")]          Health,
    /// <summary>Increases max health by 1 and heals 1 HP on pickup. Not stored in the backpack.</summary>
    [JsonStringEnumMemberName("shield")]          Shield,
    /// <summary>Lethal only when explicitly picked up — safe to step on.</summary>
    [JsonStringEnumMemberName("poison_mushroom")] PoisonMushroom,
}

/// <summary>
/// The action verb an agent can perform.
/// </summary>
public enum ActionType
{
    Move,
    Jump,
    Crawl,
    Pickup,
    Build,
    Attack,
    Wait,
}
