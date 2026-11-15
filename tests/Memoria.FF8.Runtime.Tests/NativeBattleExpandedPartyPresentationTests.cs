using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleExpandedPartyPresentationTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo Cache = typeof(BattleSession).GetField("expandedPartyPresentation", PrivateInstance)!;

    [Fact]
    public void ExtendedRowsUseTheirCachedHpUntilOrderedContactsAndNativeHudSmoothing()
    {
        BattleSession session = Fixture();
        BattleModelRoster roster = session.ReadBattlePresentationRoster();
        BattleCombatant fourth = Actor(session, "ally-3");
        BattleCombatant eleventh = Actor(session, "ally-10");
        Assert.Contains(session.ReadPresentationModelOwner(fourth.Id), roster.Party);
        Assert.Contains(session.ReadPresentationModelOwner(eleventh.Id), roster.Party);
        Assert.DoesNotContain(PartyOwner("ally-5"), roster.Party);
        fourth.CurrentHp = 463;
        eleventh.CurrentHp = 375;
        string logical = NativeBattleTestSnapshots.Json(session.Capture());
        Assert.Equal(500, session.ReadPresentedVital(fourth.Id).HitPoints);
        Assert.Equal(500, Recover(session, fourth));

        Publish(session, roster, Half(PartyOwner("ally-3"), 37));
        Assert.Equal(463, session.ReadPresentedVital(fourth.Id).HitPoints);
        Assert.Equal(500, session.ReadPresentedVital(eleventh.Id).HitPoints);
        Publish(session, roster, Half(PartyOwner("ally-10"), 125));
        Assert.Equal(375, session.ReadPresentedVital(eleventh.Id).HitPoints);
        Assert.Equal(500, session.ReadDisplayedVital(fourth.Id).HitPoints);
        foreach (int expected in new[] { 489, 479, 468, 463 })
        {
            Invoke(session, "AdvanceExpandedPresentedVitals4B0500");
            Assert.Equal(expected, session.ReadDisplayedVital(fourth.Id).HitPoints);
        }
        Assert.Equal(logical, NativeBattleTestSnapshots.Json(session.Capture()));
    }

    [Fact]
    public void KoThenReviveOnTheSameActorUsesOrderedCacheAndEnemyRemainsCanonical()
    {
        BattleSession session = Fixture();
        BattleModelRoster roster = session.ReadBattlePresentationRoster();
        BattleCombatant actor = Actor(session, "ally-10");
        actor.CurrentHp = 0;
        actor.NativeStatus0 |= 1;
        Publish(session, roster, Half(PartyOwner("ally-10"), 10, flags: 1, saved: 1));
        Assert.Equal(0, session.ReadPresentedVital(actor.Id).HitPoints);
        Assert.Equal(new BattleVitalAppearance(false, false, true), session.ReadVitalAppearance(actor.Id));
        for (int poll = 0; poll < 96; poll++)
        {
            Invoke(session, "AdvanceExpandedPresentedVitals4B0500");
        }
        Assert.Equal(new BattleVitalAppearance(true, true, true), session.ReadVitalAppearance(actor.Id));

        actor.CurrentHp = 300;
        actor.NativeStatus0 &= 0xFFFE;
        Publish(session, roster, Half(PartyOwner("ally-10"), 300, flags: 1));
        Assert.Equal(300, session.ReadPresentedVital(actor.Id).HitPoints);
        Assert.Equal(0, session.ReadDisplayedVital(actor.Id).HitPoints);
        Publish(session, roster, Half(PartyOwner("ally-10"), 80));
        Assert.Equal(220, session.ReadPresentedVital(actor.Id).HitPoints);
        Assert.Equal(300, actor.CurrentHp);
        BattleCombatant enemy = Actor(session, "enemy");
        enemy.CurrentHp = 210;
        object? before = Cache.GetValue(session);
        Publish(session, roster, Half(session.ReadPresentationModelOwner("enemy"), 25));
        Assert.Same(before, Cache.GetValue(session));
        Assert.Equal(210, session.ReadDisplayedVital(enemy.Id).HitPoints);
    }

    [Fact]
    public void GuardianCacheRequiresASeedAndFormContactUsesCurrentFormHp()
    {
        BattleSession session = Fixture();
        BattleModelRoster roster = session.ReadBattlePresentationRoster();
        BattleCombatant actor = Actor(session, "ally-3");
        BattleGuardianForceVital gf = Assert.Single(session.GuardianForces);
        object? before = Cache.GetValue(session);
        Assert.Throws<NotSupportedException>(() => session.ReadPresentedVital(actor.Id, gf.Id));
        Assert.IsType<NotSupportedException>(Assert.Throws<TargetInvocationException>(() => Publish(session, roster, Half(PartyOwner("ally-3"), 37, flags: 0x20))).InnerException);
        Assert.Same(before, Cache.GetValue(session));

        Invoke(session, "BeginNativePresentedGuardianForce", actor, gf);
        Publish(session, roster, Half(PartyOwner("ally-3"), 37, flags: 0x20));
        Assert.Equal(new BattlePresentedVital(63, 200), session.ReadPresentedVital(actor.Id, gf.Id));
        Assert.Equal(new BattlePresentedVital(100, 200), session.ReadDisplayedVital(actor.Id, gf.Id));
        Assert.Equal(500, session.ReadPresentedVital(actor.Id).HitPoints);
        Assert.Equal(gf, Assert.Single(session.GuardianForces));

        actor.GuardianFormVitals = new(() => gf with { HitPoints = 49, MaximumHitPoints = 250 }, _ => throw new InvalidOperationException("Presentation must not write form HP."), new(), null!);
        Publish(session, roster, Half(PartyOwner("ally-3"), 37, flags: 0x20));
        Assert.Equal(new BattlePresentedVital(49, 250), session.ReadPresentedVital(actor.Id, gf.Id));
        Assert.Equal(new BattlePresentedVital(49, 250), session.ReadDisplayedVital(actor.Id));
        Assert.Equal(49, Recover(session, actor));
    }

    [Fact]
    public void HoleUnknownIdentityAndMissingCacheRowRejectBeforeReplacingCache()
    {
        BattleSession session = Fixture();
        BattleModelRoster roster = session.ReadBattlePresentationRoster();
        object? original = Cache.GetValue(session);
        Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() => Publish(session, roster, Half(PartyOwner("ally-5"), 1))).InnerException);
        BattleModelRoster stale = new(roster.Owners.Select(owner => owner == PartyOwner("ally-3") ? PartyOwner("retired") : owner));
        Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() => Publish(session, stale, Half(PartyOwner("ally-3"), 1))).InnerException);
        Assert.Same(original, Cache.GetValue(session));

        var missing = ((ImmutableDictionary<string, NativeBattlePartyPresentedVital>)original!).Remove("ally-3");
        Cache.SetValue(session, missing);
        Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() => Publish(session, roster, Half(PartyOwner("ally-3"), 1))).InnerException);
        Assert.Same(missing, Cache.GetValue(session));
    }

    private static BattleSession Fixture()
    {
        BattleCombatantDefinition[] party = Enumerable.Range(0, 11).Select(index => new BattleCombatantDefinition { Id = $"ally-{index}", Name = "Ally", IsPlayer = true, CurrentHp = 500, MaximumHp = 1000 }).ToArray();
        BattleSession session = new(new BattleDefinition
        {
            UsesExpandedRoster = true,
            Combatants = [.. party, new() { Id = "enemy", Name = "Enemy", CurrentHp = 500, MaximumHp = 1000 }],
            GuardianForces = [new(0, 100, 200)],
            NativeGlobalRandom = new(123)
        }, BattleAtbMode.TurnBased);
        Actor(session, "ally-5").BindRosterSlot(null, bindNative: false);
        Invoke(session, "InitializeExpandedPartyPresentation");
        return session;
    }

    private static BattleCombatant Actor(BattleSession session, string id) => session.Combatants.Single(actor => actor.Id == id);
    private static BattlePartyModelOwner PartyOwner(string id) => new BattlePartyActorModel(new BattlePartyActorReference(id));
    private static NativeBattleContactHalf Half(BattleModelOwner owner, ushort amount, byte flags = 0, ushort saved = 0) => new(owner, 0, 0, flags, saved, amount, 0);
    private static void Publish(BattleSession session, BattleModelRoster roster, NativeBattleContactHalf half) => Invoke(session, "PublishExpandedPartyPresentedContact", roster, half);
    private static int Recover(BattleSession session, BattleCombatant actor) => (int)Invoke(session, "ReadNativeRecoverSourceHp", actor)!;
    private static object? Invoke(BattleSession session, string name, params object[] args) => typeof(BattleSession).GetMethod(name, PrivateInstance)!.Invoke(session, args);
}
