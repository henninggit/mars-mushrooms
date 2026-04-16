/*  SmartAgent — A* pathfinding agent for the Marsville game.
 *
 *  Usage:
 *    dotnet run -- [teamName] [serverUrl]
 *
 *  Examples:
 *    dotnet run                                  # "SmartTeam" vs localhost
 *    dotnet run -- TeamRocket                    # named team
 *    dotnet run -- TeamRocket http://10.0.0.5:5181
 *
 *  Strategy:
 *    1. Attack adjacent enemies
 *    2. Pickup items on current cell
 *    3. A* toward nearest mushroom in memory
 *    4. A* toward nearest plank/nail in memory (if not already carrying both)
 *    5. A* toward goal
 *    6. Explore unknown territory
 *    7. Wait
 *
 *  A* edge costs: Goal=0, cell with items=1, any other step=2
 *  The board is memorized across turns; memory resets on new round.
 */

using MarsvilleStarter;
using MarsvilleStarter.Model;
using SmartAgent;

Console.OutputEncoding = System.Text.Encoding.UTF8;

string teamName = args.Length > 0 ? args[0] : "SmartTeam";
string server   = args.Length > 1 ? args[1] : "http://localhost:5181";

Console.WriteLine($"SmartAgent  |  team={teamName}  server={server}");

await using var client = await MarsvilleClient.ConnectAsync(server, teamName);

// Brain holds the memorized board — lives across turns within a round
var brain = new SmartAgentBrain();

// ── Lobby loop: play every round, then wait for the next one ─────────────
while (true)
{
    Console.WriteLine($"[{teamName}] Waiting for a round to start...");

    GameStateView? state = null;
    while (state is null)
    {
        await Task.Delay(1000);
        state = await client.GetStateAsync();
    }

    Console.WriteLine($"[{teamName}] Round started — Level {state.Level}. Let's go!");

    int lastLevel = state.Level;

    // ── Play loop for a single round ──────────────────────────────────────
    while (true)
    {
        var current = await client.GetStateAsync();

        if (current is null)
        {
            Console.WriteLine($"[{teamName}] Round ended. Returning to lobby.");
            break;
        }

        if (current.Level != lastLevel)
        {
            Console.WriteLine($"[{teamName}] New round detected (Level {current.Level}).");
            break;
        }

        if (current.Health <= 0)
        {
            Console.WriteLine($"[{teamName}] Eliminated. Mushrooms={current.MushroomsCollected}.");
            break;
        }

        var action = brain.ChooseAction(current);

        Console.WriteLine(
            $"[{teamName}] ({current.X},{current.Y}) HP={current.Health}/{current.MaxHealth} " +
            $"Mushrooms={current.MushroomsCollected} Inv=[{string.Join(",", current.Inventory)}]  →  {action}");

        var result = await client.ExecuteAsync(action);

        if (result?.Contains("GoalReached") == true)
        {
            Console.WriteLine($"[{teamName}] GOAL REACHED! Mushrooms={current.MushroomsCollected}.");
            break;
        }

        await Task.Delay(300);
    }

    await Task.Delay(2000);
}
