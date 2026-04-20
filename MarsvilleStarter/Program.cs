/*  MarsvilleStarter — scaffolding for Marsville AI agents.
 *
 *  Usage:
 *    dotnet run -- [teamName] [serverUrl]
 *
 *  Examples:
 *    dotnet run                                  # "MyTeam" vs localhost
 *    dotnet run -- TeamRocket                    # named team
 *    dotnet run -- TeamRocket http://10.0.0.5:5181
 *
 *  ── How to build your agent ────────────────────────────────────────────────
 *  Edit the ChooseAction method below. It receives a GameStateView with
 *  everything your agent can currently see, and must return a PossibleAction.
 *
 *  Useful GameStateView properties:
 *    state.Level                   — current level number (1–12)
 *    state.X / state.Y             — agent's position
 *    state.Health / state.MaxHealth
 *    state.ShieldHealth            — bonus max health from collected shields
 *    state.IsCrawling
 *    state.MushroomsCollected
 *    state.Inventory               — backpack contents (ItemType.Plank, ItemType.Nail, …)
 *    state.HasPlankAndNail         — true when backpack has plank + nail
 *    state.VisibleCells            — all cells within vision radius
 *    state.GetCurrentCell()        — cell the agent stands on
 *    state.GetCell(x, y)           — any visible cell (null = fog)
 *
 *  Action helpers:
 *    state.GetPossibleActions()          — actions from current position
 *    state.GetPossibleActions(x, y)      — actions from any visible cell
 *    cell.IsPartlyVisible                — some neighbours hidden by fog
 *
 *  Cell shorthand properties (on CellView):
 *    cell.IsFloor / .IsHole / .IsWall / .IsBrokenBridge / .IsBridge
 *    cell.IsLowObstacle / .IsGoal / .IsTeleporter / .IsWarning / .IsWalkable / .IsJumpable / .IsCrawlable
 *    cell.HasItems / .HasEnemy / .Items / .Entity
 * ───────────────────────────────────────────────────────────────────────────
 */

using MarsvilleStarter;
using MarsvilleStarter.Model;

Console.OutputEncoding = System.Text.Encoding.UTF8;

string teamName = args.Length > 0 ? args[0] : "MyTeam";
string server   = args.Length > 1 ? args[1] : "http://localhost:5181";

Console.WriteLine($"Marsville Starter  |  team={teamName}  server={server}");

await using var client = await MarsvilleClient.ConnectAsync(server, teamName);

// ── Lobby loop: play every round, then wait for the next one ─────────────
while (true)
{
    Console.WriteLine($"[{teamName}] Waiting for a round to start...");

    // Poll until the server has an active round with a board for us
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

        // Detect a new round starting (level changed)
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

        var action = ChooseAction(current);

        Console.WriteLine(
            $"[{teamName}] ({current.X},{current.Y}) HP={current.Health}/{current.MaxHealth} " +
            $"Shield={current.ShieldHealth} Mushrooms={current.MushroomsCollected}  →  {action}");

        var result = await client.ExecuteAsync(action);

        if (result?.Contains("GoalReached") == true)
        {
            Console.WriteLine($"[{teamName}] GOAL REACHED! Mushrooms={current.MushroomsCollected}. Waiting for round to end...");
            // Poll until the round ends before returning to lobby
            while (true)
            {
                await Task.Delay(1000);
                var endState = await client.GetStateAsync();
                if (endState is null || endState.Level != lastLevel) break;
            }
            Console.WriteLine($"[{teamName}] Round ended. Returning to lobby.");
            break;
        }

        await Task.Delay(300);
    }

    await Task.Delay(2000);
}

// ──────────────────────────────────────────────────────────────────────────────
// YOUR AGENT LOGIC GOES HERE
// Replace the body of this method with your own decision-making code.
// ──────────────────────────────────────────────────────────────────────────────
static PossibleAction ChooseAction(GameStateView state)
{
    // GetPossibleActions() lists every valid action from the current position.
    // Actions toward hidden fog-of-war cells are excluded automatically.
    var options = state.GetPossibleActions();

    // You can also ask "what could I do from any visible cell?" — useful for planning.
    // var actionsAt = state.GetPossibleActions(x, y);

    // Example: prefer moving toward the goal, otherwise take the first available action.
    var moveToGoal = options.FirstOrDefault(a => a.ActionType == ActionType.Move &&
        state.GetCell(a.TargetX!.Value, a.TargetY!.Value)?.IsGoal == true);
    if (moveToGoal is not null) return moveToGoal;

    var anyMove = options.FirstOrDefault(a => a.ActionType == ActionType.Move);
    if (anyMove is not null) return anyMove;

    // Fall back to waiting if no better option is available.
    return options.First(a => a.ActionType == ActionType.Wait);
}
