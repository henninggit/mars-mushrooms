namespace MarsvilleStarter.Model;

/// <summary>An entity (player or enemy) visible on the board.</summary>
public sealed class EntityView
{
    /// <summary>The type of this entity.</summary>
    public EntityType EntityType { get; }

    /// <summary>Unique entity identifier.</summary>
    public string Id { get; }

    /// <summary>Current health points.</summary>
    public int Health { get; }

    /// <summary>True when this entity is an enemy.</summary>
    public bool IsEnemy => EntityType == EntityType.Enemy;

    /// <summary>True when this entity is another player.</summary>
    public bool IsPlayer => EntityType == EntityType.Player;

    internal EntityView(EntityDto dto)
    {
        EntityType = dto.EntityType;
        Id         = dto.Id;
        Health     = dto.Health;
    }
}
