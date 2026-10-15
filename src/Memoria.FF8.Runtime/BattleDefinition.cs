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

public enum BattleActionEffect
{
    Damage,
    Heal,
    Revive,
    FullHeal,
    Scan,
    Status,
    RemoveStatus
}

public sealed record BattleActionDefinition(BattleAbility Ability, int Id, string Name, BattleActionEffect Effect, int Power, bool Allies, bool AllTargets = false, BattleStatusKind[]? Statuses = null, int DrawResist = 0, int Element = 0, int LevelPowerCoefficient = 0, int ConstantPower = 0, int Level = 1, int DamagePercent = 0, bool Available = true, string? DisplayName = null);
public sealed record BattleStock(int Id, int Count);
public sealed record BattleDrawSpell(int Id, int Resistance);

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
    public bool Permanent { get; init; }
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
    public int AbilityPoints { get; init; }
    public int Level { get; init; } = 1;
    public int Magic { get; init; } = 20;
    public int Spirit { get; init; } = 5;
    public int PhysicalPower { get; init; } = 20;
    public int DrainPercent { get; init; }
    public int ElementAttack { get; init; }
    public int ElementAttackPercent { get; init; }
    public int[] ElementDefenses { get; init; } = [800, 800, 800, 800, 800, 800, 800, 800];
    public BattleStock[] MagicStocks { get; init; } = [];
    public BattleDrawSpell[] DrawSpells { get; init; } = [];
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
    public BattleActionDefinition[] ActionDefinitions { get; init; } = [];
    public BattleStock[] Items { get; init; } = [];
    public byte NativeSeed { get; init; }
    public NativeBattleRandomState? InitialRandom { get; init; }
}

public enum BattleActionPhase
{
    Windup,
    Recovery
}

public sealed record BattleAttackCommand(string ActorId, string TargetId, BattleAbility Ability = BattleAbility.Attack, int ActionId = 0, bool DrawCast = false);
public sealed record BattleActionPresentation(string ActorId, string TargetId, BattleActionPhase Phase, int RemainingFrames, BattleAbility Ability = BattleAbility.Attack, int ActionId = 0, bool DrawCast = false);
public sealed record BattleEvent(long Frame, string Kind, string? ActorId, string? TargetId, int Amount, string Message, BattleAbility Ability = BattleAbility.Attack);
public sealed record BattleStatusSnapshot(BattleStatusKind Kind, int RemainingFrames, int UntilTickFrames, BattleStatusEffectDefinition? Definition = null);
public sealed record BattleCombatantSnapshot(string Id, int CurrentHp, int Atb, BattleStatusSnapshot[] StatusEffects)
{
    public BattleStock[] MagicStocks { get; init; } = [];
}

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
    public BattleStock[] Items { get; init; } = [];

    public NativeBattleRandomState? Random { get; init; }
    public BattleStatusKind[] CustomHandlers { get; init; } = [];
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
        Stocks.AddRange(definition.MagicStocks);
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
    public int Level => definition.Level;
    public int Magic => definition.Magic;
    public int Spirit => definition.Spirit;
    public int PhysicalPower => definition.PhysicalPower;
    public int DrainPercent => definition.DrainPercent;
    public int ElementAttack => definition.ElementAttack;
    public int ElementAttackPercent => definition.ElementAttackPercent;
    public IReadOnlyList<int> ElementDefenses => Array.AsReadOnly(definition.ElementDefenses);
    internal List<BattleStock> Stocks { get; } = [];
    public IReadOnlyList<BattleStock> MagicStocks => Stocks.AsReadOnly();
    public IReadOnlyList<BattleDrawSpell> DrawSpells => Array.AsReadOnly(definition.DrawSpells);
    public int? WeaponId => definition.WeaponId;
    public string? WeaponName => definition.WeaponName;
    public IReadOnlyList<int> EquipmentIds => Array.AsReadOnly(definition.EquipmentIds);
    public IReadOnlyList<BattleAbility> Abilities => Array.AsReadOnly(definition.Abilities);
    public IReadOnlyList<BattleStatusSnapshot> StatusEffects => Effects.Select(effect => new BattleStatusSnapshot(effect.Definition.Kind, effect.RemainingFrames, effect.UntilTickFrames, effect.Definition)).ToArray();
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
