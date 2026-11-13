using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Runtime;

internal static class GuestBattleConversionTests
{
    public static void Run()
    {
        Elements();
        Statuses();
        StatusCounterparts();
        OriginStatusOperations();
        Drain();
    }

    private static void Elements()
    {
        Check(Ff7GuestBattleElements.ToHost(0xFF) == 0x1DF, "The eight FFVIII elements must project without setting Gravity.");
        Check(Ff7GuestBattleElements.ToHost(0x20) == 0x80 && Ff7GuestBattleElements.ToOrigin(0x100) == 0x80, "Wind and Holy must map by identity.");
        Check(Ff7GuestBattleElements.ToOrigin(0xFE20) == 0 && Ff7GuestBattleElements.ToOrigin(ushort.MaxValue) == 0, "Host-only elements and the empty native mask must be neutral.");

        Ff7BattleDamageActor source = new(10, 30, 35, 0, 0, 0, 0, 0, 0);
        Ff7BattleDamageActor target = new(10, 0, 0, 0, 0, 0, 0, 0, 0);
        Ff7BattleDamageAction action = new(0x22, 8, 255);
        int Damage(Ff7BattleDamageAction hit) => Ff7NativeBattleDamage.Resolve(source, target, hit, Random()).Damage;
        int neutral = Damage(action);
        SceneBattleEnemy enemy = new(0, new(0, default, 0, 0, 0), 0, new(0, default, 0, 0, 0, [], default), 10, 0, 0, 0, 0, 0, 0, 0, 999, 0, uint.MaxValue, [new(0, 5), new(1, 6), new(2, 2)], [], null);
        Ff7BattleDamageAction Affinity(byte originElement) => action with { Affinity = Ff7NativeBattleDamage.ResolveEnemyAffinity(enemy, Ff7GuestBattleElements.ToHost(originElement)) };
        Check(neutral > 0 && Damage(Affinity(1)) == 0 && Damage(Affinity(2)) == -neutral && Damage(Affinity(4)) == 2 * neutral, "Outgoing spell damage must use the target FFVII enemy's native affinity.");
        Check(Damage(Affinity(1) with { GuestElementPercent = 50 }) == neutral - neutral / 2 && Damage(Affinity(2) with { GuestElementPercent = 50 }) == 0, "A partial junction must leave the unaligned part of a hit neutral.");

        Ff7BattleGuestProjection sourceProjection = new(486, 6, 14, 24, 17) { OriginElementDefenses = [900, 800, 1000, 800, 800, 800, 800, 800] };
        Ff7BattleGuestProjection restored = JsonSerializer.Deserialize<Ff7BattleGuestProjection>(JsonSerializer.Serialize(sourceProjection))!;
        Check(restored == sourceProjection && restored.GetHashCode() == sourceProjection.GetHashCode(), "JSON round-trip must preserve the guest projection by value.");
        IReadOnlyList<int> defenses = restored.OriginElementDefenses!.Value;
        int Incoming(ushort elements) => Damage(action with { GuestElementAdjustment = Ff7GuestBattleElements.DefenseAdjustment(elements, defenses) });
        Check(Incoming(1) == 0 && Incoming(2) == neutral && Incoming(4) == -neutral, "Incoming hits must honor the restored FFVIII immunity, neutral and absorption affinities.");
        Check(Incoming(0x20) == neutral && Incoming(0x400) == neutral, "Gravity and Cut have no FFVIII elemental resistance.");
    }

