// Types mirroring C# DTOs from the game server

export type ItemType = 'mushroom' | 'nail' | 'plank' | 'health' | 'shield' | 'poison_mushroom';
export type EntityType = 'player' | 'enemy';
export type CellType = 'floor' | 'hole' | 'broken_bridge' | 'bridge' | 'low_obstacle' | 'wall' | 'goal' | 'teleporter' | 'warning';

export interface CellDto {
  x: number;
  y: number;
  cellType: CellType;
  items: ItemType[];
  entity?: { entityType: EntityType; id: string; health: number } | null;
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
  hasReachedGoal: boolean;
  roundScore: number;
  backpack: ItemType[];
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
  levelName: string;
  scores: Array<{ playerId: string; teamName: string; score: number }>;
  cumulative: Array<{ team: string; score: number }>;
}

export interface Leaderboard {
  cumulative: ScoreEntry[];
  rounds: Array<{
    roundId: string;
    level: number;
    levelName: string;
    startedAt: string;
    endedAt: string;
    scores: Array<{ playerId: string; teamName: string; score: number }>;
  }>;
  currentRound?: RoundInfo | null;
}
