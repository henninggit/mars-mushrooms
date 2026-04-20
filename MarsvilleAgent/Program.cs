/*  MarsvilleAgent -- simple heuristic test agent for the Marsville game server.
 *
 *  Usage:
 *    dotnet run -- [teamName] [serverUrl] [agentCount]
 *
 *  Examples:
 *    dotnet run                                  # 1 bot "TestBot-1" vs localhost
 *    dotnet run -- TeamRocket                    # named team
 *    dotnet run -- Bot http://localhost:5181 3   # 3 bots, explicit server
 *
 *  The agent:
 *    1. Registers with the server.
 *    2. Polls game state every 300 ms.
 *    3. Acts greedily toward the goal:
 *       - Picks up any Plank/Nail on the current cell.
 *       - Builds a bridge on the adjacent BrokenBridgeCell if it has materials.
 *       - Jumps over a single HoleCell.
 *       - Crawls through a LowObstacleCell.
 *       - Otherwise moves toward the goal (East on corridors; E/S on 2D boards).
 *    4. Waits if it cannot determine a valid action.
 *    5. Exits once it gets GoalReached or its HP drops to 0.
 */

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

const string DefaultServer = "http://localhost:5181";

// --- Parse args ---
string rawTeam  = args.Length > 0 ? args[0] : "TestBot";
string server   = args.Length > 1 ? args[1] : DefaultServer;
int agentCount  = args.Length > 2 ? int.Parse(args[2]) : 1;

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.WriteLine($"Marsville Test Agent  |  server={server}  agents={agentCount}");

// Spawn multiple agents in parallel if requested
var tasks = Enumerable.Range(1, agentCount)
    .Select(i => RunAgent($"{rawTeam}-{i}", server))
    .ToArray();

await Task.WhenAll(tasks);
Console.WriteLine("All agents finished.");

// ---------------------------------------------------------------------------

static async Task RunAgent(string teamName, string server)
{
    using var http = new HttpClient { BaseAddress = new Uri(server) };
    http.Timeout = TimeSpan.FromSeconds(10);

    // ── Register (once; re-registers automatically on token invalidation) ──
    await Register(http, teamName);

    // ── Lobby loop: play every round, then wait for the next one ──
    while (true)
    {
        // Wait until a round is in Playing phase and we have a board
        Console.WriteLine($"[{teamName}] Waiting for next round...");
        GameState? state = null;
        while (state is null)
        {
            await Task.Delay(1000);
            var (fetchedState, unauthorized) = await FetchState(http, teamName);
            if (unauthorized)
            {
                // Token no longer valid (server restarted) — re-register
                Console.WriteLine($"[{teamName}] Token rejected, re-registering...");
                await Register(http, teamName);
            }
            state = fetchedState;
        }

        Console.WriteLine($"[{teamName}] Round started. Level={state.Level}. Playing...");

        // ── Play loop for a single round ──
        int idleStreak = 0;
        const int MaxIdle = 30;
        string? lastRoundLevel = state.Level.ToString();

        while (true)
        {
            var (current, unauth) = await FetchState(http, teamName);
            if (unauth)
            {
                Console.WriteLine($"[{teamName}] Token rejected mid-round, re-registering...");
                await Register(http, teamName);
                break; // go back to lobby wait
            }

            // Round ended (no state) or a new round already started at a different level
            if (current is null)
            {
                Console.WriteLine($"[{teamName}] Round ended. Returning to lobby.");
                break;
            }

            // Detect round change (level changed) — treat as new round
            if (current.Level.ToString() != lastRoundLevel)
            {
                Console.WriteLine($"[{teamName}] New round detected (Level {current.Level}). Restarting play.");
                break;
            }

            if (current.Health <= 0)
            {
                Console.WriteLine($"[{teamName}] Eliminated. Mushrooms={current.MushroomsCollected}. Returning to lobby.");
                break;
            }

            var action = ChooseAction(current);
            Console.WriteLine($"[{teamName}] ({current.X},{current.Y}) HP={current.Health}/{current.MaxHealth} " +
                              $"Shield={current.ShieldHealth} Mushrooms={current.MushroomsCollected} " +
                              $"-> {action.Verb}({action.Direction?.ToString() ?? "-"})");

            var result = await PostAction(http, teamName, action);
            if (result is null) { await Task.Delay(500); continue; }

            if (result.Contains("GoalReached"))
            {
                Console.WriteLine($"[{teamName}] GOAL REACHED! Level={current.Level} Mushrooms={current.MushroomsCollected}. Waiting for round to end...");
                // Poll until the round ends before returning to lobby
                while (true)
                {
                    await Task.Delay(1000);
                    var (endState, endUnauth) = await FetchState(http, teamName);
                    if (endUnauth) { await Register(http, teamName); break; }
                    if (endState is null || endState.Level.ToString() != lastRoundLevel) break;
                }
                Console.WriteLine($"[{teamName}] Round ended. Returning to lobby.");
                break;
            }

            if (action.Verb == "wait") idleStreak++;
            else idleStreak = 0;

            if (idleStreak >= MaxIdle)
            {
                Console.WriteLine($"[{teamName}] Stuck for {MaxIdle} ticks. Returning to lobby.");
                break;
            }

            await Task.Delay(300);
        }

        // Brief pause before polling for the next round
        await Task.Delay(2000);
    }
}

