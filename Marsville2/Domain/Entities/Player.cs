using Marsville2.Domain.Items;

namespace Marsville2.Domain.Entities;

/// <summary>
/// An astronaut controlled by a participating team's agent.
/// </summary>
public class Player : EntityBase
{
    public override int MaxHealth => _maxHealth;
    public override string EntityType => "player";

    public string TeamName { get; }
    public string Token { get; }
    public Backpack Backpack { get; } = new();
    public int MushroomsCollected { get; private set; }
    public int TotalScore { get; set; }

    /// <summary>True once the player has stepped onto the goal cell for this round.</summary>
    public bool HasReachedGoal { get; private set; }

    private int _actionCount;
    private readonly int _maxHealth;

    /// <summary>Total number of actions this player has taken this round (used for -1/turn scoring).</summary>
    public int TurnCount { get; private set; }
    private bool _isCrawling;
    public bool IsCrawling => _isCrawling;

    /// <summary>Number of shield charges remaining. Each charge absorbs one incoming attack.</summary>
    public int ShieldHealth { get; private set; }

    public Player(string id, string teamName, string token, int x, int y, int maxHealth = 2)
        : base(id, x, y, maxHealth)
    {
        TeamName = teamName;
        Token = token;
        _maxHealth = maxHealth;
    }

    /// <summary>Adds one shield charge. Shields negate incoming attacks one-for-one and are lost on use.</summary>
    public void AddShield() => ShieldHealth++;

    /// <summary>Restores health to the current maximum (used by health packs).</summary>
    public void HealToFull() => Health = MaxHealth;

    /// <summary>
    /// Override: if a shield charge is available, absorb the hit and lose one shield.
    /// Otherwise deal the damage normally.
    /// </summary>
    public override bool TakeDamage(int amount)
    {
        if (ShieldHealth > 0)
        {
            ShieldHealth--;
            return false; // absorbed — player survives
        }
        return base.TakeDamage(amount);
    }

    /// <summary>Instantly kills the player, bypassing any shield charges (e.g. poison).</summary>
    public void ApplyPoison() => Health = 0;

    public void CollectMushroom()
    {
        MushroomsCollected++;
        TotalScore++;
    }

    public void SetCrawling(bool crawling) => _isCrawling = crawling;

    /// <summary>Marks the player as having reached the goal, preventing further actions.</summary>
    public void MarkGoalReached() => HasReachedGoal = true;

    /// <summary>
    /// Called after every action the player performs.
    /// </summary>
    public void RecordAction()
    {
        _actionCount++;
        TurnCount++;
    }
}
