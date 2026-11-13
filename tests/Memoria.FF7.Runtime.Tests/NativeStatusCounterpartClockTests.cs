using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeStatusCounterpartClockTests
{
    public static void Run()
    {
        const uint regen = Ff7NativeBattleStatuses.Regen;
        const uint doom = Ff7NativeBattleStatuses.DeathSentence;
        const uint gradual = Ff7NativeBattleStatuses.SlowNumb;
        const uint peerless = Ff7NativeBattleStatuses.Peerless;
        const uint statuses = regen | doom | gradual | peerless;
        Ff7BattleStatusTimers old = JsonSerializer.Deserialize<Ff7BattleStatusTimers>("{\"Remainder\":0,\"Sleep\":0,\"Poison\":0}");
        Ff7BattleStatusTimers restored = Ff7NativeBattleStatusClock.Apply(old, statuses, statuses);
        Check(restored.Regen == 127 && restored.DeathSentence == 60 && restored.SlowPetrify == 30 && restored.Peerless == 64, "Older snapshots must restore missing counterpart durations from the host status contract.");
        Ff7BattleStatusTimers phased = restored with { Remainder = 8100, FixedRemainder = 4050, HitPointRemainder = 32760, Regen = 1, DeathSentence = 1, SlowPetrify = 1, Peerless = 1 };
        Ff7BattleStatusTick next = Ff7NativeBattleStatusClock.Advance(phased, statuses, 128, maximumHitPoints: 320);
        Check(next.HitPointDelta > 0 && (next.Statuses & (regen | gradual | peerless)) == 0 && (next.Statuses & 0x4000) != 0 && (next.Statuses & doom) != 0 && next.DeathSentenceDue, "The final Regen tick must heal before expiry, Slow-numb must petrify, Peerless must expire and Doom must schedule its action.");
        Ff7BattleStatusTick continued = Ff7NativeBattleStatusClock.Advance(RoundTrip(next.Timers), next.Statuses, 128, maximumHitPoints: 320);
        Check(!continued.DeathSentenceDue && continued.Timers.DeathSentence == 0 && continued.HitPointDelta == 0, "A saved expired Doom counter must never be reinitialized or enqueue its action twice.");
        Ff7BattleStatusTick normal = Ff7NativeBattleStatusClock.Advance(restored, statuses, 0, maximumHitPoints: 320);
        Ff7BattleStatusTick fast = Ff7NativeBattleStatusClock.Advance(restored, statuses, 255, maximumHitPoints: 320);
        Check(normal.Timers.FixedRemainder == 68 && fast.Timers.FixedRemainder == 68 && normal.Timers.Remainder != fast.Timers.Remainder, "Doom and Slow-numb must use the separate fixed clock rather than the battle-speed clock.");
        Ff7BattleStatusTick slow = Ff7NativeBattleStatusClock.Advance(restored, statuses | 0x200, 128);
        Ff7BattleStatusTick haste = Ff7NativeBattleStatusClock.Advance(restored, statuses | 0x100, 128);
        Check(slow.Timers.FixedRemainder == 34 && haste.Timers.FixedRemainder == 136, "Slow and Haste must modify the fixed countdown clock at their native rates.");
        Ff7BattleStatusTick stopped = Ff7NativeBattleStatusClock.Advance(phased, statuses | 0x400, 128, maximumHitPoints: 320);
        Check(stopped.Timers == phased && !stopped.DeathSentenceDue && stopped.HitPointDelta == 0, "Stop must preserve both countdown phases and the HP accumulator.");
        Ff7BattleStatusTick cancellation = Ff7NativeBattleStatusClock.Advance(default, regen | Ff7NativeBattleStatuses.Imprisoned, 128, 1, 320);
        Check(cancellation.HitPointDelta == 0, "Regen and draining capture must cancel in their shared native HP coefficient.");
        Ff7BattleStatusTimers healed = Ff7NativeBattleStatusClock.Apply(default, 0, regen);
        int recovery = 0;
        for (int frame = 0; frame < 90; frame++)
        {
            Ff7BattleStatusTick direct = Ff7NativeBattleStatusClock.Advance(healed, regen, 128, maximumHitPoints: 320);
            Ff7BattleStatusTick cold = Ff7NativeBattleStatusClock.Advance(RoundTrip(healed), regen, 128, maximumHitPoints: 320);
            Check(direct == cold, "JSON continuation must preserve native Regen fractions and countdown phases.");
            recovery += direct.HitPointDelta;
            healed = direct.Timers;
        }
        Check(recovery == (90 * 10 * Ff7NativeAtb.BaseRate(128) * 2 >> 15), "Regen must accumulate maxHP/32 on the native signed Q15 clock.");
        InstalledCountdown();
        Console.WriteLine("PASS native status counterpart clocks, Regen recovery, expiry, countdown ownership and JSON continuation.");
    }

    private static void InstalledCountdown()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            Console.WriteLine("SKIP installed status countdown session: MEMORIA_FF7_GAME_DIR unset.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(directory);
        using Ff7Session original = Ff7Session.NewGame(installation);
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path)).Single(item => item.FormationId == 300);
        IReadOnlyList<short> modifiers = Ff7PcExecutableData.Load(installation).NativeMateriaStatModifiers;
        Ff7BattleAttack death = Ff7BattleAttack.Read(kernel[1].Data.Span.Slice(0x36 * Ff7BattleAttack.RecordSize, Ff7BattleAttack.RecordSize));
        Check(death.TargetFlags == 0 && death.CastingCost == 0 && death.StatusMask == 1, "The original countdown must use its cost-free self-targeted Death record.");
        Ff7BattleSession Create(Ff7BattleSnapshot? snapshot = null) => new(formation, original.Progress, kernel, modifiers, 1, snapshot: snapshot);
        Ff7BattleSnapshot initial = Create().Capture();
        Ff7BattleSnapshot setup = initial with { Actors = initial.Actors.Select(actor => actor.Actor.Slot == 0 ? actor with { Actor = actor.Actor with { Statuses = Ff7NativeBattleStatuses.DeathSentence | Ff7NativeBattleActionResolver.Sleep }, Timers = new(0, 100, 0) { DeathSentence = 1, FixedRemainder = 4050 } } : actor with { Actor = actor.Actor with { Statuses = 0x400 } }).ToArray() };
        Ff7BattleSession battle = Create(RoundTrip(setup));
        battle.AdvanceTurn();
        Ff7BattleSnapshot queued = battle.Capture();
        Ff7BattleQueuedCommand command = queued.Queue.Slots.Where(entry => entry?.Command.StatusCountdown == true).Single()!.Command;
        Check(command is { Priority: 3, CommandId: 2, Argument: 0x36, TargetMask: 0 } && queued.Actors.Single(actor => actor.Actor.Slot == 0).Timers.DeathSentence == 0, "Doom expiry must enqueue exactly one native Death command while retaining its expired counter.");
        Ff7BattleSession cold = Create(RoundTrip(queued));
        for (int step = 0; step < 12; step++)
        {
            battle.Advance();
            cold.Advance();
            Check(JsonSerializer.Serialize(battle.Capture()) == JsonSerializer.Serialize(cold.Capture()), "A queued countdown must continue identically after a real snapshot round-trip.");
        }
        Ff7BattleAiActor target = battle.Capture().Actors.Single(actor => actor.Actor.Slot == 0).Actor;
        Check((target.Statuses & 1) != 0 && (target.Statuses & Ff7NativeBattleStatuses.DeathSentence) == 0 && target.MagicPoints == setup.Actors.Single(actor => actor.Actor.Slot == 0).Actor.MagicPoints, "A sleeping actor's countdown must execute Death, clear Doom and spend no MP.");
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
