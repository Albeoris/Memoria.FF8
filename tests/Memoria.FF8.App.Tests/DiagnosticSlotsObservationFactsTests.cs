using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json;
using Memoria.FF8.BattleEffects;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Tests;

public sealed class DiagnosticSlotsObservationFactsTests
{
    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(true, false, true, false)]
    [InlineData(false, true, false, false)]
    [InlineData(true, false, false, true)]
    public void Scalar_observation_matches_original_snapshot_projection_without_writing_native_state(bool once, bool disabled, bool retained, bool learnedAfter)
    {
        BattleSession battle = Create(once, disabled, retained, learnedAfter);
        Guid instance = Guid.NewGuid();
        string before = JsonSerializer.Serialize(battle.Capture());
        long restores = battle.DiagnosticRestoreCount;
        long resets = battle.DiagnosticResetCount;
        DiagnosticSlotsObservation expected = SnapshotObserve(battle, instance, "selphie");
        DiagnosticSlotsObservation actual = DiagnosticSlotsObservations.Observe(battle, instance, "selphie");
        DiagnosticSlotsObservation repeated = DiagnosticSlotsObservations.Observe(battle, instance, "selphie");
        Assert.Equal(JsonSerializer.Serialize(expected with { Seconds = 0 }), JsonSerializer.Serialize(actual with { Seconds = 0 }));
        Assert.Equal(JsonSerializer.Serialize(actual with { Seconds = 0 }), JsonSerializer.Serialize(repeated with { Seconds = 0 }));
        Assert.Equal(learnedAfter ? (byte)32 : (byte)0, actual.LearnedFlags);
        Assert.Equal(retained ? (byte?)3 : null, actual.RetainedCrisis);
        Assert.Equal(1, actual.DeadParty);
        Assert.Equal(before, JsonSerializer.Serialize(battle.Capture()));
        Assert.Equal(restores, battle.DiagnosticRestoreCount);
        Assert.Equal(resets, battle.DiagnosticResetCount);
        BattleSnapshot snapshot = battle.Capture();
        BattleDiagnosticSlotsFacts facts = battle.ObserveDiagnosticSlotsFacts("selphie");
        Assert.Equal(snapshot.Definition.LimitProgress?.SpecialMagicFlags, facts.SavedSpecialMagicFlags);
        Assert.Equal(snapshot.Definition.NativeLimitsDisabled, facts.NativeLimitsDisabled);
        Assert.Equal(snapshot.Definition.LimitOncePerTurn, facts.LimitOncePerTurn);
        Assert.Equal(snapshot.Combatants.Single(actor => actor.Id == "selphie").LimitTurnCrisis, facts.LimitTurnCrisis);
        Assert.Equal(JsonSerializer.Serialize(snapshot.Random), JsonSerializer.Serialize(facts.Random));
    }

    [Theory]
    [InlineData("learned")]
    [InlineData("limits")]
    [InlineData("limit-owner")]
    [InlineData("actor")]
    public void Missing_required_owners_fail_like_the_snapshot_backed_observer(string missing)
    {
        BattleSession battle = Create(missing: missing);
        string actor = missing == "actor" ? "absent" : "selphie";
        Guid instance = Guid.NewGuid();
        string before = JsonSerializer.Serialize(battle.Capture());
        Exception expected = Assert.ThrowsAny<Exception>(() => SnapshotObserve(battle, instance, actor));
        Exception actual = Assert.ThrowsAny<Exception>(() => DiagnosticSlotsObservations.Observe(battle, instance, actor));
        Assert.Equal(expected.GetType(), actual.GetType());
        Assert.Equal(expected.Message, actual.Message);
        Assert.Equal(before, JsonSerializer.Serialize(battle.Capture()));
    }

    [Fact]
    public void Source_rule_mismatch_precedes_missing_saved_learning_in_both_observers()
    {
        BattleSession battle = Create(missing: "learned");
        Guid instance = Guid.NewGuid();
        BattleNativeLimitDefinition rules = battle.Combatants.Single(actor => actor.Id == "selphie").NativeLimit!;
        DiagnosticSlotsSource source = new(instance, battle.DiagnosticLifecycleId, "selphie", 0, 51, 0, rules with { Command = 15 }, [], [], [], [], [], "");
        InvalidDataException expected = Assert.Throws<InvalidDataException>(() => SnapshotObserve(battle, instance, "selphie", source));
        InvalidDataException actual = Assert.Throws<InvalidDataException>(() => DiagnosticSlotsObservations.Observe(battle, instance, "selphie", source));
        Assert.Equal(expected.Message, actual.Message);
    }

    [Fact]
    public void Nullable_facts_remain_unknown_until_the_existing_App_consumer_requires_them()
    {
        BattleSession noLearning = Create(missing: "learned");
        BattleSession noProhibition = Create(missing: "limits");
        Assert.Null(noLearning.ObserveDiagnosticSlotsFacts("selphie").SavedSpecialMagicFlags);
        Assert.Null(noProhibition.ObserveDiagnosticSlotsFacts("selphie").NativeLimitsDisabled);
        Assert.Throws<InvalidOperationException>(() => noLearning.ObserveDiagnosticSlotsFacts("absent"));
    }

    [Fact]
    public void Returned_random_is_a_stable_immutable_observation_across_one_real_component_roll()
    {
        BattleSession battle = Create();
        BattleDiagnosticSlotsFacts before = battle.ObserveDiagnosticSlotsFacts("selphie");
        string retained = JsonSerializer.Serialize(before.Random);
        NativeBattleRandomState expected = before.Random;
        for (int index = 0; index < 4; index++)
        {
            expected = expected.Draw(out _);
        }
        BattleNativeSlotsRoll result = battle.RollNativeSlots("selphie");
        BattleDiagnosticSlotsFacts after = battle.ObserveDiagnosticSlotsFacts("selphie");
        Assert.Equal(retained, JsonSerializer.Serialize(before.Random));
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(after.Random));
        Assert.Equal(result, battle.GetNativeSlotsRoll("selphie"));
        Assert.Equal(JsonSerializer.Serialize(after with { Random = before.Random }), JsonSerializer.Serialize(before));
    }

    private static BattleSession Create(bool once = false, bool disabled = false, bool retained = false, bool learnedAfter = false, string? missing = null)
    {
        BattleNativeLimitDefinition rules = new(5, 16, 0, 0, new byte[32].ToImmutableArray())
        {
            SlotsSelectionBytes = new byte[80].ToImmutableArray(),
            SlotsSpellBytes = Enumerable.Repeat((byte)1, 16).ToImmutableArray()
        };
        NativeLimitProgress progress = new(new string('0', 64), 0, 0, 0, 0, new byte[8].ToImmutableArray()) { SpecialMagicFlags = 0 };
        BattleNativeAction action = new(16, 1, 0, 0)
        {
            Name = "Spell", MagicId = 1, AttackType = 0, Power = 0, HitCount = 1, TargetFlags = 2, AttackFlags = 0, DamagePresentation = 0,
            Element = 0, ElementPercent = 0, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = 0, Critical = 0, PowerModifier = 0, LevelModifier = 0, RawRecord = [], SupplementalRecord = []
        };
        BattleSession original = new(new BattleDefinition
        {
            LimitOncePerTurn = once,
            NativeLimitsDisabled = missing == "limits" ? null : disabled,
            LimitProgress = missing == "learned" ? null : progress,
            NativeActions = [action],
            Combatants =
            [
                new() { Id = "selphie", Name = "Selphie", IsPlayer = true, NativeStatusResistances = new byte[40], Level = 20, ModelId = 5, InitialAtb = BattleSession.AtbMaximum, NativeLimit = missing == "limit-owner" ? null : rules },
                new() { Id = "dead", Name = "Dead", IsPlayer = true, NativeStatusResistances = new byte[40], CurrentHp = 0, NativeStatus0 = 1 },
                new() { Id = "inactive", Name = "Inactive", SceneSlot = 1, NativeStatusResistances = new byte[40], CurrentHp = 0, InitialActive = false },
                new() { Id = "enemy", Name = "Enemy", SceneSlot = 0, NativeStatusResistances = new byte[40], Speed = 1 }
            ]
        }, BattleAtbMode.TurnBased);
        BattleSnapshot snapshot = original.Capture();
        return BattleSession.Restore(snapshot with
        {
            NativeLimits = missing == "limit-owner" ? [] : [new("selphie", 4)],
            Combatants = snapshot.Combatants.Select(actor => actor.Id == "selphie" ? actor with { LimitTurnCrisis = retained ? (byte?)3 : null } : actor).ToArray(),
            NativeScriptState = learnedAfter ? snapshot.NativeScriptState with { LimitSpecialFlagsBefore = 0, LimitSpecialFlagsAfter = 32, LimitSpecialSetMask = 32 } : snapshot.NativeScriptState
        });
    }

    private static DiagnosticSlotsObservation SnapshotObserve(BattleSession battle, Guid instance, string actorId, DiagnosticSlotsSource? source = null)
    {
        BattleCombatant actor = battle.Combatants.Single(candidate => candidate.Id == actorId);
        BattleSnapshot snapshot = battle.Capture();
        BattleDiagnosticCombatant native = battle.ObserveDiagnosticCombatants().Single(candidate => candidate.Id == actorId);
        BattleNativeLimitDefinition rules = actor.NativeLimit ?? throw new NotSupportedException("Slots observation requires its actual character Limit owner.");
        if (source is not null && JsonSerializer.Serialize(rules) != JsonSerializer.Serialize(source.Rules))
        {
            throw new InvalidDataException("Slots actor rules changed from the initialized resource source.");
        }
        byte learned = battle.NativeScriptState.LimitSpecialFlagsAfter ?? snapshot.Definition.LimitProgress?.SpecialMagicFlags ?? throw new NotSupportedException("Slots observation requires its initialized saved learned owner.");
        return new(instance, battle.DiagnosticLifecycleId, battle.DiagnosticRestoreCount, battle.DiagnosticResetCount, battle.NativeHostCallbacks ?? battle.Frame, battle.Frame, Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency, actor.Id, checked((byte)(native.NativeIndex ?? throw new NotSupportedException("Slots actor requires its physical party index."))), actor.ModelId, rules.CharacterId, rules.Command, actor.Level, native.CurrentHp, native.MaximumHp, native.Statuses0, native.Statuses1, battle.GetNativeLimitLevel(actor.Id), learned, battle.ObserveDiagnosticCombatants().Count(candidate => battle.Combatants.Single(member => member.Id == candidate.Id) is { IsPlayer: true, IsActive: true } && (candidate.Statuses0 & 1) != 0), snapshot.Definition.NativeLimitsDisabled ?? throw new NotSupportedException("Slots requires the actual native Limit prohibition field."), snapshot.Definition.LimitOncePerTurn, snapshot.Combatants.Single(candidate => candidate.Id == actorId).LimitTurnCrisis, battle.CanChoosePlayerCommand(actor.Id), actor.IsPlayer && actor.IsAlive && actor.IsActive && battle.Outcome == BattleOutcome.Active, battle.SelphieCardSlots, battle.PendingSlotsDeal is not null, snapshot.Random ?? throw new NotSupportedException("Slots observation requires the actual native battle random owner."), battle.GetNativeSlotsRoll(actor.Id));
    }
}
