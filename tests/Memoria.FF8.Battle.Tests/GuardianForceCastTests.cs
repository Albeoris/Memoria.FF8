using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Battle.Tests;

public sealed class GuardianForceCastTests
{
    [Fact]
    public void Turn_based_ready_ally_holds_charge_until_pass_reaches_completion_and_the_script_publishes_it()
    {
        BattleSession session = CreateTurnBasedWithReadyAlly(350);
        session.QueueAction("player", "enemy", BattleAbility.GuardianForce, 0);
        BattleSession restored = BattleSession.Restore(session.Capture());
        for (int callback = 0; callback < 20; callback++)
        {
            session.Tick();
            restored.Tick();
        }
        Assert.Equal(0, session.Frame);
        Assert.Equal((ushort)40, Assert.Single(session.GuardianForceCasts).RemainingTicks);
        Assert.True(session.Combatants.Single(actor => actor.Id == "ally").IsReady);
        Assert.Equal(0, session.Combatants.Single(actor => actor.Id == "enemy").Atb);
        Assert.Equal(JsonSerializer.Serialize(session.Capture()), JsonSerializer.Serialize(restored.Capture()));

        Assert.Equal(20, session.AdvanceTurn());
        Assert.Equal((ushort)0, Assert.Single(session.GuardianForceCasts).RemainingTicks);
        Assert.Empty(session.QueuedActions);
        Assert.Null(session.ActiveAction);
        Assert.Equal(20, session.Combatants.Single(actor => actor.Id == "enemy").Atb);
        BattleSession completed = BattleSession.Restore(session.Capture());
        session.Tick();
        completed.Tick();

        Assert.Equal(20, session.Frame);
        Assert.Empty(session.GuardianForceCasts);
        Assert.Equal((byte)255, session.Capture().NativeScriptState.Tasks.Tail);
        Assert.Equal(BattleAbility.GuardianForce, Assert.Single(session.QueuedActions).Ability);
        Assert.Single(session.Events, item => item.Kind == "GuardianForceReady");
        Assert.True(session.Combatants.Single(actor => actor.Id == "ally").IsReady);
        Assert.Equal(JsonSerializer.Serialize(session.Capture()), JsonSerializer.Serialize(completed.Capture()));
    }

    [Fact]
    public void Turn_based_pass_stops_at_next_actor_and_its_attack_keeps_the_Gf_charge_frozen()
    {
        BattleSession session = CreateTurnBasedWithReadyAlly(3500, enemySpeed: 1000);
        session.QueueAction("player", "enemy", BattleAbility.GuardianForce, 0);

        Assert.Equal(10, session.AdvanceTurn());

        Assert.Equal(10, session.Frame);
        Assert.Equal((ushort)380, Assert.Single(session.GuardianForceCasts).RemainingTicks);
        Assert.True(session.Combatants.Single(actor => actor.Id == "enemy").IsReady);
        Assert.Null(session.ActiveAction);
        session.Tick();
        Assert.Equal("enemy", session.ActiveAction!.ActorId);
        for (int callback = 0; callback < BattleSession.AttackWindupFrames + BattleSession.AttackRecoveryFrames; callback++)
        {
            session.Tick();
        }

        Assert.Null(session.ActiveAction);
        Assert.Equal((ushort)380, Assert.Single(session.GuardianForceCasts).RemainingTicks);
        Assert.True(session.Combatants.Single(actor => actor.Id == "ally").IsReady);
        Assert.Equal(0, session.Combatants.Single(actor => actor.Id == "enemy").Atb);
    }

    [Fact]
    public void Turn_based_cancelled_Gf_yields_to_its_script_before_advancing_to_another_actor()
    {
        BattleSession session = Create(compatibility: 350);
        session.SetMode(BattleAtbMode.TurnBased);
        session.QueueAction("player", "enemy", BattleAbility.GuardianForce, 0);
        BattleSnapshot pending = session.Capture();
        session = BattleSession.Restore(pending with { GuardianForces = [pending.GuardianForces[0] with { HitPoints = 0 }] });

        session.Tick();

        Assert.Equal(1, session.Frame);
        Assert.Equal(1, session.Combatants.Single(actor => actor.Id == "enemy").Atb);
        Assert.Empty(session.GuardianForceCasts);
        Assert.Empty(session.QueuedActions);
        Assert.Null(session.ActiveAction);
        Assert.False(session.Combatants[0].IsBusy);
        Assert.Equal((byte)255, session.Capture().NativeScriptState.Tasks.Tail);
        Assert.Single(session.Events, item => item.Kind == "GuardianForceCancelled");
        Assert.DoesNotContain(session.Events, item => item.Kind == "GuardianForceReady");
    }

