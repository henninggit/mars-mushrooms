# GitHub Copilot Instructions — mars-mushrooms (Marsville)

## Project Overview

**Marsville: Mushrooms on Mars** is a hackathon game platform. Competing teams write AI agents that play a turn-based grid game across 10 levels, collecting mushrooms and reaching goal cells. The repo has three projects:

| Project           | Language / Stack                              | Purpose                        |
| ----------------- | --------------------------------------------- | ------------------------------ |
| `Marsville2/`     | C# / ASP.NET Core 10 Minimal API              | Game server (backend)          |
| `MarsvilleAgent/` | C# / .NET 10 console                          | Reference heuristic game agent |
| `marsville-ui/`   | TypeScript / React 19 / Vite / Tailwind CSS 4 | Spectator & admin web UI       |

The backend holds all state **in memory** (no database). There are no automated tests — this is intentional for a hackathon prototype.

---

## Architecture

```
marsville-ui (React SPA)
  └─ HTTP REST  ──►  Marsville2 /api/*
  └─ SignalR WS ──►  Marsville2 /hubs/game
MarsvilleAgent (console agent)
  └─ HTTP REST  ──►  Marsville2 /api/*
```

The backend is structured as:

- **`GameSession`** (singleton) — player registry, current round, cumulative scores
- **`GameService`** (singleton) — orchestrates rounds, calls `BoardService`, broadcasts SignalR events
- **`BoardService`** (one per active board) — executes player actions, calls `EnemyAiService` after each action
- **`EnemyAiService`** — Manhattan-distance heuristic; moves enemies after each player turn
- **`LevelFactory`** — static, deterministic board generation (same seed = same board)

---

## Domain Concepts

### Game Flow

1. Admin creates a round (`POST /api/admin/rounds/create`) → `Registration` phase
2. Teams register agents (`POST /api/players/register`) → receive `token`
3. Admin starts the round (`POST /api/admin/rounds/start`) → `Playing` phase
4. Agents poll state (`GET /api/game/state`) and POST actions
5. Round ends automatically on timeout or when all players finish, or admin force-ends it

### Round Phases: `Registration` → `Playing` → `Ended`

### Board Architecture

- Flat `CellBase[]` array indexed by `y * Width + x`
- Levels 1–8: each player gets their own `Board` instance
- Levels 9–10: all players share a single `Board`

### Cell Types

| `cellType` | Walkable | Jumpable (over it) | Crawlable |
|------------|----------|--------------------|---------\_\_|
| `floor` | ✅ | ❌ | ✅ |
| `hole` | ❌ | ✅ | ❌ |
| `wall` | ❌ | ❌ | ❌ |
| `broken_bridge` | ❌ | ❌ | ❌ |
| `bridge` | ✅ | ❌ | ✅ |
| `low_obstacle` | ❌ | ❌ | ✅ |
| `goal` | ✅ | ❌ | ✅ |

### Player Actions

All directional actions take `{ "direction": 0-3 }` in the request body.
Direction encoding: **0=East, 1=West, 2=North, 3=South**

| Action   | Endpoint                | Description                                            |
| -------- | ----------------------- | ------------------------------------------------------ |
| `move`   | `POST /api/game/move`   | Walk one cell                                          |
| `jump`   | `POST /api/game/jump`   | Jump 2 cells over a `hole`                             |
| `crawl`  | `POST /api/game/crawl`  | Enter/traverse `low_obstacle`                          |
| `pickup` | `POST /api/game/pickup` | Take item from current cell (no direction)             |
| `build`  | `POST /api/game/build`  | Repair adjacent `broken_bridge` using 1 Plank + 1 Nail |
| `attack` | `POST /api/game/attack` | Deal 1 damage to adjacent enemy                        |
| `wait`   | `POST /api/game/wait`   | Skip turn (enemies still move)                         |

### Items

- **`mushroom`** — auto-collected on step; worth 1 point each
- **`plank`** — required for `build` (with nail); pick up manually with `pickup`
- **`nail`** — required for `build` (with plank); pick up manually with `pickup`

### Backpack

