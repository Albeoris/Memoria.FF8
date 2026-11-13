namespace Memoria.FF7.Runtime;

/// <summary>An origin-owned ability, including capabilities that the host cannot yet execute.</summary>
public sealed record Ff7BattleGuestAbility(Ff7BattleCommand Command, ushort Ability, string Name, byte TargetFlags, int Stock = 0, string? UnsupportedReason = null)
{
    public string Category { get; init; } = "Magic";
    public bool RequiresMagic { get; init; } = true;
    public bool CanTargetDead { get; init; }
    public bool AllTargets { get; init; }
}

public interface IFf7BattleGuestAbilities
{
    IReadOnlyList<Ff7BattleGuestAbility> Abilities { get; }
    /// <summary>Opts into command menu reuse while this identity and host combat inputs remain unchanged. Replace the token when origin abilities, items, support rules or relevant presentation geometry change; return null to evaluate every read.</summary>
    object? CommandMenuState => null;
    bool SupportsAbility(Ff7BattleCommand command, ushort ability, Ff7BattleGuestAttackTarget target);
    string? TargetUnsupportedReason(Ff7BattleCommand command, ushort ability, Ff7BattleGuestAttackTarget target) => SupportsAbility(command, ability, target) ? null : "The selected ability cannot be applied to this target.";
}

public interface IFf7BattleGuestGroupMagic
{
    string? PrepareGroupUnsupportedReason(IReadOnlyList<Ff7BattleGuestPhysicalHit> prepared);
    void PrepareGroup(IReadOnlyList<Ff7BattleGuestPhysicalHit> prepared);
}