    [Fact]
    public void Native_pause_flag_freezes_charge_across_restore_until_it_is_released()
    {
        BattleDefinition original = Create(compatibility: 350).Capture().Definition;
        BattleDefinition definition = original with { Combatants = original.Combatants.Select(actor => actor.IsPlayer ? actor with { NativeCombatantFlags = actor.NativeCombatantFlags | 0x400 } : actor).ToArray() };
        BattleSession session = new(definition, BattleAtbMode.Normal);
        session.QueueAction("player", "enemy", BattleAbility.GuardianForce, 0);
        session.Tick();
        Assert.Equal((ushort)40, Assert.Single(session.GuardianForceCasts).RemainingTicks);
        BattleSnapshot pending = session.Capture();
        BattleSession restored = BattleSession.Restore(pending);
        restored.Tick();
        Assert.Equal((ushort)40, Assert.Single(restored.GuardianForceCasts).RemainingTicks);
        BattleCombatantSnapshot player = pending.Combatants[0];
        pending = pending with { Combatants = [player with { NativeParticipant = player.NativeParticipant! with { NativeCombatantFlags = player.NativeParticipant.NativeCombatantFlags!.Value & ~0x400u } }, .. pending.Combatants.Skip(1)] };
        BattleSession resumed = BattleSession.Restore(pending);
        resumed.Tick();
        Assert.Equal((ushort)38, Assert.Single(resumed.GuardianForceCasts).RemainingTicks);
    }

    [Fact]
    public void Defeated_gf_releases_a_paused_callback_instead_of_leaving_actor_busy()
    {
        BattleSession session = Create(compatibility: 350);
        session.QueueAction("player", "enemy", BattleAbility.GuardianForce, 0);
        BattleSnapshot pending = session.Capture();
        BattleCombatantSnapshot player = pending.Combatants[0];
        pending = pending with
        {
            GuardianForces = [pending.GuardianForces[0] with { HitPoints = 0 }],
            Combatants = [player with { NativeParticipant = player.NativeParticipant! with { NativeCombatantFlags = player.NativeParticipant.NativeCombatantFlags!.Value | 0x400u } }, .. pending.Combatants.Skip(1)]
        };
        BattleSession restored = BattleSession.Restore(pending);
        restored.Tick();
        Assert.Empty(restored.GuardianForceCasts);
        Assert.Equal((byte)255, restored.Capture().NativeScriptState.Tasks.Tail);
        Assert.False(restored.Combatants[0].IsBusy);
        Assert.Single(restored.Events, item => item.Kind == "GuardianForceCancelled");
        Assert.DoesNotContain(restored.Events, item => item.Kind == "GuardianForceReady");
        Assert.Equal(0u, restored.Capture().Combatants[0].NativeParticipant!.NativeCombatantFlags!.Value & 0x400u);
        Assert.Equal(JsonSerializer.Serialize(restored.Capture()), JsonSerializer.Serialize(BattleSession.Restore(restored.Capture()).Capture()));
    }