    private static void Statuses()
    {
        const uint darkness = Ff7NativeBattleActionResolver.Darkness;
        const uint sadness = 0x10;
        const ushort zombie = 0x40;
        Ff7BattleAttack outgoing = Ff7GuestBattleStatuses.ToHostStatusRecord(8 | zombie, 0x100, 200)!;
        Check(outgoing.StatusMask == darkness, "FFVIII Blind maps to Darkness while Zombie and Aura are ignored.");
        Check(Ff7GuestBattleStatuses.ToHostStatusRecord(zombie, 0x100, 255) is null, "An unmapped status must not produce a status roll.");
        Check(Ff7GuestBattleStatuses.ToOrigin(darkness, zombie) == (8 | zombie), "Terminal settlement must preserve the original Zombie bit.");

        Ff7BattleDamageActor stats = new(10, 20, 25, 0, 0, 0, 0, 0, 0);
        Ff7BattleActionVitals source = new(486, 486, 0, 0, 0);
        Ff7BattleActionVitals target = new(486, 486, 0, 0, sadness);
        Ff7BattleDamageAction hit = new(0x22, 8, 255);
        Ff7BattleActionReceipt Hit(Ff7BattleAttack? record, Ff7BattleActionVitals recipient, byte[]? defenses = null)
            => Ff7NativeBattleActionResolver.ResolveGuestContact(stats, source, stats, recipient, hit, record, Random(), originStatusResistances: defenses);
        Check(Hit(outgoing, target).Target.Statuses == (darkness | sadness), "Outgoing mapped status must apply without dropping a native FFVII status.");
        Check(Hit(outgoing, target with { StatusImmunities = darkness }).Target.Statuses == sadness, "Outgoing mapped status must respect the destination FFVII immunity.");

        Ff7BattleAttack incoming = Ff7GuestBattleStatuses.ProjectIncoming(outgoing with { StatusMask = darkness | sadness })!;
        byte[] junction = Enumerable.Repeat((byte)100, 40).ToArray();
        Check(incoming.StatusMask == darkness && Hit(incoming, target with { Statuses = 0 }, junction).Target.Statuses == darkness, "The incoming guest path must apply Blind and ignore Sadness.");
        junction[3] = 200;
        Ff7BattleActionReceipt defended = Hit(incoming, target with { Statuses = 0 }, junction);
        Check(defended.Target.Statuses == 0 && defended.Damage.Damage > 0, "FFVIII status defense must block Blind without blocking the hit.");
    }

