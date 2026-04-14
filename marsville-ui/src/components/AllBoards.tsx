import type { BoardStateDto } from '../types/game';
import BoardGrid from './BoardGrid';

interface AllBoardsProps {
  boards: BoardStateDto[];
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
          <BoardGrid board={board} />
        </div>
      ))}
    </div>
  );
}
