using Marsville2.Domain.Cells;
using Marsville2.Domain.Entities;
using Marsville2.Domain.Items;

namespace Marsville2.Domain;

/// <summary>
/// Builds a Board for a given level and seed using a procedural placement pipeline.
/// Every level is encased in walls. Corridor levels (1–5) are a walled 1-row lane.
/// Grid levels (6–10) use DFS maze carving with optional room widening.
/// The goal is always placed on a reachable cell. Broken bridges that block the path
/// to the goal always have a plank and nail placed in the reachable area before them.
/// The same seed always produces the same board layout.
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
            int highLevel  => CreateHighlevel(highLevel, seed)
        };

        EnsureBridgeMaterials(board, new Random(seed ^ (level * 1031)));
        return board;
    }

    /// <summary>Returns the thematic name for a given level number.</summary>
    public static string GetLevelName(int level) => level switch
    {
        1  => "Dust & Boots",
        2  => "Mind the Gap",
        3  => "Duck and Dash",
        4  => "Bridge Builders",
        5  => "Blind Repair",
        6  => "Labyrinth of Dust",
        7  => "Spore Highway",
        8  => "Hostile Corridors",
        9  => "Lost Caves of Mars",
        10 => "Colony Convergence",
        11 => "Last Spore Standing",
        _  => $"Level {level}"
    };

    /// <summary>Returns true for levels that use a single shared board for all players.</summary>
    public static bool IsSharedLevel(int level) => level is 10 or 11;

    // ------------------------------------------------------------------ Level config

    /// <summary>Defines the procedural parameters for a single level.</summary>
    private record LevelConfig(
        int Width, int Height, int Level,
        int VisionRadius = 0,
        bool IsShared = false,
        bool IsCorridor = false,
        int HoleCount = 0,
        int BrokenBridgeCount = 0,
        int LowObstacleCount = 0,
        int MushroomCount = 0,
        int EnemyCount = 0,
        int RoomCount = 0
    );

    // ------------------------------------------------------------------ Level definitions

    // Corridors are width × 3: wall border on top and bottom, floor lane in the middle.
    private static Board CreateLevel1(int seed) =>
        BuildCorridorLevel(new LevelConfig(12, 3, 1, IsCorridor: true), seed);

    private static Board CreateLevel2(int seed) =>
        BuildCorridorLevel(new LevelConfig(14, 3, 2, IsCorridor: true, HoleCount: 1), seed);

    private static Board CreateLevel3(int seed) =>
        BuildCorridorLevel(new LevelConfig(16, 3, 3, IsCorridor: true,
            HoleCount: 1, LowObstacleCount: 1, MushroomCount: 1), seed);

    private static Board CreateLevel4(int seed) =>
        BuildCorridorLevel(new LevelConfig(18, 3, 4, IsCorridor: true,
            BrokenBridgeCount: 3, MushroomCount: 1), seed);

    private static Board CreateLevel5(int seed) =>
        BuildCorridorLevel(new LevelConfig(20, 3, 5, VisionRadius: 3, IsCorridor: true,
            BrokenBridgeCount: 3, MushroomCount: 1), seed);

    private static Board CreateLevel6(int seed) =>
        BuildGridLevel(new LevelConfig(12, 10, 6, VisionRadius: 3,
            BrokenBridgeCount: 3, LowObstacleCount: 3, RoomCount: 2), seed);

    private static Board CreateLevel7(int seed) =>
        BuildGridLevel(new LevelConfig(14, 12, 7, VisionRadius: 3,
            BrokenBridgeCount: 4, LowObstacleCount: 4, MushroomCount: 2, RoomCount: 3), seed);

    private static Board CreateLevel8(int seed) =>
        BuildGridLevel(new LevelConfig(14, 12, 8, VisionRadius: 3,
            BrokenBridgeCount: 4, LowObstacleCount: 4, MushroomCount: 2, EnemyCount: 3, RoomCount: 3), seed);

    private static Board CreateLevel9(int seed) =>
        BuildGridLevel(new LevelConfig(21, 19, 9, VisionRadius: 1,
            BrokenBridgeCount: 1, RoomCount: 1), seed);

    private static Board CreateLevel10(int seed) =>
        BuildGridLevel(new LevelConfig(18, 16, 10, VisionRadius: 3, IsShared: true,
            BrokenBridgeCount: 3, LowObstacleCount: 3, MushroomCount: 6, EnemyCount: 4, RoomCount: 3), seed);

    public static Board CreateHighlevel(int level, int seed)
    {
        var random = new Random();
        var size = level * 2;
        var visionRadius = 2;
        var isShared = false;
        var brokenBridgeCount = random.Next(size / 8, size / 2);
        var lowObstacleCount = random.Next(size / 8, size / 2);
        var mushroomCount = random.Next(size / 8, size / 2);
        var enemyCount = random.Next(size / 16, size / 8);
        var roomCount = random.Next(0, size / 2);
        return BuildGridLevel(new LevelConfig(size, size, level, visionRadius, isShared, false,
            brokenBridgeCount, lowObstacleCount, mushroomCount, enemyCount, roomCount), seed);
    }

    // ------------------------------------------------------------------ Level 11 (battle royale — kept hardcoded)

    // Battle royale: no goal, shared board. Mushrooms are placed by GameService.
    private static Board CreateLevel11(int seed)
    {
        const int w = 22, h = 22;
        var flat = new CellBase[w * h];

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                flat[y * w + x] = new FloorCell(x, y);

        void Set(int x, int y, CellBase c) => flat[y * w + x] = c;

        // Border walls
        for (int x = 0; x < w; x++) { Set(x, 0, new WallCell(x, 0)); Set(x, h - 1, new WallCell(x, h - 1)); }
        for (int y = 0; y < h; y++) { Set(0, y, new WallCell(0, y)); Set(w - 1, y, new WallCell(w - 1, y)); }

        // Cross-shaped divider
        for (int x = 2; x < 10; x++) Set(x, 10, new WallCell(x, 10));
        for (int x = 12; x < 20; x++) Set(x, 10, new WallCell(x, 10));
        for (int y = 2; y < 10; y++) Set(10, y, new WallCell(10, y));
        for (int y = 12; y < 20; y++) Set(10, y, new WallCell(10, y));

        // Per-quadrant internal walls
        Set(3, 3, new WallCell(3, 3)); Set(4, 3, new WallCell(4, 3)); Set(5, 3, new WallCell(5, 3));
        Set(6, 5, new WallCell(6, 5)); Set(7, 5, new WallCell(7, 5)); Set(8, 5, new WallCell(8, 5));
        Set(2, 7, new WallCell(2, 7)); Set(3, 7, new WallCell(3, 7)); Set(4, 7, new WallCell(4, 7));
        Set(13, 3, new WallCell(13, 3)); Set(14, 3, new WallCell(14, 3)); Set(15, 3, new WallCell(15, 3));
        Set(12, 5, new WallCell(12, 5)); Set(13, 5, new WallCell(13, 5));
        Set(17, 8, new WallCell(17, 8)); Set(18, 8, new WallCell(18, 8)); Set(19, 8, new WallCell(19, 8));
        Set(15, 6, new WallCell(15, 6)); Set(16, 6, new WallCell(16, 6)); Set(17, 6, new WallCell(17, 6));
        Set(2, 13, new WallCell(2, 13)); Set(3, 13, new WallCell(3, 13)); Set(4, 13, new WallCell(4, 13));
        Set(6, 15, new WallCell(6, 15)); Set(7, 15, new WallCell(7, 15)); Set(8, 15, new WallCell(8, 15));
        Set(3, 17, new WallCell(3, 17)); Set(4, 17, new WallCell(4, 17));
        Set(6, 18, new WallCell(6, 18)); Set(7, 18, new WallCell(7, 18)); Set(8, 18, new WallCell(8, 18));
        Set(16, 13, new WallCell(16, 13)); Set(17, 13, new WallCell(17, 13)); Set(18, 13, new WallCell(18, 13));
        Set(12, 16, new WallCell(12, 16)); Set(13, 16, new WallCell(13, 16)); Set(14, 16, new WallCell(14, 16));
        Set(14, 18, new WallCell(14, 18)); Set(15, 18, new WallCell(15, 18)); Set(16, 18, new WallCell(16, 18));

        // Low obstacles
        Set(10, 10, new LowObstacleCell(10, 10));
        for (int x = 4; x < 7; x++) Set(x, 6, new LowObstacleCell(x, 6));
        for (int x = 15; x < 18; x++) Set(x, 15, new LowObstacleCell(x, 15));

        // Bridge gaps
        Set(11, 4, new BrokenBridgeCell(11, 4)); Set(12, 4, new BrokenBridgeCell(12, 4));
        Set(8, 16, new BrokenBridgeCell(8, 16)); Set(9, 16, new BrokenBridgeCell(9, 16));

        return new Board(w, h, 11, flat, visionRadius: 3, isShared: true);
    }

    // ------------------------------------------------------------------ Pipeline: corridor levels

    /// <summary>
    /// Builds a walled corridor level of size <c>width × 3</c>: wall top row, floor
    /// middle row, wall bottom row. Spawn is at the west end (1, 1); goal is at the
    /// east end (width-2, 1). Obstacles are placed at seeded positions in the interior.
    /// </summary>
    private static Board BuildCorridorLevel(LevelConfig cfg, int seed)
    {
        var rng = new Random(seed ^ (cfg.Level * 397));
        int w = cfg.Width, h = 3;

        var cells = new CellBase[h * w];
        // Top and bottom rows: all walls
        for (int x = 0; x < w; x++)
        {
            cells[0 * w + x] = new WallCell(x, 0);
            cells[2 * w + x] = new WallCell(x, 2);
        }
        // Middle row: walls on edges, floor inside
        cells[1 * w + 0]       = new WallCell(0, 1);
        cells[1 * w + (w - 1)] = new WallCell(w - 1, 1);
        for (int x = 1; x < w - 1; x++)
            cells[1 * w + x] = new FloorCell(x, 1);

        // Goal at east end
        cells[1 * w + (w - 2)] = new GoalCell(w - 2, 1);

        // Track occupied interior x positions (1-indexed; x=0 and x=w-1 are border walls)
        var occupied = new HashSet<int> { 1, w - 2 };

        // Place holes: each needs a walkable cell 2 steps away for jumping
        for (int i = 0; i < cfg.HoleCount; i++)
        {
            var candidates = Enumerable.Range(3, Math.Max(0, w - 6))
                .Where(x => !occupied.Contains(x)
                         && !occupied.Contains(x - 1)
                         && !occupied.Contains(x + 1))
                .ToList();
            if (candidates.Count == 0) break;
            int hx = candidates[rng.Next(candidates.Count)];
            cells[1 * w + hx] = new HoleCell(hx, 1);
            occupied.Add(hx);
        }

        // Place broken bridge span (consecutive cells — classic corridor bridge gap)
        if (cfg.BrokenBridgeCount > 0)
        {
            int span = cfg.BrokenBridgeCount;
            var starts = Enumerable.Range(3, Math.Max(0, w - 5 - span))
                .Where(s => Enumerable.Range(s - 1, span + 2).All(x => !occupied.Contains(x)))
                .ToList();
            if (starts.Count > 0)
            {
                int bx = starts[rng.Next(starts.Count)];
                for (int i = 0; i < span; i++)
                {
                    cells[1 * w + bx + i] = new BrokenBridgeCell(bx + i, 1);
                    occupied.Add(bx + i);
                }
            }
        }

        // Place low obstacles
        for (int i = 0; i < cfg.LowObstacleCount; i++)
        {
            var candidates = Enumerable.Range(2, Math.Max(0, w - 4))
                .Where(x => !occupied.Contains(x))
                .ToList();
            if (candidates.Count == 0) break;
            int lx = candidates[rng.Next(candidates.Count)];
            cells[1 * w + lx] = new LowObstacleCell(lx, 1);
            occupied.Add(lx);
        }

        var board = new Board(w, h, cfg.Level, cells, cfg.VisionRadius, startX: 1, startY: 1);

        if (cfg.MushroomCount > 0)
        {
            // Treat all bridges as passable so mushrooms can appear anywhere
            var (reachable, _) = BfsReachable(board, 1, 1, AllBridgePositions(board));
            PlaceMushrooms(board, reachable, 1, 1, cfg.MushroomCount, rng);
        }

        return board;
    }

    // ------------------------------------------------------------------ Pipeline: 2D grid levels

    /// <summary>
    /// Builds a 2D grid level via the procedural pipeline:
    /// fill with walls → pick spawn on inner perimeter → DFS carve → carve rooms →
    /// place goal (BFS-weighted to opposite side) → place broken bridges →
    /// place low obstacles → place mushrooms → place enemies.
    /// </summary>
    private static Board BuildGridLevel(LevelConfig cfg, int seed)
    {
        var rng = new Random(seed ^ (cfg.Level * 397));
        int w = cfg.Width, h = cfg.Height;

        // Step 1: fill entirely with walls
        var grid = new CellBase[h, w];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                grid[y, x] = new WallCell(x, y);

        // Step 2: pick spawn on inner perimeter at odd coordinates (DFS requirement)
        var (spawnX, spawnY, side) = PickSpawnOnPerimeter(w, h, rng);

        // Step 3: DFS maze carve from spawn
        CarvePassages(grid, spawnX, spawnY, 1, w - 2, 1, h - 2, rng);

        // Step 4: widen selected carved cells into open rooms
        if (cfg.RoomCount > 0)
            CarveRooms(grid, w, h, cfg.RoomCount, rng);

        // Step 5: create the Board (no goal or items yet)
        var flat = FlattenGrid(grid, w, h);
        var board = new Board(w, h, cfg.Level, flat, cfg.VisionRadius,
            startX: spawnX, startY: spawnY, isShared: cfg.IsShared);

        // Step 6: place goal weighted towards opposite perimeter side
        PlaceGoalReachable(board, spawnX, spawnY, side, rng);

        // Step 7: place broken bridges in the middle-far range from spawn
        if (cfg.BrokenBridgeCount > 0)
        {
            var (reachable, _) = BfsReachable(board, spawnX, spawnY, new HashSet<(int, int)>());
            PlaceBrokenBridges(board, reachable, spawnX, spawnY, cfg.BrokenBridgeCount, rng);
        }

        // Step 8: place low obstacles (bridges treated as passable for candidate set)
        if (cfg.LowObstacleCount > 0)
        {
            var (reachable, _) = BfsReachable(board, spawnX, spawnY, AllBridgePositions(board));
            PlaceLowObstacles(board, reachable, spawnX, spawnY, cfg.LowObstacleCount, rng);
        }

        // Step 9: place mushrooms anywhere in the level (all bridges passable)
        if (cfg.MushroomCount > 0)
        {
            var (reachable, _) = BfsReachable(board, spawnX, spawnY, AllBridgePositions(board));
            PlaceMushrooms(board, reachable, spawnX, spawnY, cfg.MushroomCount, rng);
        }

        // Step 10: place enemies in the far half of the level
        if (cfg.EnemyCount > 0)
        {
            var (reachable, _) = BfsReachable(board, spawnX, spawnY, AllBridgePositions(board));
            PlaceEnemies(board, reachable, spawnX, spawnY, cfg.EnemyCount, rng);
        }

        return board;
    }

    // ------------------------------------------------------------------ Helper: spawn on perimeter

    /// <summary>
    /// Picks a spawn position on the inner perimeter at an odd coordinate,
    /// which is required for the DFS maze carver. Returns spawn (x, y) and
    /// the side (0=north, 1=south, 2=west, 3=east).
    /// </summary>
    private static (int x, int y, int side) PickSpawnOnPerimeter(int w, int h, Random rng)
    {
        // Largest odd value within the inner range [1, w-2] and [1, h-2]
        int maxOddX = (w - 2) % 2 == 1 ? w - 2 : w - 3;
        int maxOddY = (h - 2) % 2 == 1 ? h - 2 : h - 3;
        int oddXCount = (maxOddX - 1) / 2 + 1;
        int oddYCount = (maxOddY - 1) / 2 + 1;

        int side = rng.Next(4);
        return side switch
        {
            0 => (1 + 2 * rng.Next(oddXCount), 1,        0), // north  (y = 1)
            1 => (1 + 2 * rng.Next(oddXCount), maxOddY,  1), // south
            2 => (1,        1 + 2 * rng.Next(oddYCount), 2), // west   (x = 1)
            _ => (maxOddX,  1 + 2 * rng.Next(oddYCount), 3), // east
        };
    }

    // ------------------------------------------------------------------ Helper: carve rooms

    /// <summary>
    /// Widens <paramref name="roomCount"/> randomly chosen carved cells into random×random (max 5x5) open
    /// rooms by replacing surrounding wall cells with floor cells.
    /// </summary>
    private static void CarveRooms(CellBase[,] grid, int w, int h, int roomCount, Random rng)
    {
        var floorCells = new List<(int x, int y)>();
        for (int y = 1; y < h - 1; y++)
            for (int x = 1; x < w - 1; x++)
                if (grid[y, x] is FloorCell)
                    floorCells.Add((x, y));

        for (int r = 0; r < roomCount && floorCells.Count > 0; r++)
        {
            var roomWidth = rng.Next(2, 5);
            var roomHeight = rng.Next(2, 5);
            var (cx, cy) = floorCells[rng.Next(floorCells.Count)];
            for (int dy = -1; dy <= roomHeight; dy++)
                for (int dx = -1; dx <= roomWidth; dx++)
                {
                    int nx = cx + dx, ny = cy + dy;
                    if (nx >= 1 && nx <= w - 2 && ny >= 1 && ny <= h - 2)
                    {
                        // small chance to leave in a column
                        if (rng.Next(1, 8) > 1)
                        {
                            grid[ny, nx] = new FloorCell(nx, ny);
                        }
                    }
                }
        }
    }

    // ------------------------------------------------------------------ Helper: place goal

    /// <summary>
    /// Finds all cells reachable from spawn via BFS, weights them by Manhattan
    /// distance to the opposite perimeter side, and places a <see cref="GoalCell"/>
    /// at a randomly chosen cell from the top 25% farthest candidates.
    /// </summary>
    private static void PlaceGoalReachable(Board board, int spawnX, int spawnY, int side, Random rng)
    {
        var (reachable, depth) = BfsReachable(board, spawnX, spawnY, new HashSet<(int, int)>());

        var candidates = reachable
            .Where(p => board.GetCell(p.x, p.y) is FloorCell
                        && !(p.x == spawnX && p.y == spawnY))
            .OrderByDescending(p => depth[(p.x, p.y)]!)
            .ToList();

        if (candidates.Count == 0) return;

        int topN = Math.Max(1, candidates.Count / 4);
        var (gx, gy) = candidates[rng.Next(topN)];
        board.SetCell(gx, gy, new GoalCell(gx, gy));
    }

    // ------------------------------------------------------------------ Helper: place broken bridges

    /// <summary>
    /// Replaces <paramref name="count"/> reachable floor cells with
    /// <see cref="BrokenBridgeCell"/>s. Bridges are placed in the middle-to-far
    /// distance range from spawn with a minimum spacing of 3 between bridge cells.
    /// Bridges intentionally may block the path to the goal; <see cref="EnsureBridgeMaterials"/>
    /// will place the required plank and nail before each blocking bridge.
    /// </summary>
    private static void PlaceBrokenBridges(Board board, HashSet<(int x, int y)> reachable,
        int spawnX, int spawnY, int count, Random rng)
    {
        var pool = reachable
            .Where(p => board.GetCell(p.x, p.y) is FloorCell
                        && !(p.x == spawnX && p.y == spawnY))
            .OrderBy(p => ManhattanDistance(p.x, p.y, spawnX, spawnY))
            .ToList();

        // Use the middle-to-far two-thirds as candidates
        pool = pool.Skip(pool.Count / 3).ToList();

        for (int i = 0; i < count && pool.Count > 0; i++)
        {
            int idx = rng.Next(pool.Count);
            var (x, y) = pool[idx];
            board.SetCell(x, y, new BrokenBridgeCell(x, y));
            pool.RemoveAt(idx);
            pool.RemoveAll(p => ManhattanDistance(p.x, p.y, x, y) < 3);
        }
    }

    // ------------------------------------------------------------------ Helper: place low obstacles

    /// <summary>
    /// Replaces <paramref name="count"/> reachable floor cells (excluding spawn and goal)
    /// with <see cref="LowObstacleCell"/>s.
    /// </summary>
    private static void PlaceLowObstacles(Board board, HashSet<(int x, int y)> reachable,
        int spawnX, int spawnY, int count, Random rng)
    {
        var pool = reachable
            .Where(p => board.GetCell(p.x, p.y) is FloorCell
                        && !(p.x == spawnX && p.y == spawnY))
            .ToList();

        for (int i = 0; i < count && pool.Count > 0; i++)
        {
            int idx = rng.Next(pool.Count);
            var (x, y) = pool[idx];
            board.SetCell(x, y, new LowObstacleCell(x, y));
            pool.RemoveAt(idx);
        }
    }

    // ------------------------------------------------------------------ Helper: place mushrooms

    /// <summary>
    /// Adds <see cref="Mushroom"/> items to <paramref name="count"/> reachable cells,
    /// skipping spawn and goal. Mushrooms are spread by removing nearby candidates
    /// after each placement.
    /// </summary>
    private static void PlaceMushrooms(Board board, HashSet<(int x, int y)> reachable,
        int spawnX, int spawnY, int count, Random rng)
    {
        var pool = reachable
            .Where(p => board.GetCell(p.x, p.y) is FloorCell
                        && !(p.x == spawnX && p.y == spawnY))
            .ToList();

        for (int i = 0; i < count && pool.Count > 0; i++)
        {
            int idx = rng.Next(pool.Count);
            var (x, y) = pool[idx];
            board.GetCell(x, y).Items.Add(new Mushroom());
            pool.RemoveAt(idx);
            pool.RemoveAll(p => ManhattanDistance(p.x, p.y, x, y) < 2);
        }
    }

    // ------------------------------------------------------------------ Helper: place enemies

    /// <summary>
    /// Adds <see cref="Enemy"/> entities to the board on reachable floor cells,
    /// preferring positions in the far half of the level away from spawn.
    /// </summary>
    private static void PlaceEnemies(Board board, HashSet<(int x, int y)> reachable,
        int spawnX, int spawnY, int count, Random rng)
    {
        var pool = reachable
            .Where(p => board.GetCell(p.x, p.y) is FloorCell
                        && !(p.x == spawnX && p.y == spawnY))
            .OrderByDescending(p => ManhattanDistance(p.x, p.y, spawnX, spawnY))
            .Take(reachable.Count / 2 + 1)
            .ToList();

        for (int i = 0; i < count && pool.Count > 0; i++)
        {
            int idx = rng.Next(pool.Count);
            var (x, y) = pool[idx];
            board.AddEnemy(new Enemy(Guid.NewGuid().ToString(), x, y));
            pool.RemoveAt(idx);
            pool.RemoveAll(p => ManhattanDistance(p.x, p.y, x, y) < 3);
        }
    }

    // ------------------------------------------------------------------ Utilities

    private static int ManhattanDistance(int x1, int y1, int x2, int y2)
        => Math.Abs(x1 - x2) + Math.Abs(y1 - y2);

    /// <summary>Returns all (x, y) positions of <see cref="BrokenBridgeCell"/>s on the board.</summary>
    private static HashSet<(int x, int y)> AllBridgePositions(Board board)
    {
        var result = new HashSet<(int x, int y)>();
        for (int y = 0; y < board.Height; y++)
            for (int x = 0; x < board.Width; x++)
                if (board.GetCell(x, y) is BrokenBridgeCell)
                    result.Add((x, y));
        return result;
    }

    private static CellBase[] FlattenGrid(CellBase[,] grid, int w, int h)
    {
        var flat = new CellBase[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                flat[y * w + x] = grid[y, x];
        return flat;
    }

    // random offset within [min, max] deterministic for seed+level
    private static int SeededOffset(int seed, int level, int min, int max)
    {
        var rng = new Random(seed ^ (level * 397));
        return rng.Next(min, max + 1);
    }

    // ------------------------------------------------------------------ DFS maze carver

    /// <summary>
    /// Recursive-backtracking (DFS) maze carver.
    /// Carves passages starting at (startX, startY) within bounds [minX..maxX] × [minY..maxY].
    /// Only visits cells at odd coordinates (standard grid-maze convention).
    /// </summary>
    private static void CarvePassages(CellBase[,] grid, int startX, int startY,
        int minX, int maxX, int minY, int maxY, Random rng)
    {
        grid[startY, startX] = new FloorCell(startX, startY);

        var stack = new Stack<(int x, int y)>();
        stack.Push((startX, startY));

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

    // ------------------------------------------------------------------ Bridge-material placement

    /// <summary>
    /// Ensures every broken-bridge segment that lies on the path from the player start
    /// to the goal has exactly one <see cref="Plank"/> and one <see cref="Nail"/> placed
    /// somewhere in the reachable area before it.
    ///
    /// The algorithm uses BFS to discover which cells are reachable without crossing any
    /// broken bridge. It then iterates: for each broken-bridge cell adjacent to the
    /// reachable frontier it places one plank and one nail on two distinct floor cells
    /// already reachable, "unlocks" that bridge (treats it as passable for subsequent BFS
    /// passes), and repeats until the goal is reachable or there are no more bridges to unlock.
    ///
    /// This method is a no-op when the board has no goal cell (e.g. level 11 battle
    /// royale) or when the goal is already reachable without any bridge repairs.
    /// </summary>
    public static void EnsureBridgeMaterials(Board board, Random rng)
    {
        if (!HasGoal(board)) return;

        var unlockedBridges = new HashSet<(int x, int y)>();

        while (true)
        {
            var (reachable, _) = BfsReachable(board, board.StartX, board.StartY, unlockedBridges);

            if (reachable.Any(pos => board.GetCell(pos.x, pos.y) is GoalCell))
                return;

            var nextBridge = FindFrontierBridge(board, reachable, unlockedBridges);
            if (nextBridge is null)
                return;

            PlaceMaterialPair(board, reachable, board.StartX, board.StartY, rng);
            unlockedBridges.Add(nextBridge.Value);
        }
    }

    /// <summary>
    /// BFS from (startX, startY) on <paramref name="board"/>.
    /// Treats <see cref="BrokenBridgeCell"/>s as passable only when they appear in
    /// <paramref name="unlockedBridges"/>.
    /// Treats <see cref="LowObstacleCell"/>s as passable (reachable via the crawl action).
    /// Includes jump moves over <see cref="HoleCell"/>s.
    /// Returns the set of reachable (x, y) positions.
    /// </summary>
    private static (HashSet<(int x, int y)>, Dictionary<(int x, int y), int>) BfsReachable(
        Board board, int startX, int startY,
        HashSet<(int x, int y)> unlockedBridges)
    {
        var visited = new HashSet<(int x, int y)>();
        var queue = new Queue<(int x, int y)>();

        var depthMap = new Dictionary<(int x, int y), int>();
        depthMap.Add((startX, startY), 0);
        visited.Add((startX, startY));
        queue.Enqueue((startX, startY));


        int[] dx = { 1, -1, 0, 0 };
        int[] dy = { 0, 0, -1, 1 };

        while (queue.Count > 0)
        {
            var (cx, cy) = queue.Dequeue();

            var depth = depthMap[(cx, cy)];

            for (int d = 0; d < 4; d++)
            {
                int nx = cx + dx[d];
                int ny = cy + dy[d];
                if (!board.InBounds(nx, ny)) continue;

                var neighbour = board.GetCell(nx, ny);

                // Normal walk, or crawl through a low obstacle, or cross an unlocked bridge
                if ((neighbour.IsWalkable
                     || neighbour is LowObstacleCell
                     || (neighbour is BrokenBridgeCell && unlockedBridges.Contains((nx, ny))))
                    && visited.Add((nx, ny)))
                {
                    if (depthMap.ContainsKey((nx, ny))) {
                        var prevDepth = depthMap[(nx, ny)];
                        if (depth + 1 < prevDepth)
                        {
                            depthMap[(nx, ny)] = depth + 1;
                        }
                    }
                    else
                    {
                        depthMap[(nx, ny)] = depth + 1;
                    }
                    queue.Enqueue((nx, ny));
                }

                // Jump over a hole: mid cell must be HoleCell, landing cell must be passable
                if (neighbour is HoleCell)
                {
                    int lx = cx + dx[d] * 2;
                    int ly = cy + dy[d] * 2;
                    if (board.InBounds(lx, ly))
                    {
                        var landing = board.GetCell(lx, ly);
                        if ((landing.IsWalkable || landing is LowObstacleCell) && visited.Add((lx, ly)))
                        {
                            if (depthMap.ContainsKey((lx, ly))) {
                                var prevDepth = depthMap[(lx, ly)];
                                if (depth + 1 < prevDepth)
                                {
                                    depthMap[(lx, ly)] = depth + 1;
                                }
                            }
                            else
                            {
                                depthMap[(lx, ly)] = depth + 1;
                            }
                            queue.Enqueue((lx, ly));
                        }
                    }
                }
            }
        }

        return (visited, depthMap);
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