    private static void StatusCounterparts()
    {
        (ushort Saved, uint Battle, uint Host, int Defense)[] counterparts =
        [
            (1, 0, 1, 0),
            (2, 0, 8, 1),
            (4, 0, 0x4000, 2),
            (8, 0, 0x4000000, 3),
            (16, 0, 0x80, 4),
            (32, 0, 0x800000, 5),
            (0, 1, 4, 8),
            (0, 2, 0x100, 9),
            (0, 4, 0x200, 10),
            (0, 8, 0x400, 11),
            (0, 0x10, 0x8000, 12),
            (0, 0x20, 0x10000, 13),
            (0, 0x40, 0x20000, 14),
            (0, 0x80, 0x40000, 15),
            (0, 0x400, 0x200000, 18),
            (0, 0x800, 0x1000000, 19),
            (0, 0x1000, 0x2000, 20),
            (0, 0x4000, 0x40, 22)
        ];
        uint union = 0;
        Ff7BattleDamageActor stats = new(10, 20, 25, 0, 0, 0, 0, 0, 0);
        Ff7BattleActionVitals source = new(100, 100, 0, 0, 0);
        Ff7BattleDamageAction action = new(0x50, 0, 255, HasStatusOperation: true, AddsStatuses: true);
        foreach ((ushort saved, uint battle, uint host, int defense) in counterparts)
        {
            Check(Ff7GuestBattleStatuses.ToHostStatuses(saved, battle) == host, $"The counterpart at resistance row {defense} must retain its exact host identity.");
            Check(Ff7GuestBattleStatuses.ToOrigin(host, 0xFFC0) == (saved | 0xFFC0), "Saved origin-only bits must survive host status round-trip.");
            uint originalBattle = ~Ff7GuestBattleStatuses.SupportedOriginBattle;
            Check(Ff7GuestBattleStatuses.ToOriginBattle(host, originalBattle) == (battle | originalBattle), "Battle origin-only bits must survive host status round-trip.");
            Ff7BattleAttack record = Ff7GuestBattleStatuses.ToHostStatusRecord(saved, battle, 200)!;
            Check(record.StatusMask == host && Ff7GuestBattleStatuses.ProjectIncoming(record)!.StatusMask == host, "Both contact directions must carry every exact counterpart.");
            byte[] defenses = Enumerable.Repeat((byte)100, 40).ToArray();
            defenses[defense] = 200;
            Ff7BattleActionReceipt resisted = Ff7NativeBattleActionResolver.ResolveGuestContact(stats, source, stats, source, action, record, Random(), originStatusResistances: defenses);
            Check(resisted.AddedStatuses == 0, $"Counterpart resistance must read the native row {defense}.");
            defenses[defense] = 100;
            Ff7BattleActionReceipt admitted = Ff7NativeBattleActionResolver.ResolveGuestContact(stats, source, stats, source, action, record, Random(), originStatusResistances: defenses);
            Check(admitted.AddedStatuses == host, "A neutral native resistance must admit the counterpart.");
            Ff7BattleActionReceipt immune = Ff7NativeBattleActionResolver.ResolveGuestContact(stats, source, stats, source with { StatusImmunities = host }, action, record, Random());
            Check(immune.AddedStatuses == 0, "The destination host immunity must reject the counterpart.");
            if ((host & Ff7NativeBattleStatuses.SupportedRecordStatuses) != 0)
            {
                Ff7BattleActionReceipt native = Ff7NativeBattleActionResolver.Resolve(stats, source, stats, source, action, record, Random());
                Check(native.AddedStatuses == host, "A supported native FFVII record must retain the same status identity outside the guest adapter.");
            }
            union |= host;
        }
        Check(union == Ff7GuestBattleStatuses.SupportedHost, "The reversible counterpart inventory must cover the complete supported host mask.");
        Check(Ff7GuestBattleStatuses.ToHostStatuses(0xFFC0, ~Ff7GuestBattleStatuses.SupportedOriginBattle) == 0, "Origin-only statuses and flags must never alias a host status.");
        Check(Ff7GuestBattleStatuses.ToOriginBattle(0x100000 | 0x400000) == 0, "Shield and Manipulate must never become Invincible or Float.");
        Check(Ff7GuestBattleStatuses.ToOriginBattle(0, uint.MaxValue) == ~Ff7GuestBattleStatuses.SupportedOriginBattle, "Removing mapped statuses must preserve every unmapped original battle bit.");
    }

    private static void OriginStatusOperations()
    {
        Ff7BattleDamageActor stats = new(10, 20, 25, 0, 0, 0, 0, 0, 0);
        Ff7BattleActionVitals vitals = new(100, 100, 0, 0, 0);
        Ff7BattleDamageAction action = new(0x22, 0, 255, HasStatusOperation: true, AddsStatuses: true) { GuestOriginBattleStatuses = 0x60000, GuestOriginStatusAttack = 255 };
        Ff7BattleActionReceipt prepared = Ff7NativeBattleActionResolver.ResolveGuestContact(stats, vitals, stats, vitals, action, null, Random());
        Check(!prepared.Damage.Missed && prepared.AddedOriginBattleStatuses == 0x60000 && prepared.Target.Statuses == 0, "Double and Triple must remain in the origin bank instead of becoming host barriers.");
        Ff7BattleActionReceipt restored = JsonSerializer.Deserialize<Ff7BattleActionReceipt>(JsonSerializer.Serialize(prepared));
        Ff7BattleActionReceipt published = Ff7NativeBattleActionResolver.ApplyPreparedGuestContact(vitals, vitals, restored, action, false);
        Check(published.AddedOriginBattleStatuses == 0x60000 && published.Target == vitals, "The serialized prepared contact must publish its source status operation without changing host status bits.");
        byte[] defenses = Enumerable.Repeat((byte)100, 40).ToArray();
        defenses[25] = 200;
        defenses[26] = 200;
        Ff7BattleActionReceipt resisted = Ff7NativeBattleActionResolver.ResolveGuestContact(stats, vitals, stats, vitals, action with { GuestOriginStatusAttack = 200 }, null, Random(), originStatusResistances: defenses);
        Check(resisted.AddedOriginBattleStatuses == 0, "Source status defense rows25 and26 must reject non-forced Double and Triple.");
        Ff7BattleActionReceipt present = Ff7NativeBattleActionResolver.ResolveGuestContact(stats, vitals, stats, vitals, action, null, Random(), originBattleStatuses: 0x60000);
        Check(present.AddedOriginBattleStatuses == 0, "Reapplying a present source status must not produce another mutation.");
    }

