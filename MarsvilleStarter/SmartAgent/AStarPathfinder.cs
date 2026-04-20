using MarsvilleStarter.Model;

namespace SmartAgent;

/// <summary>
/// A* pathfinder over the memorized board + currently visible cells.
/// Edge costs: goal cell = 0, cell with items = 1, any other traversal = 2, unknown fog cell = 4.
/// </summary>
public static class AStarPathfinder
{
    /// <summary>
    /// Returns the first action toward a specific (targetX, targetY).
    /// Returns null if already there or no path exists.
    /// </summary>
    public static PossibleAction? FindFirstAction(
        GameStateView state, BoardMemory memory, int targetX, int targetY)
    {
        if (state.X == targetX && state.Y == targetY) return null;
        return RunAstar(state, memory,
            heuristic: (x, y) => Math.Abs(x - targetX) + Math.Abs(y - targetY),
            isGoal:    (x, y) => x == targetX && y == targetY);
    }

    /// <summary>
    /// Returns the first action toward the nearest cell in <paramref name="candidates"/>.
    /// Useful for multi-target routing (e.g., all frontier cells, all item cells).
    /// </summary>
    public static PossibleAction? FindFirstActionToAnyOf(
        GameStateView state, BoardMemory memory,
        IReadOnlyList<(int x, int y)> candidates,
        int? maxDepth = null)
    {
        if (candidates.Count == 0) return null;

        var targets = new HashSet<(int, int)>(candidates);
        targets.Remove((state.X, state.Y));
        if (targets.Count == 0) return null;

        // Admissible heuristic: min Manhattan distance to any target
        return RunAstar(state, memory,
            heuristic: (x, y) => targets.Min(t => (double)(Math.Abs(x - t.Item1) + Math.Abs(y - t.Item2))),
            isGoal:    (x, y) => targets.Contains((x, y)),
            maxDepth: maxDepth);
    }

    // ---------------------------------------------------------------- core A*

    private static PossibleAction? RunAstar(
        GameStateView state, BoardMemory memory,
        Func<int, int, double> heuristic,
        Func<int, int, bool> isGoal,
        int? maxDepth = null)
    {
        var start = new Node(state.X, state.Y, null, null, 0, heuristic(state.X, state.Y));
        var open  = new PriorityQueue<Node, double>();
        open.Enqueue(start, start.F);

        var bestG = new Dictionary<(int, int), double> { [(state.X, state.Y)] = 0.0 };

        while (open.Count > 0)
        {
            var cur = open.Dequeue();

            // Skip stale entries
            if (bestG.TryGetValue((cur.X, cur.Y), out double best) && best < cur.G)
                continue;

            if (isGoal(cur.X, cur.Y))
                return ReconstructFirstAction(cur);

            foreach (var (action, nx, ny, cost) in GetNeighbors(cur.X, cur.Y, state, memory))
            {
                double newG = cur.G + cost;
                if (bestG.TryGetValue((nx, ny), out double eg) && eg <= newG)
                    continue;

                bestG[(nx, ny)] = newG;
                var next = new Node(nx, ny, cur, action, newG, heuristic(nx, ny));
                open.Enqueue(next, next.F);
            }
        }

        return null;
    }

    // ---------------------------------------------------------------- neighbor generation

    private static IEnumerable<(PossibleAction, int nx, int ny, double cost)> GetNeighbors(
        int x, int y, GameStateView state, BoardMemory memory)
    {
        // Current position: use authoritative live actions
        if (x == state.X && y == state.Y)
        {
            foreach (var action in state.GetPossibleActions())
            {
                if (action.TargetX is null || action.TargetY is null) continue;
                int nx = action.TargetX.Value, ny = action.TargetY.Value;
                yield return (action, nx, ny, EdgeCost(nx, ny, state, memory));
            }
            yield break;
        }

        // Other positions: synthesize from memory + fog speculation
        foreach (Direction dir in Enum.GetValues<Direction>())
        {
            var (dx, dy) = dir.ToOffset();
            int nx = x + dx, ny = y + dy;

            if (nx < 0 || ny < 0 || nx >= state.BoardWidth || ny >= state.BoardHeight) continue;

            var mem  = memory.GetCell(nx, ny);
            var live = state.GetCell(nx, ny);

            if (mem is null && live is null)
            {
                // Unknown fog — speculative move with penalty
                yield return (new PossibleAction(ActionType.Move, dir, nx, ny, $"Fog {dir}"),
                    nx, ny, 4.0);
                continue;
            }

            var cellType = live?.CellType ?? mem!.CellType;

            switch (cellType)
            {
                case CellType.Floor:
                case CellType.Bridge:
                case CellType.Goal:
                case CellType.Teleporter:
                    yield return (new PossibleAction(ActionType.Move, dir, nx, ny, $"Move {dir}"),
                        nx, ny, EdgeCost(nx, ny, state, memory));
                    break;

                // Warning cells are walkable but dangerous — penalise heavily so the agent
                // routes inward whenever an alternative path exists.
                case CellType.Warning:
                    yield return (new PossibleAction(ActionType.Move, dir, nx, ny, $"Move {dir} (warning!)"),
                        nx, ny, EdgeCost(nx, ny, state, memory) + 10);
                    break;

                case CellType.LowObstacle:
                    yield return (new PossibleAction(ActionType.Crawl, dir, nx, ny, $"Crawl {dir}"),
                        nx, ny, EdgeCost(nx, ny, state, memory));
                    break;

                case CellType.Hole:
                {
                    int lx = x + dx * 2, ly = y + dy * 2;
                    if (lx < 0 || ly < 0 || lx >= state.BoardWidth || ly >= state.BoardHeight) break;
                    var landType = (state.GetCell(lx, ly) ?? memory.GetCell(lx, ly))?.CellType;
                    bool walkable = landType is CellType.Floor or CellType.Bridge or CellType.Goal or CellType.Teleporter;
                    bool unknown  = landType is null;
                    if (walkable || unknown)
                        yield return (new PossibleAction(ActionType.Jump, dir, lx, ly, $"Jump {dir}"),
                            lx, ly, EdgeCost(lx, ly, state, memory));
                    break;
                }

                case CellType.BrokenBridge:
                    if (state.HasPlankAndNail)
                        yield return (new PossibleAction(ActionType.Build, dir, nx, ny, $"Build {dir}"),
                            nx, ny, EdgeCost(nx, ny, state, memory));
                    break;

                // Wall: no action possible
            }
        }
    }

    // ---------------------------------------------------------------- helpers

    /// <summary>Edge cost to land on cell (nx, ny). Goal=0, item cell=1, other=2.</summary>
    private static double EdgeCost(int nx, int ny, GameStateView state, BoardMemory memory)
    {
        var cell = state.GetCell(nx, ny) ?? memory.GetCell(nx, ny);
        if (cell?.IsGoal == true)        return 0;
        if (cell?.Items.Count > 0)       return 1;
        return 2;
    }

    private static PossibleAction? ReconstructFirstAction(Node goal)
    {
        var node = goal;
        while (node.Parent?.Parent is not null)
            node = node.Parent;
        return node.Action;
    }

    private sealed class Node
    {
        public readonly int X, Y;
        public readonly Node? Parent;
        public readonly PossibleAction? Action;
        public readonly double G, H;
        public double F => G + H;
        public int Depth;

        public Node(int x, int y, Node? parent, PossibleAction? action, double g, double h)
        {
            X = x;
            Y = y;
            Parent = parent;
            Action = action;
            G = g;
            H = h;
            Depth = 0;
            if (parent?.Depth != null)
            {
                Depth = parent.Depth + 1;
            }
        }
    }
}
