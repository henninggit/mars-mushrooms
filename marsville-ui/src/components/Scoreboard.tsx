import type { ScoreEntry } from '../types/game';

interface ScoreboardProps {
  scores: ScoreEntry[];
  title?: string;
}

export default function Scoreboard({ scores, title = '🏆 Leaderboard' }: ScoreboardProps) {
  return (
    <div className="bg-stone-900 rounded-lg p-4 border border-orange-900 min-w-[220px]">
      <h2 className="text-orange-200 font-bold text-lg mb-3">{title}</h2>
      {scores.length === 0 ? (
        <p className="text-gray-400 text-sm">No scores yet.</p>
      ) : (
        <ol className="space-y-1">
          {scores.map((entry, idx) => (
            <li key={entry.team} className="flex items-center gap-2">
              <span className="text-orange-400 font-mono w-5 text-right">{idx + 1}.</span>
              <span className="text-orange-100 flex-1 truncate">{entry.team}</span>
              <span className="text-green-400 font-bold font-mono">{entry.score}</span>
            </li>
          ))}
        </ol>
      )}
    </div>
  );
}
