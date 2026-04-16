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
