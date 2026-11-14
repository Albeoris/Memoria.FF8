using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class PartyBattleAiTests
{
    public static void Run(Ff7Progress progress, IReadOnlyList<KernelSection> kernel, Ff7PcExecutableData executable, Ff7Installation installation)
    {
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path)).Single(formation => formation.FormationId == 292);
        SceneBattleData data = SceneBattleData.Read(formation);
        Ff7BattleSession battle = new(formation, progress with { Party = [9, 10, 255] }, kernel, executable.NativeMateriaStatModifiers, 1);
        Ff7BattleActorState automatic = battle.Actors.Single(actor => actor.Identity == 10);
        Check((automatic.Actor.Conditions & 0x710) == 0x710, "Original party initialization did not publish its automatic control conditions.");
        Check((battle.Actors.Single(actor => actor.Identity == 9).Actor.Conditions & 0x10) == 0, "Native kernel initialization made the ordinary player automatic.");
        Check(Ff7KernelBattleAi.ReadScript(kernel[2].Data, 10, 0).Length == 0x29 && !Ff7KernelBattleAi.ReadScript(kernel[2].Data, 10, 1).IsEmpty && Ff7KernelBattleAi.ReadScript(kernel[2].Data, 10, 2).IsEmpty, "Original logical party callback directory changed.");
        TestInterpreter(data, kernel, battle);
        bool restored = false;
        for (int tick = 0; tick < 8000 && battle.LastHit?.Source != automatic.Actor.Slot; tick++)
        {
            Check(battle.ReadyPlayer != automatic.Actor.Slot, "The original automatic actor was exposed as a manual choice.");
            battle.Advance();
            if (!restored && battle.Action?.Command.ActorSlot == automatic.Actor.Slot)
            {
                Ff7BattleSnapshot snapshot = JsonSerializer.Deserialize<Ff7BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!;
                Ff7BattleSession cold = new(formation, progress with { Party = [9, 10, 255] }, kernel, executable.NativeMateriaStatModifiers, 1, snapshot: snapshot);
                for (int callback = 0; callback < 8; callback++)
                {
                    battle.Advance();
                    cold.Advance();
                }
                Check(JsonSerializer.Serialize(battle.Capture()) == JsonSerializer.Serialize(cold.Capture()), "Original automatic party action changed through JSON continuation.");
                battle = cold;
                restored = true;
            }
        }
        Check(restored && battle.LastHit?.Source == automatic.Actor.Slot, "Original party AI did not select and execute its own native turn.");
        TestFlashbackFieldEncounters(progress, kernel, executable, installation);
        Console.WriteLine("Original party AI initialization, source selectors, affinity/debug operands and automatic turn/JSON continuation passed.");
    }

    private static void TestFlashbackFieldEncounters(Ff7Progress progress, IReadOnlyList<KernelSection> kernel, Ff7PcExecutableData executable, Ff7Installation installation)
    {
        using LgpArchive fields = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.FieldArchive).Path);
        HashSet<ushort> selected = [];
        foreach (string field in new[] { "mtnvl3", "mtnvl4", "nvdun2" })
        {
            PcFieldFile resource = PcFieldFile.ReadCompressed(fields.ReadEntry(fields.Find(field).Single()));
            IReadOnlyList<PcFieldEncounterTable> tables = PcFieldEncounterTable.Read(resource.GetSection(PcFieldSectionKind.Encounters).Span);
            ushort[] formations = tables.Where(table => table.Enabled).SelectMany(table => table.Standard.Concat(table.Special)).Where(encounter => encounter.Frequency > 0).Select(encounter => encounter.Formation).Distinct().Order().ToArray();
            Check(formations.Length > 0, $"Original {field} has no weighted enabled encounter entries.");
            selected.UnionWith(formations);
            Console.WriteLine($"Original {field} weighted formation set: {string.Join(", ", formations)}.");
        }
        IReadOnlyList<SceneFormation> scenes = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path));
        Ff7Progress party = progress with { Party = [9, 10, 255] };
        foreach (ushort id in selected.Order())
        {
            SceneFormation formation = scenes.Single(formation => formation.FormationId == id);
            Ff7BattleSession warm = new(formation, party, kernel, executable.NativeMateriaStatModifiers, id + 1);
            Ff7BattleSession cold = Restore(warm);
            int source = warm.Actors.Single(actor => actor.Identity == 10).Actor.Slot;
            Check((warm.Actors.Single(actor => actor.Actor.Slot == source).Actor.Conditions & 0x710) == 0x710, $"Original field formation {id} lost native party initialization.");
            bool midActionRestored = false;
            bool hit = false;
            for (int tick = 0; tick < 8000; tick++)
            {
                Check(warm.ReadyPlayer != source, $"Original field formation {id} exposed its automatic actor as a manual choice.");
                warm.Advance();
                cold.Advance();
                if (!midActionRestored && warm.Action?.Command.ActorSlot == source)
                {
                    Equal($"Original field formation {id} changed initialization or its first native party action through cold restore.");
                    cold = Restore(warm);
                    midActionRestored = true;
                }
                hit |= warm.LastHit?.Source == source;
                if (hit && warm.Action is null)
                {
                    break;
                }
            }
            Check(midActionRestored && hit && warm.Action is null, $"Original field formation {id} did not finish a native party turn.");
            Equal($"Original field formation {id} changed its party turn across a live JSON restore.");
            cold = Restore(warm);
            for (int callback = 0; callback < 8; callback++)
            {
                warm.Advance();
                cold.Advance();
            }
            Equal($"Original field formation {id} changed its next callbacks after a completed party turn.");

            Ff7BattleSession Restore(Ff7BattleSession sourceBattle)
            {
                Ff7BattleSnapshot snapshot = JsonSerializer.Deserialize<Ff7BattleSnapshot>(JsonSerializer.Serialize(sourceBattle.Capture()))!;
                return new(formation, party, kernel, executable.NativeMateriaStatModifiers, id + 1, snapshot: snapshot);
            }

            void Equal(string message)
            {
                Check(JsonSerializer.Serialize(warm.Capture()) == JsonSerializer.Serialize(cold.Capture()), message);
            }
        }
        Console.WriteLine($"Original flashback field tables: {selected.Count} distinct formations initialized and completed native party turns with cold continuation.");
    }

    private static void TestInterpreter(SceneBattleData data, IReadOnlyList<KernelSection> kernel, Ff7BattleSession battle)
    {
        Ff7BattleAiActor[] actors = battle.Actors.Select(actor => actor.Actor).ToArray();
        ushort mask = (ushort)actors.Aggregate(0, (value, actor) => value | 1 << actor.Slot);
        Ff7BattleRng rng = new(kernel[2].Data.Span, Ff7BattleRng.BeginBattle(1));
        byte[] attackOpponents = [0x60, 1, 0x60, 0, 0x92, 0x73];
        Ff7BattleAiResult party = Ff7BattleAi.Run(attackOpponents, new(1, actors, mask, 0), [], data, rng, kernel[1].Data);
        Check(party.Commands.Single().TargetMask == 0x10, "A native party source must select enemy opponents.");
        Ff7BattleAiResult enemy = Ff7BattleAi.Run(attackOpponents, new(4, actors, mask, 0), [], data, rng, kernel[1].Data);
        Check(enemy.Commands.Single().TargetMask == 3, "Existing enemy-side selector semantics changed.");
        Ff7BattleAiActor[] confused = actors.Select(actor => actor.Slot == 1 ? actor with { Statuses = actor.Statuses ^ 0x40 } : actor).ToArray();
        Ff7BattleAiResult swapped = Ff7BattleAi.Run(attackOpponents, new(1, confused, mask, 0), [], data, rng, kernel[1].Data);
        Check(swapped.Commands.Single().TargetMask == 3, "Native confusion/source-side parity changed.");
        foreach (uint status in new uint[] { 0x400000, 0x400040 })
        {
            Ff7BattleAiActor[] affected = actors.Select(actor => actor.Slot == 1 ? actor with { Statuses = status } : actor).ToArray();
            Ff7BattleAiResult parity = Ff7BattleAi.Run(attackOpponents, new(1, affected, mask, 0), [], data, rng, kernel[1].Data);
            Check(parity.Commands.Single().TargetMask == (status == 0x400000 ? 3 : 0x10), "Native Berserk/confusion parity must count the source-side term and both status bits.");
        }
        Ff7BattleRngState before = rng.Capture();
        byte[] query = [0x60, 0x10, 0x60, 8, 0x96, 0x60, 0, 0xA0, 1, (byte)'x', 0, 0x73];
        Ff7BattleAiResult queried = Ff7BattleAi.Run(query, new(1, actors, mask, 0), [], data, rng, kernel[1].Data);
        Check(queried.ActorUpdates.All(actor => actor.ElementReaction is not null) && JsonSerializer.Serialize(before) == JsonSerializer.Serialize(rng.Capture()) && queried.Commands.Length == 0, "Affinity query/debug operands must preserve RNG and publish no action.");
        SceneBattleData rawAffinity = data with { Enemies = data.Enemies.Select(enemy => enemy with { Affinities = [new(3, 7)] }).ToArray() };
        Ff7BattleAiResult raw = Ff7BattleAi.Run([0x60, 0x10, 0x60, 8, 0x96, 0x73], new(1, actors, mask, 0), [], rawAffinity, rng, kernel[1].Data);
        Check(raw.ActorUpdates.Single(actor => actor.Slot == 4).ElementReaction == 7 && BinaryPrimitives.ReadUInt16LittleEndian(raw.GlobalMemory.AsSpan(14)) == 0x10, "Affinity query must retain raw rate7 rather than a damage-category conversion.");
        Ff7BattleAiActor[] afterRaw = actors.Select(actor => raw.ActorUpdates.Single(update => update.Slot == actor.Slot)).ToArray();
        Ff7BattleAiResult reset = Ff7BattleAi.Run([0x60, 0, 0x60, 8, 0x96, 0x73], new(1, afterRaw, mask, 0), [], rawAffinity, rng, kernel[1].Data);
        Check(reset.ActorUpdates.All(actor => actor.ElementReaction == 3), "Every query must reset reactions for actors outside its selected mask.");
        Ff7BattleAiActor[] ordinary = actors.Select(actor => actor with { ElementHalve = 0, ElementNullify = 0, ElementAbsorb = 0, ExtraElementAbsorb = 0 }).ToArray();
        Ff7BattleAiResult empty = Ff7BattleAi.Run([0x60, 0, 0x60, 8, 0x96, 0x73], new(1, ordinary, mask, 0), [], data, rng, kernel[1].Data);
        Check(BinaryPrimitives.ReadUInt16LittleEndian(empty.GlobalMemory.AsSpan(14)) == 0 && empty.ActorUpdates.All(actor => actor.ElementReaction == 3), "An empty affinity selection must clear the target owner and publish normal reaction3.");
        Reject(() => Ff7BattleAi.Run([0x60, 8, 0x96, 0x73], new(1, actors, mask, 0), [], data, rng));
        Reject(() => Ff7BattleAi.Run([0xA0, 0, (byte)'x'], new(1, actors, mask, 0), [], data, rng));
    }

    private static void Reject(Action action)
    {
        try
        {
            action();
        }
        catch (InvalidDataException)
        {
            return;
        }
        throw new InvalidOperationException("A truncated or underflowing native party AI operation was accepted.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
