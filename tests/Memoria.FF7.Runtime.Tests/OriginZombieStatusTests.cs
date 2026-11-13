using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class OriginZombieStatusTests
{
    public static void Run()
    {
        const ushort zombie = Ff7OriginSavedStatusRules.Zombie;
        Ff7BattleDamageActor stats = new(20, 50, 80, 10, 10, 0, 0, 20, 20);
        Ff7BattleActionVitals living = new(100, 200, 0, 0, 0);
        Ff7BattleRng Random() => new(new byte[3988], new(new byte[8]));
        Ff7BattleDamageAction operation = new(0x22, 0, 255, HasStatusOperation: true, AddsStatuses: true) { AlwaysHits = true, GuestOriginSavedStatuses = zombie, GuestOriginStatusAttack = 200 };
        Ff7BattleActionReceipt Hit(Ff7BattleDamageActor recipient, Ff7BattleActionVitals vitals, Ff7BattleDamageAction action, Ff7BattleAttack? record = null, byte[]? defenses = null)
            => Ff7NativeBattleActionResolver.ResolveGuestContact(stats, living, recipient, vitals, action, record, Random(), originStatusResistances: defenses);
        Ff7BattleActionReceipt applied = Hit(stats, living with { Statuses = Ff7NativeBattleStatuses.DeathSentence }, operation);
        Check(applied.AddedOriginSavedStatuses == zombie && applied.Target.Statuses == 0 && (applied.RemovedStatuses & Ff7NativeBattleStatuses.DeathSentence) != 0, "A source saved-status receipt must add Zombie independently and remove Doom without allocating a host status bit.");
        byte[] defenses = Enumerable.Repeat((byte)100, 40).ToArray();
        defenses[6] = 200;
        Check(Hit(stats, living, operation, defenses: defenses).AddedOriginSavedStatuses == 0 && Hit(stats, living, operation with { GuestOriginStatusAttack = 255 }, defenses: defenses).AddedOriginSavedStatuses == zombie, "Zombie must use its original resistance row and the native guaranteed-status bypass.");
        Check(Hit(stats, living with { Statuses = Ff7NativeBattleStatuses.Resist }, operation).AddedOriginSavedStatuses == 0, "The shared host Resist boundary must reject an origin saved-status producer.");
        Ff7BattleDamageActor undead = stats with { OriginSavedStatuses = zombie };
        Ff7BattleActionVitals zombieVitals = living with { OriginSavedStatuses = zombie };
        Ff7BattleDamageAction healing = new(0x26, 1, 255, Healing: true) { FixedDamage = 20 };
        Check(Hit(stats, living, healing).Damage.Damage == -20 && Hit(undead, zombieVitals, healing).Damage.Damage == 20, "Curative HP must reverse on an existing source Zombie before a simultaneous cure.");
        Ff7BattleActionReceipt cured = Hit(undead, zombieVitals, healing with { GuestOriginSavedStatuses = zombie, GuestOriginStatusAttack = 200 });
        Check(cured.Damage.Damage == 20 && cured.Target.Hp == 80 && cured.RemovedOriginSavedStatuses == zombie && cured.Target.OriginSavedStatuses == 0, "A curative contact must use the pre-contact Zombie direction and then clear its saved-status owner.");
        Ff7BattleDamageAction holy = healing with { Healing = false, NativeElements = 0x100 };
        Check(Hit(undead, zombieVitals, holy).Damage.Damage == 40 && Hit(stats, living, holy).Damage.Damage == 20, "The saved Zombie owner must supply the original Holy weakness.");
        Ff7BattleAttack death = Ff7GuestBattleStatuses.ToHostStatusRecord(1, 0, 255)!;
        Ff7BattleDamageAction statusOnly = operation with { GuestOriginSavedStatuses = 0 };
        Check((Hit(undead, zombieVitals, statusOnly, death).Target.Statuses & 1) == 0, "Ordinary Death must not kill a source Zombie.");
        Ff7BattleAttack doom = Ff7GuestBattleStatuses.ToHostStatusRecord(0, 0x400, 255)!;
        Check((Hit(undead, zombieVitals, statusOnly, doom).Target.Statuses & Ff7NativeBattleStatuses.DeathSentence) == 0, "A source Zombie must reject new Doom.");
        Ff7BattleDamageAction revival = new(0x24, 4, 255, Healing: true, HasStatusOperation: true) { RequiresKnockedOutTarget = true, AlwaysHits = true };
        Ff7BattleActionReceipt killed = Hit(undead, zombieVitals, revival, death with { StatusChange = 0x7F });
        Check(killed.OffensiveRevival && killed.Target.Hp == 0 && killed.Target.Statuses == 1 && killed.Damage.Damage == 0 && killed.RemovedOriginSavedStatuses == zombie, "Life against living Zombie must be a zero-magnitude offensive Death receipt, with KO clearing the source status.");
        Ff7BattleActionReceipt protectedTarget = Hit(undead, zombieVitals with { Statuses = Ff7NativeBattleStatuses.Peerless }, revival, death with { StatusChange = 0x7F });
        Check(protectedTarget.Target.Hp == living.Hp && (protectedTarget.Target.Statuses & 1) == 0, "The offensive revival branch must preserve the ordinary target immunity contract.");
        Ff7BattleActionReceipt prepared = Ff7NativeBattleActionResolver.ApplyPreparedGuestContact(living, zombieVitals, RoundTrip(protectedTarget), revival, false);
        Check(prepared.Target.Hp == living.Hp && (prepared.Target.Statuses & 1) == 0, "A prepared blocked offensive revival must not manufacture KO when its original revival descriptor is applied.");
        Ff7BattleDamageAction drain = holy with { NativeElements = 0, GuestDrainStatusAttack = 200 };
        Ff7BattleActionReceipt drained = Hit(undead, zombieVitals, drain);
        Check(drained.Target.Hp == 80 && drained.Source.Hp == 80 && drained.SourceHitPointDamage == 20, "Drain must reverse its secondary source transfer when only the recipient is Zombie.");
        Ff7BattleStatusTimers regen = Ff7NativeBattleStatusClock.Apply(default, 0, Ff7NativeBattleStatuses.Regen);
        Ff7BattleStatusTick normalTick = Ff7NativeBattleStatusClock.Advance(regen, Ff7NativeBattleStatuses.Regen, 128, maximumHitPoints: 320);
        Ff7BattleStatusTick zombieTick = Ff7NativeBattleStatusClock.Advance(regen, Ff7NativeBattleStatuses.Regen, 128, maximumHitPoints: 320, originSavedStatuses: zombie);
        Check(normalTick.Timers.HitPointRemainder > 0 && zombieTick.HitPointDelta < 0, "The ordinary Regen accumulator must reverse on source Zombie without acquiring a second duration clock.");
        InstalledClockKnockout();
        Console.WriteLine("PASS origin Zombie admission, cure, healing, Holy, Doom, offensive revival, Drain and Regen contracts.");
    }

    private static void InstalledClockKnockout()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            Console.WriteLine("SKIP installed origin status KO: MEMORIA_FF7_GAME_DIR unset.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(directory);
        using Ff7Session original = Ff7Session.NewGame(installation);
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path)).Single(item => item.FormationId == 300);
        IReadOnlyList<short> modifiers = Ff7PcExecutableData.Load(installation).NativeMateriaStatModifiers;
        Ff7BattleSession Create(Ff7BattleSnapshot? snapshot = null) => new(formation, original.Progress, kernel, modifiers, 1, snapshot: snapshot);
        Ff7BattleSession seeded = Create();
        uint sourceOwners = Ff7GuestBattleStatuses.OriginMulticast | Ff7GuestBattleStatuses.OriginDefenseReduction;
        seeded.BindGuestVitals(new(Guid.NewGuid(), new('A', 64), 0, 0, 0, 0, 1, 320, 6, 14, 24, 17) { OriginStatuses = Ff7OriginSavedStatusRules.Zombie, OriginBattleStatuses = sourceOwners | 0x10 });
        Ff7BattleSnapshot initial = seeded.Capture();
        Ff7BattleSnapshot setup = initial with { Actors = initial.Actors.Select(actor => actor.Actor.Slot == 0 ? actor with { Actor = actor.Actor with { Statuses = Ff7NativeBattleStatuses.Regen }, Timers = Ff7NativeBattleStatusClock.Apply(default, 0, Ff7NativeBattleStatuses.Regen) } : actor with { Actor = actor.Actor with { Statuses = 0x400 } }).ToArray() };
        Ff7BattleSession live = Create(RoundTrip(setup));
        Check(Ff7OriginSavedStatusRules.IsZombie(live.GetOriginSavedStatuses(0)) && live.Capture().Actors[0].ParameterStatus is not null && (live.GetGuestOriginBattleStatuses(0) & Ff7GuestBattleStatuses.OriginMulticast) != 0, "The clock KO fixture must begin with all independently owned source statuses present.");
        live.Advance();
        Ff7BattleSnapshot dead = RoundTrip(live.Capture());
        Check(dead.Actors[0].Actor.HitPoints == 0 && (dead.Actors[0].Actor.Statuses & 1) != 0 && dead.Actors[0].ParameterStatus is null && !Ff7OriginSavedStatusRules.IsZombie(live.GetOriginSavedStatuses(0)) && (live.GetGuestOriginBattleStatuses(0) & sourceOwners) == 0, "Regen's direct clock KO must clear Zombie, defensive parameter status and multicast through the same native source KO masks as a contact.");
        Ff7BattleSnapshot revival = dead with { Actors = dead.Actors.Select(actor => actor.Actor.Slot == 0 ? actor with { Actor = actor.Actor with { HitPoints = 100, Statuses = 0 } } : actor).ToArray() };
        Ff7BattleSession restored = Create(RoundTrip(revival));
        Check(restored.Capture().Actors[0].ParameterStatus is null && !Ff7OriginSavedStatusRules.IsZombie(restored.GetGuestOriginSavedStatuses(0)) && (restored.GetGuestOriginBattleStatuses(0) & sourceOwners) == 0, "A revived JSON continuation must not reconstruct source owners removed by KO from their original entry binding.");
    }

    private static T RoundTrip<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
