using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeStatusConsumersTests
{
    public static void Run()
    {
        Ff7BattleTargetMasks masks = new(0x33, 0x33, 0, 0x33, 3, 0x30);
        Ff7BattleRng Random() => new(new byte[3988], new(new byte[8]));
        Ff7BattleRng confused = Random();
        Check(Ff7NativeBattleTargets.Resolve(0, 0, 0x23, ushort.MaxValue, false, masks, confused, Ff7NativeBattleStatuses.Confusion, 1) == 1, "Confuse must replace an automatic Attack target with the source side, including itself.");
        Check(Ff7NativeBattleTargets.Resolve(0, 0x10, 0x23, ushort.MaxValue, false, masks, Random(), Ff7NativeBattleStatuses.Confusion, 0x14) == 0x10, "Limit target selection must retain its native Confuse exception.");
        Check(Ff7NativeBattleTargets.Resolve(4, 0, 0x23, ushort.MaxValue, false, masks, Random(), Ff7NativeBattleStatuses.Confusion, 0x20) == 0x10, "Confused enemy commands must use the same inverted-side target selector.");
        Ff7BattleCommandQueueState queue = Ff7BattleCommandQueue.StageReady(Ff7BattleCommandQueueState.Create(), 0, 1, guestAttack: true, compelledAttack: true);
        RoundTrip(queue).Validate();
        Check(queue.Ready.Single(command => command is not null) is { CompelledAttack: true, TargetMask: 0 }, "A compelled guest must retain an unselected automatic target until dispatch.");
        Installed();
        Console.WriteLine("PASS common Confuse targets, automatic guest queue, native Reflect charges and JSON continuation.");
    }

    private static void Installed()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            Console.WriteLine("SKIP installed status consumers: MEMORIA_FF7_GAME_DIR unset.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(directory);
        using Ff7Session original = Ff7Session.NewGame(installation);
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path)).Single(value => value.FormationId == 300);
        IReadOnlyList<short> modifiers = Ff7PcExecutableData.Load(installation).NativeMateriaStatModifiers;
        Ff7Progress progress = original.Progress with { Party = [0, 1, 255] };
        Ff7BattleSession Create(Ff7BattleSnapshot? snapshot = null) => new(formation, progress, kernel, modifiers, 1, snapshot: snapshot);
        var healing = Enumerable.Range(0, kernel[1].Data.Length / Ff7BattleAttack.RecordSize).Select(index => (Id: (ushort)index, Attack: Ff7BattleAttack.Read(kernel[1].Data.Span.Slice(index * Ff7BattleAttack.RecordSize, Ff7BattleAttack.RecordSize)))).First(value => value.Attack.DamageCalculation == 0x25 && (value.Attack.SpecialFlags & 0x200) == 0 && value.Attack.AdditionalEffect == byte.MaxValue && Ff7NativeBattleStatuses.ReadRecordMask(value.Attack) == 0);
        foreach ((byte? count, bool immune) in new (byte?, bool)[] { (null, false), (0, false), (3, true) })
        {
            Ff7BattleSnapshot entry = Create().Capture();
            Ff7BattleActorState[] actors = entry.Actors.Select(actor => actor with
            {
                Gauge = 0,
                AwaitingTurn = false,
                ReflectionCount = actor.Actor.Slot == 1 ? count : null,
                StatusProtection = actor.Actor.Slot == 1 && immune ? Ff7NativeBattleStatuses.Reflect : 0,
                Actor = actor.Actor with { Statuses = actor.Actor.Slot == 1 ? Ff7NativeBattleStatuses.Reflect : actor.Actor.Slot >= 4 ? 0x400U : 0, HitPoints = 40, MaxHitPoints = 1000, MagicPoints = 100, MaxMagicPoints = 100 }
            }).ToArray();
            Ff7BattleSnapshot setup = entry with { Actors = actors, Queue = Ff7BattleCommandQueue.StageReady(entry.Queue, 0, 2, healing.Id, 2) };
            Ff7BattleSession live = Create(RoundTrip(setup));
            Ff7BattleSession cold = Create(RoundTrip(setup));
            for (int frame = 0; frame < 16 && live.LastHit is null; frame++)
            {
                live.Advance();
                cold = Create(RoundTrip(cold.Capture()));
                cold.Advance();
                Check(JsonSerializer.Serialize(live.Capture()) == JsonSerializer.Serialize(cold.Capture()), "Reflect must preserve recipients, RNG, charges and one casting cost after JSON restore.");
            }
            Ff7BattleActorState reflector = live.Actors.Single(actor => actor.Actor.Slot == 1);
            Check(live.LastHit is { Source: 0, Target: >= 4 } && reflector.Actor.HitPoints == 40, "Same-side Reflect must heal the selected opposite-side actor while retaining its source identity.");
            Check(live.Actors.Single(actor => actor.Actor.Slot == 0).Actor.MagicPoints == 100 - healing.Attack.CastingCost, "A reflected native spell must pay its source MP once.");
            Check(immune ? reflector.ReflectionCount == 3 && (reflector.Actor.Statuses & Ff7NativeBattleStatuses.Reflect) != 0 : count == 0 ? reflector.ReflectionCount is null && (reflector.Actor.Statuses & Ff7NativeBattleStatuses.Reflect) == 0 : reflector.ReflectionCount == 2, "Reflect must retain an immune ward, hydrate an old counter, and remove the fourth-use ward.");
        }
        Ff7BattleSnapshot initial = Create().Capture();
        Ff7BattleSnapshot bouncing = initial with
        {
            Actors = initial.Actors.Select(actor => actor with
            {
                Gauge = 0,
                AwaitingTurn = false,
                ReflectionCount = actor.Actor.Slot is 0 or 4 ? (byte)0 : null,
                Actor = actor.Actor with { Statuses = actor.Actor.Slot is 0 or 4 ? Ff7NativeBattleStatuses.Reflect : 0x400U, HitPoints = 40, MaxHitPoints = 1000, MagicPoints = 100, MaxMagicPoints = 100 }
            }).ToArray(),
            Queue = Ff7BattleCommandQueue.StageReady(initial.Queue, 0, 2, healing.Id, 0x10)
        };
        Ff7BattleSession bounced = Create(RoundTrip(bouncing));
        Ff7BattleSession bouncedCold = Create(RoundTrip(bouncing));
        for (int frame = 0; frame < 32 && (bounced.LastHit is null || bounced.Action is not null); frame++)
        {
            bounced.Advance();
            bouncedCold = Create(RoundTrip(bouncedCold.Capture()));
            bouncedCold.Advance();
            Check(JsonSerializer.Serialize(bounced.Capture()) == JsonSerializer.Serialize(bouncedCold.Capture()), "Two reflected wards must exhaust and finish identically after each cold callback.");
        }
        Check(bounced.LastHit is { Source: 0, Target: 4 } && bounced.Actors.Single(actor => actor.Actor.Slot == 0).ReflectionCount is null && (bounced.Actors.Single(actor => actor.Actor.Slot == 0).Actor.Statuses & Ff7NativeBattleStatuses.Reflect) == 0, "An opposite-side bounce must return through its caster, exhaust the caster's fourth-use ward and settle once on its original target.");
        Ff7BattleSnapshot next = bounced.Capture();
        Check(next.Action is null, "The reflected action must release its ordinary action owner.");
        Ff7BattleSession continuation = Create(RoundTrip(next with { Queue = Ff7BattleCommandQueue.StageReady(next.Queue, 0, 2, healing.Id, 0x10) }));
        for (int frame = 0; frame < 16 && continuation.LastHit?.Tick == next.LastHit?.Tick; frame++)
        {
            continuation.Advance();
        }
        Check(continuation.LastHit is { Target: 4 } && continuation.LastHit.Tick > next.LastHit!.Tick && (continuation.Actors.Single(actor => actor.Actor.Slot == 0).Actor.Statuses & Ff7NativeBattleStatuses.Reflect) == 0, "The next contact after cold restore must not reconstruct an exhausted ward.");
        Check(continuation.Actors.Single(actor => actor.Actor.Slot == 0).Actor.MagicPoints == 100 - 2 * healing.Attack.CastingCost, "Two original cast occurrences must spend exactly two MP costs across a reflected continuation.");
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
