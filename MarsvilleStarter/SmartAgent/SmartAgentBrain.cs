using MarsvilleStarter.Model;

namespace SmartAgent;

/// <summary>
/// Smart Marsville agent with A* pathfinding over a memorized board.
///
/// Priority each turn:
///   1. Attack adjacent enemy
///   2. Pickup non-mushroom item on current cell
///   3. A* to goal (if seen)
///   4. A* to nearest plank or nail (if we don't yet have both)
///   5. A* to nearest mushroom
///   6. A* to nearest frontier cell (systematic fog exploration)
///   7. Directional fallback (East > South > North > West)
///   8. Wait
/// </summary>
public sealed class SmartAgentBrain
{
    private readonly BoardMemory _memory = new();

    public PossibleAction ChooseAction(GameStateView state)
    {
        _memory.UpdateFromState(state);
        var options = state.GetPossibleActions();

        // 1. Attack adjacent enemy
        var attack = options.FirstOrDefault(a => a.ActionType == ActionType.Attack);
        if (attack is not null)
            return Log("Attack enemy", attack);

        // 2. Pickup item on current cell — smart priority:
        //    - Shield: always pick up (increases max health)
        //    - Health pack: only pick up when missing HP
        //    - Plank / nail: always pick up
        var currentCell = state.GetCurrentCell();
        if (currentCell?.HasItems == true)
        {
            bool hasShield = currentCell.Items.Contains(ItemType.Shield);
            bool hasHealth = currentCell.Items.Contains(ItemType.Health) && state.Health < state.MaxHealth;
            bool hasMaterials = currentCell.Items.Any(i => i is ItemType.Plank or ItemType.Nail);
            bool hasMushrooms = currentCell.Items.Any(i => i is ItemType.Mushroom);
            if (hasShield || hasHealth || hasMaterials || hasMushrooms)
            {
                var pickup = options.FirstOrDefault(a => a.ActionType == ActionType.Pickup);
                if (pickup is not null)
                    return Log("Pickup item", pickup);
            }
        }

        // 3. Route to goal (if seen)
        if (_memory.GoalPosition is { } goal)
        {
            var a = AStarPathfinder.FindFirstAction(state, _memory, goal.X, goal.Y);
            if (a is not null) return Log($"→ Goal ({goal.X},{goal.Y})", a);
        }

        // 5. Route to nearest item
        {
            var easyGrabItemCells = _memory.GetCellsWithItems()
                .Where(x => !x.Items.All(x => x == ItemType.Health) || state.Health < state.MaxHealth)
                .Where(x => !x.Items.All(x => x == ItemType.PoisonMushroom))
                .Select(c => (c.X, c.Y))
                .Where(x => Math.Abs(x.X - state.X) + Math.Abs(x.Y - state.Y) < 2).ToList();

            var a = AStarPathfinder.FindFirstActionToAnyOf(state, _memory, easyGrabItemCells);
            if (a is not null) return Log("→ Nearest item", a);
        }

        // 6. Frontier exploration — navigate to nearest unseen border through known terrain
        {
            var frontier = _memory.GetFrontierCells(state.BoardWidth, state.BoardHeight);
            var a = AStarPathfinder.FindFirstActionToAnyOf(state, _memory, frontier);
            if (a is not null) return Log("→ Frontier", a);
        }

        // 7. Directional fallback: prefer East > South > North > West
        var preferred = new[] { Direction.East, Direction.South, Direction.North, Direction.West };
        foreach (var dir in preferred)
        {
            var random = new Random();
            var moveOptions = options.Where(x => x.ActionType is ActionType.Move or ActionType.Crawl or ActionType.Jump).ToList();
            var move = moveOptions[random.Next(0, moveOptions.Count)];
            if (move is not null) return Log($"Fallback random {dir}", move);
        }

        // 8. Wait
        return Log("Wait", options.First(a => a.ActionType == ActionType.Wait));
    }

    private static PossibleAction Log(string label, PossibleAction action)
    {
        Console.WriteLine($"  [{label}] {action.Description}");
        return action;
    }
}
