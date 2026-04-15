using System.Net.Http.Json;
using System.Text.Json;
using MarsvilleStarter.Model;

namespace MarsvilleStarter;

/// <summary>
/// HTTP client wrapper for the Marsville game server.
/// Handles registration, token management, state polling, and all agent actions.
/// </summary>
public sealed class MarsvilleClient : IDisposable, IAsyncDisposable
{
    private static readonly JsonSerializerOptions _jsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly string _teamName;

    private MarsvilleClient(HttpClient http, string teamName)
    {
        _http     = http;
        _teamName = teamName;
    }

    // ---------------------------------------------------------------- factory

    /// <summary>
    /// Creates a new client, connects to the server, and registers the team.
    /// Blocks (retries every 3 s) until registration succeeds.
    /// </summary>
    /// <param name="serverUrl">Base URL of the Marsville server, e.g. "http://localhost:5181".</param>
    /// <param name="teamName">Your team name.</param>
    public static async Task<MarsvilleClient> ConnectAsync(string serverUrl, string teamName)
    {
        var http = new HttpClient
        {
            BaseAddress = new Uri(serverUrl),
            Timeout     = TimeSpan.FromSeconds(10)
        };

        var client = new MarsvilleClient(http, teamName);
        await client.RegisterAsync();
        return client;
    }

    // ---------------------------------------------------------------- state

    /// <summary>
    /// Fetches the current game state.
    /// Returns <c>null</c> when no round is active or the player has not joined yet.
    /// If the token is rejected (server restart) the client automatically re-registers.
    /// </summary>
    public async Task<GameStateView?> GetStateAsync()
    {
        var resp = await _http.GetAsync("/api/game/state");

        if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            Log("Token rejected — re-registering...");
            await RegisterAsync();
            return null;
        }

        if (!resp.IsSuccessStatusCode) return null;

        var dto = await resp.Content.ReadFromJsonAsync<GameStateDto>(_jsonOptions);
        return dto is null ? null : new GameStateView(dto);
    }

    // ---------------------------------------------------------------- actions

    /// <summary>Moves one cell in the given direction. Returns the server result string.</summary>
    public Task<string?> MoveAsync(Direction dir)   => PostDirectional("move",   dir);

    /// <summary>Jumps 2 cells in the given direction over a hole.</summary>
    public Task<string?> JumpAsync(Direction dir)   => PostDirectional("jump",   dir);

    /// <summary>Crawls into an adjacent low-obstacle cell.</summary>
    public Task<string?> CrawlAsync(Direction dir)  => PostDirectional("crawl",  dir);

    /// <summary>Repairs an adjacent broken bridge (requires 1 plank + 1 nail in backpack).</summary>
    public Task<string?> BuildAsync(Direction dir)  => PostDirectional("build",  dir);

    /// <summary>Attacks an enemy in the given direction.</summary>
    public Task<string?> AttackAsync(Direction dir) => PostDirectional("attack", dir);

    /// <summary>Picks up an item from the current cell.</summary>
    public async Task<string?> PickupAsync()
    {
        var resp = await _http.PostAsync("/api/game/pickup", null);
        return await ReadResultAsync(resp);
    }

    /// <summary>Waits one turn (enemies still move).</summary>
    public async Task<string?> WaitAsync()
    {
        var resp = await _http.PostAsync("/api/game/wait", null);
        return await ReadResultAsync(resp);
    }

    /// <summary>
    /// Executes a <see cref="PossibleAction"/> returned by
    /// <see cref="Model.GameStateView.GetPossibleActions()"/>.
    /// </summary>
    public Task<string?> ExecuteAsync(PossibleAction action) => action.ActionType switch
    {
        ActionType.Move   => MoveAsync(action.Direction!.Value),
        ActionType.Jump   => JumpAsync(action.Direction!.Value),
        ActionType.Crawl  => CrawlAsync(action.Direction!.Value),
        ActionType.Build  => BuildAsync(action.Direction!.Value),
        ActionType.Attack => AttackAsync(action.Direction!.Value),
        ActionType.Pickup => PickupAsync(),
        ActionType.Wait   => WaitAsync(),
        _                 => throw new ArgumentException($"Unknown action type: {action.ActionType}")
    };

    // ---------------------------------------------------------------- helpers

    private async Task<string?> PostDirectional(string verb, Direction dir)
    {
        var resp = await _http.PostAsJsonAsync(
            $"/api/game/{verb}",
            new DirectionBody((int)dir),
            _jsonOptions);
        return await ReadResultAsync(resp);
    }

    private static async Task<string?> ReadResultAsync(HttpResponseMessage resp)
    {
        try { return await resp.Content.ReadAsStringAsync(); }
        catch { return null; }
    }

    private async Task RegisterAsync()
    {
        _http.DefaultRequestHeaders.Remove("X-Player-Token");

        while (true)
        {
            try
            {
                var reg = await _http.PostAsJsonAsync(
                    "/api/players/register",
                    new { teamName = _teamName },
                    _jsonOptions);

                if (reg.IsSuccessStatusCode)
                {
                    var body      = await reg.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
                    var token     = body.GetProperty("token").GetString()!;
                    var playerId  = body.GetProperty("playerId").GetString()!;
                    _http.DefaultRequestHeaders.Remove("X-Player-Token");
                    _http.DefaultRequestHeaders.Add("X-Player-Token", token);
                    Log($"Registered. PlayerId={playerId}");
                    return;
                }

                var err = await reg.Content.ReadAsStringAsync();
                Log($"Registration failed ({reg.StatusCode}): {err} — retrying in 3 s...");
            }
            catch (Exception ex)
            {
                Log($"Registration error: {ex.Message} — retrying in 3 s...");
            }

            await Task.Delay(3000);
        }
    }

    private void Log(string message) =>
        Console.WriteLine($"[{_teamName}] {message}");

    public void Dispose() => _http.Dispose();
    public ValueTask DisposeAsync() { _http.Dispose(); return ValueTask.CompletedTask; }
}