- Capacity: 10 items
- Mushrooms are **not** stored in backpack — they are collected directly

### Scoring

- Each mushroom = 1 point
- Reaching goal (levels 3+) = 1 bonus point
- Level 10 battle royale: last survivor +4, 2nd-to-last +1; border shrinks every 30s and on player death

### Authentication

- **Players**: `X-Player-Token: <token>` header (from `/api/players/register`)
- **Admin**: `X-Admin-Password: <password>` header (from `appsettings.json`)

---

## SignalR Events (server → client)

| Event               | Payload                  | When                                          |
| ------------------- | ------------------------ | --------------------------------------------- |
| `BoardUpdated`      | `(playerId, boardState)` | After each player action                      |
| `AllBoardsSnapshot` | `(boardState[])`         | Broadcast to all spectators after each action |
| `RoundStarted`      | `roundInfo`              | When admin starts the round                   |
| `RoundEnded`        | `scores`                 | When round ends                               |

Board state DTO shape (TypeScript interface in `marsville-ui/src/types/game.ts`):

```typescript
interface BoardStateDto {
  playerId: string;
  teamName: string;
  x: number;
  y: number;
  health: number;
  maxHealth: number;
  isCrawling: boolean;
  mushroomsCollected: number;
  backpack: string[];
  visibleCells: CellDto[];
  boardWidth: number;
  boardHeight: number;
  level: number;
}
```

---

## Coding Conventions

### C# Backend (`Marsville2/`)

- **Minimal API** pattern throughout — no controllers. Endpoints live in static extension methods in `Endpoints/`.
- **Singleton in-memory state** — `GameSession` and `GameService` are singletons; all game state lives in these objects.
- **Thread safety** — `GameService` uses `_globalLock` for all global mutations; `BoardService` uses a per-board `_lock`. Always lock before mutating shared state.
- **Domain-driven naming** — classes named after game concepts (`Board`, `Player`, `Enemy`, `HoleCell`, `GoalCell`, etc.).
- **OpenAPI on every endpoint** — always add `.WithName("...")` and `.WithOpenApi(op => { op.Summary = ...; op.Description = ...; return op; })` to new endpoints.
- **`ActionResult` enum** — all `BoardService` methods return `ActionResult`; map to HTTP results in `GamePlayingEndpoints.MapResult()`.
- **Flat cell indexing** — cell arrays always use `_cells[y * Width + x]` (never 2D arrays at runtime).
- **Deterministic levels** — use `LevelFactory.SeededOffset(seed, level, min, max)` for any randomness inside `LevelFactory`. Never use `Random.Shared` inside level builders.
- **XML doc comments** on all public domain classes and service methods.
- **Nullable reference types** enabled — use `?` appropriately; avoid `!` (null-forgiving) unless genuinely safe.
- **Target framework**: `net10.0`

### TypeScript Frontend (`marsville-ui/`)

- **React 19** with functional components and hooks only — no class components.
- **Tailwind CSS 4** for all styling — no CSS modules or inline styles unless unavoidable.
- **Types in `src/types/game.ts`** — all DTOs that mirror backend types live here. Keep them in sync when backend changes.
- **SignalR in `useGameHub` hook** — do not create `HubConnection` outside this hook.
- **`useCallback`** for all SignalR event handler props to avoid stale closure re-subscriptions.
- **Vite dev proxy** — the UI is expected to proxy `/api/*` and `/hubs/*` to the backend at `localhost:5181` in development. The `vite.config.ts` should have a proxy config (or the backend CORS allows the dev origin).
- **No state management library** — use `useState` / `useReducer`; the app is small enough.

---

## Project Structure

