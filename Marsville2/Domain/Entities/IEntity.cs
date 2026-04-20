namespace Marsville2.Domain.Entities;

public interface IEntity
{
    string Id { get; }
    int X { get; set; }
    int Y { get; set; }
    int Health { get; }
    int MaxHealth { get; }
    bool IsAlive { get; }
    EntityType EntityType { get; }

    /// <summary>Apply damage. Returns true if the entity dies.</summary>
    bool TakeDamage(int amount);
}