    private static void Drain()
    {
        Ff7BattleDamageActor stats = new(10, 20, 25, 0, 0, 0, 0, 0, 0);
        Ff7BattleActionVitals source = new(20, 500, 0, 0, 0);
        Ff7BattleActionVitals target = new(500, 500, 0, 0, 0);
        Ff7BattleDamageAction action = new(0x26, 1, 255) { FixedDamage = 100, AlwaysHits = true, GuestDrainStatusAttack = 200 };
        Ff7BattleActionReceipt prepared = Ff7NativeBattleActionResolver.ResolveGuestContact(stats, source, stats, target, action, null, Random());
        Check(prepared.Target.Hp == 400 && prepared.Source.Hp == 120 && prepared.SourceHitPointDamage == -100, "The native Drain status operation must publish damage and source recovery in one receipt.");
        Ff7BattleActionReceipt restored = JsonSerializer.Deserialize<Ff7BattleActionReceipt>(JsonSerializer.Serialize(prepared));
        Ff7BattleActionReceipt published = Ff7NativeBattleActionResolver.ApplyPreparedGuestContact(source with { Hp = 30 }, target with { Hp = 450 }, restored, action, false);
        Check(published.Source.Hp == 130 && published.Target.Hp == 350, "Restored Drain must apply its prepared source amount to current vitals exactly once.");
        byte[] defenses = Enumerable.Repeat((byte)100, 40).ToArray();
        defenses[23] = 150;
        Ff7BattleActionReceipt resisted = Ff7NativeBattleActionResolver.ResolveGuestContact(stats, source, stats, target, action, null, Random(), originStatusResistances: defenses);
        Check(resisted.Source.Hp == 70 && resisted.Target.Hp == 400, "Drain resistance row23 reduces recovery without reducing primary damage.");
        Ff7BattleActionReceipt absorbed = Ff7NativeBattleActionResolver.ResolveGuestContact(stats, source, stats, target, action with { Affinity = Ff7BattleDamageAffinity.Absorb }, null, Random());
        Check(absorbed.Source.Hp == 0 && absorbed.Target.Hp == 500, "An absorbed primary Drain contact must reverse its source transaction.");
        Ff7BattleActionReceipt self = Ff7NativeBattleActionResolver.ResolveGuestContact(stats, target, stats, target, action, null, Random(), sameActor: true);
        Check(self.Source == self.Target && self.Source.Hp == 500, "A self-targeted Drain must settle through one shared HP owner.");
        Ff7BattleActionReceipt sourceZombie = Ff7NativeBattleActionResolver.ResolveGuestContact(stats, target, stats, target, action, null, Random(), originSourceStatuses: 0x40);
        Ff7BattleActionReceipt targetZombie = Ff7NativeBattleActionResolver.ResolveGuestContact(stats, target, stats, target, action, null, Random(), originTargetStatuses: 0x40);
        Ff7BattleActionReceipt bothZombie = Ff7NativeBattleActionResolver.ResolveGuestContact(stats, source, stats, target, action, null, Random(), originSourceStatuses: 0x40, originTargetStatuses: 0x40);
        Check(sourceZombie.Source.Hp == 400 && targetZombie.Source.Hp == 400 && sourceZombie.Target.Hp == 400 && targetZombie.Target.Hp == 400, "Exactly one Zombie owner reverses only the secondary Drain half.");
        Check(bothZombie.Source.Hp == 120 && bothZombie.Target.Hp == 400, "Two Zombie owners cancel the secondary Drain reversal.");
    }

    private static Ff7BattleRng Random() => new(new byte[3988], new(new byte[8]));

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
