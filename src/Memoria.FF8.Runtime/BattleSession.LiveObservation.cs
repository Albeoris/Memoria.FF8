using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Accepted host facts, independent of effect working memory and persistence snapshots.</summary>
public sealed record BattleLiveObservation
{
    public int? EncounterId { get; init; }
    public bool NativeEntry { get; init; }
    public BattleOutcome? DebugOutcome { get; init; }
    public byte? NativeResult { get; init; }
    public NativeExamTimerState? Timer { get; init; }
    public NativeVibrationProcessState? Vibration { get; init; }
    public NativeGlobalSoundProcess? Sound { get; init; }
    public NativeBattleMusicProcessState? Music { get; init; }
    public ImmutableArray<BattleEnemyDefeat> EnemyDefeats { get; init; } = [];
    public ImmutableArray<BattleOriginalStockDrawOperation> StockDraws { get; init; } = [];
    public ImmutableArray<BattleLiveMaximumHp> MaximumHitPoints { get; init; } = [];
    public NativeCharacterVitals? Vitals { get; init; }
    public NativeCardInventory? Cards { get; init; }
    public ImmutableArray<NativeItemStock> Items { get; init; } = [];
    public ImmutableArray<BattleGuardianForceVital> GuardianForces { get; init; } = [];
    public bool OmegaProof { get; init; }

    internal static BattleLiveObservation FromSnapshot(BattleSnapshot snapshot)
    {
        return new()
        {
            EncounterId = snapshot.Definition.EncounterId,
            NativeEntry = snapshot.Definition.NativeEntry is not null,
            DebugOutcome = snapshot.DebugOutcome,
            NativeResult = snapshot.NativeGraphics?.NativeOutcome?.ResultByte6E7,
            Timer = snapshot.NativeExamTimer,
            Vibration = snapshot.NativeVibrationProcess,
            Sound = snapshot.NativeGlobalSoundProcess,
            Music = snapshot.NativeMusicProcess,
            EnemyDefeats = [.. snapshot.EnemyDefeats],
            StockDraws = [.. snapshot.OriginalStockDrawOperations ?? []],
            MaximumHitPoints = [.. snapshot.Combatants.Select(actor => MaximumHp(actor.Id, actor.NativeParticipant, snapshot.Definition.Combatants.SingleOrDefault(source => source.Id == actor.Id)))],
            Vitals = snapshot.MagicVitals?.DeepCopy(),
            Cards = snapshot.NativeCards?.DeepCopy(),
            Items = [.. snapshot.NativeItemInventory ?? []],
            GuardianForces = [.. snapshot.GuardianForces],
            OmegaProof = snapshot.NativeScriptState?.UnlockedTutorials.Contains(127) == true
        };
    }

    internal static BattleLiveMaximumHp MaximumHp(string id, NativeBattleParticipantSnapshot? participant, BattleCombatantDefinition? definition)
    {
        bool maximum = participant is { Active: true, Suspended: false, MaximumHitPoints: 9999 };
        bool eligible = maximum && participant is { ActorIndex: >= 0 and < 3 } && definition is { IsPlayer: true, MagicRecordId: >= 0 and < 11, NativePlayerStatRules: not null };
        return new(id, definition?.MagicRecordId, maximum, eligible);
    }
}

public sealed record BattleLiveMaximumHp(string Id, int? MagicRecordId, bool Maximum, bool Eligible);

public sealed partial class BattleSession
{
    /// <summary>Observes bounded host facts without capturing effect, task, staging or render storage.</summary>
    public BattleLiveObservation ObserveLiveState()
    {
        return new()
        {
            EncounterId = definition.EncounterId,
            NativeEntry = definition.NativeEntry is not null,
            DebugOutcome = DebugOutcome,
            NativeResult = NativeResult,
            Timer = NativeExamTimer is { } timer ? timer with { Alternatives = timer.Alternatives.ToArray() } : null,
            Vibration = NativeVibrationProcess,
            Sound = NativeGlobalSoundProcess,
            Music = NativeMusicProcess,
            EnemyDefeats = [.. enemyDefeats],
            StockDraws = [.. originalStockDrawOperations],
            MaximumHitPoints = [.. combatants.Select(actor => ObserveMaximumHitPoints(actor))],
            Vitals = magicVitals?.DeepCopy(),
            Cards = nativeCards?.DeepCopy(),
            Items = [.. nativeItems],
            GuardianForces = [.. GuardianForces],
            OmegaProof = nativeScriptState.UnlockedTutorials.Contains(127)
        };
    }

    private static BattleLiveMaximumHp ObserveMaximumHitPoints(BattleCombatant actor)
    {
        bool maximum = actor.IsActive && !actor.Suspended && actor.IsPlayer && actor.NativePlayerStatRules is not null && actor.GuardianFormVitals is null && actor.MaximumHp == 9999;
        return new(actor.Id, actor.MagicRecordId, maximum, maximum && actor.NativeIndex is >= 0 and < 3 && actor.MagicRecordId is >= 0 and < 11);
    }

    public byte EntrySeed => definition.NativeSeed;
    public ushort EntryFlags => definition.NativeFlags;
    public int? EntryStageId => definition.StageId;
    public long? EntryClockCalls => definition.NativeExamTimerAtEntry?.ClockCalls;
    public NativeCrtRandomState? CurrentCrtRandom => nativeCrtRandom;
}
