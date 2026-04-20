namespace Marsville2.Domain.Entities;

/// <summary>
/// A hostile Martian creature.
/// Moves toward the nearest player using a simple heuristic.
/// Can damage adjacent players. Has 1 life and cannot pick up items.
/// </summary>
public class Enemy : EntityBase
{
    public override int MaxHealth => 1;
    public override EntityType EntityType => EntityType.Enemy;

    public Enemy(string id, int x, int y) : base(id, x, y, health: 1) { }
}
