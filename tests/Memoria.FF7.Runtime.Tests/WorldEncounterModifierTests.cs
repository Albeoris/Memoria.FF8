using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class WorldEncounterModifierTests
{
    public static void Run()
    {
        TestSelection();
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("SKIP installed world encounter modifiers: MEMORIA_FF7_GAME_DIR is not configured.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(root);
        ReadOnlyMemory<byte> materia = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path))[8].Data;
        Check(Ff7WorldEncounterModifiers.Baseline, Read([0, 255, 255], Character(0, (1, 50000)), Character(1, (8, 50000))), "Unrelated materia and equipment outside the party");
        Check(new(9, 16, 0, 0), Read([0, 255, 255], Character(0, (7, 9999))), "Enemy Away below its AP threshold");
        Check(new(2, 16, 0, 0), Read([0, 255, 255], Character(0, (7, 10000))), "Enemy Away at its AP threshold");
        Check(new(46, 16, 0, 0), Read([0, 255, 255], Character(0, (8, 9999))), "Enemy Lure world rate doubling");
        Check(new(64, 16, 0, 0), Read([0, 1, 255], Character(0, (8, 10000)), Character(1, (8, 50000))), "Party Enemy Lure cap");
        Check(new(12, 16, 0, 0), Read([0, 1, 255], Character(0, Enumerable.Repeat(((byte)8, 50000u), 16).ToArray()), Character(1, (8, 50000), (8, 50000))), "Enemy Lure result truncated before the cap");
        Check(new(2, 16, 0, 0), Read([0, 255, 255], Character(0, (7, 50000), (7, 50000))), "Negative Enemy Away result");
        Check(new(16, 16, 8, 0), Read([0, 255, 255], Character(0, (9, 2999))), "Chocobo Lure below its AP threshold");
        Check(new(16, 16, 12, 0), Read([0, 255, 255], Character(0, (9, 3000))), "Chocobo Lure at its AP threshold");
        Check(new(16, 16, 32, 0), Read([0, 1, 255], Character(0, (9, 30000), (9, 30000)), Character(1, (9, 30000))), "Chocobo Lure cap and mastered fallback");
        Check(new(16, 64, 0, 0), Read([0, 255, 255], Character(0, (10, 7999))), "Pre-Emptive below its AP threshold");
        Check(new(16, 76, 0, 0), Read([0, 255, 255], Character(0, (10, 8000))), "Pre-Emptive at its AP threshold");
        Check(new(16, 128, 0, 1), Read([0, 255, 255], Character(0, (10, 80000))), "Mastered Pre-Emptive in the last occupied party slot");
        Check(new(16, 128, 0, 0), Read([0, 255, 1], Character(0, (10, 80000)), Character(1)), "Later occupied slot resets the mastered Pre-Emptive flag");
        Check(new(16, 170, 0, 1), Read([1, 255, 0], Character(0, (10, 80000)), Character(1, (10, 80000))), "Party Pre-Emptive cap");
        Check(new(16, 15, 0, 1), Read([0, 1, 2], Character(0, (10, 80000), (10, 80000)), Character(1, (10, 80000), (10, 80000)), Character(2, (10, 80000), (10, 80000))), "Party Pre-Emptive byte overflow");

        Ff7WorldEncounterModifiers Read(int[] party, params Ff7CharacterState[] characters)
        {
            return Ff7WorldEncounterModifiers.Read(new() { InitialKernelData = [], Characters = characters, Party = party, Inventory = [], Materia = [] }, materia.Span);
        }
    }

    private static void TestSelection()
    {
        byte[] data = new byte[0x8A0];
        const int record = 0xA0;
        data[record] = 1;
        data[record + 1] = 16;
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(record + 0x02), 0xFC00 | 12);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(record + 0x0E), 0x2000 | 13);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(record + 0x12), 0x2000 | 14);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(record + 0x14), 0x2000 | 15);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(record + 0x16), 0x2000 | 16);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(0x20), 16);
        data[0x22] = 7;
        Ff7WorldEncounterTable table = new(data);
        Ff7WorldEncounterModifiers baseline = Ff7WorldEncounterModifiers.Baseline;
        Test(baseline with { EncounterRate = 0 }, 1, false, -1, false, null, 256, []);
        Test(baseline with { EncounterRate = 2 }, 1, false, -1, false, null, 384, [1]);
        Test(baseline, 1, false, 13, false, null, 0, [0, 0, 16, 16]);
        Test(baseline with { SpecialRollShift = 1 }, 2, false, 14, false, null, 0, [0, 0, 16, 17, 16]);
        Test(baseline with { SpecialRollShift = 1 }, 1, false, 15, false, null, 0, [0, 0, 16, 17, 16]);
        Test(baseline with { SpecialRollShift = 1 }, 1, false, 12, false, null, 0, [0, 0, 16, 17, 17, 16]);
        Test(baseline with { ChocoboLure = 16 }, 1, true, 16, true, 7, 0, [0, 0, 32, 15]);
        Test(baseline with { ChocoboLure = 16 }, 1, true, 13, false, null, 0, [0, 0, 33, 16, 16]);
        Test(baseline with { ChocoboLure = 16 }, 1, false, 13, false, null, 0, [0, 0, 16, 16]);

        void Test(Ff7WorldEncounterModifiers modifiers, int partyCount, bool allowChocobo, int expected, bool preemptive, byte? rating, int expectedDanger, byte[] rolls)
        {
            Queue<byte> random = new(rolls);
            int danger = 256;
            int formation = table.Select(0x8000, partyCount, false, -1, ref danger, random.Dequeue, out bool actualPreemptive, out byte? actualRating, 1, modifiers, allowChocobo);
            if (formation != expected || preemptive != actualPreemptive || rating != actualRating || danger != expectedDanger || random.Count != 0)
            {
                throw new InvalidOperationException($"World equipment changed formation, initiative, rating, danger, or random consumption for {modifiers}.");
            }
        }
    }

    public static void RunSession(Ff7Installation installation, Ff7SessionSave fieldSave, byte[] variables, Ff7WorldProcess ready)
    {
        Ff7SessionSave baseline = fieldSave with { Vm = fieldSave.Vm with { PersistentVariables = variables }, World = ready };
        Ff7CharacterState[] equipped = fieldSave.Progress.Characters.Select(character => character.Id == fieldSave.Progress.Party[0] ? Equip(character, [(10, 0)]) : character).ToArray();
        Ff7SessionSave withMateria = baseline with { Progress = baseline.Progress with { Characters = equipped } };
        TestInitiative(baseline, 16, false);
        TestInitiative(withMateria, 16, true);
        TestInitiative(withMateria, 64, false);

        void TestInitiative(Ff7SessionSave save, byte sample, bool preemptive)
        {
            Ff7WorldBattleState battle = ready.BattleState! with { Random = Enumerable.Repeat(sample, 0x209).ToArray() };
            using Ff7Session walking = Ff7Session.Restore(installation, save with { World = ready with { BattleState = battle } });
            walking.SetFullCombatEnabled(true);
            walking.SetRandomEncountersEnabled(true);
            walking.World!.SetMovement(1, 0, true);
            using Ff7Session cold = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(walking.Capture()))!);
            walking.AdvanceTick(false);
            cold.AdvanceTick(false);
            if (walking.World.PendingBattle is not { Scripted: false } occurrence || occurrence.Preemptive != preemptive || walking.Battle is null || JsonSerializer.Serialize(walking.Capture()) != JsonSerializer.Serialize(cold.Capture()))
            {
                throw new InvalidOperationException($"Equipped world initiative did not select the expected native boundary at random byte {sample} and retain it on cold continuation.");
            }
        }
    }

    private static Ff7CharacterState Character(int id, params (byte Id, uint Ap)[] equipment)
    {
        byte[] data = new byte[132];
        data.AsSpan(64, 64).Fill(255);
        return Equip(new(id, string.Empty, 1, 0, 1, 0, data), equipment);
    }

    private static Ff7CharacterState Equip(Ff7CharacterState character, (byte Id, uint Ap)[] equipment)
    {
        byte[] data = character.NativeRecord.ToArray();
        for (int index = 0; index < equipment.Length; index++)
        {
            (byte materia, uint ap) = equipment[index];
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(64 + (15 - index) * 4), (ap << 8) | materia);
        }
        return character with { NativeRecord = data };
    }

    private static void Check(Ff7WorldEncounterModifiers expected, Ff7WorldEncounterModifiers actual, string scenario)
    {
        if (expected != actual)
        {
            throw new InvalidOperationException($"{scenario} expected {expected}, got {actual}.");
        }
    }
}
