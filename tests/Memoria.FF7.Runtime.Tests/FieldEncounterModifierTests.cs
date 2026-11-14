using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class FieldEncounterModifierTests
{
    public static void Run()
    {
        TestSelection();
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("SKIP installed field encounter modifiers: MEMORIA_FF7_GAME_DIR is not configured.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(root);
        ReadOnlyMemory<byte> materia = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path))[8].Data;
        Check(new(23, 16, 0, 0), Read([0, 255, 255], Character(0, (8, 9999))), "Field Enemy Lure retains raw likelihood");
        Check(new(32, 16, 0, 0), Read([0, 1, 255], Character(0, (8, 10000)), Character(1, (8, 50000))), "Field party likelihood cap");
        Check(new(9, 16, 0, 0), Read([0, 255, 255], Character(0, (7, 9999))), "Field Enemy Away below threshold");
        Check(new(2, 16, 0, 0), Read([0, 255, 255], Character(0, (7, 10000))), "Field Enemy Away threshold");
        Check(new(16, 32, 0, 0), Read([0, 255, 255], Character(0, (10, 7999))), "Field raw initiative below threshold");
        Check(new(16, 38, 0, 0), Read([0, 255, 255], Character(0, (10, 8000))), "Field raw initiative threshold");
        Ff7EncounterModifiers mastered = Read([0, 255, 255], Character(0, (10, 80000)));
        Check(new(16, 64, 0, 1), mastered, "Field mastered initiative fallback");
        Assert(mastered.FieldInitiative == 192, "Mastered initiative lost its native high bit.");
        Check(new(16, 64, 0, 0), Read([0, 255, 1], Character(0, (10, 80000)), Character(1)), "Last occupied party slot owns mastered flag");
        Check(new(16, 15, 0, 1), Read([0, 1, 2], Character(0, (10, 80000), (10, 80000)), Character(1, (10, 80000), (10, 80000)), Character(2, (10, 80000), (10, 80000))), "Raw party initiative wraps before cap");
        Check(new(12, 16, 0, 0), Read([0, 1, 255], Character(0, Enumerable.Repeat(((byte)8, 50000u), 16).ToArray()), Character(1, (8, 50000), (8, 50000))), "Raw party likelihood byte truncation");
        Ff7EncounterModifiers lure = Read([0, 255, 255], Character(0, (9, 3000)));
        Check(new(16, 16, 12, 0), lure, "Chocobo Lure retains its separate World owner");
        Ff7FieldResources field = Ff7FieldResources.Load(installation, "psdun_2");
        PcFieldEncounterTable table = PcFieldEncounterTable.Read(field.Container.GetSection(PcFieldSectionKind.Encounters).Span)[0];
        byte[] random = Ff7PcExecutableData.Load(installation).NativeFieldRandomTable.ToArray();
        Ff7EncounterState state = new(MovementCounter: 224, Danger: 4096);
        for (int tick = 0; tick < 256; tick++)
        {
            Ff7EncounterState cold = JsonSerializer.Deserialize<Ff7EncounterState>(JsonSerializer.Serialize(state))!;
            Ff7EncounterResult expected = Ff7NativeEncounters.Step(state, table, random, 4096, false);
            Ff7EncounterResult actual = Ff7NativeEncounters.Step(cold, table, random, 4096, false, lure.EncounterRate, lure.FieldInitiative);
            Assert(expected == actual, "Original mine selection/RNG or cold continuation changed with Chocobo Lure.");
            if (actual.Formation is ushort formation)
            {
                Assert(table.Standard.Concat(table.Special).Any(entry => entry.Formation == formation), "Original mine selected a formation absent from its table.");
            }
            state = actual.State;
        }
        TestSavedMovement(installation);

        Ff7EncounterModifiers Read(int[] party, params Ff7CharacterState[] characters)
        {
            return Ff7EncounterModifiers.Read(new() { InitialKernelData = [], Characters = characters, Party = party, Inventory = [], Materia = [] }, materia.Span);
        }
    }

    private static void TestSavedMovement(Ff7Installation installation)
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_FIELD_ENCOUNTER_SAVE");
        if (string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP saved field encounter movement: MEMORIA_FF7_FIELD_ENCOUNTER_SAVE is not configured.");
            return;
        }
        byte[] source = File.ReadAllBytes(path);
        using MemoryStream stream = new(source);
        Ff7SessionSave save = CombinedSessionCodec.Read(stream).Ff7 ?? throw new InvalidDataException("The configured encounter fixture has no FFVII continuation.");
        Assert(save.Progress.Characters.Where(character => save.Progress.Party.Contains(character.Id)).Any(character => Enumerable.Range(0, 16).Any(slot => character.NativeRecord[64 + slot * 4] == 9)), "The configured encounter fixture must retain equipped Chocobo Lure.");
        using Ff7Session session = Ff7Session.Restore(installation, save);
        Assert(session.World is null && session.MapId == 350, "The configured encounter fixture must be at the eastern mine arrival.");
        bool wrapped = false;
        for (int tick = 0; tick < 64; tick++)
        {
            byte before = session.Progress.Encounters.MovementCounter;
            using Ff7Session cold = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(session.Capture()))!);
            session.MovePlayer(0, -1, true);
            cold.MovePlayer(0, -1, true);
            session.AdvanceTick();
            cold.AdvanceTick();
            Assert(JsonSerializer.Serialize(session.Capture()) == JsonSerializer.Serialize(cold.Capture()), "Saved mine movement/counterwrap diverged from its independent cold continuation.");
            if (session.Progress.Encounters.MovementCounter < before)
            {
                wrapped = true;
                break;
            }
        }
        Assert(wrapped, "Ordinary movement from the configured mine fixture did not reach its first native encounter counterwrap.");
        Assert(source.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "The standalone encounter source changed during read-only continuation.");
    }

    private static void TestSelection()
    {
        byte[] encoded = new byte[48];
        encoded[0] = 1;
        encoded[1] = 32;
        BinaryPrimitives.WriteUInt16LittleEndian(encoded.AsSpan(2), 1 | 31 << 10);
        PcFieldEncounterTable table = PcFieldEncounterTable.Read(encoded)[0];
        byte[] random = new byte[256];
        random[1] = 30;
        random[2] = 20;
        Ff7EncounterState state = new(MovementCounter: 224, Danger: 4096);
        Ff7EncounterResult baseline = Ff7NativeEncounters.Step(state, table, random, 0, false);
        Ff7EncounterResult modified = Ff7NativeEncounters.Step(state, table, random, 0, false, 23, 32);
        Assert(baseline.Formation is null && baseline.Initiative == 0 && baseline.State.RollIndex == 2 && baseline.State.FormationIndex == 0, "Baseline likelihood or RNG consumption changed.");
        Assert(modified.Formation == 1 && modified.Initiative == 4 && modified.State.RollIndex == 2 && modified.State.FormationIndex == 2, "Raw likelihood/initiative did not control native selection.");
        Assert(Ff7NativeEncounters.Step(state, table, random, 0, false, 0, 32).Formation is null, "Zero likelihood produced an encounter.");
        table = table with { Special = [new(100, 8), new(101, 8), new(102, 8), new(103, 8)] };
        random[2] = 0;
        foreach ((int roll, ushort expected) in new (int, ushort)[] { (5, 101), (13, 102), (17, 103) })
        {
            random[1] = (byte)(roll << 2);
            Ff7EncounterResult selected = Ff7NativeEncounters.Step(state, table, random, 0, false, 16, 128 | 64);
            Assert(selected.Formation == expected && selected.State.FormationIndex == 1 && selected.State.RollIndex == 2, "Mastered initiative must halve special weights0/1/3 while preserving weight2 and RNG order.");
        }
    }

    private static Ff7CharacterState Character(int id, params (byte Id, uint Ap)[] equipment)
    {
        byte[] data = new byte[132];
        data.AsSpan(64, 64).Fill(255);
        for (int index = 0; index < equipment.Length; index++)
        {
            (byte materia, uint ap) = equipment[index];
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(64 + index * 4), (ap << 8) | materia);
        }
        return new(id, string.Empty, 1, 0, 1, 0, data);
    }

    private static void Check(Ff7EncounterModifiers expected, Ff7EncounterModifiers actual, string scenario)
    {
        Assert(expected == actual, $"{scenario}: expected {expected}, got {actual}.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
