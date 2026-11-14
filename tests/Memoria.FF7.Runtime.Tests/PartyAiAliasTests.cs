using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class PartyAiAliasTests
{
    public static void Run()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            Console.WriteLine("SKIP installed party AI aliases: MEMORIA_FF7_GAME_DIR is unset.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(directory);
        using Ff7Session original = Ff7Session.NewGame(installation);
        Ff7Progress progress = original.Progress with { Party = [0, 1, 2] };
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        Ff7PcExecutableData executable = Ff7PcExecutableData.Load(installation);
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path)).Single(formation => formation.FormationId == 324);
        Ff7BattleSession fresh = new(formation, progress, kernel, executable.NativeMateriaStatModifiers, 1);
        Ff7BattleSnapshot initial = fresh.Capture();
        Check(initial.PartyAiAliases is { Length: 12 } && initial.PartyAiAliases[1] == 3 && initial.PartyAiAliases[2] == 3 && initial.Actors.Single(actor => actor.Actor.Slot == 1).Identity == 1, "Original regular-party initialization must link shared banks without replacing logical actors.");
        TestOpcode(SceneBattleData.Read(formation), initial, kernel);
        byte[] explicitAliases = initial.PartyAiAliases!.ToArray();
        explicitAliases[1] = 7;
        explicitAliases[3] = 5;
        Ff7BattleSession recorded = Restore(initial with { PartyAiAliases = explicitAliases }, progress);
        Check(recorded.Capture().PartyAiAliases!.SequenceEqual(explicitAliases), "Cold restore overwrote recorded process aliases.");
        Ff7BattleSession legacy = Restore(initial with { PartyAiAliases = null }, progress);
        Check(legacy.Capture().PartyAiAliases!.SequenceEqual(initial.PartyAiAliases), "Absent historical alias state must restore decoded constant link declarations.");
        Check(JsonSerializer.Serialize(legacy.Capture().Actors) == JsonSerializer.Serialize(initial.Actors) && JsonSerializer.Serialize(legacy.Capture().Random) == JsonSerializer.Serialize(initial.Random) && legacy.Tick == initial.Tick, "Alias restoration must not replay initialization, local state or RNG.");
        Ff7Progress retained = progress with { PartyAiAliases = explicitAliases };
        Ff7BattleSession retainedOwner = Restore(initial with { PartyAiAliases = null }, retained);
        Check(retainedOwner.Capture().PartyAiAliases!.SequenceEqual(explicitAliases), "Recorded progress aliases must win over resource-derived declarations.");
        Ff7Progress published = recorded.PublishVitals(progress);
        Check(published.PartyAiAliases!.SequenceEqual(explicitAliases), "Battle vitals publication lost process-global alias linkage.");
        Ff7BattleSession next = new(formation, published with { Party = [0, 255, 255] }, kernel, executable.NativeMateriaStatModifiers, 2);
        Check(next.Capture().PartyAiAliases!.SequenceEqual(explicitAliases), "A later encounter invented a reset of process-global aliases.");
        Ff7BattleSession chain = new(formation, progress, kernel, executable.NativeMateriaStatModifiers, 3, previousFormation: initial with { PartyAiAliases = explicitAliases });
        Check(chain.Capture().PartyAiAliases![1] == 7 && chain.Capture().PartyAiAliases![3] == 5, "Formation chaining lost explicit prior process aliases.");
        TestCounter(false);
        TestCounter(true);
        Console.WriteLine("Original regular-party aliases, selected-bank ownership, process/cold compatibility and affection counter phases passed.");

        Ff7BattleSession Restore(Ff7BattleSnapshot snapshot, Ff7Progress source)
        {
            Ff7BattleSnapshot cold = JsonSerializer.Deserialize<Ff7BattleSnapshot>(JsonSerializer.Serialize(snapshot))!;
            return new(formation, source, kernel, executable.NativeMateriaStatModifiers, 1, snapshot: cold);
        }

        void TestCounter(bool lethal)
        {
            byte expectedAffection = lethal ? (byte)96 : (byte)98;
            byte[] saved = new byte[256];
            saved[0x53] = 100;
            Ff7BattleSession entry = new(formation, progress, kernel, executable.NativeMateriaStatModifiers, 1, initialSaveBank1: saved);
            Ff7BattleSnapshot start = entry.Capture();
            if (lethal)
            {
                start = start with { Actors = start.Actors.Select(actor => actor.Actor.Slot == 1 ? actor with { Actor = actor.Actor with { HitPoints = 1 } } : actor).ToArray() };
            }
            uint beforeHp = start.Actors.Single(actor => actor.Actor.Slot == 1).Actor.HitPoints;
            start = start with { Queue = Ff7BattleCommandQueue.Enqueue(start.Queue, new(2, 0, (byte)Ff7BattleCommand.Magic, 0x1E, 2)) };
            Ff7BattleSession warm = Restore(start, progress);
            Ff7BattleSession cold = Restore(start, progress);
            bool beforeAction = false;
            for (int tick = 0; tick < 1000; tick++)
            {
                warm.Advance(waitForCommand: true);
                cold.Advance(waitForCommand: true);
                if (!beforeAction && warm.Action is { Applied: false })
                {
                    Ff7BattleSnapshot prepared = warm.Capture();
                    Check(BinaryPrimitives.ReadUInt32LittleEndian(prepared.Actors.Single(actor => actor.Actor.Slot == 1).AiMemory) == beforeHp, "Shared callback7 must capture party HP before the action.");
                    Check(prepared.AiSaveBank1![0x53] == 100, "Pre-action party callbacks must not publish affection changes.");
                    cold = Restore(prepared, progress);
                    beforeAction = true;
                }
                if (warm.LastHit is { Source: 0, Target: 1 } && warm.Action is null)
                {
                    break;
                }
            }
            Ff7BattleSnapshot completed = warm.Capture();
            Ff7BattleActorState observed = completed.Actors.Single(actor => actor.Actor.Slot == 1);
            string counter = $"lethal={lethal}, prepared={beforeAction}, HP={beforeHp}->{observed.Actor.HitPoints}, hit={completed.LastHit}, affection={completed.AiSaveBank1![0x53]}, dirty={string.Join(",", completed.AiSaveBankDirty ?? [])}, beforeHP={BinaryPrimitives.ReadUInt32LittleEndian(observed.AiMemory)}, afterHP={BinaryPrimitives.ReadUInt32LittleEndian(observed.AiMemory.AsSpan(4))}, attacker={observed.Actor.LastAttackerMask:X}, variant={observed.Actor.Variant:X}, alias={completed.PartyAiAliases![1]}";
            Check(beforeAction && completed.LastHit is { Damage.Missed: false } && completed.AiSaveBank1![0x53] == expectedAffection, $"The original damage/death counter must publish exactly one future affection decrement: {counter}.");
            Ff7BattleAiActor recipient = completed.Actors.Single(actor => actor.Actor.Slot == 1).Actor;
            Check(lethal ? recipient.HitPoints == 0 && (recipient.Conditions & 0x2000) != 0 : recipient.HitPoints < beforeHp && recipient.HitPoints != 0, "Original party counter selection lost its damage/death phase.");
            Check(JsonSerializer.Serialize(warm.Capture()) == JsonSerializer.Serialize(cold.Capture()), "Party counter/save-bank state changed through live JSON continuation.");
            for (int callback = 0; callback < 8; callback++)
            {
                warm.Advance(waitForCommand: true);
                cold.Advance(waitForCommand: true);
            }
            Check(warm.Capture().AiSaveBank1![0x53] == expectedAffection && JsonSerializer.Serialize(warm.Capture()) == JsonSerializer.Serialize(cold.Capture()), "Completed affection counters must not publish twice after continuation.");
        }
    }

    private static void TestOpcode(SceneBattleData formation, Ff7BattleSnapshot snapshot, IReadOnlyList<KernelSection> kernel)
    {
        byte[] aliases = snapshot.PartyAiAliases!.ToArray();
        aliases[1] = 3;
        aliases[3] = 5;
        int selected = Ff7KernelBattleAi.ResolveIdentity(1, aliases);
        Check(selected == 3, "Native party alias resolution must take exactly one hop.");
        Ff7BattleAiActor[] actors = snapshot.Actors.Select(actor => actor.Actor).ToArray();
        ushort mask = (ushort)actors.Aggregate(0, (value, actor) => value | 1 << actor.Slot);
        Ff7BattleAiContext context = new(1, actors, mask, 0) { ScriptIdentity = selected };
        Ff7BattleRng random = new(kernel[2].Data.Span, snapshot.Random);
        string before = JsonSerializer.Serialize(random.Capture());
        Ff7BattleAiResult result = Ff7BattleAi.Run([0x61, 6, 1, 0x75, 0x60, 1, 0x60, 0, 0x92, 0x73], context, [], formation, random);
        Check(result.PartyAliasWrites.Count == 1 && result.PartyAliasWrites[3] == 6 && result.Commands.Single().ActorSlot == 1, "Opcode75 must pop/cast its scalar, write the selected bank and continue in the physical actor callback.");
        Check(JsonSerializer.Serialize(random.Capture()) == before, "Native alias linkage must not consume RNG.");
        Ff7BattleAiResult ignored = Ff7BattleAi.Run([0x60, 3, 0x75, 0x73], context with { ScriptIdentity = -1 }, [], formation, random);
        Check(ignored.PartyAliasWrites.Count == 0, "Negative native interpreter owners must consume but not publish alias writes.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
