namespace Memoria.FF8.Runtime;

// These handlers define explicit scaffold behavior, not recovered original FF8 formulas.
// Future effects must validate their definition before session construction and use the same dispatcher.
// Validation must be pure. Handlers must be stateless and mutate only through the supplied context,
// allowing detached fast-forward and rejected restores to leave the owning session unchanged.
public interface IBattleStatusEffectHandler
{
    BattleStatusKind Kind { get; }
    void Validate(BattleStatusEffectDefinition definition);
    int AtbRatePercent { get; }
    void Dispatch(BattleEffectContext context);
}

public sealed class BattleEffectContext
{
    private readonly Action<int> damage;
    private readonly Action<int> heal;

    internal BattleEffectContext(BattleCombatant combatant, BattleStatusEffectDefinition definition, long frame, bool isPeriodicTick, Action<int> damage, Action<int> heal)
    {
        Combatant = combatant;
        Definition = definition;
        Frame = frame;
        IsPeriodicTick = isPeriodicTick;
        this.damage = damage;
        this.heal = heal;
    }

    public BattleCombatant Combatant { get; }
    public BattleStatusEffectDefinition Definition { get; }
    public long Frame { get; }
    public bool IsPeriodicTick { get; }

    public void DealDamage(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        damage(amount);
    }

    public void Heal(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        heal(amount);
    }
}

internal sealed class ScaffoldBattleEffectHandler(BattleStatusKind kind) : IBattleStatusEffectHandler
{
    public BattleStatusKind Kind => kind;
    // TODO(TD-009): Replace scaffold status rates with recovered combat contracts
    public int AtbRatePercent => kind == BattleStatusKind.Haste ? 200 : kind == BattleStatusKind.Slow ? 50 : 100;

    public void Validate(BattleStatusEffectDefinition definition)
    {
        if (definition.Kind != kind || definition.DurationFrames is < 1 or > 1_000_000 || definition.TickIntervalFrames is < 1 or > 1_000_000 || definition.Magnitude is < 0 or > 1_000_000)
        {
            throw new ArgumentException("Invalid scaffold status effect definition.");
        }
    }

    public void Dispatch(BattleEffectContext context)
    {
        // TODO(TD-009): Periodic magnitude is explicit debug tuning until the native status damage/healing contract is recovered
        if (!context.IsPeriodicTick)
        {
            return;
        }
        if (kind == BattleStatusKind.Poison)
        {
            context.DealDamage(context.Definition.Magnitude);
        }
        else if (kind == BattleStatusKind.Regen)
        {
            context.Heal(context.Definition.Magnitude);
        }
    }
}

