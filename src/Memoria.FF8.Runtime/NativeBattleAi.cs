namespace Memoria.FF8.Runtime;

public sealed record NativeBattleAiAbility(byte Command, byte Sequence, ushort Id, int AttackType = 0, int Power = 0, int Element = 0, ushort Statuses0 = 0, uint Statuses1 = 0, int StatusAttack = 0)
{
    public byte? NativeAttackFlags { get; init; }
}
public sealed record NativeBattleLastAction(byte? AttackType, byte? AttackerModelId, byte? Command, byte? ActionId, byte? ElementMask, byte? AttackerIndex);
public sealed record NativeBattleAiDefinition(byte[][] Scripts, NativeBattleAiAbility[] Abilities, int LevelBand, string[] Texts)
{
    public System.Collections.Immutable.ImmutableArray<NativeBattleSequenceTrack> SequenceTracks { get; init; } = [];
    public byte[][] NameBuffers { get; init; } = [];
    public string[] DisplayNames { get; init; } = [];
}
public sealed record NativeBattleEnemyLevel(int MaximumHp, int Strength, int Vitality, int Magic, int Spirit, int Speed, int[] ElementDefenses, BattleDrawSpell[] DrawSpells, int[] DrawGuardianForces, int AiLevelBand)
{
    public int Evasion { get; init; }
    public int[] BaseStatistics { get; init; } = [];
}
public sealed record NativeBattleEnemyTemplate(byte SceneLevel, NativeBattleEnemyLevel[] Levels)
{
    public byte? Information246 { get; init; }
    public NativeBattleDrawSlot[][]? NativeDrawTables { get; init; }
    public byte? Information254 { get; init; }
    public NativeBattleMugDefinition? NativeMug { get; init; }
    public BattleNativeEnemySpecial? NativeSpecial { get; init; }
    public NativeBattleStatusAllocation? NativeStatusAllocation { get; init; }
    public uint NativeCombatantFlags { get; init; }
}
public sealed record NativeBattleParticipantSnapshot(bool Active, bool Hidden, bool Untargetable, bool Suspended, int ActorIndex, int? ResolvedLevel, uint[] Variables, byte TurnCount, string? LastAttackerId, BattleAbility? LastAttackerAbility, bool DeathHandled)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? MaximumHitPoints { get; init; }
    [System.Text.Json.Serialization.JsonRequired]
    public ushort? ReturnDamage { get; init; }
    [System.Text.Json.Serialization.JsonRequired]
    public NativeBattlePendingReaction? PendingReaction { get; init; }
    [System.Text.Json.Serialization.JsonRequired]
    public byte? RemainingHits { get; init; }
    public BattleNativeCharacterGrowth? Growth { get; init; }
    // Older valid snapshots predate opcode40 support, so their six allocation multipliers are still10.
    public byte[] StatMultipliers { get; init; } = [10, 10, 10, 10, 10, 10];
    public byte? LastHealing { get; init; }
    public bool AllocationPendingStatistics { get; init; }
    public NativeBattleLastAction? LastAction { get; init; }
    public ushort PreviousTargetMask { get; init; }
    public byte? DrawnSpell { get; init; }
    public ushort? KillCount { get; init; }
    public ushort? DeathCount { get; init; }
    public uint? NativeCombatantFlags { get; init; }
    public int? DisplayNameIndex { get; init; }
    public int[]? ElementDefenses { get; init; }
}
