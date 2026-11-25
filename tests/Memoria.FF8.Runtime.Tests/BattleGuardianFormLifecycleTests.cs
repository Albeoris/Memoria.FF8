using System.Text.Json;
using System.Collections.Immutable;
using System.Reflection;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class BattleGuardianFormLifecycleTests
{
    [Theory]
    [InlineData(1, 70, BattleGuardianFormAction.Attack, BattleGuardianFormAction.Fire, 1)]
    [InlineData(14, 100, BattleGuardianFormAction.Attack, BattleGuardianFormAction.Fire, 1)]
    [InlineData(15, 1, BattleGuardianFormAction.HeavyStrike, BattleGuardianFormAction.Fire, 1)]
    [InlineData(29, 99, BattleGuardianFormAction.HeavyStrike, BattleGuardianFormAction.Fire, 1)]
    [InlineData(30, 1, BattleGuardianFormAction.HeavyStrike, BattleGuardianFormAction.Fira, 2)]
    [InlineData(69, 70, BattleGuardianFormAction.HeavyStrike, BattleGuardianFormAction.Fira, 2)]
    [InlineData(70, 1, BattleGuardianFormAction.HeavyStrike, BattleGuardianFormAction.Firaga, 3)]
    [InlineData(99, 30, BattleGuardianFormAction.HeavyStrike, BattleGuardianFormAction.Firaga, 3)]
    [InlineData(100, 1, BattleGuardianFormAction.HeavyStrike, BattleGuardianFormAction.Firaga, 3)]
    [InlineData(120, 1, BattleGuardianFormAction.HeavyStrike, BattleGuardianFormAction.Firaga, 3)]
    public void IfritCommandsUseHisOwnLevelWhileHisAttacksUseCasterStatistics(int gfLevel, int casterLevel, BattleGuardianFormAction physical, BattleGuardianFormAction magic, int spellId)
    {
        BattleDefinition definition = SummonDefinition(gfLevel);
        definition.Combatants[0] = definition.Combatants[0] with { Level = casterLevel };
        BattleSession battle = ReadySummonForm(definition);
        Assert.Equal(BattleGuardianFormPhase.Active, battle.GetGuardianForm("actor")!.Phase);
        Assert.Equal(casterLevel, Actor(battle).Level);
        Assert.Equal(23, Actor(battle).Attack);
        Assert.Equal(31, Actor(battle).Magic);
        BattleGuardianFormAction[] expected = gfLevel < 100 ? [physical, magic, BattleGuardianFormAction.HellFire] : [physical, magic, BattleGuardianFormAction.Flare, BattleGuardianFormAction.HellFire];
        Assert.Equal(expected, battle.GetGuardianFormActions("actor"));
        Actor(battle).Atb = BattleSession.AtbMaximum;
        BattleGuardianFormAction unavailable = physical == BattleGuardianFormAction.Attack ? BattleGuardianFormAction.HeavyStrike : BattleGuardianFormAction.Attack;
        Assert.Throws<InvalidOperationException>(() => battle.QueueGuardianFormAction("actor", "enemy", unavailable));
        Assert.Equal(BattleSession.AtbMaximum, Actor(battle).Atb);
        Assert.Empty(battle.QueuedActions);
        battle.QueueGuardianFormAction("actor", "enemy", magic);
        battle = RoundTrip(battle.Capture());
        BattleAttackCommand command = Assert.Single(battle.QueuedActions);
        Assert.Equal(spellId, command.ActionId);
        Assert.Equal((byte)11, command.NativeSequence);
        Assert.True(command.FreeNativeMagic);
        Assert.Empty(Actor(battle).MagicStocks);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PartySummonSlotStaysOccupiedAcrossChargeAndQueuedActionRestores(bool transform)
    {
        BattleDefinition definition = SummonDefinition(15);
        BattleSession battle = new(definition, BattleAtbMode.Normal);
        int first = transform ? 2 : 0;
        int second = transform ? 0 : 2;
        string firstActor = transform ? "actor" : "other";
        string secondActor = transform ? "other" : "actor";
        if (transform)
        {
            battle.RequestGuardianForm(firstActor, first);
        }
        else
        {
            battle.QueueAction(firstActor, "enemy", BattleAbility.GuardianForce, first);
        }
        battle = RoundTrip(battle.Capture());
        Assert.True(battle.IsGuardianSummonOccupied);
        Assert.False(battle.CanSelectExpandedGuardianForce(secondActor, second));
        Assert.False(battle.CanRequestGuardianForm("actor", 2));
        Assert.Throws<InvalidOperationException>(() => battle.QueueAction(secondActor, "enemy", BattleAbility.GuardianForce, second));
        Assert.True(battle.CanChoosePlayerCommand(secondActor));
        Assert.Equal(BattleSession.AtbMaximum, battle.Combatants.Single(actor => actor.Id == secondActor).Atb);
        Assert.Single(battle.GuardianForceCasts);
        if (transform)
        {
            battle = ReadySummonForm(definition);
            Assert.Equal(BattleGuardianFormPhase.Active, battle.GetGuardianForm(firstActor)!.Phase);
            battle.QueueGuardianFormAction(firstActor, "enemy", BattleGuardianFormAction.HellFire);
            battle = RoundTrip(battle.Capture());
            Assert.Equal(BattleGuardianFormPhase.Finishing, battle.GetGuardianForm(firstActor)!.Phase);
        }
        else
        {
            RunLogicalStep(battle, "DispatchExpandedGuardianForceCharges");
            Assert.Empty(battle.GuardianForceCasts);
            battle = RoundTrip(battle.Capture());
        }
        Assert.Single(battle.QueuedActions);
        Assert.True(battle.IsGuardianSummonOccupied);
        Assert.False(battle.CanSelectExpandedGuardianForce(secondActor, second));
    }

    [Fact]
    public void GuardianDefeatAndChargeCancellationFreeTheSlotForAnotherGuardian()
    {
        BattleDefinition definition = SummonDefinition(19);
        BattleSession battle = new(definition, BattleAtbMode.Normal);
        battle.RequestGuardianForm("actor", 2);
        Actor(battle).CurrentHp = 0;
        Actor(battle).NativeStatus0 |= 1;
        RunLogicalStep(battle, "DispatchGuardianForceCasts");
        RunLogicalStep(battle, "DispatchExpandedGuardianForceCharges");
        Assert.False(battle.IsGuardianSummonOccupied);
        Assert.True(battle.CanSelectExpandedGuardianForce("other", 0));

        battle = ReadySummonForm(definition);
        Actor(battle).CurrentHp = 0;
        RunLogicalStep(battle, "DispatchGuardianFormTransitions");
        Assert.Null(battle.GetGuardianForm("actor"));
        Assert.Equal(37, Actor(battle).CurrentHp);
        Assert.False(battle.IsGuardianSummonOccupied);
        Assert.True(battle.CanSelectExpandedGuardianForce("other", 0));
        battle.QueueAction("other", "enemy", BattleAbility.GuardianForce, 0);
        Assert.Equal(0, Assert.Single(battle.GuardianForceCasts).Command.ActionId);
    }

    [Fact]
    public void NormalFinisherCompletionReturnsIfritAndAllowsAnotherGuardian()
    {
        BattleSession battle = ReadySummonForm(SummonDefinition(20));
        battle.QueueGuardianFormAction("actor", "enemy", BattleGuardianFormAction.HellFire);
        BattleAttackCommand queued = Assert.Single(battle.QueuedActions);
        BattleActionPresentation committed = new(queued.ActorId, queued.TargetId, BattleActionPhase.Recovery, queued.TargetRequest, queued.Ability, queued.ActionId)
        {
            GuardianFormAction = queued.GuardianFormAction,
            GuardianFormInvocation = queued.GuardianFormInvocation,
            NativeAction = queued.NativeAction,
            NativeQueuedCommand = queued.NativeQueuedCommand,
            NativeSequence = queued.NativeSequence
        };
        battle = RoundTrip(battle.Capture() with { QueuedActions = [], ActiveAction = committed });
        Assert.True(battle.IsGuardianSummonOccupied);
        RunLogicalStep(battle, "CompleteActionPresentation", committed, "Finisher completed.", true);
        Assert.Null(battle.GetGuardianForm("actor"));
        Assert.False(battle.IsGuardianSummonOccupied);
        battle.QueueAction("other", "enemy", BattleAbility.GuardianForce, 0);
        Assert.Equal(0, Assert.Single(battle.GuardianForceCasts).Command.ActionId);
    }

    [Theory]
    [InlineData(14, 19, BattleGuardianFormAction.Attack, 79, true)]
    [InlineData(19, 19, BattleGuardianFormAction.HeavyStrike, 59, false)]
    [InlineData(29, 31, BattleGuardianFormAction.Fire, 1, false)]
    public void OlderStandaloneFormCheckpointKeepsItsAlreadyAdmittedNativeAction(int queuedLevel, int restoredLevel, BattleGuardianFormAction action, int actionId, bool legacyBands)
    {
        BattleSession battle = ReadySummonForm(SummonDefinition(queuedLevel));
        battle.QueueGuardianFormAction("actor", "enemy", action);
        BattleSnapshot saved = battle.Capture();
        BattleDefinition oldDefinition = saved.Definition with
        {
            GuardianFormResources = [saved.Definition.GuardianFormResources[0] with { InnateBindings = default, AbilityBands = legacyBands ? IfritBands() : null }],
            ActionDefinitions = saved.Definition.ActionDefinitions.Select(definition => definition is { Ability: BattleAbility.GuardianForce, Id: 2 } ? definition with { Level = restoredLevel } : definition).ToArray()
        };
        saved = saved with { Definition = oldDefinition };

        BattleSession restored = RoundTrip(saved);
        Assert.Contains(BattleGuardianFormAction.HeavyStrike, restored.GetGuardianFormActions("actor"));
        Assert.Equal(actionId, Assert.Single(restored.QueuedActions).ActionId);
        Assert.NotEmpty(restored.Capture().Definition.GuardianFormResources[0].InnateBindings);
        Assert.Null(restored.Capture().Definition.GuardianFormResources[0].AbilityBands);
        RunLogicalStep(restored, "DispatchAction");
        BattleActionPresentation windup = Assert.IsType<BattleActionPresentation>(restored.ActiveAction);
        Assert.Equal(action, windup.GuardianFormAction);
        BattleActionDefinition resolved = Assert.IsType<BattleActionDefinition>(RunLogicalStep(restored, "ExpandedActionDefinition", Actor(restored), windup));
        Assert.Equal(actionId, resolved.Id);
        Assert.Equal(windup.NativeAction, resolved.NativeAction);
    }

    [Fact]
    public void LegacyCheckpointWithActiveIfritAndAnotherChargeReportsConflictingOwners()
    {
        BattleDefinition definition = SummonDefinition(19);
        BattleSession battle = ReadySummonForm(definition);
        BattleSnapshot overlap = battle.Capture();

        BattleSession classic = new(definition, BattleAtbMode.Normal);
        classic.QueueAction("other", "enemy", BattleAbility.GuardianForce, 0);
        BattleSnapshot charged = classic.Capture();
        overlap.Combatants[1] = charged.Combatants[1];
        overlap = overlap with { GuardianForceCasts = charged.GuardianForceCasts };

        InvalidDataException error = Assert.Throws<InvalidDataException>(() => RoundTrip(overlap));
        Assert.Contains("overlapping GF summons", error.Message);
    }

    [Fact]
    public void ExpandedIfritChargesAndUsesHisInstalledCommandsAgainstTheLastEnemy()
    {
        BattleDefinition initial = Definition();
        BattleNativeAction hellFire = Native(254, 66, 11, 20, 0x40) with { KernelSection = 2, KernelIndex = 2, PowerModifier = 10, LevelModifier = 10, RawRecord = [.. new byte[128]] };
        BattleDefinition definition = initial with
        {
            NativeBattleSpeed = 0,
            NativeActions = [.. initial.NativeActions, hellFire],
            ActionDefinitions = [initial.ActionDefinitions[0], initial.ActionDefinitions[1] with { NativeAction = hellFire }],
            Combatants = [initial.Combatants[0] with { InitialAtb = BattleSession.AtbMaximum }, .. Enumerable.Range(0, 7).Select(index => initial.Combatants[1] with { Id = $"enemy-{index}", Speed = 1 })]
        };
        BattleSession battle = new(definition, BattleAtbMode.Normal);
        battle.RequestGuardianForm("actor", 2);
        Assert.Equal(BattleGuardianFormPhase.Charging, battle.GetGuardianForm("actor")!.Phase);
        battle = RoundTrip(battle.Capture());
        battle.Tick();
        Assert.Equal(61, battle.GetGuardianFormVisual("actor"));
        Assert.Equal(80, Actor(battle).CurrentHp);
        foreach (BattleGuardianFormAction action in new[] { BattleGuardianFormAction.Attack, BattleGuardianFormAction.Fire, BattleGuardianFormAction.HellFire })
        {
            Actor(battle).Atb = BattleSession.AtbMaximum;
            battle.QueueGuardianFormAction("actor", "enemy-6", action);
            BattleAttackCommand queued = Assert.Single(battle.QueuedActions);
            Assert.Equal("enemy-6", queued.TargetId);
            if (action == BattleGuardianFormAction.Attack)
            {
                BattleTargetSelection.Explicit selected = Assert.IsType<BattleTargetSelection.Explicit>(queued.TargetRequest.Selection);
                Assert.Equal(new BattleEnemyActorReference("enemy-6"), Assert.Single(selected.Actors));
            }
            battle = RoundTrip(battle.Capture());
            int before = battle.Events.Count;
            for (int frame = 0; frame < 32 && Actor(battle).IsBusy; frame++)
            {
                battle.Tick();
            }
            Assert.Contains(battle.Events.Skip(before), item => item.Kind == "EffectContact" && item.TargetId == "enemy-6");
        }
        Assert.Null(battle.GetGuardianForm("actor"));
        Assert.Equal(37, Actor(battle).CurrentHp);
    }
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
        AssertCasterStatistics(actor);
        Assert.Equal(new[] { 1000, 700, 800, 800, 800, 800, 800, 800 }, actor.ElementDefenses);
        Assert.Equal(2, restored.Combatants.Count);
        Assert.Equal(73, restored.GetGuardianForm("actor")!.RemainingTicks);
    }

    [Theory]
    [InlineData(0, 1, -100)]
    [InlineData(1, 2, 200)]
    public void ActiveAndRestoredFormsUsePrototypeElementalDamage(int element, byte mask, int expected)
    {
        BattleSession battle = Active();
        Assert.Equal(expected, NativeBattleDamage.Element(100, mask, Actor(battle).ElementDefenses[element], false, 0));
        BattleSession restored = RoundTrip(battle.Capture());
        Assert.Equal(expected, NativeBattleDamage.Element(100, mask, Actor(restored).ElementDefenses[element], false, 0));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, false)]
    public void QueuedNativeElementalMagicChangesOnlyTheCanonicalGuardianHp(byte element, bool absorbed)
    {
        BattleSnapshot saved = Active(secondOwner: true).Capture();
        BattleNativeAction native = Native(2, 1, 2, 18, 16) with { AttackFlags = 1, Element = element };
        BattleDefinition definition = saved.Definition with
        {
            NativeActions = [saved.Definition.GuardianFormResources[0].PhysicalAction, native],
            ActionDefinitions = [new(BattleAbility.Magic, 1, absorbed ? "Fire" : "Ice", BattleActionEffect.Native, 18, true) { NativeAction = native }, saved.Definition.ActionDefinitions[1]],
            GuardianForces = [new(2, 8000, 10000, true) { DeathCount = 0 }]
        };
        definition.Combatants[1] = definition.Combatants[1] with { Magic = 20, MagicStocks = [new(1, 3)], Abilities = [BattleAbility.Magic] };
        NativeMagicStock[] inventory = saved.Combatants[1].NativeMagicInventory!.ToArray();
        inventory[0] = new(1, 3);
        saved.Combatants[1] = saved.Combatants[1] with { NativeMagicInventory = inventory };
        saved = saved with { Definition = definition, GuardianForces = definition.GuardianForces };
        BattleSession battle = RoundTrip(saved);
        battle.QueueAction("other", "actor", BattleAbility.Magic, 1);
        for (int frame = 0; frame < 1200 && !(battle.ActiveAction is null && battle.QueuedActions.Count == 0); frame++)
        {
            battle.Tick();
        }
        int difference = Actor(battle).CurrentHp - 8000;
        Assert.True(absorbed ? difference > 0 : difference < 0);
        int multiplier = absorbed ? 1 : 2;
        Assert.Contains(Math.Abs(difference), Enumerable.Range(0, 256).Select(random => multiplier * NativeBattleDamage.Magic(20, Actor(battle).Spirit, 18, random, false)));
        Assert.Equal(Actor(battle).CurrentHp, Assert.Single(battle.GuardianForces).HitPoints);
        Assert.Equal(37, battle.Capture().Combatants[0].CurrentHp);
        Assert.Equal(BattleGuardianFormPhase.Active, battle.GetGuardianForm("actor")!.Phase);
        BattleSession restored = RoundTrip(battle.Capture());
        Assert.Equal(Actor(battle).CurrentHp, Actor(restored).CurrentHp);
        Assert.Equal(37, restored.Capture().Combatants[0].CurrentHp);
        AssertCasterStatistics(Actor(restored));
    }

    [Fact]
    public void EnteringKeepsHumanDefensesAndRestoredDepletionReturnsTheirOriginalBanks()
    {
        BattleSnapshot entering = Active().Capture();
        BattleGuardianFormState form = Assert.Single(entering.GuardianForms!.Forms);
        entering = entering with { GuardianForms = entering.GuardianForms with { Forms = [form with { Phase = BattleGuardianFormPhase.Entering, EntryCompleted = false, RemainingTicks = 0 }] } };
        BattleSession beforeEntry = RoundTrip(entering);
        int[] humanElements = Definition().Combatants[0].ElementDefenses;
        Assert.Equal(humanElements, Actor(beforeEntry).ElementDefenses);
        Assert.Equal(37, Actor(beforeEntry).CurrentHp);
        AssertCasterStatistics(Actor(beforeEntry));

        BattleSession active = Active();
        BattleSnapshot saved = active.Capture();
        Assert.Equal(humanElements, saved.Definition.Combatants[0].ElementDefenses);
        Assert.Equal(humanElements, saved.Combatants[0].NativeParticipant!.ElementDefenses);
        Assert.Equal(Enumerable.Repeat((byte)0, 40), saved.Combatants[0].NativeStatus!.Resistances);
        BattleSession restored = RoundTrip(saved);
        Assert.Equal(BattleGuardianFormPhase.Active, restored.GetGuardianForm("actor")!.Phase);
        Assert.Equal(37, Actor(restored).CurrentHp);
        Assert.Equal(humanElements, restored.Capture().Definition.Combatants[0].ElementDefenses);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativePoisonConsumerUsesPrototypeImmunityOnlyWhileTransformed(bool transformed)
    {
        BattleSnapshot saved = Active(secondOwner: true).Capture();
        BattleNativeAction poison = Native(2, 1, 2, 0, 16) with { AttackFlags = 1, StatusAttack = 250, Statuses0 = 2 };
        BattleDefinition definition = saved.Definition with
        {
            NativeStatusRules = new(0, Enumerable.Repeat((byte)30, 14).ToImmutableArray()),
            NativeActions = [saved.Definition.GuardianFormResources[0].PhysicalAction, poison],
            ActionDefinitions = [new(BattleAbility.Magic, 1, "Poison", BattleActionEffect.Status, 0, true) { NativeAction = poison }, saved.Definition.ActionDefinitions[1]]
        };
        definition.Combatants[1] = definition.Combatants[1] with { MagicStocks = [new(1, 3)], Abilities = [BattleAbility.Magic] };
        NativeMagicStock[] inventory = saved.Combatants[1].NativeMagicInventory!.ToArray();
        inventory[0] = new(1, 3);
        saved.Combatants[1] = saved.Combatants[1] with { NativeMagicInventory = inventory };
        saved = saved with { Definition = definition, GuardianForms = transformed ? saved.GuardianForms : new(true, 1, []) };
        BattleSession battle = RoundTrip(saved);
        battle.QueueAction("other", "actor", BattleAbility.Magic, 1);
        for (int frame = 0; frame < 1200 && !(battle.ActiveAction is null && battle.QueuedActions.Count == 0); frame++)
        {
            battle.Tick();
        }
        NativeBattleStatusSnapshot status = battle.Capture().Combatants[0].NativeStatus!;
        Assert.Equal(!transformed, (status.Saved & 2) != 0);
        Assert.Equal(Enumerable.Repeat((byte)0, 40), status.Resistances);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ActiveFormRequiresCompletePrototypeDefenseResources(bool missingElements)
    {
        BattleSnapshot saved = Active().Capture();
        BattleGuardianFormResources resources = saved.Definition.GuardianFormResources[0];
        resources = missingElements ? resources with { ElementDefenses = [] } : resources with { StatusResistances = [] };
        saved = saved with { Definition = saved.Definition with { GuardianFormResources = [resources] } };
        Assert.Throws<NotSupportedException>(() => BattleSession.Restore(saved));
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
    public void Debug_victory_does_not_cancel_the_admitted_fire()
    {
        BattleSession battle = Active(ready: true);
        battle.QueueGuardianFormAction("actor", "enemy", BattleGuardianFormAction.Fire);
        battle.Tick();
        var header = battle.Presentation!.NativeActionPackets.ActiveHeader;
        Assert.NotNull(header);
        BattleActionPresentation? action = battle.ActiveAction;

        Assert.Throws<InvalidOperationException>(battle.InstantVictory);
        Assert.Equal(BattleOutcome.Active, battle.Outcome);
        Assert.Equal(header, battle.Presentation.NativeActionPackets.ActiveHeader);
        Assert.Same(action, battle.ActiveAction);
        Assert.NotNull(battle.GetGuardianForm("actor"));
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
        Assert.NotNull(battle.Presentation!.NativeActionPackets.ActiveHeader);
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
        BattleSnapshot saved = Active(secondOwner: true).Capture();
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
        return BattleSession.Restore(snapshot);
    }

    private static void AssertCasterStatistics(BattleCombatant actor)
    {
        Assert.Equal(17, actor.Defense);
        Assert.Equal(19, actor.Spirit);
        Assert.Equal(3, actor.Speed);
        Assert.Equal(11, actor.Level);
        Assert.Equal(13, actor.Luck);
        Assert.Equal(93, actor.Hit);
        Assert.Equal(5, actor.Evasion);
    }

    private static BattleCombatant Actor(BattleSession battle)
    {
        return battle.Combatants.Single(actor => actor.Id == "actor");
    }

    private static BattleSession Active(bool ready = false, bool secondOwner = false, bool berserk = false)
    {
        BattleDefinition definition = Definition();
        definition.Combatants[0] = definition.Combatants[0] with { InitialAtb = ready ? BattleSession.AtbMaximum : 0 };
        if (berserk)
        {
            definition.Combatants[0] = definition.Combatants[0] with { NativeStatus0 = 0x20, NativeAttack = Native(1, 0, 10, 255, null), MagicStocks = [new(1, 3)] };
            definition = definition with { Items = [new(1, 5)] };
        }
        if (secondOwner)
        {
            definition = definition with { Combatants = [definition.Combatants[0], new() { Id = "other", Name = "Other", IsPlayer = true, MaximumHp = 100, CurrentHp = 100, InitialAtb = BattleSession.AtbMaximum, Abilities = [BattleAbility.GuardianForce], EquipmentIds = [2], GuardianForceCompatibility = new ushort[16] }, definition.Combatants[1]] };
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
            GuardianFormResources = [new(2, physical, 12, new([0, 0]) { AnimationFrames = [] }) { AbilityBands = IfritBands(), ElementDefenses = [1000, 700, 800, 800, 800, 800, 800, 800], StatusResistances = Enumerable.Range(0, 40).Select(index => index == 1 ? (byte)255 : (byte)100).ToImmutableArray() }],
            GuardianForces = [new(2, 80, 100, true) { DeathCount = 0 }],
            NativeActions = [physical, fire],
            NativeCommandSequences = NativeBattleCommandSequenceFixture.Values,
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            ActionDefinitions = [new(BattleAbility.Magic, 1, "Fire", BattleActionEffect.Native, 18, false) { NativeAction = fire }, new(BattleAbility.GuardianForce, 2, "Hell Fire", BattleActionEffect.Damage, 20, false)],
            Combatants =
            [
                new() { Id = "actor", Name = "Actor", IsPlayer = true, ModelId = 0, MaximumHp = 100, CurrentHp = 37, Attack = 23, Magic = 31, Defense = 17, Spirit = 19, Speed = 3, Level = 11, Luck = 13, Hit = 93, Evasion = 5, ElementDefenses = [700, 800, 900, 1000, 800, 800, 800, 800], NativeStatusResistances = new byte[40], Abilities = [BattleAbility.Attack, BattleAbility.Magic, BattleAbility.GuardianForce], EquipmentIds = [2], GuardianForceCompatibility = new ushort[16] },
                new() { Id = "enemy", Name = "Enemy", MaximumHp = 1000, CurrentHp = 1000, Speed = 1 }
            ]
        };
    }

    private static BattleGuardianFormAbilityBands IfritBands()
    {
        BattleGuardianFormActionBinding attack = new(BattleGuardianFormAction.Attack, "Attack", BattleGuardianFormActionSource.Physical, 79, 12);
        BattleGuardianFormActionBinding fire = new(BattleGuardianFormAction.Fire, "Fire", BattleGuardianFormActionSource.Magic, 1, 11, BattleGuardianFormActionCost.Innate);
        BattleGuardianFormActionBinding heavy = new(BattleGuardianFormAction.HeavyStrike, "Heavy Strike", BattleGuardianFormActionSource.Physical, 59, 12);
        BattleGuardianFormActionBinding finisher = BattleGuardianFormProfiles.Find(2)!.Finisher;
        return new(20, 30, [[attack, fire, heavy, finisher], [attack, fire, finisher], [attack, fire, finisher]]);
    }

    private static BattleDefinition SummonDefinition(int level)
    {
        BattleDefinition initial = Definition();
        BattleNativeAction ifrit = Native(254, 66, 11, 20, 0x40) with { KernelSection = 2, KernelIndex = 2, PowerModifier = 10, LevelModifier = 10, RawRecord = [.. new byte[128]] };
        BattleNativeAction quezacotl = ifrit with { ActionId = 64, KernelIndex = 0 };
        BattleNativeAction fira = Native(2, 2, 2, 24, 0x54);
        BattleNativeAction firaga = Native(2, 3, 2, 36, 0x54);
        BattleNativeAction flare = Native(2, 15, 2, 48, 0x54);
        return initial with
        {
            NativeBattleSpeed = 0,
            GuardianForces = [initial.GuardianForces[0], new(0, 100, 100)],
            NativeActions = [.. initial.NativeActions, Native(8, 59, 1, 20, null), ifrit, quezacotl, fira, firaga, flare],
            ActionDefinitions =
            [
                initial.ActionDefinitions[0],
                initial.ActionDefinitions[1] with { NativeAction = ifrit, Level = level },
                new(BattleAbility.GuardianForce, 0, "Thunder Storm", BattleActionEffect.Native, 20, false) { NativeAction = quezacotl },
                new(BattleAbility.Magic, 2, "Fira", BattleActionEffect.Native, fira.Power, false) { NativeAction = fira },
                new(BattleAbility.Magic, 3, "Firaga", BattleActionEffect.Native, firaga.Power, false) { NativeAction = firaga },
                new(BattleAbility.Magic, 15, "Flare", BattleActionEffect.Native, flare.Power, false) { NativeAction = flare }
            ],
            Combatants = [initial.Combatants[0] with { InitialAtb = BattleSession.AtbMaximum }, new() { Id = "other", Name = "Other", IsPlayer = true, MaximumHp = 100, CurrentHp = 100, InitialAtb = BattleSession.AtbMaximum, Abilities = [BattleAbility.Attack, BattleAbility.GuardianForce], EquipmentIds = [0], GuardianForceCompatibility = new ushort[16] }, initial.Combatants[1]]
        };
    }

    private static BattleSession ReadySummonForm(BattleDefinition definition)
    {
        BattleSession battle = new(definition, BattleAtbMode.Normal);
        return RoundTrip(battle.Capture() with
        {
            GuardianForms = new(true, 1, [new("actor", 2, "ifrit-v1", BattleGuardianFormPhase.Active, 1, 73) { EntryCompleted = true }])
        });
    }

    private static object? RunLogicalStep(BattleSession battle, string name, params object?[] arguments)
    {
        return typeof(BattleSession).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(battle, arguments);
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
