using Marsville2.Domain.Items;

namespace Marsville2.Domain.Entities;

/// <summary>
/// The player's spacesuit pockets — carries up to <see cref="Capacity"/> items.
/// </summary>
public class Backpack
{
    public const int Capacity = 10;
    private readonly List<IItem> _items = new();

    public IReadOnlyList<IItem> Items => _items.AsReadOnly();
    public int Count => _items.Count;
    public bool IsFull => _items.Count >= Capacity;

    public bool TryAdd(IItem item)
    {
        if (IsFull) return false;
        _items.Add(item);
        return true;
    }

    /// <summary>Removes the first item of the given type. Returns null if not found.</summary>
    public IItem? TakeFirst<T>() where T : IItem
    {
        var item = _items.OfType<T>().FirstOrDefault();
        if (item is null) return null;
        _items.Remove(item);
        return item;
    }

    public bool HasPlankAndNail() =>
        _items.OfType<Plank>().Any() && _items.OfType<Nail>().Any();

    public void ConsumePlankAndNail()
    {
        var plank = _items.OfType<Plank>().First();
        var nail = _items.OfType<Nail>().First();
        _items.Remove(plank);
        _items.Remove(nail);
    }
}
