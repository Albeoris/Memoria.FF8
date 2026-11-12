namespace Memoria.FF7.Runtime;

/// <summary>An origin-owned ability, including capabilities that the host cannot yet execute.</summary>
public sealed record Ff7BattleGuestAbility(Ff7BattleCommand Command, ushort Ability, string Name, byte TargetFlags, int Stock = 0, string? UnsupportedReason = null)
{
    public string Category { get; init; } = "Magic";
    public bool RequiresMagic { get; init; } = true;
    public bool CanTargetDead { get; init; }
}

public interface IFf7BattleGuestAbilities
{
    IReadOnlyList<Ff7BattleGuestAbility> Abilities { get; }
    bool SupportsAbility(Ff7BattleCommand command, ushort ability, Ff7BattleGuestAttackTarget target);
}
