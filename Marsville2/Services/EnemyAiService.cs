using Marsville2.Domain;
using Marsville2.Domain.Cells;
using Marsville2.Domain.Entities;

namespace Marsville2.Services;

/// <summary>
/// Simple enemy AI: each enemy moves one step toward the nearest living player
/// using Manhattan-distance heuristic. Called after each player action (turn-based).
/// Enemies damage adjacent players.
/// </summary>
public class EnemyAiService
{
    private readonly Board _board;

    public EnemyAiService(Board board)
    {
        _board = board;
    }

    public void MoveEnemies(Player actingPlayer)
    {
        foreach (var enemy in _board.Enemies.ToList())
        {
            if (!enemy.IsAlive) continue;
            MoveEnemy(enemy);
            DamageNearbyPlayers(enemy);
        }
    }

    private void MoveEnemy(Enemy enemy)
    {
        var target = _board.Players
            .Where(p => p.IsAlive)
            .OrderBy(p => Math.Abs(p.X - enemy.X) + Math.Abs(p.Y - enemy.Y))
            .FirstOrDefault();

        if (target is null) return;

        // Pick the cardinal step that most reduces Manhattan distance and is walkable
        var candidates = new[]
        {
            (dx: 1, dy: 0), (dx: -1, dy: 0), (dx: 0, dy: -1), (dx: 0, dy: 1)
        };

        var best = candidates
            .Select(d => (dx: d.dx, dy: d.dy,
                nx: enemy.X + d.dx, ny: enemy.Y + d.dy))
            .Where(c => _board.InBounds(c.nx, c.ny))
            .Where(c => _board.GetCell(c.nx, c.ny).IsWalkable)
            .Where(c => _board.GetEntityAt(c.nx, c.ny) is null) // don't stack on players or other enemies
            .OrderBy(c => Math.Abs(c.nx - target.X) + Math.Abs(c.ny - target.Y))
            .FirstOrDefault();

        if (best != default)
        {
            enemy.X = best.nx;
            enemy.Y = best.ny;
        }
    }

    private void DamageNearbyPlayers(Enemy enemy)
    {
        // Damage all players at Manhattan distance == 1 from this enemy
        foreach (var player in _board.Players.ToList())
        {
            if (!player.IsAlive) continue;
            int dist = Math.Abs(player.X - enemy.X) + Math.Abs(player.Y - enemy.Y);
            if (dist == 1)
                player.TakeDamage(1);
        }
    }
}