```
mars-mushrooms/
├── Marsville2/                    # ASP.NET Core backend
│   ├── Program.cs                 # DI, middleware, route registration
│   ├── appsettings.json           # AdminPassword, CorsOrigins
│   ├── Domain/
│   │   ├── Board.cs               # Grid, vision, combat helpers
│   │   ├── GameRound.cs           # Round lifecycle & phase
│   │   ├── GameSession.cs         # Top-level singleton state
│   │   ├── LevelFactory.cs        # Deterministic board generation (all 10 levels)
│   │   ├── Cells/                 # ICell, CellBase, + one class per cell type
│   │   ├── Entities/              # IEntity, EntityBase, Player, Enemy, Backpack
│   │   └── Items/                 # IItem, Mushroom, Plank, Nail
│   ├── Services/
│   │   ├── GameService.cs         # Main orchestrator (singleton)
│   │   ├── BoardService.cs        # Per-board action execution
│   │   └── EnemyAiService.cs      # Enemy movement AI
│   ├── Endpoints/
│   │   ├── PlayerEndpoints.cs     # /api/players/*
│   │   ├── GamePlayingEndpoints.cs# /api/game/*
│   │   └── AdminEndpoints.cs      # /api/admin/*
│   └── Hubs/
│       └── GameHub.cs             # SignalR hub at /hubs/game
├── MarsvilleAgent/
│   └── Program.cs                 # All-in-one reference agent (greedy heuristic)
├── marsville-ui/
│   ├── src/
│   │   ├── App.tsx                # Root component, SignalR wiring
│   │   ├── components/            # AdminPanel, AllBoards, BoardGrid, Cell, Scoreboard
│   │   ├── hooks/useGameHub.ts    # SignalR hub React hook
│   │   └── types/game.ts          # TypeScript DTOs
│   ├── package.json
│   └── vite.config.ts
└── mars-mushrooms.sln
```

---

## How to Run

### Backend

```bash
cd Marsville2
dotnet run
# Listens on http://localhost:5181 by default
# OpenAPI at http://localhost:5181/openapi/v1.json (dev only)
```

### Frontend

```bash
cd marsville-ui
npm install
npm run dev
# Vite dev server at http://localhost:5173
```

### Reference Agent

```bash
cd MarsvilleAgent
dotnet run -- TeamName http://localhost:5181 1
# Args: [teamName] [serverUrl] [agentCount]
```

---

## Key Patterns to Follow When Adding Features

### Adding a new action

1. Add `ActionResult` variant in `BoardService.cs` if needed
2. Implement the action method in `BoardService` (lock, validate, mutate, call `EnemyAiService.MoveEnemies`)
3. Add the POST endpoint in `GamePlayingEndpoints.cs` with OpenAPI annotation
4. Add a case in `MapResult()` in `GamePlayingEndpoints.cs`
5. Handle in `GameService.PerformAction()` switch expression
6. Update `MarsvilleAgent/Program.cs` `ChooseAction()` if the agent should use it
7. Update the TypeScript types in `marsville-ui/src/types/game.ts` if the state DTO changes

### Adding a new level

1. Add a `CreateLevelN(int seed)` private method in `LevelFactory.cs`
2. Register it in the `CreateBoard()` switch expression
3. Use `SeededOffset(seed, level, min, max)` for any position randomness
4. Update the admin UI level max in `marsville-ui/src/components/AdminPanel.tsx`

### Adding a new cell type

1. Create `Domain/Cells/MyNewCell.cs` inheriting `CellBase`
2. Override all abstract properties (`IsWalkable`, `IsJumpable`, `IsCrawlable`, `CanPlaceItems`, `CellType`)
3. Handle movement logic in `BoardService` (or the existing cell property flags cover it automatically)
4. Add the type string to `CellDto.cellType` union in `marsville-ui/src/types/game.ts`
5. Add styling in `marsville-ui/src/components/Cell.tsx`

---

## Things to Avoid

- **Do not add a database** — this is intentional in-memory design for hackathon use
- **Do not use controllers** — this project uses Minimal API exclusively
- **Do not break deterministic seeding** in `LevelFactory` — same seed must always produce the same board
- **Do not expose the admin password** in client code or commit real passwords to source control
- **Do not bypass `_globalLock`** in `GameService` or per-board locks in `BoardService` — all state mutations must be synchronized
- **Do not store mushrooms in the backpack** — mushrooms are auto-collected and tracked separately via `Player.MushroomsCollected`

---

## Things to Do

- **Update copilot-instructions.md** whenever relevant