    [Fact]
    public void Native_script_pool_owns_the_completion_packet_and_retains_it_while_B8C400_is_set()
    {
        BattleDefinition definition = Create(compatibility: 0).Capture().Definition;
        definition = definition with { Combatants = definition.Combatants.Select(actor => actor.IsPlayer ? actor with { NativeCombatantFlags = actor.NativeCombatantFlags | 0x400 } : actor).ToArray() };
        BattleSession session = new(definition, BattleAtbMode.Normal);
        session.QueueAction("player", "enemy", BattleAbility.GuardianForce, 0);
        session.Tick();
        BattleSnapshot pending = session.Capture();
        int slot = Assert.Single(Enumerable.Range(0, 16), pending.NativeScriptState.Tasks.Allocated);
        NativeBattleScriptTask task = pending.NativeScriptState.Tasks.Slots[slot];
        Assert.Equal(0x48E620u, task.ReadDword(0));
        Assert.Equal((byte)64, task.ReadByte(14));
        Assert.Equal((byte)254, task.ReadByte(13));
        Assert.Equal((ushort)0, Assert.Single(session.GuardianForceCasts).RemainingTicks);
        Assert.Empty(session.QueuedActions);
        BattleCombatantSnapshot player = pending.Combatants[0];
        pending = pending with { Combatants = [player with { NativeParticipant = player.NativeParticipant! with { NativeCombatantFlags = player.NativeParticipant.NativeCombatantFlags!.Value & ~0x400u } }, .. pending.Combatants.Skip(1)] };
        BattleSession restored = BattleSession.Restore(pending);
        restored.Tick();
        Assert.Empty(restored.GuardianForceCasts);
        Assert.Equal((byte)255, restored.Capture().NativeScriptState.Tasks.Tail);
        BattleAttackCommand completed = Assert.Single(restored.QueuedActions);
        Assert.Equal((ushort)0x80F8, completed.NativeTargetMask);
        Assert.Single(restored.Events, item => item.Kind == "GuardianForceReady");
        Assert.Equal(JsonSerializer.Serialize(restored.Capture()), JsonSerializer.Serialize(BattleSession.Restore(restored.Capture()).Capture()));
    }

