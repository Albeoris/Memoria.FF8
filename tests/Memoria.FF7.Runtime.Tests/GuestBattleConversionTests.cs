using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Runtime;

internal static class GuestBattleConversionTests
{
    public static void Run()
    {
        Elements();
        Statuses();
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

    private static Ff7BattleRng Random() => new(new byte[3988], new(new byte[8]));

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
