using System.Text.Json;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class BattleGuardianFormLifecycleTests
{
    [Fact]
    public void DisabledBattleUsesNoFeatureMetadataOrResources()
    {
        BattleDefinition definition = Definition() with { GuardianFormsEnabled = false, GuardianFormResources = [] };
        BattleSession battle = new(definition, BattleAtbMode.Normal);
        BattleSnapshot saved = battle.Capture();
        Assert.Null(saved.GuardianForms);
        Assert.Empty(saved.Definition.GuardianFormResources);
        Assert.DoesNotContain("\"GuardianForms\":", JsonSerializer.Serialize(saved));
        BattleSession restored = RoundTrip(saved);
        Assert.Null(restored.GetGuardianForm("actor"));
        Assert.False(restored.CanRequestGuardianForm("actor", 2));
        Assert.Equal(saved.NativeGlobalRandomCalls, restored.Capture().NativeGlobalRandomCalls);
    }

    [Fact]
    public void FreshAndActiveStateRoundTripWithoutReplacingTheLogicalActorOrHumanHp()
    {
        BattleSession fresh = new(Definition(), BattleAtbMode.Normal);
        Assert.Empty(RoundTrip(fresh.Capture()).Capture().GuardianForms!.Forms);
        BattleSession active = Active();
        BattleSnapshot saved = active.Capture();
        BattleSession restored = RoundTrip(saved);
        BattleCombatant actor = Actor(restored);
        Assert.Equal(JsonSerializer.Serialize(saved.GuardianForms), JsonSerializer.Serialize(restored.Capture().GuardianForms));
        Assert.Equal(37, restored.Capture().Combatants.Single(item => item.Id == "actor").CurrentHp);
        Assert.Equal(80, actor.CurrentHp);
        Assert.Equal(100, actor.MaximumHp);
        Assert.Equal(61, restored.GetGuardianFormVisual(actor.Id));
        Assert.True(actor.IsPlayer);
        Assert.Equal(0, actor.ModelId);
        Assert.Equal(23, actor.Attack);
        Assert.Equal(31, actor.Magic);
        Assert.Equal(new[] { 700, 800, 900, 1000, 800, 800, 800, 800 }, actor.ElementDefenses);
        Assert.Equal(2, restored.Combatants.Count);
        Assert.Equal(73, restored.GetGuardianForm("actor")!.RemainingTicks);
    }

    [Theory]
    [InlineData(BattleStatusKind.Poison, 10, 70)]
    [InlineData(BattleStatusKind.Regen, 10, 90)]
    public void PeriodicDamageAndHealingUseTheCanonicalGuardianPool(BattleStatusKind kind, int amount, int expected)
    {
        BattleStatusEffectDefinition effect = new() { Kind = kind, DurationFrames = 100, TickIntervalFrames = 1, Magnitude = amount };
        BattleSession battle = Active(effect);
        battle.Tick();
        Assert.Equal(expected, Actor(battle).CurrentHp);
        Assert.Equal(expected, Assert.Single(battle.GuardianForces).HitPoints);
        Assert.Equal(37, battle.Capture().Combatants.Single(item => item.Id == "actor").CurrentHp);
        Assert.Equal(99, Assert.Single(Actor(battle).StatusEffects).RemainingFrames);
        BattleSession restored = RoundTrip(battle.Capture());
        Assert.Equal(expected, Actor(restored).CurrentHp);
        Assert.Equal(Actor(battle).StatusEffects, Actor(restored).StatusEffects);
    }

    [Fact]
    public void GuardianDepletionReturnsHumanHpWithoutHeroDeathOrStatusCleansing()
    {
        BattleSession battle = Active(new() { Kind = BattleStatusKind.Poison, DurationFrames = 100, TickIntervalFrames = 1, Magnitude = 100 });
        battle.Tick();
        Assert.Null(battle.GetGuardianForm("actor"));
        Assert.Equal(37, Actor(battle).CurrentHp);
        Assert.Equal(100, Actor(battle).MaximumHp);
        Assert.Equal(0, Assert.Single(battle.GuardianForces).HitPoints);
        Assert.True(Actor(battle).IsAlive);
        Assert.Equal(BattleStatusKind.Poison, Assert.Single(Actor(battle).StatusEffects).Kind);
        Assert.DoesNotContain(battle.Events, item => item.Kind == "Defeated" && item.TargetId == "actor");
        Assert.Equal(37, Actor(RoundTrip(battle.Capture())).CurrentHp);
    }

    [Fact]
    public void InnateFireAdmitsWithoutMagicStockAndKeepsAnExplicitFreeBinding()
    {
        BattleSession battle = Active(ready: true);
        Assert.Empty(Actor(battle).MagicStocks);
        battle.QueueGuardianFormAction("actor", "enemy", BattleGuardianFormAction.Fire);
        BattleAttackCommand command = Assert.Single(battle.QueuedActions);
        Assert.True(command.FreeNativeMagic);
        Assert.Equal(BattleGuardianFormAction.Fire, command.GuardianFormAction);
        Assert.Null(command.MagicReservationId);
        Assert.Empty(Actor(battle).MagicStocks);
        BattleSession restored = RoundTrip(battle.Capture());
        Assert.True(Assert.Single(restored.QueuedActions).FreeNativeMagic);
        Assert.Empty(Actor(restored).MagicStocks);
    }

    [Fact]
    public void InnateFireCompletesWithPlayerMagicFormulaAndRestoresItsPresentationBinding()
    {
        BattleSession battle = Active(ready: true);
        battle.QueueGuardianFormAction("actor", "enemy", BattleGuardianFormAction.Fire);
        bool capturedPresentation = false;
        for (int tick = 0; tick < 100 && (battle.QueuedActions.Count != 0 || battle.ActiveAction is not null || Actor(battle).IsBusy); tick++)
        {
            battle.Tick();
            if (battle.ActiveAction is { } action)
            {
                capturedPresentation = true;
                Assert.Equal(BattleGuardianFormAction.Fire, action.GuardianFormAction);
                Assert.True(action.FreeNativeMagic);
                battle = RoundTrip(battle.Capture());
            }
        }
        Assert.True(capturedPresentation);
        Assert.Empty(battle.QueuedActions);
        Assert.Null(battle.ActiveAction);
        Assert.Empty(Actor(battle).MagicStocks);
        BattleEvent damage = Assert.Single(battle.Events, item => item.Kind == "Damage" && item.ActorId == "actor" && item.TargetId == "enemy");
        Assert.Contains(damage.Amount, Enumerable.Range(0, 256).Select(random => NativeBattleDamage.Magic(31, 5, 18, random, false)));
        Assert.DoesNotContain(damage.Amount, Enumerable.Range(0, 256).Select(random => NativeBattleDamage.Magic(31, 5, 18, random, true)));
        Assert.Equal(37, battle.Capture().Combatants.Single(item => item.Id == "actor").CurrentHp);
        Assert.Equal(80, Assert.Single(battle.GuardianForces).HitPoints);
    }

    [Theory]
    [InlineData(BattleAtbMode.Normal, false)]
    [InlineData(BattleAtbMode.Normal, true)]
    [InlineData(BattleAtbMode.TurnBased, false)]
    [InlineData(BattleAtbMode.TurnBased, true)]
    public void LifetimeTracksAdvancingSimulationEvenWithFullAtb(BattleAtbMode mode, bool ready)
    {
        BattleSession battle = Active(ready: ready);
        BattleSnapshot boundary = battle.Capture();
        boundary.Combatants[1] = boundary.Combatants[1] with { Atb = BattleSession.AtbMaximum - 1 };
        battle = BattleSession.Restore(boundary);
        battle.SetMode(mode);
        long frame = battle.Frame;
        if (mode == BattleAtbMode.TurnBased)
        {
            battle.AdvanceTurn(1);
        }
        else
        {
            battle.Tick();
        }
        Assert.Equal(frame + 1, battle.Frame);
        Assert.Equal(72, battle.GetGuardianForm("actor")!.RemainingTicks);
        Assert.Equal(72, RoundTrip(battle.Capture()).GetGuardianForm("actor")!.RemainingTicks);
    }

    [Theory]
    [InlineData(BattleGuardianFormPhase.Charging)]
    [InlineData(BattleGuardianFormPhase.Finishing)]
    public void SavedTransitionalPhaseRequiresItsOwnChargeOrAction(BattleGuardianFormPhase phase)
    {
        BattleSnapshot saved = Active().Capture();
        BattleGuardianFormState form = Assert.Single(saved.GuardianForms!.Forms) with { Phase = phase, EntryCompleted = phase == BattleGuardianFormPhase.Finishing, FinisherAdmitted = phase == BattleGuardianFormPhase.Finishing };
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => BattleSession.Restore(saved with { GuardianForms = saved.GuardianForms with { Forms = [form] } }));
        Assert.Contains("matching charge or action owner", error.Message);
    }

    [Fact]
    public void ExplicitActorKoReturnsWithNoRetainedHumanRevival()
    {
        BattleSnapshot saved = Active(new() { Kind = BattleStatusKind.Poison, DurationFrames = 100, TickIntervalFrames = 1, Magnitude = 100 }, secondOwner: true).Capture();
        BattleCombatantSnapshot actor = saved.Combatants[0];
        Assert.NotNull(actor.NativeStatus);
        saved.Combatants[0] = actor with { NativeStatus = actor.NativeStatus! with { Saved = (ushort)(actor.NativeStatus.Saved | 1) } };
        BattleSession battle = BattleSession.Restore(saved);
        battle.Tick();
        Assert.Null(battle.GetGuardianForm("actor"));
        Assert.False(Actor(battle).IsAlive);
        Assert.Equal(0, Actor(battle).CurrentHp);
    }

    [Fact]
    public void ActiveFormRejectsClassicAndHumanCommandsWithoutChangingState()
    {
        BattleSession battle = Active(ready: true);
        string saved = JsonSerializer.Serialize(battle.Capture());
        Assert.Throws<InvalidOperationException>(() => battle.QueueAction("actor", "enemy", BattleAbility.GuardianForce, 2));
        Assert.Throws<InvalidOperationException>(() => battle.QueueAction("actor", "enemy", BattleAbility.Magic, 1));
        Assert.Throws<InvalidOperationException>(() => battle.QueueAttack("actor", "enemy"));
        Assert.False(battle.CanRequestGuardianForm("actor", 2));
        Assert.Equal(saved, JsonSerializer.Serialize(battle.Capture()));
    }

    [Fact]
    public void GuardianOwnedByFormCannotAlsoBeSummonedByAnotherActor()
    {
        BattleSession battle = Active(secondOwner: true);
        string saved = JsonSerializer.Serialize(battle.Capture());
        Assert.False(battle.CanRequestGuardianForm("other", 2));
        Assert.Throws<InvalidOperationException>(() => battle.QueueAction("other", "enemy", BattleAbility.GuardianForce, 2));
        Assert.Equal(saved, JsonSerializer.Serialize(battle.Capture()));
    }

    [Theory]
    [InlineData("unknown", 73)]
    [InlineData("ifrit-v1", -1)]
    [InlineData("ifrit-v1", 100000)]
    public void InvalidSavedFeatureStateFailsSpecifically(string profile, int ticks)
    {
        BattleSnapshot saved = Active().Capture();
        BattleGuardianFormState form = Assert.Single(saved.GuardianForms!.Forms) with { ProfileId = profile, RemainingTicks = ticks };
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => BattleSession.Restore(saved with { GuardianForms = saved.GuardianForms with { Forms = [form] } }));
        Assert.Contains("GF transformation", error.Message);
    }

    private static BattleSession RoundTrip(BattleSnapshot snapshot)
    {
        return BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(snapshot))!);
    }

    private static BattleCombatant Actor(BattleSession battle)
    {
        return battle.Combatants.Single(actor => actor.Id == "actor");
    }

    private static BattleSession Active(BattleStatusEffectDefinition? effect = null, bool ready = false, bool secondOwner = false)
    {
        BattleDefinition definition = Definition();
        definition.Combatants[0] = definition.Combatants[0] with { InitialAtb = ready ? BattleSession.AtbMaximum : 0, StatusEffects = effect is null ? [] : [effect] };
        if (secondOwner)
        {
            definition = definition with { Combatants = [definition.Combatants[0], new() { Id = "other", Name = "Other", IsPlayer = true, MaximumHp = 100, CurrentHp = 100, InitialAtb = BattleSession.AtbMaximum, Abilities = [BattleAbility.GuardianForce], EquipmentIds = [2] }, definition.Combatants[1]] };
        }
        BattleSession battle = new(definition, BattleAtbMode.Normal);
        return BattleSession.Restore(battle.Capture() with
        {
            GuardianForms = new(true, 1, [new("actor", 2, "ifrit-v1", BattleGuardianFormPhase.Active, 1, 73) { EntryCompleted = true }])
        });
    }

    private static BattleDefinition Definition()
    {
        BattleNativeAction physical = Native(8, 79, 1, 14, null);
        BattleNativeAction fire = Native(2, 1, 2, 18, 0);
        return new()
        {
            GuardianFormsEnabled = true,
            GuardianFormResources = [new(2, physical, 12, new([0, 0]) { AnimationFrames = [] })],
            GuardianForces = [new(2, 80, 100, true) { DeathCount = 0 }],
            NativeActions = [physical, fire],
            NativeCommandSequences = NativeBattleCommandSequenceFixture.Values,
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            ActionDefinitions = [new(BattleAbility.Magic, 1, "Fire", BattleActionEffect.Native, 18, false) { NativeAction = fire }, new(BattleAbility.GuardianForce, 2, "Hell Fire", BattleActionEffect.Damage, 20, false)],
            Combatants =
            [
                new() { Id = "actor", Name = "Actor", IsPlayer = true, ModelId = 0, MaximumHp = 100, CurrentHp = 37, Attack = 23, Magic = 31, ElementDefenses = [700, 800, 900, 1000, 800, 800, 800, 800], Abilities = [BattleAbility.Attack, BattleAbility.Magic, BattleAbility.GuardianForce], EquipmentIds = [2], GuardianForceCompatibility = new ushort[16], Speed = 1 },
                new() { Id = "enemy", Name = "Enemy", MaximumHp = 1000, CurrentHp = 1000, Speed = 1 }
            ]
        };
    }

    private static BattleNativeAction Native(byte command, ushort id, byte type, byte power, byte? targets)
    {
        return new(command, id, command == 2 ? 1 : 3, id)
        {
            MagicId = 0,
            AttackType = type,
            Power = power,
            HitCount = 1,
            TargetFlags = targets,
            AttackFlags = 0,
            DamagePresentation = 0,
            Element = 0,
            ElementPercent = null,
            StatusAttack = 0,
            Statuses0 = 0,
            Statuses1 = 0,
            Hit = null,
            Critical = null,
            PowerModifier = null,
            LevelModifier = null,
            RawRecord = new byte[command == 2 ? 64 : 20].ToImmutableArray(),
            SupplementalRecord = []
        };
    }
}