    [Theory]
    [InlineData(0u, 2)]
    [InlineData(2u, 3)]
    [InlineData(4u, 1)]
    public void ChargeUsesSavedCompatibilitySpeedAndNativeStatusDecrement(uint statuses, int decrement)
    {
        BattleSession session = Create(compatibility: 350, statuses: statuses);
        session.QueueAction("player", "enemy", BattleAbility.GuardianForce, 0);
        Assert.Equal((ushort)40, Assert.Single(session.GuardianForceCasts).InitialTicks);
        session.Tick();
        Assert.Equal((ushort)(40 - decrement), Assert.Single(session.GuardianForceCasts).RemainingTicks);
        Assert.True(session.Combatants[0].IsBusy);
        BattleSession restored = BattleSession.Restore(session.Capture());
        session.Tick();
        restored.Tick();
        Assert.Equal(JsonSerializer.Serialize(session.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    [Fact]
    public void ChargingGfAbsorbsDamageWithoutSpillAndZeroHpCancelsSummon()
    {
        BattleSession session = Create(compatibility: 3500, gfHp: 10, enemyReady: true);
        session.QueueAttack("enemy", "player");
        session.QueueAction("player", "enemy", BattleAbility.GuardianForce, 0);
        for (int frame = 0; frame <= BattleSession.AttackWindupFrames + 1; frame++)
        {
            session.Tick();
        }
        Assert.Equal(100, session.Combatants[0].CurrentHp);
        Assert.Equal((ushort)0, Assert.Single(session.GuardianForces).HitPoints);
        Assert.Empty(session.GuardianForceCasts);
        Assert.Equal((ushort)0, Assert.Single(session.GuardianForces).DeathCount);
        BattleGuardianForceCounterChange counters = Assert.Single(session.GuardianForceCounterChanges);
        Assert.Equal((ushort)65535, counters.DeathsBefore);
        Assert.Equal((ushort)0, counters.DeathsAfter);
        Assert.Equal(counters, Assert.Single(BattleSession.Restore(session.Capture()).GuardianForceCounterChanges));
        Assert.Single(session.Events, item => item.Kind == "GuardianForceDamage" && item.Amount == 10);
        Assert.DoesNotContain(session.Events, item => item.Kind == "GuardianForceReady");
    }

    [Fact]
    public void MissingCompatibilityFailsWithoutConsumingReadyActor()
    {
        BattleSnapshot initial = Create().Capture();
        BattleDefinition definition = initial.Definition with { Combatants = initial.Definition.Combatants.Select(actor => actor with { GuardianForceCompatibility = null }).ToArray() };
        BattleSession session = new(definition, BattleAtbMode.Normal);
        Assert.Throws<NotSupportedException>(() => session.QueueAction("player", "enemy", BattleAbility.GuardianForce, 0));
        Assert.True(session.Combatants[0].IsReady);
        Assert.Empty(session.GuardianForceCasts);
        Assert.Empty(session.QueuedActions);
    }

    [Fact]
    public void HealingResultRetainsCharacterHpOwnerDuringGfCharge()
    {
        BattleDefinition original = Create(compatibility: 3500, gfHp: 50).Capture().Definition;
        BattleNativeAction native = Native(4, 1, 4);
        BattleDefinition definition = original with
        {
            Items = [new(1, 1)],
            ActionDefinitions = [.. original.ActionDefinitions, new(BattleAbility.Item, 1, "Potion", BattleActionEffect.Native, 1, true) { NativeAction = native with { Power = 1 } }],
            Combatants = [original.Combatants[0] with { CurrentHp = 50 }, new() { Id = "medic", Name = "Medic", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum, Abilities = [BattleAbility.Item] }, original.Combatants[1]]
        };
        BattleSession session = new(definition, BattleAtbMode.Normal);
        session.QueueAction("player", "enemy", BattleAbility.GuardianForce, 0);
        session.QueueAction("medic", "player", BattleAbility.Item, 1);
        for (int frame = 0; frame <= BattleSession.AttackWindupFrames; frame++)
        {
            session.Tick();
        }
        Assert.Equal(100, session.Combatants[0].CurrentHp);
        Assert.Equal((ushort)50, Assert.Single(session.GuardianForces).HitPoints);
        Assert.Single(session.GuardianForceCasts);
    }

    [Fact]
    public void MoogleDanceHealsEveryJunctionedGfUsingOwnedPercentage()
    {
        BattleSession original = Create();
        BattleNativeAction native = Native(4, 1, 24);
        BattleDefinition definition = original.Capture().Definition with
        {
            NativePocketStation = new(NativeInputConfiguration.OriginalInitHash, Enumerable.Range(0, 64).Select(index => index == 1 ? (byte)50 : (byte)0).ToImmutableArray()),
            Items = [new(1, 1)],
            GuardianForces = [new(0, 500, 1000), new(1, 100, 200)],
            Combatants = original.Capture().Definition.Combatants.Select(actor => actor.IsPlayer ? actor with { EquipmentIds = [0, 1], Abilities = [BattleAbility.Item] } : actor).ToArray(),
            ActionDefinitions = [new(BattleAbility.Item, 1, "Moogle Dance", BattleActionEffect.Native, 0, true) { NativeAction = native }]
        };
        BattleSession session = new(definition, BattleAtbMode.Normal);
        session.QueueAction("player", "player", BattleAbility.Item, 1);
        for (int frame = 0; frame <= BattleSession.AttackWindupFrames; frame++)
        {
            session.Tick();
        }
        Assert.Equal(new ushort[] { 1000, 200 }, session.GuardianForces.Select(gf => gf.HitPoints));
        Assert.Equal(100, session.Combatants[0].CurrentHp);
    }

    [Fact]
    public void UnacquiredNativeRowsMayRetainTheirUnclampedInitialHp()
    {
        BattleDefinition definition = Create().Capture().Definition with { GuardianForces = [new(0, 1000, 300, false)] };
        BattleSession session = new(definition, BattleAtbMode.Normal);
        Assert.Equal((ushort)1000, Assert.Single(BattleSession.Restore(session.Capture()).GuardianForces).HitPoints);
        Assert.Throws<InvalidOperationException>(() => session.QueueAction("player", "enemy", BattleAbility.GuardianForce, 0));
    }

    [Fact]
    public void MidbattleAcquisitionOnlySetsTheFlagAndRestoresItsOriginalSavedHp()
    {
        NativeBattleAiDefinition ai = new([[49, 0, 0], [0], [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, []);
        BattleDefinition original = Create().Capture().Definition;
        BattleDefinition definition = original with { GuardianForces = [new(0, 1000, 300, false)], Combatants = original.Combatants.Select(actor => actor.IsPlayer ? actor : actor with { NativeAi = ai }).ToArray() };
        BattleSession session = new(definition, BattleAtbMode.Normal);
        session.Tick();
        BattleGuardianForceVital acquired = Assert.Single(session.GuardianForces);
        Assert.True(acquired.Acquired);
        Assert.Equal((ushort)1000, acquired.HitPoints);
        Assert.Equal((ushort)300, acquired.MaximumHitPoints);
        Assert.Equal(acquired, Assert.Single(BattleSession.Restore(session.Capture()).GuardianForces));
    }

    [Theory]
    [InlineData(1000, 90, 1000)]
    [InlineData(6000, 200, 6000)]
    [InlineData(2000, 125, 2025)]
    [InlineData(0, 0, 6000)]
    public void CompletedSummonUpdatesAcquiredGfAffinityWithNativeWordWrapBeforeClamping(ushort initial, byte source, ushort expected)
    {
        BattleDefinition original = Create(compatibility: initial).Capture().Definition;
        byte[] raw = Enumerable.Repeat((byte)100, 132).ToArray();
        raw[112] = source;
        BattleActionDefinition action = original.ActionDefinitions[0];
        BattleNativeAction native = action.NativeAction! with { RawRecord = ImmutableArray.CreateRange(raw) };
        BattleDefinition definition = original with { NativeActions = [native], ActionDefinitions = [action with { NativeAction = native }] };
        BattleSession session = new(definition, BattleAtbMode.Normal);
        session.QueueAction("player", "enemy", BattleAbility.GuardianForce, 0);
        for (int frame = 0; frame < 1000 && !session.Events.Any(item => item.Kind == "Damage" && item.ActorId == "player"); frame++)
        {
            session.Tick();
        }
        BattleSnapshot snapshot = session.Capture();
        Assert.Equal(expected, snapshot.Combatants[0].GuardianForceCompatibility![0]);
        Assert.Equal(initial, snapshot.Definition.Combatants[0].GuardianForceCompatibility![0]);
        Assert.Equal(expected, BattleSession.Restore(snapshot).Capture().Combatants[0].GuardianForceCompatibility![0]);
    }

    private static BattleSession CreateTurnBasedWithReadyAlly(ushort compatibility, int enemySpeed = 1)
    {
        BattleDefinition original = Create(compatibility).Capture().Definition;
        BattleDefinition definition = original with
        {
            Combatants = [original.Combatants[0], new() { Id = "ally", Name = "Ally", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum }, original.Combatants[1] with { Speed = enemySpeed }]
        };
        return new(definition, BattleAtbMode.TurnBased);
    }

    private static BattleSession Create(ushort compatibility = 35, uint statuses = 0, ushort gfHp = 100, bool enemyReady = false)
    {
        BattleNativeAction native = Native(254, 64, 11);
        return new(new BattleDefinition
        {
            NativeCommandSequences = NativeBattleCommandSequenceFixture.Values,
            NativeActions = [native],
            NativeBattleSpeed = 0,
            GuardianForces = [new(0, gfHp, 100) { KillCount = 0, DeathCount = ushort.MaxValue }],
            ActionDefinitions = [new(BattleAbility.GuardianForce, 0, "GF", BattleActionEffect.Native, 10, false) { NativeAction = native, NativeGuardianBoostAvailable = false }],
            Combatants =
            [
                new() { Id = "player", Name = "Caster", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum, Abilities = [BattleAbility.GuardianForce], EquipmentIds = [0], GuardianForceCompatibility = Enumerable.Repeat(compatibility, 16).ToArray(), NativeStatus1 = statuses },
                new() { Id = "enemy", Name = "Opponent", MaximumHp = 10000, CurrentHp = 10000, Attack = 100, PhysicalPower = 30, Speed = 1, InitialAtb = enemyReady ? BattleSession.AtbMaximum : 0 }
            ]
        }, BattleAtbMode.Normal);
    }

    private static BattleNativeAction Native(byte command, ushort id, byte type)
    {
        return new(command, id, 2, 0) { TargetFlags = 0x48, ElementPercent = null, Hit = 255, Critical = null, PowerModifier = null, LevelModifier = null, MagicId = 0, AttackType = type, Power = 10, HitCount = 1, AttackFlags = 0, DamagePresentation = 0, Element = 0, StatusAttack = 255, Statuses0 = 0, Statuses1 = 0, RawRecord = ImmutableArray<byte>.Empty, SupplementalRecord = ImmutableArray<byte>.Empty };
    }
}
