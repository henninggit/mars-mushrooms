import type { BoardStateDto } from '../types/game';
import Cell from './Cell';
import CellEffectsCanvas from './CellEffectsCanvas';
import EntityCanvas from './EntityCanvas';

interface BoardGridProps {
  board: BoardStateDto;
}

export default function BoardGrid({ board }: BoardGridProps) {
  const { visibleCells, boardWidth, boardHeight } = board;

  // Build a lookup map: "x,y" -> CellDto
  const cellMap = new Map(visibleCells.map(c => [`${c.x},${c.y}`, c]));

  const rows = Array.from({ length: boardHeight }, (_, row) =>
    Array.from({ length: boardWidth }, (_, col) => ({ col, row }))
  );

  return (
    <div className="flex flex-col gap-0">
      <div className="flex items-center gap-2 mb-1">
        <span className="font-bold text-orange-200 text-sm">
          👨‍🚀 {board.teamName}
        </span>
        <span className="text-orange-300 text-xs">Level {board.level} — {board.levelName}</span>
        <span className="text-orange-300 text-xs">🍄 {board.mushroomsCollected}</span>
        <span className="text-orange-300 text-xs">
          ❤️ {Array.from({ length: board.maxHealth }, (_, i) => (
            <span key={i}>{i < board.health ? '♥' : '♡'}</span>
          ))}
          {board.shieldHealth > 0 && (
            <span className="text-blue-300 ml-1">🛡️×{board.shieldHealth}</span>
          )}
        </span>
        {board.isCrawling && <span className="text-yellow-300 text-xs">🐛 crawling</span>}
        <span className="text-gray-400 text-xs">
          🎒 [{board.backpack.join(', ')}]
        </span>
      </div>
      <div className="relative inline-flex flex-col border border-orange-800">
        <CellEffectsCanvas board={board} />
        <EntityCanvas board={board} />
        {rows.map((row, ri) => (
          <div key={ri} className="flex">
            {row.map(({ col, row: rowIdx }) => {
              const cell = cellMap.get(`${col},${rowIdx}`);
              if (!cell) {
                // Fog of war
                return (
                  <div
                    key={col}
                    className="w-8 h-8 bg-gray-800 border border-gray-900/50"
                  />
                );
              }
              return <Cell key={col} cell={cell} hideEntity />;
            })}
          </div>
        ))}
      </div>
    </div>
  );
}
