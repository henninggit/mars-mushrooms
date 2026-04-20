namespace Marsville2.Domain.Entities;

public abstract class EntityBase : IEntity
{
    public string Id { get; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Health { get; protected set; }
    public abstract int MaxHealth { get; }
    public bool IsAlive => Health > 0;
    public abstract EntityType EntityType { get; }

    protected EntityBase(string id, int x, int y, int health)
    {
        Id = id;
        X = x;
        Y = y;
        Health = health;
    }

    public virtual bool TakeDamage(int amount)
    {
        Health = Math.Max(0, Health - amount);
        return !IsAlive;
    }
}
