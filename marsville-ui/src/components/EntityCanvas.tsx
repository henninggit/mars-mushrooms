import { useEffect, useRef } from "react";
import type { BoardStateDto } from "../types/game";

const CELL_SIZE = 36;
// Per-frame lerp factor — at 60fps this settles visually in ~150ms
const LERP_FACTOR = 0.18;
// Multiplicative decay per frame for the red damage flash
const FLASH_DECAY = 0.9;

interface EntityRenderState {
  lerpX: number;
  lerpY: number;
  targetX: number;
  targetY: number;
  flashAlpha: number;
  prevHealth: number;
  icon: string;
}

interface EntityCanvasProps {
  board: BoardStateDto;
}

export default function EntityCanvas({ board }: EntityCanvasProps) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const stateRef = useRef<Map<string, EntityRenderState>>(new Map());
  const rafRef = useRef<number>(0);
  const boardRef = useRef(board);

  // Sync incoming board state into render targets (no re-render needed)
  useEffect(() => {
    boardRef.current = board;

    const entities = [
      {
        id: `player:${board.playerId}`,
        x: board.x,
        y: board.y,
        health: board.health,
        icon: "👨‍🚀",
      },
      ...board.visibleCells
        .filter((c) => c.entity?.entityType === "enemy")
        .map((c) => ({
          id: `enemy:${c.entity!.id}`,
          x: c.x,
          y: c.y,
          health: c.entity!.health,
          icon: "👾",
        })),
    ];

    const prev = stateRef.current;
    const next = new Map<string, EntityRenderState>();

    for (const e of entities) {
      const existing = prev.get(e.id);
      if (existing) {
        next.set(e.id, {
          ...existing,
          targetX: e.x,
          targetY: e.y,
          // Trigger full flash if health dropped
          flashAlpha:
            e.health < existing.prevHealth ? 1.0 : existing.flashAlpha,
          prevHealth: e.health,
          icon: e.icon,
        });
      } else {
        // New entity: place directly at target (no lerp pop-in)
        next.set(e.id, {
          lerpX: e.x,
          lerpY: e.y,
          targetX: e.x,
          targetY: e.y,
          flashAlpha: 0,
          prevHealth: e.health,
          icon: e.icon,
        });
      }
    }

    stateRef.current = next;
  }, [board]);

  // Single rAF loop — starts once, never restarts
  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext("2d")!;

    let lastW = 0;
    let lastH = 0;

    const draw = () => {
      const b = boardRef.current;
      const w = b.boardWidth * CELL_SIZE;
      const h = b.boardHeight * CELL_SIZE;

      // Resize only when board dimensions change (resizing clears the canvas)
      if (w !== lastW || h !== lastH) {
        canvas.width = w;
        canvas.height = h;
        lastW = w;
        lastH = h;
      }

      ctx.clearRect(0, 0, w, h);
      ctx.font = `${CELL_SIZE - 8}px sans-serif`;
      ctx.textAlign = "center";
      ctx.textBaseline = "middle";

      for (const state of stateRef.current.values()) {
        // Lerp position toward target
        state.lerpX += (state.targetX - state.lerpX) * LERP_FACTOR;
        state.lerpY += (state.targetY - state.lerpY) * LERP_FACTOR;

        const cx = state.lerpX * CELL_SIZE + CELL_SIZE / 2;
        const cy = state.lerpY * CELL_SIZE + CELL_SIZE / 2;

        // Damage flash: red rect behind the sprite with decaying alpha
        if (state.flashAlpha > 0.01) {
          ctx.fillStyle = `rgba(239, 68, 68, ${state.flashAlpha * 0.75})`;
          ctx.fillRect(
            state.lerpX * CELL_SIZE + 1,
            state.lerpY * CELL_SIZE + 1,
            CELL_SIZE - 2,
            CELL_SIZE - 2,
          );
          state.flashAlpha *= FLASH_DECAY;
        } else {
          state.flashAlpha = 0;
        }

        ctx.fillText(state.icon, cx, cy);
      }

      rafRef.current = requestAnimationFrame(draw);
    };

    rafRef.current = requestAnimationFrame(draw);
    return () => cancelAnimationFrame(rafRef.current);
  }, []);

  return (
    <canvas
      ref={canvasRef}
      className="absolute inset-0 pointer-events-none z-10"
    />
  );
}