static async Task Register(HttpClient http, string teamName)
{
    // Remove any previous token header before re-registering
    http.DefaultRequestHeaders.Remove("X-Player-Token");

    while (true)
    {
        try
        {
            var reg = await http.PostAsJsonAsync("/api/players/register", new { teamName });
            if (reg.IsSuccessStatusCode)
            {
                var body = await reg.Content.ReadFromJsonAsync<JsonElement>();
                var token = body.GetProperty("token").GetString()!;
                var playerId = body.GetProperty("playerId").GetString()!;
                http.DefaultRequestHeaders.Remove("X-Player-Token");
                http.DefaultRequestHeaders.Add("X-Player-Token", token);
                Console.WriteLine($"[{teamName}] Registered. PlayerId={playerId}");
                return;
            }
            var err = await reg.Content.ReadAsStringAsync();
            Console.WriteLine($"[{teamName}] Register failed ({reg.StatusCode}): {err} -- retrying in 3 s...");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{teamName}] Register error: {ex.Message} -- retrying in 3 s...");
        }
        await Task.Delay(3000);
    }
}

// ---------------------------------------------------------------------------
// Action selection -- simple greedy heuristic

static AgentAction ChooseAction(GameState s)
{
    throw new NotImplementedException();
}

// ---------------------------------------------------------------------------
// HTTP helpers

static async Task<(GameState? state, bool unauthorized)> FetchState(HttpClient http, string teamName)
{
    try
    {
        var resp = await http.GetAsync("/api/game/state");
        if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            return (null, true);
        if (!resp.IsSuccessStatusCode) return (null, false);
        var state = await resp.Content.ReadFromJsonAsync<GameState>(new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
        });
        return (state, false);
    }
    catch { return (null, false); }
}

static async Task<string?> PostAction(HttpClient http, string teamName, AgentAction action)
{
    try
    {
        HttpResponseMessage resp;
        if (action.Direction.HasValue)
            resp = await http.PostAsJsonAsync($"/api/game/{action.Verb}",
                new { direction = action.Direction.Value });
        else
            resp = await http.PostAsync($"/api/game/{action.Verb}", null);

        return await resp.Content.ReadAsStringAsync();
    }
    catch { return null; }
}

// ---------------------------------------------------------------------------
// DTOs

record AgentAction(string Verb, int? Direction);

record EntityDto(string EntityType, string Id, int Health);

record CellDto(int X, int Y, CellType CellType, List<ItemType> Items, EntityDto? Entity);

record GameState(
    string PlayerId,
    string TeamName,
    int X, int Y,
    int Health, int MaxHealth,
    int ShieldHealth,
    bool IsCrawling,
    int MushroomsCollected,
    List<ItemType> Backpack,
    List<CellDto> VisibleCells,
    int BoardWidth, int BoardHeight,
    int Level
);

// ---------------------------------------------------------------------------
// Enums

/// <summary>Cell types as returned by the server (snake_case strings).</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
enum CellType
{
    [JsonStringEnumMemberName("floor")]         Floor,
    [JsonStringEnumMemberName("hole")]          Hole,
    [JsonStringEnumMemberName("wall")]          Wall,
    [JsonStringEnumMemberName("broken_bridge")] BrokenBridge,
    [JsonStringEnumMemberName("bridge")]        Bridge,
    [JsonStringEnumMemberName("low_obstacle")]  LowObstacle,
    [JsonStringEnumMemberName("goal")]          Goal,
    [JsonStringEnumMemberName("teleporter")]    Teleporter,
    [JsonStringEnumMemberName("warning")]       Warning,
}

/// <summary>Item types as returned by the server (snake_case strings).</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
enum ItemType
{
    [JsonStringEnumMemberName("mushroom")]        Mushroom,
    [JsonStringEnumMemberName("nail")]            Nail,
    [JsonStringEnumMemberName("plank")]           Plank,
    [JsonStringEnumMemberName("health")]          Health,
    [JsonStringEnumMemberName("shield")]          Shield,
    [JsonStringEnumMemberName("poison_mushroom")] PoisonMushroom,
}
