using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeEventProgressWitnessTests
{
    private sealed class Inputs
    {
        internal byte[] Persistent = new byte[1280];
        internal byte[] Map = new byte[256];
        internal Ff7Progress Progress = new()
        {
            InitialKernelData = [1],
            Inventory = [new(1, 2, 0), new(3, 4, null)],
            Materia = [new(1, 23, 0), new(4, 56, null)],
            Characters = [new(0, "first", 3, 40, 120, 30, [1]), new(1, "second", 4, 50, 140, 40, [2])],
            Party = [0, 1, 255],
            Gil = 200,
            AvailableCharacters = 3,
            LockedCharacters = 1,
            CompletedBattles = [new(1, 10, 20, new(5, 6, 7, [new(1, 2), new(3, 4)])), new(2, 11, 21, new(8, 9, 10, [new(5, 6)]))]
        };
    }

    internal static void Run()
    {
        Inputs input = new();
        Ff7EventProgressWitnessMemo memo = new();
        string native = JsonSerializer.Serialize(new { input.Persistent, input.Map, input.Progress });
        string first = Read(memo, input);
        Check(first == Reference(input) && ReferenceEquals(first, Read(memo, input)), "A frozen witness did not reuse the exact original hash string.");
        Check(JsonSerializer.Serialize(new { input.Persistent, input.Map, input.Progress }) == native, "Witness memoization changed its source values.");
        foreach ((string name, Action<Inputs> change) in new (string, Action<Inputs>)[]
        {
            ("persistent", value => value.Persistent[24]++),
            ("inventory item", value => value.Progress.Inventory[0] = value.Progress.Inventory[0] with { ItemId = 2 }),
            ("inventory quantity", value => value.Progress.Inventory[0] = value.Progress.Inventory[0] with { Quantity = 3 }),
            ("inventory slot", value => value.Progress.Inventory[0] = value.Progress.Inventory[0] with { NativeSlot = null }),
            ("inventory order", value => Array.Reverse(value.Progress.Inventory)),
            ("inventory length", value => value.Progress = value.Progress with { Inventory = [] }),
            ("materia id", value => value.Progress.Materia[0] = value.Progress.Materia[0] with { MateriaId = 2 }),
            ("materia AP", value => value.Progress.Materia[0] = value.Progress.Materia[0] with { AbilityPoints = 24 }),
            ("materia slot", value => value.Progress.Materia[0] = value.Progress.Materia[0] with { NativeSlot = null }),
            ("materia order", value => Array.Reverse(value.Progress.Materia)),
            ("materia length", value => value.Progress = value.Progress with { Materia = [] }),
            ("gil", value => value.Progress = value.Progress with { Gil = 201 }),
            ("character id", value => value.Progress.Characters[0] = value.Progress.Characters[0] with { Id = 2 }),
            ("character level", value => value.Progress.Characters[0] = value.Progress.Characters[0] with { Level = 5 }),
            ("character experience", value => value.Progress.Characters[0] = value.Progress.Characters[0] with { Experience = 41 }),
            ("character HP", value => value.Progress.Characters[0] = value.Progress.Characters[0] with { HitPoints = 121 }),
            ("character MP", value => value.Progress.Characters[0] = value.Progress.Characters[0] with { MagicPoints = 31 }),
            ("character order", value => Array.Reverse(value.Progress.Characters)),
            ("character length", value => value.Progress = value.Progress with { Characters = [] }),
            ("party slot", value => value.Progress.Party[0] = 2),
            ("party order", value => Array.Reverse(value.Progress.Party)),
            ("party length", value => value.Progress = value.Progress with { Party = [] }),
            ("available characters", value => value.Progress = value.Progress with { AvailableCharacters = 7 }),
            ("locked characters", value => value.Progress = value.Progress with { LockedCharacters = 3 }),
            ("battle generation", value => value.Progress.CompletedBattles[0] = value.Progress.CompletedBattles[0] with { FieldGeneration = 3 }),
            ("battle request", value => value.Progress.CompletedBattles[0] = value.Progress.CompletedBattles[0] with { RequestId = 12 }),
            ("battle formation", value => value.Progress.CompletedBattles[0] = value.Progress.CompletedBattles[0] with { Formation = 22 }),
            ("battle gil", value => ChangeReward(value, reward => reward with { Gil = 9 })),
            ("battle experience", value => ChangeReward(value, reward => reward with { Experience = 11 })),
            ("battle AP", value => ChangeReward(value, reward => reward with { AbilityPoints = 12 })),
            ("battle item id", value => value.Progress.CompletedBattles[0].Reward.Items[0] = new(7, 2)),
            ("battle item quantity", value => value.Progress.CompletedBattles[0].Reward.Items[0] = new(1, 3)),
            ("battle item order", value => Array.Reverse(value.Progress.CompletedBattles[0].Reward.Items)),
            ("battle item length", value => ChangeReward(value, reward => reward with { Items = [] })),
            ("battle order", value => Array.Reverse(value.Progress.CompletedBattles)),
            ("battle length", value => value.Progress = value.Progress with { CompletedBattles = [] })
        })
        {
            Inputs changed = new();
            Ff7EventProgressWitnessMemo current = new();
            string before = Read(current, changed);
            change(changed);
            string expected = Reference(changed);
            string actual = Read(current, changed);
            Check(actual == expected && actual != before && ReferenceEquals(actual, Read(current, changed)), $"A changed {name} reused a stale witness or changed its reference serialization.");
        }
        Inputs omitted = new();
        Ff7EventProgressWitnessMemo stable = new();
        string original = Read(stable, omitted);
        Array.Fill(omitted.Persistent, (byte)99, 16, 8);
        omitted.Map[0]++;
        omitted.Progress.Characters[0].NativeRecord[0]++;
        omitted.Progress.Characters[0] = omitted.Progress.Characters[0] with { Name = "renamed" };
        omitted.Progress = omitted.Progress with { Clock = new(19, 123, 1, 2, 3), FieldRandomIndex = 19, NativeMessageSpeed = 4, InitialKernelData = [2] };
        Check(original == Reference(omitted) && ReferenceEquals(original, Read(stable, omitted)), "An omitted clock or presentation value changed existing witness semantics.");
        Ff7Progress restored = JsonSerializer.Deserialize<Ff7Progress>(JsonSerializer.Serialize(input.Progress))!;
        Ff7EventProgressWitnessMemo cold = new();
        Check(!cold.TryRead(Normalize(input.Persistent), restored, out _), "A restored context inherited process-local cached witness state.");
        string receipts = JsonSerializer.Serialize(restored.CompletedBattles);
        Inputs restoredInput = new() { Persistent = input.Persistent.ToArray(), Progress = restored };
        Check(Read(cold, restoredInput) == first && JsonSerializer.Serialize(restored.CompletedBattles) == receipts, "Cold memo initialization changed the original witness or recorded battle receipts.");
        CheckUnknownLeaf();
        CheckSessionRestore();
        Benchmark(new(), 10000, "authored");
        Console.WriteLine("Event-progress witness memo matches the original ordered serialization and snapshots nested mutable rewards.");
    }

    private static void ChangeReward(Inputs input, Func<Ff7BattleReward, Ff7BattleReward> change)
    {
        input.Progress.CompletedBattles[0] = input.Progress.CompletedBattles[0] with { Reward = change(input.Progress.CompletedBattles[0].Reward) };
    }

    private static byte[] Normalize(byte[] source)
    {
        byte[] persistent = source.ToArray();
        Array.Clear(persistent, 16, 8);
        return persistent;
    }

    private static string Reference(Inputs input)
    {
        byte[] persistent = Normalize(input.Persistent);
        Ff7Progress Progress = input.Progress;
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new { Persistent = persistent, Progress.Inventory, Progress.Materia, Progress.Gil, Characters = Progress.Characters.Select(character => new { character.Id, character.Level, character.Experience, character.HitPoints, character.MagicPoints }), Progress.Party, Progress.AvailableCharacters, Progress.LockedCharacters, Progress.CompletedBattles });
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private static string Read(Ff7EventProgressWitnessMemo memo, Inputs input)
    {
        byte[] persistent = Normalize(input.Persistent);
        if (memo.TryRead(persistent, input.Progress, out string value))
        {
            return value;
        }
        value = Reference(input);
        memo.Remember(persistent, input.Progress, value);
        return value;
    }

    private static void CheckUnknownLeaf()
    {
        Inputs input = new();
        input.Progress.CompletedBattles[0].Reward.Items[0] = null!;
        Ff7EventProgressWitnessMemo memo = new();
        string value = Read(memo, input);
        Check(value == Reference(input) && !memo.TryRead(Normalize(input.Persistent), input.Progress, out _), "A null reward leaf acquired a partial cache key instead of the original computation.");
    }

    private static void CheckSessionRestore()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_WITNESS_SAVE");
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(game))
        {
            Console.WriteLine("SKIP optional standalone witness cold-restore check; configure MEMORIA_FF7_WITNESS_SAVE and MEMORIA_FF7_GAME_DIR.");
            return;
        }
        byte[] source = File.ReadAllBytes(path);
        using MemoryStream stream = new(source, false);
        Ff7SessionSave save = CombinedSessionCodec.Read(stream).Ff7!;
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(game), save);
        Ff7EventProgressWitnessMemo memo = (Ff7EventProgressWitnessMemo)typeof(Ff7Session).GetField("eventProgressWitnessMemo", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(session)!;
        Check(!memo.TryRead(Normalize(session.Machine.Variables.CopyPersistent()), session.Progress, out _), "A restored Session inherited a witness memo.");
        string before = JsonSerializer.Serialize(session.Capture());
        string witness = session.CaptureEventProgressWitness();
        Inputs actual = new() { Persistent = session.Machine.Variables.CopyPersistent(), Progress = session.Progress };
        Check(witness == Reference(actual), "A restored Session changed the original witness serialization.");
        Check(ReferenceEquals(witness, session.CaptureEventProgressWitness()), "A restored frozen Session recomputed its witness.");
        Benchmark(actual, 1000, "standalone", session.CaptureEventProgressWitness);
        Check(before == JsonSerializer.Serialize(session.Capture()) && source.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "Memo reads changed saved native facts or source bytes.");
    }

    private static void Benchmark(Inputs input, int count, string label, Func<string>? readWarm = null)
    {
        Ff7EventProgressWitnessMemo memo = new();
        string Cached() => readWarm is null ? Read(memo, input) : readWarm();
        string expected = Cached();
        for (int index = 0; index < 100; index++)
        {
            Reference(input);
            Cached();
        }
        long allocation = GC.GetAllocatedBytesForCurrentThread();
        Stopwatch timer = Stopwatch.StartNew();
        string? result = null;
        for (int index = 0; index < count; index++)
        {
            result = Reference(input);
        }
        timer.Stop();
        double uncached = timer.Elapsed.TotalMilliseconds;
        long uncachedBytes = GC.GetAllocatedBytesForCurrentThread() - allocation;
        Check(result == expected, "The uncached benchmark changed witness semantics.");
        allocation = GC.GetAllocatedBytesForCurrentThread();
        timer.Restart();
        for (int index = 0; index < count; index++)
        {
            result = Cached();
        }
        timer.Stop();
        long cachedBytes = GC.GetAllocatedBytesForCurrentThread() - allocation;
        Check(ReferenceEquals(result, expected), "The warm benchmark did not exercise actual memo hits.");
        Console.WriteLine($"Frozen {label} witness microbenchmark only ({count} calls; inventory {input.Progress.Inventory.Length}, materia {input.Progress.Materia.Length}, characters {input.Progress.Characters.Length}, battles {input.Progress.CompletedBattles.Length}): uncached {uncached:F3}ms/{uncachedBytes}B; warm memo {timer.Elapsed.TotalMilliseconds:F3}ms/{cachedBytes}B. This is not an App or gameplay speedup measurement.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
