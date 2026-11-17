using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleExpandedReadyInvalidationTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string Fourth = "ally-3";
    private const string Eleventh = "ally-10";

    private static readonly BattlePartyModelOwner FourthModel = new BattlePartyActorModel(new(Fourth));
    private static readonly BattlePartyModelOwner EleventhModel = new BattlePartyActorModel(new(Eleventh));
    private static readonly BattleEnemyModelOwner SparseEnemy = new BattleEnemyActorModel(new("sparse-enemy"));
    private static readonly BattlePartyModelOwner Unbound = new BattlePartyActorModel(new("ally-5"));

    [Fact]
    public void OrderedCompelledTransitionsUsePriorMirrorAndExistingMenuEligibility()
    {
        BattleSession session = Fixture();
        BattleCombatant fourth = Actor(session, Fourth);
        BattleCombatant eleventh = Actor(session, Eleventh);
        BattleModelRoster roster = session.ReadBattlePresentationRoster();
        Assert.Equal(FourthModel, session.ReadPresentationModelOwner(Fourth));
        Assert.Equal(EleventhModel, session.ReadPresentationModelOwner(Eleventh));
        Assert.False(roster.Contains(Unbound));
        Assert.True(session.CanChoosePlayerCommand(Fourth));
        Assert.True(session.CanChoosePlayerCommand(Eleventh));

        fourth.NativeStatus0 = 0x20;
        fourth.NativePresentationStatus = new(0x20, 0);
        fourth.NativeCombatantFlags |= 0xC;
        Publish(session, roster, Half(FourthModel, saved: 0x20));
        Assert.False(session.CanChoosePlayerCommand(Fourth));
        Assert.Equal(0u, fourth.NativeCombatantFlags & 0xC);
        Assert.Equal(new NativeBattleStatusMirror(0x20, 0), Mirrors(session)[Fourth]);
        fourth.NativeCombatantFlags |= 0xC;
        Publish(session, roster, Half(FourthModel, saved: 0x20));
        Assert.Equal(0xCu, fourth.NativeCombatantFlags & 0xC);

        fourth.NativeStatus0 = 0;
        fourth.NativePresentationStatus = new(0, 0);
        Publish(session, roster, Half(FourthModel), caster: Fourth);
        Assert.Equal(0xCu, fourth.NativeCombatantFlags & 0xC);
        Assert.True(session.CanChoosePlayerCommand(Fourth));
        Assert.True(session.CanChoosePlayerCommand(Eleventh));

        eleventh.NativeStatus1 = 0x4000;
        eleventh.NativePresentationStatus = new(0, 0x4000);
        eleventh.NativeCombatantFlags |= 0xC;
        Publish(session, roster, Half(EleventhModel, battle: 0x4000), caster: Fourth);
        Assert.False(session.CanChoosePlayerCommand(Eleventh));
        eleventh.NativeStatus1 = 0;
        eleventh.NativePresentationStatus = new(0, 0);
        eleventh.NativeCombatantFlags |= 0xC;
        Publish(session, roster, Half(EleventhModel), caster: Fourth);
        Assert.True(session.CanChoosePlayerCommand(Eleventh));
        Assert.Equal(0u, eleventh.NativeCombatantFlags & 0xC);

        var mirrors = Mirrors(session);
        Publish(session, roster, Half(SparseEnemy, saved: 1, battle: 0x80010000));
        Assert.Same(mirrors, Mirrors(session));
        Assert.Equal(0, Actor(session, "sparse-enemy").NativeStatus0);
        Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() => Publish(session, roster, Half(Unbound))).InnerException);
        Assert.Same(mirrors, Mirrors(session));
    }

    [Fact]
    public void KoAndReviveFollowPacketOrderAndEjectionNormalizesCanonicalFlags()
    {
        BattleSession session = Fixture();
        BattleCombatant actor = Actor(session, Eleventh);
        BattleModelRoster roster = session.ReadBattlePresentationRoster();
        actor.CurrentHp = 321;
        actor.NativeCombatantFlags |= 0xC;
        actor.IsBusy = true;
        var random = session.NativeGlobalRandomChange;
        Publish(session, roster, Half(EleventhModel, saved: 1, amount: 500));
        Assert.Equal(0, session.ReadPresentedVital(Eleventh).HitPoints);
        Assert.Equal(321, actor.CurrentHp);
        Assert.Equal(0, actor.Atb);
        Assert.False(actor.IsBusy);
        Assert.Equal(0u, actor.NativeCombatantFlags & 0xC);

        Publish(session, roster, Half(EleventhModel, amount: 321, hitFlags: 1));
        Assert.Equal(321, session.ReadPresentedVital(Eleventh).HitPoints);
        actor.Atb = BattleSession.AtbMaximum;
        Assert.True(session.CanChoosePlayerCommand(Eleventh));

        actor.NativeStatus0 = 0x22;
        actor.NativeStatus1 = 0x10002;
        actor.NativePresentationStatus = new(0x22, 0x10002);
        actor.NativeCombatantFlags = 0xFF;
        Publish(session, roster, Half(EleventhModel, saved: 0x22, battle: 0x10002));
        Assert.Equal(0x3, actor.NativeStatus0);
        Assert.Equal(2u, actor.NativeStatus1);
        Assert.Equal(0u, actor.NativeCombatantFlags);
        Assert.Equal(new NativeBattleStatusMirror(0x22, 0x10002), Mirrors(session)[Eleventh]);
        Assert.Equal(new NativeBattleStatusMirror(3, 2), actor.NativePresentationStatus);
        Assert.Equal(321, actor.CurrentHp);
        Assert.Equal(random, session.NativeGlobalRandomChange);
    }

    [Fact]
    public void SummonStopsOnlyAfterApplyingDamageToThePresentedGuardianHp()
    {
        BattleSession session = Fixture();
        BattleCombatant actor = Actor(session, Fourth);
        BattleModelRoster roster = session.ReadBattlePresentationRoster();
        BattleGuardianForceVital gf = Assert.Single(session.GuardianForces);
        Invoke(session, "BeginNativePresentedGuardianForce", actor, gf);
        actor.NativeStatus1 = 0x80000000;
        actor.NativePresentationStatus = new(0, actor.NativeStatus1);
        var charges = Field<Dictionary<string, BattleGuardianForceCast>>(session, "guardianForceCasts");
        BattleGuardianForceCast cast = new(Fourth, new(Fourth, "enemy", Target("enemy"), BattleAbility.GuardianForce), 2, 4);
        charges.Add(Fourth, cast);

        Publish(session, roster, Half(FourthModel, battle: 0x80000001));
        Assert.Equal(0x400u, actor.NativeCombatantFlags & 0x400);
        Publish(session, roster, Half(FourthModel, battle: 0x80000000));
        Assert.Equal(0u, actor.NativeCombatantFlags & 0x400);
        Publish(session, roster, Half(FourthModel, battle: 0x80000001, amount: 100, hitFlags: 0x20));
        Assert.Equal(0, session.ReadPresentedVital(Fourth, gf.Id).HitPoints);
        Assert.Equal(0u, actor.NativeStatus1 & 0x80000000);
        Assert.Equal(0u, actor.NativeCombatantFlags & 0x400);
        Assert.Same(cast, charges[Fourth]);
        Assert.Equal(gf, Assert.Single(session.GuardianForces));
    }

    [Fact]
    public void TerminalPacketRefundsOnlyUncommittedInputsOnceAndPreservesOwnedActions()
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Create().ChangeMagicStock(0, 1, 3, MagicOperationReason.ScriptReward, out _);
        BattleSession session = Fixture(vitals);
        var queue = Field<Queue<BattleAttackCommand>>(session, "actions");
        BattleModelRoster roster = session.ReadBattlePresentationRoster();
        int itemSlot = (int)Invoke(session, "ConsumeNativeItem", 1)!;
        BattleAttackCommand magic = (BattleAttackCommand)Invoke(session, "EnsureMagicReservation", Actor(session, Fourth), new BattleAttackCommand(Fourth, "enemy", Target("enemy"), BattleAbility.Magic, 1))!;
        Assert.NotNull(magic.MagicReservationId);
        Assert.Single(session.MagicVitals!.MagicSystem.Reservations);
        BattleAttackCommand item = new(Fourth, Fourth, Target(Fourth), BattleAbility.Item, 1) { ReservedNativeItemSlot = itemSlot };
        BattleAttackCommand callback = new("ally-0", "ally-0", Target("ally-0")) { NativeCallback = new(0, 7) };
        BattleAttackCommand reaction = new(Fourth, "enemy", Target("enemy")) { NativePlayerReaction = BattleNativePlayerReaction.Counter };
        BattleAttackCommand header = new(Fourth, "enemy", Target("enemy")) { NativeHeader = new BattleActionHeaderReference(0) };
        BattleAttackCommand other = new(Eleventh, "enemy", Target("enemy"));
        BattleAttackCommand charged = new(Fourth, "enemy", Target("enemy"), BattleAbility.GuardianForce) { NativeQueuedCommand = 254 };
        BattleAttackCommand renzokuken = new(Fourth, "enemy", Target("enemy"), BattleAbility.LimitBreak, 5 << 16) { NativeQueuedCommand = 5, NativeRenzokuken = new(1) };
        foreach (BattleAttackCommand command in new[] { callback, item, reaction, magic, other, header, charged, renzokuken })
        {
            queue.Enqueue(command);
        }
        BattleActionPresentation active = new(Fourth, "enemy", BattleActionPhase.Recovery, 1, Target("enemy"));
        typeof(BattleSession).GetProperty(nameof(BattleSession.ActiveAction))!.SetValue(session, active);
        typeof(BattleSession).GetField("expandedHostedActionToken", PrivateInstance)!.SetValue(session, 3L);

        Publish(session, roster, Half(FourthModel, saved: 1), caster: Fourth);
        Assert.Equal(new[] { callback, reaction, other, header }, queue.ToArray());
        Assert.Equal(new BattleStock(1, 2), Assert.Single(session.Items));
        Assert.Empty(session.MagicVitals!.MagicSystem.Reservations);
        Assert.Equal(new BattleStock(1, 3), Assert.Single(Actor(session, Fourth).MagicStocks));
        Assert.Same(active, session.ActiveAction);
        Assert.Equal(3L, Field<long?>(session, "expandedHostedActionToken"));
        Publish(session, roster, Half(FourthModel, saved: 1), caster: Fourth);
        Assert.Equal(new[] { callback, reaction, other, header }, queue.ToArray());
        Assert.Equal(new BattleStock(1, 2), Assert.Single(session.Items));
        Assert.Empty(session.MagicVitals!.MagicSystem.Reservations);

        Publish(session, roster, Half(session.ReadPresentationModelOwner(callback.ActorId), saved: 1));
        Assert.Equal(new[] { callback, reaction, other, header }, queue.ToArray());
    }

    [Fact]
    public void UnstartedShotOpeningIsRemovedWhileLiveContinuationCallbacksRetainTheirOwners()
    {
        BattleSession session = Fixture();
        BattleModelRoster roster = session.ReadBattlePresentationRoster();
        var queue = Field<Queue<BattleAttackCommand>>(session, "actions");
        var shots = Field<Dictionary<string, BattleNativeShotState>>(session, "nativeShotProcesses");
        var levels = Field<Dictionary<string, byte>>(session, "nativeLimitLevels");
        var duels = Field<Dictionary<string, BattleNativeDuelState>>(session, "nativeDuelProcesses");
        BattleNativeAction shotAction = new(14, 2, 21, 2)
        {
            MagicId = 0, AttackType = 1, Power = 1, HitCount = 1, TargetFlags = 0x50, AttackFlags = 0, DamagePresentation = 0, Element = 0, ElementPercent = 0,
            StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = 255, Critical = 0, PowerModifier = null, LevelModifier = null, RawRecord = [], SupplementalRecord = []
        };
        BattleAttackCommand opening = new(Fourth, "enemy", Target("enemy"), BattleAbility.LimitBreak, 14 << 16 | 2) { NativeAction = shotAction, NativeLimitCallback = NativeLimitCallback.ShotOpening };
        queue.Enqueue(opening);
        shots.Add(Fourth, new(2, Target("enemy"), 40, 40, 2, BattleNativeShotPhase.Opening));
        levels.Add(Fourth, 2);

        Publish(session, roster, Half(FourthModel, saved: 1));
        Assert.Empty(queue);
        Assert.Null(session.GetNativeShot(Fourth));
        Assert.DoesNotContain(Fourth, levels.Keys);

        BattleAttackCommand continuation = opening with { NativeLimitCallback = NativeLimitCallback.ShotEffect, NativeAction = shotAction with { Command = 237 } };
        BattleAttackCommand duel = new(Eleventh, "enemy", Target("enemy"), BattleAbility.LimitBreak) { NativeLimitCallback = NativeLimitCallback.DuelEffect };
        queue.Enqueue(continuation);
        queue.Enqueue(duel);
        BattleNativeShotState shot = new(2, Target("enemy"), 40, 20, 1, BattleNativeShotPhase.AwaitingEffect);
        shots.Add(Fourth, shot);
        duels.Add(Eleventh, new(0, Target("enemy"), 30, 15, BattleNativeDuelPhase.AwaitingEffect));

        Publish(session, roster, Half(FourthModel, saved: 1));
        Publish(session, roster, Half(EleventhModel, saved: 1));
        Assert.Equal(new[] { continuation, duel }, queue.ToArray());
        Assert.Same(shot, session.GetNativeShot(Fourth));
        Assert.Equal(BattleNativeDuelPhase.AwaitingEffect, duels[Eleventh].Phase);
    }

    private static BattleSession Fixture(NativeCharacterVitals? vitals = null)
    {
        BattleCombatantDefinition[] party = Enumerable.Range(0, 11).Select(index => new BattleCombatantDefinition
        {
            Id = $"ally-{index}", Name = "Ally", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum, CurrentHp = 500, MaximumHp = 1000,
            MagicRecordId = vitals is not null && index == 3 ? 0 : null
        }).ToArray();
        BattleSession session = new(new BattleDefinition
        {
            Combatants = [.. party, new() { Id = "enemy", Name = "Enemy", SceneSlot = 0, CurrentHp = 500, MaximumHp = 1000 }, new() { Id = "other-enemy", Name = "Enemy", SceneSlot = 1, CurrentHp = 500, MaximumHp = 1000 }, new() { Id = "sparse-enemy", Name = "Enemy", SceneSlot = 2, CurrentHp = 500, MaximumHp = 1000 }],
            GuardianForces = [new(0, 100, 200)],
            Items = [new(1, 2)],
            MagicVitals = vitals,
            NativeGlobalRandom = new(123)
        }, BattleAtbMode.TurnBased);
        Actor(session, "ally-5").BindRosterSlot(null, bindNative: false);
        Actor(session, "other-enemy").BindRosterSlot(null, bindNative: false);
        Actor(session, "sparse-enemy").BindRosterSlot(BattleRosterSlot.Enemy(4), bindNative: false);
        foreach (BattleCombatant actor in session.Combatants.Where(actor => actor.IsPlayer))
        {
            actor.NativePresentationStatus = new(actor.NativeStatus0, actor.NativeStatus1);
        }
        Invoke(session, "InitializeExpandedPartyPresentation");
        return session;
    }

    private static BattleCombatant Actor(BattleSession session, string id) => session.Combatants.Single(actor => actor.Id == id);
    private static BattleTargetRequest Target(string id)
    {
        BattleActorReference actor = id == "enemy" ? new BattleEnemyActorReference(id) : new BattlePartyActorReference(id);
        return new(new BattleTargetSelection.Explicit([actor]), BattleTargetEligibility.SavedStatus, false, BattleTargetReplacement.None);
    }

    private static NativeBattleContactHalf Half(BattleModelOwner owner, ushort saved = 0, uint battle = 0, ushort amount = 0, byte hitFlags = 0) => new(owner, 0, 0, hitFlags, saved, amount, battle);
    private static void Publish(BattleSession session, BattleModelRoster roster, NativeBattleContactHalf half, string caster = "enemy") => Invoke(session, "PublishExpandedReadyContact", roster, half, caster);
    private static ImmutableDictionary<string, NativeBattleStatusMirror> Mirrors(BattleSession session) => Field<ImmutableDictionary<string, NativeBattleStatusMirror>>(session, "expandedPartyReadyStatusMirrors");
    private static T Field<T>(BattleSession session, string name) => (T)typeof(BattleSession).GetField(name, PrivateInstance)!.GetValue(session)!;
    private static object? Invoke(BattleSession session, string name, params object[] args) => typeof(BattleSession).GetMethod(name, PrivateInstance)!.Invoke(session, args);
}
