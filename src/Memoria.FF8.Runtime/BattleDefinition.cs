namespace Memoria.FF8.Runtime;

public enum BattleAtbMode
{
    Normal,
    TurnBased
}

public enum BattleOutcome
{
    Active,
    Victory,
    Defeat
}

public enum BattleAbility
{
    Attack,
    Magic,
    Draw,
    GuardianForce,
    Item
}

public enum BattleStatusKind
{
    Poison,
    Regen,
    Haste,
    Slow,
    Protect,
    Shell,
    Blind,
    Silence,
    Sleep
}

public sealed record BattleStatusEffectDefinition
{
    public BattleStatusKind Kind { get; init; }
    public int DurationFrames { get; init; } = 300;
    public int TickIntervalFrames { get; init; } = 30;
    public int Magnitude { get; init; } = 10;
}

public sealed record BattleCombatantDefinition
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public bool IsPlayer { get; init; }
    public int ModelId { get; init; }
    public int Costume { get; init; }
    public int MaximumHp { get; init; } = 100;
    public int CurrentHp { get; init; } = 100;
    public int Speed { get; init; } = 100;
    public int Attack { get; init; } = 20;
    public int Defense { get; init; } = 5;
    public int InitialAtb { get; init; }
    public int? WeaponId { get; init; }
    public string? WeaponName { get; init; }
    public int[] EquipmentIds { get; init; } = [];
    public BattleAbility[] Abilities { get; init; } = [BattleAbility.Attack];
    public BattleStatusEffectDefinition[] StatusEffects { get; init; } = [];
}

public sealed record BattleDefinition
{
    public string Name { get; init; } = "Debug battle";
    public int? StageId { get; init; }
    public int? EncounterId { get; init; }
    public BattleCombatantDefinition[] Combatants { get; init; } = [];
}

public enum BattleActionPhase
{
    Windup,
    Recovery
}

public sealed record BattleAttackCommand(string ActorId, string TargetId);
public sealed record BattleActionPresentation(string ActorId, string TargetId, BattleActionPhase Phase, int RemainingFrames);
public sealed record BattleEvent(long Frame, string Kind, string? ActorId, string? TargetId, int Amount, string Message);
public sealed record BattleStatusSnapshot(BattleStatusKind Kind, int RemainingFrames, int UntilTickFrames);
public sealed record BattleCombatantSnapshot(string Id, int CurrentHp, int Atb, BattleStatusSnapshot[] StatusEffects);

public sealed record BattleSnapshot
{
    public int Version { get; init; } = 1;
    public BattleDefinition Definition { get; init; } = new();
    public BattleAtbMode Mode { get; init; }
    public long Frame { get; init; }
    public BattleOutcome Outcome { get; init; }
    public BattleCombatantSnapshot[] Combatants { get; init; } = [];
    public BattleAttackCommand[] QueuedActions { get; init; } = [];
    public BattleActionPresentation? ActiveAction { get; init; }
    public BattleEvent[] Events { get; init; } = [];
}

public sealed class BattleCombatant
{
    private readonly BattleCombatantDefinition definition;
    internal readonly List<BattleStatusState> Effects;

    internal BattleCombatant(BattleCombatantDefinition definition)
    {
        this.definition = definition;
        CurrentHp = definition.CurrentHp;
        Atb = CurrentHp == 0 ? 0 : definition.InitialAtb;
        Effects = definition.StatusEffects.Select(effect => new BattleStatusState(effect, effect.DurationFrames, effect.TickIntervalFrames)).ToList();
    }

    public string Id => definition.Id;
    public string Name => definition.Name;
    public bool IsPlayer => definition.IsPlayer;
    public int ModelId => definition.ModelId;
    public int Costume => definition.Costume;
    public int MaximumHp => definition.MaximumHp;
    public int CurrentHp { get; internal set; }
    public int Speed => definition.Speed;
    public int Attack => definition.Attack;
    public int Defense => definition.Defense;
    public int? WeaponId => definition.WeaponId;
    public string? WeaponName => definition.WeaponName;
    public IReadOnlyList<int> EquipmentIds => Array.AsReadOnly(definition.EquipmentIds);
    public IReadOnlyList<BattleAbility> Abilities => Array.AsReadOnly(definition.Abilities);
    public IReadOnlyList<BattleStatusSnapshot> StatusEffects => Effects.Select(effect => new BattleStatusSnapshot(effect.Definition.Kind, effect.RemainingFrames, effect.UntilTickFrames)).ToArray();
    public int Atb { get; internal set; }
    public bool IsBusy { get; internal set; }
    public bool IsAlive => CurrentHp > 0;
    public bool IsReady => IsAlive && !IsBusy && Atb == BattleSession.AtbMaximum;
}

internal sealed class BattleStatusState(BattleStatusEffectDefinition definition, int remainingFrames, int untilTickFrames)
{
    internal BattleStatusEffectDefinition Definition { get; } = definition;
    internal int RemainingFrames { get; set; } = remainingFrames;
    internal int UntilTickFrames { get; set; } = untilTickFrames;
}
