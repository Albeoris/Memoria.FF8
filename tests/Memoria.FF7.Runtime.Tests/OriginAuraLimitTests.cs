using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class OriginAuraLimitTests
{
    public static void Run()
    {
        GaugeArithmetic();
        InstalledEnemyDamage();
        Console.WriteLine("PASS Aura multiplies newly earned native Limit gain by 5/4 after native rounding and status modifiers.");
    }

    private static void GaugeArithmetic()
    {
        foreach ((uint statuses, byte original, byte accelerated) in new (uint, byte, byte)[] { (0, 40, 50), (0x10, 20, 25), (0x20, 80, 100), (0x30, 80, 100) })
        {
            Check(Ff7NativeBattleLimits.GainFromEnemyDamage(0, 23, 302, statuses, 140) == original, "Aura admission must preserve native Fury/Sadness precedence and rounding when absent.");
            Check(Ff7NativeBattleLimits.GainFromEnemyDamage(0, 23, 302, statuses, 140, aura: true) == accelerated, "Aura must accelerate only the already computed native gain.");
        }
        Check(Ff7NativeBattleLimits.GainFromEnemyDamage(70, 23, 302, 0, 140, aura: true) == 120, "Aura must not multiply the gauge already accumulated before this contact.");
        Check(Ff7NativeBattleLimits.GainFromEnemyDamage(70, 0, 302, 0, 140, aura: true) == 70, "Aura with no earned damage must preserve the accumulated gauge.");
        Check(Ff7NativeBattleLimits.GainFromEnemyDamage(0, 1, 300, 0, 100, aura: true) == 2, "Aura must truncate its own fraction after the native divisor has already truncated the gain.");
        Check(Ff7NativeBattleLimits.GainFromEnemyDamage(0, 1, 301, 0, 1, aura: true) == 0, "Aura must preserve the original HP-ratio truncation before any status or divisor calculation.");
        Check(Ff7NativeBattleLimits.GainFromEnemyDamage(230, 23, 302, 0, 140, aura: true) == byte.MaxValue, "Aura gain must saturate at the original Limit maximum.");
        Check(Ff7NativeBattleLimits.GainFromEnemyDamage(0, 9999, 1, 0x20, 1, aura: true) == byte.MaxValue, "The Aura fraction must not overflow for the largest supported damage and smallest positive vitals/divisor.");
        Check(!Ff7NativeBattleLimits.IsReady(120) && Ff7NativeBattleLimits.Consume(byte.MaxValue) == 0, "Aura must retain native ready and consumption contracts.");
    }

    private static void InstalledEnemyDamage()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            Console.WriteLine("SKIP installed Aura Limit gain: MEMORIA_FF7_GAME_DIR unset.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(directory);
        using Ff7Session original = Ff7Session.NewGame(installation);
        Ff7Progress progress = original.Progress with { Party = [0, 255, 255] };
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path)).Single(value => value.FormationId == 300);
        IReadOnlyList<short> modifiers = Ff7PcExecutableData.Load(installation).NativeMateriaStatModifiers;
        Ff7BattleSession Create(Ff7BattleSnapshot? snapshot = null) => new(formation, progress, kernel, modifiers, 1, snapshot: snapshot);
        Ff7BattleSession initial = Create();
        SceneBattleAttack machineGun = initial.Data.Attacks.Single(attack => attack.Id == 0x110);
        Ff7BattleQueuedCommand command = new(2, 4, 0x20, (ushort)machineGun.Slot, 1, machineGun.Id);
        Ff7BattleSnapshot entry = initial.Capture();
        Ff7BattleSnapshot queued = entry with
        {
            Actors = entry.Actors.Select(actor => actor with
            {
                LimitGauge = actor.Actor.Slot == 0 ? (byte)30 : actor.LimitGauge,
                Gauge = 0,
                AwaitingTurn = false,
                Actor = actor.Actor with { HitPoints = 1000, MaxHitPoints = 1000, Statuses = 0 }
            }).ToArray(),
            Queue = Ff7BattleCommandQueue.Enqueue(entry.Queue, command)
        };
        int divisor = Ff7NativeBattleLimits.ReadGaugeDivisor(progress.Characters.Single(character => character.Id == 0), kernel);
        Ff7BattleSession plain = Create(RoundTrip(queued));
        Ff7BattleSnapshot auraEntry = queued with { GuestOriginStatuses = [new(0, Ff7GuestBattleStatuses.OriginAura)] };
        Ff7BattleSession aura = Create(RoundTrip(auraEntry));
        Ff7BattleSession cold = Create(RoundTrip(auraEntry));
        Check(aura.GetOriginBattleStatuses(0) == Ff7GuestBattleStatuses.OriginAura && aura.Actors[0].LimitGauge == 30, "Applying Aura to a native FFVII actor must retain its accumulated gauge and distinct source owner.");
        for (int frame = 0; frame < 32 && aura.LastHit is null; frame++)
        {
            plain.Advance();
            aura.Advance();
            cold = Create(RoundTrip(cold.Capture()));
            cold.Advance();
            Check(JsonSerializer.Serialize(aura.Capture()) == JsonSerializer.Serialize(cold.Capture()), "Aura enemy damage and gauge gain must continue identically after each real JSON restore.");
        }
        Check(aura.LastHit is { Source: 4, Target: 0, Damage.Damage: > 0 } && plain.LastHit?.Damage == aura.LastHit.Damage, "The installed original enemy attack must deliver identical positive HP damage with and without Aura.");
        int damage = aura.LastHit!.Damage.Damage;
        Check(plain.Actors[0].LimitGauge == Ff7NativeBattleLimits.GainFromEnemyDamage(30, damage, 1000, 0, divisor), "The unafflicted installed contact must use the original Limit calculation.");
        Check(aura.Actors[0].LimitGauge == Ff7NativeBattleLimits.GainFromEnemyDamage(30, damage, 1000, 0, divisor, aura: true), "The native FFVII damage publication must read its own Aura owner exactly once.");
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
