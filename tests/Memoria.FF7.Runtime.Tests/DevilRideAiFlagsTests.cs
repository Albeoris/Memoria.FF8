using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class DevilRideAiFlagsTests
{
    public static void Run()
    {
        SceneBattleData data = new(0, [], [], [], null);
        Ff7BattleAiActor[] actors = [new(0, 0, 8, 10, 10, 100, 100), new(4, 0, 8, 10, 10, 100, 100) { CoveredCharacter = 0x13 }];
        Ff7BattleAiContext context = new(4, actors, 0x11, 0);
        Check(!JsonSerializer.Serialize(actors[0]).Contains("CoveredCharacter", StringComparison.Ordinal), "Absent optional contact state must retain the old serialized actor shape.");
        Ff7BattleRng random = new(new byte[3988], new(new byte[8]));
        byte[] read = [0x11, 0, 0, 0x02, 0x60, 0x20, 0x01, 0xB8, 0x40, 0x80, 0x83, 0x90, 0x73];
        Check(Ff7BattleAi.Run(read, context, [], data, random).LocalMemory[0] == 0x13, "Self selection must read the unsigned covered-character byte.");
        Check(Ff7BattleAi.Run(read, context with { ActorSlot = 0 }, [], data, random).LocalMemory[0] == 0, "An untouched native actor must retain the zero-initialized byte.");
        byte[] write = [0x12, 0x60, 0x20, 0x11, 0xB8, 0x40, 0x80, 0x61, 0xFF, 1, 0x90, 0x73];
        Check(Ff7BattleAi.Run(write, context, [], data, random).ActorUpdates.Single().CoveredCharacter == 255, "A native byte store must truncate without changing its selected owner.");
        byte[] compare = [0x11, 0, 0, 0x02, 0x60, 0x20, 0x01, 0xB8, 0x40, 0x80, 0x83, 0x60, 0x13, 0x40, 0x90, 0x73];
        Check(Ff7BattleAi.Run(compare, context, [], data, random).LocalMemory[0] == 1, "The original covered-variant equality operand must compare as a byte.");
        bool rejectedWord = false;
        try
        {
            Ff7BattleAi.Run([0x02, 0x60, 0x20, 0x02, 0xB8, 0x40, 0x80, 0x73], context, [], data, random);
        }
        catch (NotSupportedException)
        {
            rejectedWord = true;
        }
        Check(rejectedWord, "The byte projection must not silently accept an unsupported word read.");
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            Console.WriteLine("SKIP original covered-character counters: MEMORIA_FF7_GAME_DIR is unset.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(directory);
        using Ff7Session original = Ff7Session.NewGame(installation);
        Ff7Progress progress = original.Progress with { Party = [0, 1, 2] };
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        IReadOnlyList<SceneFormation> formations = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path));
        Ff7PcExecutableData executable = Ff7PcExecutableData.Load(installation);
        using Ff7WorldResources world = Ff7WorldResources.Load(installation);
        byte[] table = world.ReadRequired("enc_w.bin");
        HashSet<int> selected = [];
        for (int ground = 0; ground < 4; ground++)
        {
            for (int offset = 2; offset < 32; offset += 2)
            {
                ushort entry = BinaryPrimitives.ReadUInt16LittleEndian(table.AsSpan(0xA0 + ground * 32 + offset));
                if (entry >> 10 != 0)
                {
                    selected.Add(entry & 1023);
                }
            }
        }
        foreach (int id in selected.Order())
        {
            SceneFormation formation = formations.Single(value => value.FormationId == id);
            Ff7BattleSession fresh = new(formation, progress, kernel, executable.NativeMateriaStatModifiers, id + 1);
            Ff7BattleSnapshot start = fresh.Capture();
            Check(start.Actors.All(actor => actor.Actor.CoveredCharacter == 0), $"Formation {id} lost the native zero initialization.");
            start = start with
            {
                Actors = start.Actors.Select(actor => actor.Actor.Slot < 3 ? actor with { Actor = actor.Actor with { HitPoints = 10000, MaxHitPoints = 10000 } } : actor).ToArray(),
                Queue = Ff7BattleCommandQueue.Enqueue(start.Queue, new(2, 1, (byte)Ff7BattleCommand.Attack, 0, 1))
            };
            Ff7BattleSession Restore(Ff7BattleSnapshot snapshot) => new(formation, progress, kernel, executable.NativeMateriaStatModifiers, id + 1, snapshot: JsonSerializer.Deserialize<Ff7BattleSnapshot>(JsonSerializer.Serialize(snapshot))!);
            Ff7BattleSession legacyFresh = Restore(start with { Actors = start.Actors.Select(actor => actor with { Actor = actor.Actor with { CoveredCharacter = null, LastAttackerMask = 0 } }).ToArray() });
            Check(legacyFresh.Capture().Actors.All(actor => actor.Actor.CoveredCharacter == 0), "Absent historical pre-contact bytes must derive the native startup zero without replay.");
            Ff7BattleSession warm = Restore(start);
            Ff7BattleSession cold = Restore(start);
            for (int tick = 0; tick < 2000; tick++)
            {
                warm.Advance(waitForCommand: true);
                cold.Advance(waitForCommand: true);
                if (warm.LastHit is { Source: 1, Target: 0 } && warm.Action is null)
                {
                    break;
                }
            }
            Check(warm.LastHit is { Source: 1, Target: 0 } && warm.Capture().Actors.Single(actor => actor.Actor.Slot == 0).Actor.CoveredCharacter == 255, $"Formation {id} did not publish the native contact reset before Cloud's physical counter.");
            Check(JsonSerializer.Serialize(warm.Capture()) == JsonSerializer.Serialize(cold.Capture()), $"Formation {id} counter changed after JSON restore.");
            cold = Restore(warm.Capture());
            for (int tick = 0; tick < 6000 && warm.LastHit is not { Source: >= 4 }; tick++)
            {
                warm.Advance();
                cold.Advance();
            }
            Check(warm.LastHit is { Source: >= 4 }, $"Formation {id} did not execute its original enemy turn.");
            Check(JsonSerializer.Serialize(warm.Capture()) == JsonSerializer.Serialize(cold.Capture()), $"Formation {id} enemy turn/counter changed after JSON restore.");
            Ff7BattleSnapshot recorded = warm.Capture();
            Ff7BattleActorState target = recorded.Actors.Single(actor => actor.Actor.Slot == 0);
            Ff7BattleSession explicitZero = Restore(recorded with { Actors = recorded.Actors.Select(actor => actor.Actor.Slot == 0 ? actor with { Actor = actor.Actor with { CoveredCharacter = 0 } } : actor).ToArray() });
            Check(explicitZero.Capture().Actors.Single(actor => actor.Actor.Slot == 0).Actor.CoveredCharacter == 0, "An explicitly recorded zero must survive cold restore.");
            Ff7BattleSession legacyContact = Restore(recorded with { Actors = recorded.Actors.Select(actor => actor.Actor.Slot == 0 ? actor with { Actor = actor.Actor with { CoveredCharacter = null, LastAttackerMask = 2 } } : actor).ToArray() });
            Check(legacyContact.Capture().Actors.Single(actor => actor.Actor.Slot == 0).Actor.CoveredCharacter == 255, "Recorded historical contact evidence must derive the native FF reset without replay.");
            Check(target.Actor.CoveredCharacter == 255, "The contact byte must survive subsequent snapshots.");
        }
        Console.WriteLine($"Covered-character byte selectors/comparison/store and {selected.Count} original Grasslands initialization/counter/turn/JSON continuations passed.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
