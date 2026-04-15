using System.Text.Json.Serialization;

namespace MarsvilleStarter.Model;

// ---------------------------------------------------------------------------
// Raw JSON DTOs — these mirror the shapes returned by the Marsville server.
// You normally won't need to use these directly; use GameStateView instead.
// ---------------------------------------------------------------------------

/// <summary>Raw entity data as returned by the server.</summary>
public record EntityDto(
    [property: JsonPropertyName("entityType")] EntityType EntityType,
    [property: JsonPropertyName("id")]         string     Id,
    [property: JsonPropertyName("health")]     int        Health
);

/// <summary>Raw cell data as returned by the server.</summary>
public record CellDto(
    [property: JsonPropertyName("x")]        int           X,
    [property: JsonPropertyName("y")]        int           Y,
    [property: JsonPropertyName("cellType")] CellType      CellType,
    [property: JsonPropertyName("items")]    List<string>  Items,
    [property: JsonPropertyName("entity")]   EntityDto?    Entity
);

/// <summary>
/// Raw game state as returned by <c>GET /api/game/state</c>.
/// Only the cells within the player's vision radius are included.
/// </summary>
public record GameStateDto(
    [property: JsonPropertyName("playerId")]            string        PlayerId,
    [property: JsonPropertyName("teamName")]            string        TeamName,
    [property: JsonPropertyName("x")]                   int           X,
    [property: JsonPropertyName("y")]                   int           Y,
    [property: JsonPropertyName("health")]              int           Health,
    [property: JsonPropertyName("maxHealth")]           int           MaxHealth,
    [property: JsonPropertyName("isCrawling")]          bool          IsCrawling,
    [property: JsonPropertyName("mushroomsCollected")]  int           MushroomsCollected,
    [property: JsonPropertyName("backpack")]            List<string>  Backpack,
    [property: JsonPropertyName("visibleCells")]        List<CellDto> VisibleCells,
    [property: JsonPropertyName("boardWidth")]          int           BoardWidth,
    [property: JsonPropertyName("boardHeight")]         int           BoardHeight,
    [property: JsonPropertyName("level")]               int           Level
);

/// <summary>The body sent with every action that requires a direction.</summary>
public record DirectionBody([property: JsonPropertyName("direction")] int Direction);

/// <summary>Result wrapper returned by every action endpoint on success.</summary>
public record ActionOutcomeDto(
    [property: JsonPropertyName("result")] string        Result,
    [property: JsonPropertyName("state")]  GameStateDto? State
);
