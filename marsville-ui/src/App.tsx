import { useCallback, useEffect, useState } from "react";
import "./App.css";
import AllBoards from "./components/AllBoards";
import AdminPanel from "./components/AdminPanel";
import Scoreboard from "./components/Scoreboard";
import { useGameHub } from "./hooks/useGameHub";
import type { BoardStateDto, RoundInfo, ScoreEntry } from "./types/game";

function App() {
  const [boards, setBoards] = useState<Map<string, BoardStateDto>>(new Map());
  const [scores, setScores] = useState<ScoreEntry[]>([]);
  const [currentRound, setCurrentRound] = useState<RoundInfo | null>(null);
  const [showAdmin, setShowAdmin] = useState(false);

  const { connected } = useGameHub({
    onBoardUpdated: useCallback((playerId: string, state: BoardStateDto) => {
      setBoards((prev) => new Map(prev).set(playerId, state));
    }, []),
    onAllBoardsSnapshot: useCallback((allBoards: BoardStateDto[]) => {
      setBoards(new Map(allBoards.map((b) => [b.playerId, b])));
    }, []),
    onRoundStarted: useCallback((round: RoundInfo) => {
      setCurrentRound(round);
      setBoards(new Map());
    }, []),
    onRoundEnded: useCallback(
      (data: { cumulative: Record<string, number> }) => {
        setScores(
          Object.entries(data.cumulative)
            .map(([team, score]) => ({ team, score }))
            .sort((a, b) => b.score - a.score),
        );
      },
      [],
    ),
  });

  // Fetch initial leaderboard on mount
  useEffect(() => {
    fetch("/api/game/rounds")
      .then((r) => r.json())
      .then((data) => {
        if (data.cumulative) setScores(data.cumulative);
        if (data.currentRound) setCurrentRound(data.currentRound);
      })
      .catch(() => {
        /* server may not be running yet */
      });
  }, []);

  const boardList = Array.from(boards.values());

  return (
    <div className="min-h-screen min-w-screen bg-stone-950 text-orange-100 flex flex-col">
      {/* Header */}
      <header className="flex items-center justify-between px-6 py-3 bg-stone-900 border-b border-orange-900">
        <div className="flex items-center gap-3">
          <span className="text-2xl">&#x1F344;</span>
          <h1 className="text-xl font-bold text-orange-300">
            Marsville &mdash; Mushrooms on Mars
          </h1>
          {currentRound && (
            <span className="text-sm text-orange-400 ml-2">
              Level {currentRound.level} &mdash; {currentRound.levelName}{" "}
              &middot; {currentRound.phase}
            </span>
          )}
        </div>
        <div className="flex items-center gap-3">
          <span
            className={`text-xs px-2 py-0.5 rounded-full ${connected ? "bg-green-900 text-green-300" : "bg-red-900 text-red-300"}`}
          >
            {connected ? "&#x25CF; Live" : "&#x25CB; Disconnected"}
          </span>
          <button
            onClick={() => setShowAdmin((v) => !v)}
            className="text-xs bg-stone-800 hover:bg-stone-700 border border-orange-800 text-orange-300 px-3 py-1 rounded"
          >
            {showAdmin ? "Hide Admin" : "&#x1F6F8; Admin"}
          </button>
        </div>
      </header>

      <div className="flex flex-1 overflow-hidden">
        {/* Main board area */}
        <main className="flex-1 overflow-auto flex flex-col">
          {currentRound && (
            <div className="px-6 pt-4 pb-2">
              <div className="flex items-baseline gap-3">
                <span className="text-3xl font-bold text-orange-200 tracking-tight">
                  {currentRound.levelName}
                </span>
                <span className="text-orange-500 text-sm font-medium">
                  Level {currentRound.level}
                </span>
              </div>
            </div>
          )}
          <AllBoards boards={boardList} />
        </main>

        {/* Sidebar */}
        <aside className="w-72 flex-shrink-0 overflow-auto p-4 space-y-4 border-l border-orange-900 bg-stone-900">
          {showAdmin && (
            <AdminPanel onRoundStarted={() => setBoards(new Map())} />
          )}
          <Scoreboard scores={scores} />

          {/* Level legend */}
          <div className="bg-stone-800 rounded-lg p-3 border border-orange-900">
            <h3 className="text-orange-300 font-semibold text-sm mb-2">
              Cell Legend
            </h3>
            <ul className="space-y-1 text-xs text-orange-200">
              <li>
                <span className="inline-block w-4 h-4 bg-orange-300 rounded mr-1" />{" "}
                Floor
              </li>
              <li>
                <span className="inline-block w-4 h-4 bg-gray-900 rounded mr-1" />{" "}
                Hole
              </li>
              <li>
                <span className="inline-block w-4 h-4 bg-amber-700 rounded mr-1" />{" "}
                Broken bridge
              </li>
              <li>
                <span className="inline-block w-4 h-4 bg-amber-400 rounded mr-1" />{" "}
                Bridge
              </li>
              <li>
                <span className="inline-block w-4 h-4 bg-stone-500 rounded mr-1" />{" "}
                Low obstacle
              </li>
              <li>
                <span className="inline-block w-4 h-4 bg-green-400 rounded mr-1" />{" "}
                Goal &#x1F680;
              </li>
              <li>
                <span className="inline-block w-4 h-4 bg-gray-700 rounded mr-1" />{" "}
                Wall
              </li>
              <li>
                <span className="inline-block w-4 h-4 bg-gray-800 rounded mr-1" />{" "}
                Fog of war
              </li>
            </ul>
          </div>
        </aside>
      </div>
    </div>
  );
}

export default App;
