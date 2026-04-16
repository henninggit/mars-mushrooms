// Types mirroring C# DTOs from the game server

export interface CellDto {
  x: number;
  y: number;
  cellType: 'floor' | 'hole' | 'broken_bridge' | 'bridge' | 'low_obstacle' | 'wall' | 'goal' | 'teleporter';
  items: string[];
  entity?: { entityType: 'player' | 'enemy'; id: string; health: number } | null;
}

export interface BoardStateDto {
  playerId: string;
  teamName: string;
  x: number;
  y: number;
  health: number;
  maxHealth: number;
  shieldHealth: number;
  isCrawling: boolean;
  mushroomsCollected: number;
  backpack: string[];
  visibleCells: CellDto[];
  boardWidth: number;
  boardHeight: number;
  level: number;
  levelName: string;
}

export interface RoundInfo {
  roundId: string;
  level: number;
  levelName: string;
  seed: number;
  timeoutSeconds: number;
  phase: 'Registration' | 'Playing' | 'Ended';
}

export interface ScoreEntry {
  team: string;
  score: number;
}

export interface RoundScores {
  roundId: string;
  level: number;
  scores: Record<string, number>;
  cumulative: Record<string, number>;
}

export interface Leaderboard {
  cumulative: ScoreEntry[];
  rounds: Array<{
    roundId: string;
    level: number;
    startedAt: string;
    endedAt: string;
    scores: Record<string, number>;
  }>;
  currentRound?: RoundInfo | null;
}
