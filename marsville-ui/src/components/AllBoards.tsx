import type { BoardStateDto } from '../types/game';
import BoardGrid from './BoardGrid';

interface AllBoardsProps {
  boards: BoardStateDto[];
}

function PlayerStatusBadge({ board }: { board: BoardStateDto }) {
  if (board.hasReachedGoal) {
    return <span className="text-green-400 font-bold">🏁 Done</span>;
  }
  if (board.health <= 0) {
    return <span className="text-red-400 font-bold">💀 Eliminated</span>;
  }
  return <span className="text-orange-300">🏃 Playing</span>;
}

export default function AllBoards({ boards }: AllBoardsProps) {
  if (boards.length === 0) {
    return (
      <div className="flex items-center justify-center h-64 text-orange-300 text-lg">
        🌕 Waiting for the round to start…
      </div>
    );
  }

  return (
    <div className="flex flex-wrap gap-6 p-4">
      {boards.map(board => (
        <div key={board.playerId} className="bg-stone-900 rounded-lg p-3 shadow-lg border border-orange-900">
          <div className="flex items-center justify-between mb-2 gap-3">
            <span className="text-orange-200 font-semibold truncate max-w-[120px]" title={board.teamName}>
              {board.teamName}
            </span>
            <div className="flex items-center gap-2 text-xs">
              <PlayerStatusBadge board={board} />
              <span className={`font-mono font-bold ${board.roundScore >= 0 ? 'text-yellow-400' : 'text-red-400'}`}>
                {board.roundScore >= 0 ? '+' : ''}{board.roundScore}
              </span>
            </div>
          </div>
          <BoardGrid board={board} />
        </div>
      ))}
    </div>
  );
}
