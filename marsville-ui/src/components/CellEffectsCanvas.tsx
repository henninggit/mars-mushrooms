import { useEffect, useRef } from "react";
import type { BoardStateDto } from "../types/game";

const CELL_SIZE = 36;
const ALPHA_DECAY = 0.88; // per rAF frame

interface CellFlash {
  alpha: number;
  r: number;
  g: number;
  b: number;
}

interface CellEffectsCanvasProps {
  board: BoardStateDto;
}

export default function CellEffectsCanvas({ board }: CellEffectsCanvasProps) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const flashesRef = useRef<Map<string, CellFlash>>(new Map());
  const prevBoardRef = useRef<BoardStateDto | null>(null);
  const boardRef = useRef(board);
  const rafRef = useRef<number>(0);

  // Detect events by diffing against previous board state
  useEffect(() => {
    boardRef.current = board;
    const prev = prevBoardRef.current;

    if (prev) {
      const trigger = (key: string, r: number, g: number, b: number) => {
        flashesRef.current.set(key, { alpha: 0.65, r, g, b });
      };

      const prevCells = new Map(
        prev.visibleCells.map((c) => [`${c.x},${c.y}`, c]),
      );
      const currCells = new Map(
        board.visibleCells.map((c) => [`${c.x},${c.y}`, c]),
      );

      // Item picked up: items array shrank on a cell
      for (const [key, curr] of currCells) {
        const pCell = prevCells.get(key);
        if (pCell && pCell.items.length > curr.items.length) {
          trigger(key, 45, 250, 45); // soft gold
        }
      }

      const dx = board.x - prev.x;
      const dy = board.y - prev.y;

      // Jumped over: player moved exactly 2 cells — flash the skipped middle cell
      if (Math.abs(dx) === 2 && dy === 0) {
        trigger(`${prev.x + dx / 2},${prev.y}`, 96, 165, 250); // soft blue
      } else if (Math.abs(dy) === 2 && dx === 0) {
        trigger(`${prev.x},${prev.y + dy / 2}`, 96, 165, 250);
      }

      // Crawled under: moved 1 cell while crawling (either side of the transition)
      if (
        (board.isCrawling || prev.isCrawling) &&
        ((Math.abs(dx) === 1 && dy === 0) || (Math.abs(dy) === 1 && dx === 0))
      ) {
        trigger(`${board.x},${board.y}`, 52, 211, 153); // soft teal
      }
    }

    prevBoardRef.current = board;
  }, [board]);

  // Single rAF loop — decays flash alphas and redraws
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

      if (w !== lastW || h !== lastH) {
        canvas.width = w;
        canvas.height = h;
        lastW = w;
        lastH = h;
      }

      ctx.clearRect(0, 0, w, h);

      const toDelete: string[] = [];
      for (const [key, flash] of flashesRef.current) {
        if (flash.alpha < 0.01) {
          toDelete.push(key);
          continue;
        }
        const [x, y] = key.split(",").map(Number);
        ctx.fillStyle = `rgba(${flash.r}, ${flash.g}, ${flash.b}, ${flash.alpha})`;
        ctx.fillRect(x * CELL_SIZE, y * CELL_SIZE, CELL_SIZE, CELL_SIZE);
        flash.alpha *= ALPHA_DECAY;
      }
      for (const key of toDelete) flashesRef.current.delete(key);

      rafRef.current = requestAnimationFrame(draw);
    };

    rafRef.current = requestAnimationFrame(draw);
    return () => cancelAnimationFrame(rafRef.current);
  }, []);

  return (
    <canvas
      ref={canvasRef}
      className="absolute inset-0 pointer-events-none z-[5]"
    />
  );
}
