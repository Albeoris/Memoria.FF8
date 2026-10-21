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
    public void PhysicalFormActionUsesHeroStrengthAndItsOwnPowerWithoutGunbladeInput()
    {
        BattleSession battle = Active(ready: true);
        battle.QueueGuardianFormAction("actor", "enemy", BattleGuardianFormAction.Attack);
        battle.Tick();
        BattleActionPresentation action = Assert.IsType<BattleActionPresentation>(battle.ActiveAction);
        Assert.Equal((byte)1, action.NativeAction!.AttackType);
        Assert.Equal((byte)14, action.NativeAction.Power);
        Assert.Equal((byte)12, action.NativeSequence);
        BattleEvent damage = Assert.Single(battle.Events, item => item.Kind == "Damage" && item.ActorId == "actor");
        Assert.Contains(damage.Amount, Enumerable.Range(0, 256).Select(random => NativeBattleDamage.Physical(23, battle.Combatants.Single(actor => actor.Id == "enemy").Defense, 14, random)));
        Assert.False(battle.CanTriggerGunblade);
        Assert.Null(action.GunbladeTriggerTiming);
        Assert.Equal(80, Assert.Single(battle.GuardianForces).HitPoints);
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

    [Fact]
    public void DepletionRetainsOneCommittedFireThroughReturningAndJsonRestoration()
    {
        BattleSession battle = Active(new() { Kind = BattleStatusKind.Poison, DurationFrames = 2, TickIntervalFrames = 2, Magnitude = 100 }, ready: true);
        battle.QueueGuardianFormAction("actor", "enemy", BattleGuardianFormAction.Fire);
        battle.Tick();
        BattleActionPresentation admitted = Assert.IsType<BattleActionPresentation>(battle.ActiveAction);
        Assert.NotNull(admitted.NativeHeaderIndex);
        Assert.Equal(BattleGuardianFormAction.Fire, admitted.GuardianFormAction);
        int enemyHp = battle.Combatants.Single(actor => actor.Id == "enemy").CurrentHp;
        Assert.Single(battle.Events, item => item.Kind == "Damage" && item.ActorId == "actor" && item.TargetId == "enemy");
        battle.Tick();
        Assert.Equal(BattleGuardianFormPhase.Returning, battle.GetGuardianForm("actor")!.Phase);
        Assert.Equal(0, Assert.Single(battle.GuardianForces).HitPoints);
        Assert.Equal(37, Actor(battle).CurrentHp);
        Assert.Equal(admitted.NativeHeaderIndex, battle.ActiveAction!.NativeHeaderIndex);
        BattleSession restored = RoundTrip(battle.Capture());
        Assert.Equal(BattleGuardianFormPhase.Returning, restored.GetGuardianForm("actor")!.Phase);
        Assert.Equal(admitted.NativeHeaderIndex, restored.ActiveAction!.NativeHeaderIndex);
        for (int tick = 0; tick < 100 && (restored.ActiveAction is not null || restored.GetGuardianForm("actor") is not null); tick++)
        {
            restored.Tick();
        }
        Assert.Null(restored.ActiveAction);
        Assert.Null(restored.GetGuardianForm("actor"));
        Assert.Empty(restored.QueuedActions);
        Assert.False(Actor(restored).IsBusy);
        Assert.Equal(37, Actor(restored).CurrentHp);
        Assert.Equal(enemyHp, restored.Combatants.Single(actor => actor.Id == "enemy").CurrentHp);
        Assert.Single(restored.Events, item => item.Kind == "Damage" && item.ActorId == "actor" && item.TargetId == "enemy");
        Assert.Single(restored.Events, item => item.Kind == "GuardianFormReturned");
        Assert.Empty(Actor(restored).MagicStocks);
    }

    [Fact]
    public void DepletionDiscardsUncommittedFireWithoutAContactOrOffensiveTransaction()
    {
        BattleSession battle = Active(new() { Kind = BattleStatusKind.Poison, DurationFrames = 1, TickIntervalFrames = 1, Magnitude = 100 }, ready: true);
        battle.QueueGuardianFormAction("actor", "enemy", BattleGuardianFormAction.Fire);
        Assert.Null(Assert.Single(battle.QueuedActions).NativeHeaderIndex);
        battle.Tick();
        Assert.Null(battle.GetGuardianForm("actor"));
        Assert.Empty(battle.QueuedActions);
        Assert.Null(battle.ActiveAction);
        Assert.Equal(0, battle.NativeActionPackets.ActionCount);
        Assert.False(Actor(battle).IsBusy);
        Assert.Equal(0, Actor(battle).Atb);
        Assert.Equal(37, Actor(battle).CurrentHp);
        Assert.Equal(1000, battle.Combatants.Single(actor => actor.Id == "enemy").CurrentHp);
        Assert.DoesNotContain(battle.Events, item => item.Kind == "Damage" && item.ActorId == "actor");
        Assert.Empty(RoundTrip(battle.Capture()).QueuedActions);
    }

    [Fact]
    public void AdmittedCounterRetainsItsBodyUntilRecoveryCompletesAfterDepletion()
    {
        BattleDefinition definition = Definition();
        NativeCharacterVitals vitals = MagicSystemFixture.Create();
        byte[] character = new byte[36];
        for (int offset = 12; offset < 36; offset += 4)
        {
            character[offset + 1] = 1;
            character[offset + 2] = 100;
            character[offset + 3] = 1;
        }
        BattleNativePlayerStatRules rules = new(0, 1, character.ToImmutableArray(), new byte[12].ToImmutableArray(), Enumerable.Range(0, 57).Select(_ => new byte[60].ToImmutableArray()).ToImmutableArray(), Enumerable.Range(0, 19).Select(_ => new byte[8].ToImmutableArray()).ToImmutableArray(), new byte[20].ToImmutableArray(), 0, vitals.Definitions) { NativeStatusPassiveFlags = 4 };
        BattleNativeCharacterGrowth growth = new(90, new byte[6].ToImmutableArray());
        Assert.Equal(definition.Combatants[0].MaximumHp, rules.Project([], growth).MaximumHp);
        definition.Combatants[0] = definition.Combatants[0] with { NativePlayerStatRules = rules, NativeGrowth = growth, NativeStatusResistances = rules.ProjectStatusResistances([]).ToArray(), StatusEffects = [new() { Kind = BattleStatusKind.Poison, DurationFrames = 1, TickIntervalFrames = 1, Magnitude = 100 }] };
        BattleSnapshot snapshot = new BattleSession(definition, BattleAtbMode.Normal).Capture();
        NativeBattleActionPacketsState packets = snapshot.NativeActionPackets!;
        (packets, _) = packets.Prepare(new(0, 1, 12, -1, 0, 0, null));
        (packets, _) = packets.AppendContact(new(new(3, 0, 0, 4, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        BattleNativeAction counter = definition.GuardianFormResources[0].PhysicalAction with { Command = 1, DispatchCommand = 1, ActionId = 0, Name = "Attack" };
        // A factual Counter has already resolved its contact and now owns only its remaining recovery presentation.
        BattleActionPresentation admitted = new("actor", "enemy", BattleActionPhase.Recovery, 2, BattleAbility.Attack, 0)
        {
            NativeHeaderIndex = 0,
            NativeQueuedCommand = 1,
            NativeSequence = 12,
            NativeTargetMask = 8,
            NativeAction = counter,
            NativePlayerReaction = BattleNativePlayerReaction.Counter
        };
        BattleSession battle = BattleSession.Restore(snapshot with { NativeActionPackets = packets, ActiveAction = admitted, GuardianForms = new(true, 1, [new("actor", 2, "ifrit-v1", BattleGuardianFormPhase.Active, 1, 73) { EntryCompleted = true }]) });
        battle.Tick();
        Assert.Equal(BattleGuardianFormPhase.Returning, battle.GetGuardianForm("actor")!.Phase);
        Assert.Equal(BattleNativePlayerReaction.Counter, battle.ActiveAction!.NativePlayerReaction);
        Assert.Equal(61, battle.GetGuardianFormVisual("actor"));
        battle = RoundTrip(battle.Capture());
        Assert.Equal(BattleGuardianFormPhase.Returning, battle.GetGuardianForm("actor")!.Phase);
        battle.Tick();
        Assert.Null(battle.GetGuardianForm("actor"));
        Assert.Null(battle.ActiveAction);
        Assert.Equal(37, Actor(battle).CurrentHp);
        Assert.Equal(1000, battle.Combatants.Single(actor => actor.Id == "enemy").CurrentHp);
        Assert.DoesNotContain(battle.Events, item => item.Kind == "Damage" && item.ActorId == "actor");
        Assert.Single(battle.Events, item => item.Kind == "ActionComplete" && item.ActorId == "actor");
    }

    [Fact]
    public void InstantVictoryDropsAdmittedFireAndRestoresHumanWithoutAnotherTransaction()
    {
        BattleSession battle = Active(ready: true);
        battle.QueueGuardianFormAction("actor", "enemy", BattleGuardianFormAction.Fire);
        battle.Tick();
        Assert.NotNull(battle.ActiveAction!.NativeHeaderIndex);
        int damageCount = battle.Events.Count(item => item.Kind == "Damage" && item.ActorId == "actor");
        battle.InstantVictory();
        Assert.Equal(BattleOutcome.Victory, battle.Outcome);
        Assert.Null(battle.GetGuardianForm("actor"));
        Assert.Null(battle.GetGuardianFormVisual("actor"));
        Assert.Null(battle.NativeGraphics?.GuardianFormPresentation);
        Assert.Null(battle.ActiveAction);
        Assert.Empty(battle.QueuedActions);
        Assert.Equal(37, Actor(battle).CurrentHp);
        Assert.Equal(damageCount, battle.Events.Count(item => item.Kind == "Damage" && item.ActorId == "actor"));
    }

    [Fact]
    public void BerserkUsesTheFormPhysicalBindingWithoutHeroWeaponStockOrItems()
    {
        BattleSession battle = Active(ready: true, berserk: true);
        BattleStock[] stock = Actor(battle).MagicStocks.ToArray();
        BattleStock[] items = battle.Items.ToArray();
        battle.Tick();
        BattleActionPresentation action = Assert.IsType<BattleActionPresentation>(battle.ActiveAction);
        Assert.Equal(BattleGuardianFormAction.Attack, action.GuardianFormAction);
        Assert.Equal(BattleAbility.EnemyAbility, action.Ability);
        Assert.Equal((byte)8, action.NativeQueuedCommand);
        Assert.Equal(79, action.ActionId);
        Assert.Equal((ushort)79, action.NativeAction!.ActionId);
        Assert.Equal((byte)1, action.NativeAction.AttackType);
        Assert.Equal((byte)12, action.NativeSequence);
        Assert.NotNull(action.NativeHeaderIndex);
        Assert.Equal(stock, Actor(battle).MagicStocks);
        Assert.Equal(items, battle.Items);
        BattleSession restored = RoundTrip(battle.Capture());
        Assert.Equal(BattleGuardianFormAction.Attack, restored.ActiveAction!.GuardianFormAction);
        Assert.Equal((byte)12, restored.ActiveAction.NativeSequence);
        Assert.Equal(stock, Actor(restored).MagicStocks);
        Assert.Equal(items, restored.Items);
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

    private static BattleSession Active(BattleStatusEffectDefinition? effect = null, bool ready = false, bool secondOwner = false, bool berserk = false)
    {
        BattleDefinition definition = Definition();
        definition.Combatants[0] = definition.Combatants[0] with { InitialAtb = ready ? BattleSession.AtbMaximum : 0, StatusEffects = effect is null ? [] : [effect] };
        if (berserk)
        {
            definition.Combatants[0] = definition.Combatants[0] with { NativeStatus0 = 0x20, NativeAttack = Native(1, 0, 10, 255, null), MagicStocks = [new(1, 3)] };
            definition = definition with { Items = [new(1, 5)] };
        }
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
