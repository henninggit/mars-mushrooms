using Marsville2.Domain.Items;

namespace Marsville2.Domain.Entities;

/// <summary>
/// An astronaut controlled by a participating team's agent.
/// </summary>
public class Player : EntityBase
{
    public override int MaxHealth => _maxHealth + ShieldHealth;
    public override string EntityType => "player";

    public string TeamName { get; }
    public string Token { get; }
    public Backpack Backpack { get; } = new();
    public int MushroomsCollected { get; private set; }
    public int TotalScore { get; set; }

    /// <summary>True once the player has stepped onto the goal cell for this round.</summary>
    public bool HasReachedGoal { get; private set; }

    // Health regeneration: regain 1 HP every 5 actions taken
    private int _actionCount;
    private readonly int _maxHealth;
    private bool _isCrawling;
    public bool IsCrawling => _isCrawling;

    /// <summary>Additive bonus max health from collected shields.</summary>
    public int ShieldHealth { get; private set; }

    public Player(string id, string teamName, string token, int x, int y, int maxHealth = 2)
        : base(id, x, y, maxHealth)
    {
        TeamName = teamName;
        Token = token;
        _maxHealth = maxHealth;
    }

    /// <summary>
    /// Increases maximum health by 1 (shield bonus) and immediately heals 1 HP.
    /// </summary>
    public void AddShield()
    {
        ShieldHealth++;
        Health = Math.Min(MaxHealth, Health + 1);
    }

    /// <summary>Restores health to the current maximum (used by health packs).</summary>
    public void HealToFull() => Health = MaxHealth;

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
    /// Every 5 actions, gain 1 HP (up to MaxHealth).
    /// </summary>
    public void RecordAction()
    {
        _actionCount++;
        if (_actionCount % 5 == 0 && Health < MaxHealth)
            Health = Math.Min(MaxHealth, Health + 1);
    }
}
