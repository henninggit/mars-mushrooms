import type { CellDto } from '../types/game';

interface CellProps {
  cell: CellDto;
  isPlayerPos?: boolean;
  hideEntity?: boolean;
}

const CELL_ICONS: Record<string, string> = {
  floor: '',
  hole: '',
  broken_bridge: '🔨',
  bridge: '🌉',
  low_obstacle: '🪨',
  wall: '',
  goal: '🚀',
  teleporter: '🌀',
  warning: '⚠️',
};

const CELL_BG: Record<string, string> = {
  floor: 'bg-orange-300',
  hole: 'bg-gray-900',
  broken_bridge: 'bg-amber-700',
  bridge: 'bg-amber-400',
  low_obstacle: 'bg-stone-500',
  wall: 'bg-gray-700',
  goal: 'bg-green-400',
  teleporter: 'bg-purple-500',
  warning: 'bg-red-500',
};

const ITEM_ICONS: Record<string, string> = {
  mushroom: '🍄',
  plank: '🪵',
  nail: '📌',
  health: '❤️‍🩹',
  shield: '🛡️',
  poison_mushroom: '☠️',
};

export default function Cell({ cell, isPlayerPos, hideEntity }: CellProps) {
  const bg = CELL_BG[cell.cellType] ?? 'bg-orange-300';
  const topIcon = CELL_ICONS[cell.cellType];

  const entityIcon = !hideEntity && (cell.entity
    ? cell.entity.entityType === 'enemy' ? '👾' : '👨‍🚀'
    : isPlayerPos ? '👨‍🚀' : null);

  return (
    <div
      className={`relative w-8 h-8 border border-orange-900/30 flex items-center justify-center text-xs ${bg}`}
      title={`(${cell.x},${cell.y}) ${cell.cellType}${cell.items.length ? ' [' + cell.items.join(',') + ']' : ''}`}
    >
      {entityIcon ? (
        <span className="z-10 text-base leading-none">{entityIcon}</span>
      ) : (
        <>
          {topIcon && <span className="leading-none">{topIcon}</span>}
          {cell.items.map((item, i) => (
            <span key={i} className="absolute bottom-0 right-0 text-[9px] leading-none">
              {ITEM_ICONS[item] ?? '?'}
            </span>
          ))}
        </>
      )}
    </div>
  );
}
