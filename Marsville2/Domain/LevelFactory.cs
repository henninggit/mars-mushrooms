using Marsville2.Domain.Cells;
using Marsville2.Domain.Entities;
using Marsville2.Domain.Items;

namespace Marsville2.Domain;

/// <summary>
/// Builds a Board for a given level and seed.
/// The same seed always produces the same board layout.
/// Levels 1-5 are 1-row corridors (East/West + Jump).
/// Levels 6-9 are full 2D grids (N/S/E/W + Jump).
/// Levels 10-11 use a single shared board for all players.
/// </summary>
public static class LevelFactory
{
    public static Board CreateBoard(int level, int seed)
    {
        var board = level switch
        {
            1  => CreateLevel1(seed),
            2  => CreateLevel2(seed),
            3  => CreateLevel3(seed),
            4  => CreateLevel4(seed),
            5  => CreateLevel5(seed),
            6  => CreateLevel6(seed),
            7  => CreateLevel7(seed),
            8  => CreateLevel8(seed),
            9  => CreateLevel9(seed),
            10 => CreateLevel10(seed),
            11 => CreateLevel11(seed),
            _  => throw new ArgumentOutOfRangeException(nameof(level), $"Level {level} is not defined.")
        };

        EnsureBridgeMaterials(board, new Random(seed ^ (level * 1031)));
        return board;
    }

    /// <summary>Returns the thematic name for a given level number.</summary>
    public static string GetLevelName(int level) => level switch
    {
        1 => "Dust & Boots",
        2 => "Mind the Gap",
        3 => "Duck and Dash",
        4 => "Bridge Builders",
        5 => "Blind Repair",
        6 => "Labyrinth of Dust",
        7 => "Spore Highway",
        8 => "Hostile Corridors",
        9 => "Lost Caves of Mars",
        10 => "Colony Convergence",
        11 => "Last Spore Standing",
        _ => $"Level {level}"
    };

    /// <summary>Returns true for levels that use a single shared board for all players.</summary>
    public static bool IsSharedLevel(int level) => level is 10 or 11;

    // ------------------------------------------------------------------ helpers

    private static CellBase[] BuildCorridor(int width, Action<CellBase[]> configure)
    {
        var cells = new CellBase[width];
        for (int x = 0; x < width; x++)
            cells[x] = new FloorCell(x, 0);
        cells[0] = new FloorCell(0, 0); // start
        cells[width - 1] = new GoalCell(width - 1, 0);
        configure(cells);
        return cells;
    }

