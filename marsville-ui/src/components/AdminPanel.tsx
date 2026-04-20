import { useState } from "react";

const API_BASE = "/api/admin";

const LEVEL_NAMES: Record<number, string> = {
  1: "Dust & Boots",
  2: "Mind the Gap",
  3: "Duck and Dash",
  4: "Bridge Builders",
  5: "Blind Repair",
  6: "Labyrinth of Dust",
  7: "Spore Highway",
  8: "Hostile Corridors",
  9: "Lost Caves of Mars",
  10: "Warp Nexus",
  11: "Colony Convergence",
  12: "Last Spore Standing",
};

interface AdminPanelProps {
  onRoundStarted?: () => void;
  onLeaderboardReset?: () => void;
}

export default function AdminPanel({ onRoundStarted, onLeaderboardReset }: AdminPanelProps) {
  const [password, setPassword] = useState("");
  const [level, setLevel] = useState(1);
  const [timeout, setTimeout_] = useState(300);
  const [seed, setSeed] = useState("");
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const headers = () => ({
    "Content-Type": "application/json",
    "X-Admin-Password": password,
  });

  async function createRound() {
    setMessage(null);
    setError(null);
    const body: Record<string, unknown> = { level, timeoutSeconds: timeout };
    if (seed.trim()) body.seed = parseInt(seed, 10);
    const res = await fetch(`${API_BASE}/rounds/create`, {
      method: "POST",
      headers: headers(),
      body: JSON.stringify(body),
    });
    const data = await res.json();
    if (res.ok)
      setMessage(
        `Round created: ID ${data.roundId} — Level ${data.level}, Seed ${data.seed}`,
      );
    else setError(data.error ?? "Failed to create round");
  }

  async function startRound() {
    setMessage(null);
    setError(null);
    const res = await fetch(`${API_BASE}/rounds/start`, {
      method: "POST",
      headers: headers(),
    });
    const data = await res.json();
    if (res.ok) {
      setMessage(data.message);
      onRoundStarted?.();
    } else setError(data.error ?? "Failed to start round");
  }

  async function endRound() {
    setMessage(null);
    setError(null);
    const res = await fetch(`${API_BASE}/rounds/end`, {
      method: "POST",
      headers: headers(),
    });
    const data = await res.json();
    if (res.ok) setMessage(data.message);
    else setError(data.error ?? "Failed to end round");
  }

  async function resetLeaderboard() {
    if (!window.confirm("Reset the leaderboard? This will zero all scores and clear round history."))
      return;
    setMessage(null);
    setError(null);
    const res = await fetch(`${API_BASE}/leaderboard/reset`, {
      method: "POST",
      headers: headers(),
    });
    const data = await res.json();
    if (res.ok) {
      setMessage(data.message);
      onLeaderboardReset?.();
    }
    else setError(data.error ?? "Failed to reset leaderboard");
  }

  return (
    <div className="bg-stone-900 border border-orange-900 rounded-lg p-4 space-y-3 min-w-[280px]">
      <h2 className="text-orange-200 font-bold text-lg">🛸 Admin Panel</h2>

      <label className="block text-sm text-orange-300">
        Admin password
        <input
          type="password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          className="mt-1 w-full bg-stone-800 border border-orange-800 rounded px-2 py-1 text-orange-100 text-sm"
          placeholder="Enter admin password"
        />
      </label>

      <div className="flex gap-2">
        <label className="flex-1 block text-sm text-orange-300">
          Level (1-12)
          <input
            type="number"
            min={1}
            value={level}
            onChange={(e) => setLevel(parseInt(e.target.value, 10) || 1)}
            className="mt-1 w-full bg-stone-800 border border-orange-800 rounded px-2 py-1 text-orange-100 text-sm"
          />
          {LEVEL_NAMES[level] && (
            <span className="block mt-0.5 text-xs text-orange-400 italic">
              {LEVEL_NAMES[level]}
            </span>
          )}
        </label>
        <label className="flex-1 block text-sm text-orange-300">
          Timeout (s)
          <input
            type="number"
            min={30}
            value={timeout}
            onChange={(e) => setTimeout_(parseInt(e.target.value, 10) || 300)}
            className="mt-1 w-full bg-stone-800 border border-orange-800 rounded px-2 py-1 text-orange-100 text-sm"
          />
        </label>
      </div>

      <label className="block text-sm text-orange-300">
        Seed (optional)
        <input
          type="text"
          value={seed}
          onChange={(e) => setSeed(e.target.value)}
          className="mt-1 w-full bg-stone-800 border border-orange-800 rounded px-2 py-1 text-orange-100 text-sm"
          placeholder="Leave blank for random"
        />
      </label>

      <div className="flex gap-2 flex-wrap">
        <button
          onClick={createRound}
          className="bg-orange-700 hover:bg-orange-600 text-white text-sm px-3 py-1 rounded"
        >
          Create Round
        </button>
        <button
          onClick={startRound}
          className="bg-green-700 hover:bg-green-600 text-white text-sm px-3 py-1 rounded"
        >
          Start Round
        </button>
        <button
          onClick={endRound}
          className="bg-red-800 hover:bg-red-700 text-white text-sm px-3 py-1 rounded"
        >
          Force End
        </button>
      </div>

      <div className="border-t border-orange-900 pt-3">
        <button
          onClick={resetLeaderboard}
          className="bg-stone-700 hover:bg-stone-600 text-orange-300 hover:text-orange-100 text-sm px-3 py-1 rounded border border-orange-900"
        >
          Reset Leaderboard
        </button>
      </div>

      {message && <p className="text-green-400 text-sm">✓ {message}</p>}
      {error && <p className="text-red-400 text-sm">✗ {error}</p>}
    </div>
  );
}