    private static CellBase[] BuildGrid(int width, int height, Action<CellBase[,]> configure)
    {
        var grid = new CellBase[height, width];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                grid[y, x] = new FloorCell(x, y);

        configure(grid);

        var flat = new CellBase[width * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                flat[y * width + x] = grid[y, x];
        return flat;
    }

    // random offset within [min, max] that is deterministic given seed+level
    private static int SeededOffset(int seed, int level, int min, int max)
    {
        var rng = new Random(seed ^ (level * 397));
        return rng.Next(min, max + 1);
    }

    // ------------------------------------------------------------------ Level 1
    // Straight corridor, no obstacles, no coins. Warm-up.
    private static Board CreateLevel1(int seed)
    {
        const int width = 12;
        var cells = BuildCorridor(width, _ => { });
        return new Board(width, 1, 1, cells);
    }

    // ------------------------------------------------------------------ Level 2
    // Corridor with one HoleCell to jump over.
    private static Board CreateLevel2(int seed)
    {
        const int width = 14;
        int holeX = SeededOffset(seed, 2, 3, 8);
        var cells = BuildCorridor(width, c =>
        {
            c[holeX] = new HoleCell(holeX, 0);
        });
        return new Board(width, 1, 2, cells);
    }

    // ------------------------------------------------------------------ Level 3
    // Jump (hole) + crawl (low obstacle) + 1 mushroom
    private static Board CreateLevel3(int seed)
    {
        const int width = 16;
        int holeX = SeededOffset(seed, 3, 2, 5);
        int crawlX = SeededOffset(seed, 31, 8, 12);
        int mushroomX = SeededOffset(seed, 32, 6, 7);

        var cells = BuildCorridor(width, c =>
        {
            c[holeX] = new HoleCell(holeX, 0);
            c[crawlX] = new LowObstacleCell(crawlX, 0);
            c[mushroomX].Items.Add(new Mushroom());
        });
        return new Board(width, 1, 3, cells);
    }

    // ------------------------------------------------------------------ Level 4
    // Bridge gap (3 BrokenBridge) + 1 mushroom
    private static Board CreateLevel4(int seed)
    {
        const int width = 18;
        int bridgeStart = SeededOffset(seed, 4, 5, 9);
        int mushroomX = SeededOffset(seed, 41, 2, 4);

        var cells = BuildCorridor(width, c =>
        {
            for (int i = 0; i < 3; i++)
                c[bridgeStart + i] = new BrokenBridgeCell(bridgeStart + i, 0);
            c[mushroomX].Items.Add(new Mushroom());
        });
        return new Board(width, 1, 4, cells);
    }

    // ------------------------------------------------------------------ Level 5
    // Same as level 4 but vision radius = 3
    private static Board CreateLevel5(int seed)
    {
        const int width = 20;
        int bridgeStart = SeededOffset(seed, 5, 6, 11);
        int mushroomX = SeededOffset(seed, 51, 2, 4);

        var cells = BuildCorridor(width, c =>
        {
            for (int i = 0; i < 3; i++)
                c[bridgeStart + i] = new BrokenBridgeCell(bridgeStart + i, 0);
            c[mushroomX].Items.Add(new Mushroom());
        });
        return new Board(width, 1, 5, cells, visionRadius: 3);
    }

    // ------------------------------------------------------------------ Level 6
    // Full 2D grid: walled corridors, bridge gap + low obstacle + vision=3, no mushrooms
    //
    //  Layout (12 x 10, S=start, G=goal, W=wall, B=broken bridge, L=low obstacle)
    //
    //  W W W W W W W W W W W W
    //  W S . . W . . . . . . W
    //  W . W . W . W W W W . W
    //  W . W . . . W . . . . W   <- plank @ (3,3), nail @ (1,3)
    //  W . W W W B B B W . . W   <- bridge gap cols 5-7
    //  W . . . . . . . W . . W
    //  W W W W . W W . W . . W
    //  W . . . . . W L L L . W   <- low obstacle cols 7-9
    //  W . W W W . . . . . . W
    //  W W W W W W W W W W W G
    //
    private static Board CreateLevel6(int seed)
    {
        const int width = 12, height = 10;

        var flat = BuildGrid(width, height, grid =>
        {
            // Border walls
            for (int x = 0; x < width; x++) { grid[0, x] = new WallCell(x, 0); grid[height - 1, x] = new WallCell(x, height - 1); }
            for (int y = 0; y < height; y++) { grid[y, 0] = new WallCell(0, y); grid[y, width - 1] = new WallCell(width - 1, y); }

            // Goal (bottom-right, inside border)
            grid[height - 1, width - 1] = new GoalCell(width - 1, height - 1);

            // Internal walls — vertical dividers creating corridors
            for (int y = 1; y < 6; y++) grid[y, 4] = new WallCell(4, y);   // col-4 divider rows 1-5
            for (int y = 2; y < 5; y++) grid[y, 2] = new WallCell(2, y);   // col-2 stub rows 2-4
            for (int y = 2; y < 6; y++) grid[y, 6] = new WallCell(6, y);   // col-6 stub rows 2-5 (partial)
            grid[2, 7] = new WallCell(7, 2); grid[2, 8] = new WallCell(8, 2); grid[2, 9] = new WallCell(9, 2);
            for (int y = 4; y < 9; y++) grid[y, 8] = new WallCell(8, y);   // col-8 divider rows 4-8
            grid[6, 1] = new WallCell(1, 6); grid[6, 2] = new WallCell(2, 6); grid[6, 3] = new WallCell(3, 6);
            grid[6, 5] = new WallCell(5, 6); grid[6, 6] = new WallCell(6, 6);
            grid[8, 2] = new WallCell(2, 8); grid[8, 3] = new WallCell(3, 8); grid[8, 4] = new WallCell(4, 8);

            // Bridge gap across row 4 (bridging the col-4 divider gap)
            grid[4, 5] = new BrokenBridgeCell(5, 4);
            grid[4, 6] = new BrokenBridgeCell(6, 4);
            grid[4, 7] = new BrokenBridgeCell(7, 4);

            // Low obstacle tunnel on row 7
            grid[7, 7] = new LowObstacleCell(7, 7);
            grid[7, 8] = new LowObstacleCell(8, 7);
            grid[7, 9] = new LowObstacleCell(9, 7);
        });

        return new Board(width, height, 6, flat, visionRadius: 3, startX: 1, startY: 1);
    }

    // ------------------------------------------------------------------ Level 7
    // Full 2D + walled corridors + bridge + crawl + vision=3 + 2 mushrooms
    //
    //  Layout (14 x 12, S=start top-left inside walls, G=goal bottom-right)
    //
    //  W W W W W W W W W W W W W W
    //  W S . . . . W . . . . . . W
    //  W W W W . . W . . M . . . W   <- mushroom at (9,2)
    //  W . . . . . . . W W W W . W
    //  W . W W W W . . W . p . . W   <- plank @ (10,4)
    //  W . W B B B B . W . n . . W   <- bridge cols 3-6, nail @ (10,5)
    //  W . W . . . . . . . . . . W
    //  W . . . . W W L L L L W . W   <- low obstacle cols 7-10
    //  W W W W . . . . . . . W . W
    //  W . . . . W . . . . . . . W
    //  W . . M . . . . . W W W . W   <- mushroom at (3,10)
    //  W W W W W W W W W W W W W G
    //
    private static Board CreateLevel7(int seed)
    {
        const int width = 14, height = 12;

        var flat = BuildGrid(width, height, grid =>
        {
            // Border walls
            for (int x = 0; x < width; x++) { grid[0, x] = new WallCell(x, 0); grid[height - 1, x] = new WallCell(x, height - 1); }
            for (int y = 0; y < height; y++) { grid[y, 0] = new WallCell(0, y); grid[y, width - 1] = new WallCell(width - 1, y); }

            grid[height - 1, width - 1] = new GoalCell(width - 1, height - 1);

            // Horizontal wall blocking early right-side shortcut
            grid[2, 1] = new WallCell(1, 2); grid[2, 2] = new WallCell(2, 2);
            grid[2, 3] = new WallCell(3, 2); grid[2, 4] = new WallCell(4, 2);

            // Vertical divider splitting the board
            for (int y = 1; y < 6; y++) grid[y, 6] = new WallCell(6, y);
            for (int y = 3; y < 9; y++) grid[y, 8] = new WallCell(8, y);

            // L-shaped wall creating a pocket on the west side
            grid[4, 2] = new WallCell(2, 4); grid[4, 3] = new WallCell(3, 4);
            grid[4, 4] = new WallCell(4, 4); grid[4, 5] = new WallCell(5, 4);

            // Bridge gap on row 5 across the pocket exit
            grid[5, 3] = new BrokenBridgeCell(3, 5);
            grid[5, 4] = new BrokenBridgeCell(4, 5);
            grid[5, 5] = new BrokenBridgeCell(5, 5);
            grid[5, 6] = new BrokenBridgeCell(6, 5); // spans the divider

            // Lower mid section walls
            grid[7, 5] = new WallCell(5, 7); grid[7, 6] = new WallCell(6, 7);
            grid[7, 12] = new WallCell(12, 7);
            grid[8, 1] = new WallCell(1, 8); grid[8, 2] = new WallCell(2, 8);
            grid[8, 3] = new WallCell(3, 8); grid[8, 4] = new WallCell(4, 8);
            for (int y = 8; y < 12; y++) grid[y, 11] = new WallCell(11, y);
            grid[9, 5] = new WallCell(5, 9);

            // Low obstacle across row 7
            grid[7, 7] = new LowObstacleCell(7, 7);
            grid[7, 8] = new LowObstacleCell(8, 7);
            grid[7, 9] = new LowObstacleCell(9, 7);
            grid[7, 10] = new LowObstacleCell(10, 7);

            // Mushrooms
            grid[2, 9].Items.Add(new Mushroom());
            grid[10, 3].Items.Add(new Mushroom());
        });

        return new Board(width, height, 7, flat, visionRadius: 3, startX: 1, startY: 1);
    }

    // ------------------------------------------------------------------ Level 8
    // Same walled map as level 7 + enemies patrolling the corridors, player has 2 lives
    private static Board CreateLevel8(int seed)
    {
        const int width = 14, height = 12;

        var flat = BuildGrid(width, height, grid =>
        {
            // Border walls
            for (int x = 0; x < width; x++) { grid[0, x] = new WallCell(x, 0); grid[height - 1, x] = new WallCell(x, height - 1); }
            for (int y = 0; y < height; y++) { grid[y, 0] = new WallCell(0, y); grid[y, width - 1] = new WallCell(width - 1, y); }

            grid[height - 1, width - 1] = new GoalCell(width - 1, height - 1);

            // Same internal structure as level 7
            grid[2, 1] = new WallCell(1, 2); grid[2, 2] = new WallCell(2, 2);
            grid[2, 3] = new WallCell(3, 2); grid[2, 4] = new WallCell(4, 2);

            for (int y = 1; y < 6; y++) grid[y, 6] = new WallCell(6, y);
            for (int y = 3; y < 9; y++) grid[y, 8] = new WallCell(8, y);

            grid[4, 2] = new WallCell(2, 4); grid[4, 3] = new WallCell(3, 4);
            grid[4, 4] = new WallCell(4, 4); grid[4, 5] = new WallCell(5, 4);

            grid[5, 3] = new BrokenBridgeCell(3, 5);
            grid[5, 4] = new BrokenBridgeCell(4, 5);
            grid[5, 5] = new BrokenBridgeCell(5, 5);
            grid[5, 6] = new BrokenBridgeCell(6, 5);

            grid[7, 5] = new WallCell(5, 7); grid[7, 6] = new WallCell(6, 7);
            grid[7, 12] = new WallCell(12, 7);
            grid[8, 1] = new WallCell(1, 8); grid[8, 2] = new WallCell(2, 8);
            grid[8, 3] = new WallCell(3, 8); grid[8, 4] = new WallCell(4, 8);
            for (int y = 8; y < 12; y++) grid[y, 11] = new WallCell(11, y);
            grid[9, 5] = new WallCell(5, 9);

            grid[7, 7] = new LowObstacleCell(7, 7);
            grid[7, 8] = new LowObstacleCell(8, 7);
            grid[7, 9] = new LowObstacleCell(9, 7);
            grid[7, 10] = new LowObstacleCell(10, 7);

            grid[2, 9].Items.Add(new Mushroom());
            grid[10, 3].Items.Add(new Mushroom());
        });

        var board = new Board(width, height, 8, flat, visionRadius: 3, startX: 1, startY: 1);

        // Enemies patrolling corridors — placed at walkable spots the agent must pass through
        board.AddEnemy(new Enemy(Guid.NewGuid().ToString(), 9, 1));  // upper-right corridor
        board.AddEnemy(new Enemy(Guid.NewGuid().ToString(), 7, 6));  // mid corridor east of bridge
        board.AddEnemy(new Enemy(Guid.NewGuid().ToString(), 5, 9));  // lower-left corridor

        return board;
    }

    // ------------------------------------------------------------------ Level 9
    // Split maze generated by Recursive Backtracking (DFS).
    // 21 x 19 grid, divided at x=10.
    //   Left half  (x 0-9)  : DFS from (1,1) — player start, plank + nail hidden here.
    //   Divider    (x=10)   : all walls except one BrokenBridgeCell at a seeded odd y.
    //   Right half (x 11-20): DFS from (11, bridgeY) — GoalCell in the far corner.
    // Vision radius = 1: players must explore and memorise the maze.
    private static Board CreateLevel9(int seed)
    {
        const int width = 21, height = 19;
        const int divX = 10; // divider column
        var rng = new Random(seed ^ (9 * 397));

        // Start with all cells as walls (outer ring stays wall naturally via DFS)
        var grid = new CellBase[height, width];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                grid[y, x] = new WallCell(x, y);

        // Carve left half maze (columns 1..divX-1, rows 1..height-2)
        CarvePassages(grid, 1, 1, 1, divX - 1, 1, height - 2, rng);

        // Pick a random odd-y cell on column divX-1 that was carved, to anchor the bridge
        var bridgeCandidates = new List<int>();
        for (int y = 1; y < height - 1; y += 2)
            if (grid[y, divX - 1] is FloorCell)
                bridgeCandidates.Add(y);

        int bridgeY = bridgeCandidates[rng.Next(bridgeCandidates.Count)];

        // Place bridge on the divider; ensure the entry cell on the right is a floor
        grid[bridgeY, divX] = new BrokenBridgeCell(divX, bridgeY);
        grid[bridgeY, divX + 1] = new FloorCell(divX + 1, bridgeY);

        // Carve right half maze (columns divX+1..width-2, rows 1..height-2) from bridge entry
        CarvePassages(grid, divX + 1, bridgeY, divX + 1, width - 2, 1, height - 2, rng);

        // Place goal in the far corner of the right half (bottom-right walkable cell)
        int goalX = -1, goalY = -1;
        for (int y = height - 2; y >= 1 && goalX == -1; y--)
            for (int x = width - 2; x >= divX + 1 && goalX == -1; x--)
                if (grid[y, x] is FloorCell) { goalX = x; goalY = y; }
        grid[goalY, goalX] = new GoalCell(goalX, goalY);

        // Flatten grid to array
        var flat = new CellBase[width * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                flat[y * width + x] = grid[y, x];

        return new Board(width, height, 9, flat, visionRadius: 1, startX: 1, startY: 1);
    }

    /// <summary>
    /// Recursive-backtracking (DFS) maze carver.
    /// Carves passages starting at (startX, startY) within bounds [minX..maxX] x [minY..maxY].
    /// Only visits cells at odd coordinates (standard grid-maze convention).
    /// </summary>
    private static void CarvePassages(CellBase[,] grid, int startX, int startY,
        int minX, int maxX, int minY, int maxY, Random rng)
    {
        grid[startY, startX] = new FloorCell(startX, startY);

        var stack = new Stack<(int x, int y)>();
        stack.Push((startX, startY));

        // Directions: N, S, E, W (step by 2)
        int[] dx = { 0, 0, 2, -2 };
        int[] dy = { -2, 2, 0, 0 };

        while (stack.Count > 0)
        {
            var (cx, cy) = stack.Peek();

            var neighbors = new List<(int nx, int ny)>();
            for (int i = 0; i < 4; i++)
            {
                int nx = cx + dx[i];
                int ny = cy + dy[i];
                if (nx >= minX && nx <= maxX && ny >= minY && ny <= maxY && grid[ny, nx] is WallCell)
                    neighbors.Add((nx, ny));
            }

            if (neighbors.Count > 0)
            {
                var (nx, ny) = neighbors[rng.Next(neighbors.Count)];
                // Carve the wall between current cell and chosen neighbour
                int wallX = (cx + nx) / 2;
                int wallY = (cy + ny) / 2;
                grid[wallY, wallX] = new FloorCell(wallX, wallY);
                grid[ny, nx] = new FloorCell(nx, ny);
                stack.Push((nx, ny));
            }
            else
            {
                stack.Pop();
            }
        }
    }

    // ------------------------------------------------------------------ Level 10
    // Shared 2D grid with walled corridors + multiple zones, all players, enemies, pooled mushrooms
    //
    //  18 x 16 board.  Three distinct zones connected by two chokepoints:
    //   Zone A (top-left)  : start area, materials, 1st chokepoint = bridge gap
    //   Zone B (mid)       : open-ish area with a low-obstacle wall, enemies patrol
    //   Zone C (bottom-right): final stretch to goal
    //
    private static Board CreateLevel10(int seed)
    {
        const int width = 18, height = 16;

        var flat = BuildGrid(width, height, grid =>
        {
            // Border walls
            for (int x = 0; x < width; x++) { grid[0, x] = new WallCell(x, 0); grid[height - 1, x] = new WallCell(x, height - 1); }
            for (int y = 0; y < height; y++) { grid[y, 0] = new WallCell(0, y); grid[y, width - 1] = new WallCell(width - 1, y); }

            grid[height - 1, width - 1] = new GoalCell(width - 1, height - 1);

            // ── Zone divider A/B: vertical wall col 6 rows 1-9, gap at row 6 (bridge)
            for (int y = 1; y < 10; y++)
                if (y != 6) grid[y, 6] = new WallCell(6, y);

            // Bridge gap spanning the chokepoint at row 6 col 6-8
            grid[6, 6] = new BrokenBridgeCell(6, 6);
            grid[6, 7] = new BrokenBridgeCell(7, 6);
            grid[6, 8] = new BrokenBridgeCell(8, 6);

            // Internal walls in Zone A giving it texture
            grid[2, 2] = new WallCell(2, 2); grid[2, 3] = new WallCell(3, 2);
            grid[3, 4] = new WallCell(4, 3); grid[3, 5] = new WallCell(5, 3);
            grid[5, 1] = new WallCell(1, 5); grid[5, 2] = new WallCell(2, 5);
            grid[9, 2] = new WallCell(2, 9); grid[9, 3] = new WallCell(3, 9);
            grid[9, 4] = new WallCell(4, 9); grid[9, 5] = new WallCell(5, 9);

            // ── Zone divider B/C: horizontal wall row 11 cols 7-15, gap at col 12 (low obstacle)
            for (int x = 7; x < 16; x++)
                grid[11, x] = new WallCell(x, 11);
            // Gap at cols 11-13 filled with low obstacles (crawl through)
            grid[11, 11] = new LowObstacleCell(11, 11);
            grid[11, 12] = new LowObstacleCell(12, 11);
            grid[11, 13] = new LowObstacleCell(13, 11);

            // Internal walls in Zone B
            grid[7, 9] = new WallCell(9, 7); grid[7, 10] = new WallCell(10, 7);
            grid[8, 9] = new WallCell(9, 8);
            grid[9, 13] = new WallCell(13, 9); grid[9, 14] = new WallCell(14, 9);
            grid[10, 7] = new WallCell(7, 10); grid[10, 8] = new WallCell(8, 10);

            // Internal walls in Zone C
            grid[12, 8] = new WallCell(8, 12); grid[12, 9] = new WallCell(9, 12);
            grid[13, 10] = new WallCell(10, 13); grid[13, 11] = new WallCell(11, 13);
            for (int x = 13; x < 17; x++) grid[14, x] = new WallCell(x, 14);

            // Mushrooms spread across all three zones
            grid[1, 3].Items.Add(new Mushroom());   // Zone A
            grid[9, 1].Items.Add(new Mushroom());   // Zone A low
            grid[7, 11].Items.Add(new Mushroom());  // Zone B
            grid[10, 14].Items.Add(new Mushroom()); // Zone B far
            grid[13, 7].Items.Add(new Mushroom());  // Zone C
            grid[12, 15].Items.Add(new Mushroom()); // Zone C far
        });

        var board = new Board(width, height, 10, flat, visionRadius: 3, startX: 1, startY: 1, isShared: true);
        // Enemies in Zone B guarding the approach
        board.AddEnemy(new Enemy(Guid.NewGuid().ToString(), 10, 7));
        board.AddEnemy(new Enemy(Guid.NewGuid().ToString(), 15, 8));
        board.AddEnemy(new Enemy(Guid.NewGuid().ToString(), 8, 10));
        board.AddEnemy(new Enemy(Guid.NewGuid().ToString(), 14, 13)); // Zone C

        return board;
    }

    // ------------------------------------------------------------------ Level 11
    // Battle royale — shared board, shrinking ring, #coins = #players at start.
    // Four-quadrant arena with a central plaza and internal walls creating lanes.
    // Coins and player count seeded by GameService.
    private static Board CreateLevel11(int seed)
    {
        const int width = 22, height = 22;

        var flat = BuildGrid(width, height, grid =>
        {
            // No fixed goal — last player alive wins
            // Mushrooms (coins) are placed by GameService based on player count

            // Border walls
            for (int x = 0; x < width; x++) { grid[0, x] = new WallCell(x, 0); grid[height - 1, x] = new WallCell(x, height - 1); }
            for (int y = 0; y < height; y++) { grid[y, 0] = new WallCell(0, y); grid[y, width - 1] = new WallCell(width - 1, y); }

            // ── Cross-shaped inner walls dividing arena into 4 quadrants
            //    Horizontal bar: row 10, cols 2-9 and 12-19 (gap in center)
            for (int x = 2; x < 10; x++) grid[10, x] = new WallCell(x, 10);
            for (int x = 12; x < 20; x++) grid[10, x] = new WallCell(x, 10);
            //    Vertical bar: col 10, rows 2-9 and 12-19 (gap in center)
            for (int y = 2; y < 10; y++) grid[y, 10] = new WallCell(10, y);
            for (int y = 12; y < 20; y++) grid[y, 10] = new WallCell(10, y);

            // ── Per-quadrant internal walls (secondary chokepoints)
            // Top-left quadrant
            grid[3, 3] = new WallCell(3, 3); grid[3, 4] = new WallCell(4, 3); grid[3, 5] = new WallCell(5, 3);
            grid[5, 6] = new WallCell(6, 5); grid[5, 7] = new WallCell(7, 5); grid[5, 8] = new WallCell(8, 5);
            grid[7, 2] = new WallCell(2, 7); grid[7, 3] = new WallCell(3, 7); grid[7, 4] = new WallCell(4, 7);
            // Top-right quadrant
            grid[3, 13] = new WallCell(13, 3); grid[3, 14] = new WallCell(14, 3); grid[3, 15] = new WallCell(15, 3);
            grid[5, 12] = new WallCell(12, 5); grid[5, 13] = new WallCell(13, 5);
            grid[8, 17] = new WallCell(17, 8); grid[8, 18] = new WallCell(18, 8); grid[8, 19] = new WallCell(19, 8);
            grid[6, 15] = new WallCell(15, 6); grid[6, 16] = new WallCell(16, 6); grid[6, 17] = new WallCell(17, 6);
            // Bottom-left quadrant
            grid[13, 2] = new WallCell(2, 13); grid[13, 3] = new WallCell(3, 13); grid[13, 4] = new WallCell(4, 13);
            grid[15, 6] = new WallCell(6, 15); grid[15, 7] = new WallCell(7, 15); grid[15, 8] = new WallCell(8, 15);
            grid[17, 3] = new WallCell(3, 17); grid[17, 4] = new WallCell(4, 17);
            grid[18, 6] = new WallCell(6, 18); grid[18, 7] = new WallCell(7, 18); grid[18, 8] = new WallCell(8, 18);
            // Bottom-right quadrant
            grid[13, 16] = new WallCell(16, 13); grid[13, 17] = new WallCell(17, 13); grid[13, 18] = new WallCell(18, 13);
            grid[16, 12] = new WallCell(12, 16); grid[16, 13] = new WallCell(13, 16); grid[16, 14] = new WallCell(14, 16);
            grid[18, 14] = new WallCell(14, 18); grid[18, 15] = new WallCell(15, 18); grid[18, 16] = new WallCell(16, 18);

            // ── Low obstacles in crossing lanes (aggression chokepoints)
            grid[10, 10] = new LowObstacleCell(10, 10); // central crossing
            for (int x = 4; x < 7; x++) grid[6, x] = new LowObstacleCell(x, 6);
            for (int x = 15; x < 18; x++) grid[15, x] = new LowObstacleCell(x, 15);

            // ── Bridge gaps (require materials to cross)
            grid[4, 11] = new BrokenBridgeCell(11, 4); grid[4, 12] = new BrokenBridgeCell(12, 4);
            grid[16, 8] = new BrokenBridgeCell(8, 16); grid[16, 9] = new BrokenBridgeCell(9, 16);

        });

        return new Board(width, height, 11, flat, visionRadius: 3, isShared: true);
    }

    // ------------------------------------------------------------------ Bridge-material placement

    /// <summary>
    /// Ensures every broken-bridge segment that lies on the path from the player start
    /// to the goal has exactly one <see cref="Plank"/> and one <see cref="Nail"/> placed
    /// somewhere in the reachable area before it.
    ///
    /// The algorithm uses BFS to discover which cells are reachable without crossing any
    /// broken bridge.  It then iterates: for each broken-bridge cell adjacent to the
    /// reachable frontier it places one plank and one nail on two distinct floor cells
    /// already reachable, "unlocks" that bridge (treats it as passable for subsequent BFS
    /// passes), and repeats until the goal is reachable or there are no more bridges to
    /// unlock.
    ///
    /// This method is a no-op when the board has no goal cell (e.g. level 11 battle
    /// royale) or when the goal is already reachable without any bridge repairs.
    /// </summary>
    public static void EnsureBridgeMaterials(Board board, Random rng)
    {
        // No goal means no path to guarantee (battle royale levels).
        if (!HasGoal(board)) return;

        var unlockedBridges = new HashSet<(int x, int y)>();

        while (true)
        {
            var reachable = BfsReachable(board, board.StartX, board.StartY, unlockedBridges);

            // Done if the goal is reachable.
            if (reachable.Any(pos => board.GetCell(pos.x, pos.y) is GoalCell))
                return;

            // Find broken-bridge cells that are adjacent to the reachable frontier
            // and not yet unlocked.
            var nextBridge = FindFrontierBridge(board, reachable, unlockedBridges);
            if (nextBridge is null)
                return; // No bridge found — level design issue; give up gracefully.

            // Place one plank and one nail on two distinct reachable CanPlaceItems cells.
            PlaceMaterialPair(board, reachable, board.StartX, board.StartY, rng);

            // Unlock this bridge for subsequent BFS passes.
            unlockedBridges.Add(nextBridge.Value);
        }
    }

    /// <summary>
    /// BFS from (startX, startY) on <paramref name="board"/>.
    /// Treats <see cref="BrokenBridgeCell"/>s as passable only when they appear in
    /// <paramref name="unlockedBridges"/>.  Includes jump moves over <see cref="HoleCell"/>s.
    /// Returns the set of reachable (x, y) positions.
    /// </summary>
    private static HashSet<(int x, int y)> BfsReachable(
        Board board, int startX, int startY,
        HashSet<(int x, int y)> unlockedBridges)
    {
        var visited = new HashSet<(int x, int y)>();
        var queue = new Queue<(int x, int y)>();

        visited.Add((startX, startY));
        queue.Enqueue((startX, startY));

        int[] dx = { 1, -1, 0, 0 };
        int[] dy = { 0, 0, -1, 1 };

        while (queue.Count > 0)
        {
            var (cx, cy) = queue.Dequeue();

            for (int d = 0; d < 4; d++)
            {
                int nx = cx + dx[d];
                int ny = cy + dy[d];
                if (!board.InBounds(nx, ny)) continue;

                var neighbour = board.GetCell(nx, ny);

                // Normal walk
                if ((neighbour.IsWalkable ||
                     (neighbour is BrokenBridgeCell && unlockedBridges.Contains((nx, ny))))
                    && visited.Add((nx, ny)))
                {
                    queue.Enqueue((nx, ny));
                }

                // Jump over a hole: mid cell must be a HoleCell, landing cell walkable
                if (neighbour is HoleCell)
                {
                    int lx = cx + dx[d] * 2;
                    int ly = cy + dy[d] * 2;
                    if (board.InBounds(lx, ly))
                    {
                        var landing = board.GetCell(lx, ly);
                        if (landing.IsWalkable && visited.Add((lx, ly)))
                            queue.Enqueue((lx, ly));
                    }
                }
            }
        }

        return visited;
    }

    /// <summary>
    /// Finds the first <see cref="BrokenBridgeCell"/> that is directly adjacent (cardinal)
    /// to at least one reachable cell and has not yet been unlocked.
    /// </summary>
    private static (int x, int y)? FindFrontierBridge(
        Board board,
        HashSet<(int x, int y)> reachable,
        HashSet<(int x, int y)> unlockedBridges)
    {
        int[] dx = { 1, -1, 0, 0 };
        int[] dy = { 0, 0, -1, 1 };

        foreach (var (rx, ry) in reachable)
        {
            for (int d = 0; d < 4; d++)
            {
                int bx = rx + dx[d];
                int by = ry + dy[d];
                if (!board.InBounds(bx, by)) continue;
                if (board.GetCell(bx, by) is BrokenBridgeCell
                    && !unlockedBridges.Contains((bx, by)))
                    return (bx, by);
            }
        }

        return null;
    }

    /// <summary>
    /// Places one <see cref="Plank"/> and one <see cref="Nail"/> on two distinct
    /// reachable cells that accept items (<see cref="CellBase.CanPlaceItems"/>),
    /// excluding the player start cell.
    /// </summary>
    private static void PlaceMaterialPair(
        Board board,
        HashSet<(int x, int y)> reachable,
        int startX, int startY,
        Random rng)
    {
        var candidates = reachable
            .Where(pos => !(pos.x == startX && pos.y == startY)
                          && board.GetCell(pos.x, pos.y).CanPlaceItems)
            .ToList();

        if (candidates.Count == 0) return;

        int pi = rng.Next(candidates.Count);
        var (px, py) = candidates[pi];
        board.GetCell(px, py).Items.Add(new Plank());

        // Remove the chosen cell so the nail goes somewhere different.
        candidates.RemoveAt(pi);
        if (candidates.Count == 0) return;

        var (nx, ny) = candidates[rng.Next(candidates.Count)];
        board.GetCell(nx, ny).Items.Add(new Nail());
    }

    private static bool HasGoal(Board board)
    {
        for (int y = 0; y < board.Height; y++)
            for (int x = 0; x < board.Width; x++)
                if (board.GetCell(x, y) is GoalCell)
                    return true;
        return false;
    }
}
