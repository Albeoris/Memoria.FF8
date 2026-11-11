using System.Buffers.Binary;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeSaveBankTests
{
    public static void Run()
    {
        Ff7NativeSaveBank empty = Ff7NativeSaveBank.Create();
        Check(empty.Encode().Length == 65109 && Enumerable.Range(0, 15).All(index => empty.GetSlot(index).IsEmpty), "Native bank layout or empty slots changed.");
        byte[] baseData = new byte[Ff7NativeSaveSlot.Size];
        Check(Ff7NativeSaveSlot.ComputeChecksum(baseData) == 0x4D1D, "Native empty-slot checksum vector changed.");
        byte[] record = new byte[132];
        record[1] = 12;
        record.AsSpan(16, 12).Fill(255);
        record[16] = (byte)('C' - 32);
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(46), 900);
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(50), 100);
        Ff7Progress progress = new()
        {
            InitialKernelData = new byte[2876],
            Characters = Enumerable.Range(0, 9).Select(index => new Ff7CharacterState(index, "C", 12, 123456, 777, 88, record.ToArray())).ToArray(),
            Party = [0, 2, 255],
            Inventory = [new(42, 17)],
            Materia = [new(3, 0x123456, 175)],
            Gil = 98765,
            Clock = new(3661, 600, 1000, 2000),
            AvailableCharacters = 0x105,
            LockedCharacters = 4,
            BattleEscapes = 15
        };
        byte[] persistent = Enumerable.Range(0, 1280).Select(index => (byte)(index % 224)).ToArray();
        baseData[0x10B0] = 0xA5;
        baseData[2] = 0xCC;
        Ff7NativeSaveSlot slot = Ff7NativeSaveCodec.ExportField(progress, persistent, 123, -321, 456, 7, 128, baseData);
        Check(slot.ChecksumValid && !slot.IsEmpty && slot.MapId == 123 && slot.Module == 1, "Native slot metadata/checksum failed.");
        Check(slot.Name == "C" && slot.Level == 12 && slot.Gil == 98765 && slot.ElapsedSeconds == 3661, "Native preview failed.");
        byte[] encoded = slot.Encode();
        Check(encoded[0x10B0] == 0xA5 && encoded[2] == 0xCC && encoded.AsSpan(0xBA4, 26).SequenceEqual(persistent.AsSpan(0, 26)), "Opaque native bytes or persistent variables were lost.");
        Check(BinaryPrimitives.ReadInt16LittleEndian(encoded.AsSpan(0xB9A)) == -321 && BinaryPrimitives.ReadUInt16LittleEndian(encoded.AsSpan(0xB9E)) == 7, "Native field position was not encoded.");
        Ff7Progress imported = Ff7NativeSaveCodec.ImportProgress(slot, progress.InitialKernelData);
        Check(imported.Gil == progress.Gil && imported.Party.SequenceEqual(progress.Party) && imported.Characters[0].HitPoints == 777 && imported.Characters[0].Experience == 123456 && imported.Materia.Single() == progress.Materia.Single() && imported.Inventory.SequenceEqual(progress.Inventory) && imported.Clock == progress.Clock && imported.LockedCharacters == 4 && imported.AvailableCharacters == 0x105, "Native progress roundtrip failed.");
        byte[] before = empty.Encode();
        Ff7NativeSaveBank edited = empty.WithSlot(8, slot);
        Check(empty.Encode().SequenceEqual(before) && edited.GetSlot(8).Encode().SequenceEqual(encoded) && edited.GetSlot(7).IsEmpty && edited.GetSlot(9).IsEmpty, "Native bank edit changed another owner.");
        Check(Ff7NativeSaveBank.Decode(edited.Encode()).Encode().SequenceEqual(edited.Encode()) && edited.WithSlot(8, null).GetSlot(8).IsEmpty, "Native bank encode or deletion failed.");
        encoded[500] ^= 1;
        Check(!Ff7NativeSaveSlot.Decode(encoded).ChecksumValid, "A corrupted native slot passed its checksum.");
        Throws<InvalidDataException>(() => Ff7NativeSaveCodec.ImportProgress(Ff7NativeSaveSlot.Decode(encoded), progress.InitialKernelData));
        Throws<InvalidDataException>(() => Ff7NativeSaveBank.Decode(new byte[65109]));
        Throws<ArgumentOutOfRangeException>(() => empty.GetSlot(15));
        Console.WriteLine("PASS native save banks: fixed checksum vector, projection, opaque preservation, slot isolation and corruption rejection");
        RunInstalled();
    }

    private static void RunInstalled()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? checkpoint = Environment.GetEnvironmentVariable("MEMORIA_FF7_NATIVE_CHECKPOINT");
        if (string.IsNullOrWhiteSpace(game) || string.IsNullOrWhiteSpace(checkpoint))
        {
            Console.WriteLine("SKIP native original-resource roundtrip: configure MEMORIA_FF7_GAME_DIR and MEMORIA_FF7_NATIVE_CHECKPOINT.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(game);
        using FileStream stream = File.OpenRead(checkpoint);
        Ff7SessionSave save = CombinedSessionCodec.Read(stream).Ff7 ?? throw new InvalidDataException("The configured native checkpoint has no FFVII state.");
        Ff7NativeSaveSlot native = Ff7NativeSaveCodec.Export(save, installation);
        Ff7SessionSave imported = Ff7NativeSaveCodec.Import(native, installation);
        int[] changedVariables = Enumerable.Range(0, 1280).Where(index => imported.Vm.PersistentVariables[index] != save.Vm.PersistentVariables[index]).ToArray();
        Check(changedVariables.Length == 0, "The configured native field roundtrip changed persistent variables during initialization: " + string.Join(',', changedVariables.Select(index => $"{index:X3}")));
        Check(imported.FieldName == save.FieldName && imported.Progress.Gil == save.Progress.Gil && imported.Progress.Inventory.SequenceEqual(save.Progress.Inventory), "Installed native import changed field or progress.");
        Ff7Actor expected = save.Process.Actors.Single(actor => actor.PlayerControlled);
        Ff7Actor actual = imported.Process.Actors.Single(actor => actor.PlayerControlled);
        Check(actual.X4096 >> 12 == expected.X4096 >> 12 && actual.Y4096 >> 12 == expected.Y4096 >> 12 && actual.Triangle == expected.Triangle && actual.Direction == expected.Direction, "Installed native import changed field entry coordinates.");
        using Ff7Session resumed = Ff7Session.Restore(installation, imported);
        resumed.AdvanceTick();
        Ff7SessionSave continued = resumed.Capture();
        Check(continued.NativeSaveData!.SequenceEqual(native.Encode()), "Cold continuation lost opaque native data.");
        Console.WriteLine($"PASS native installed field roundtrip and one tick: {continued.FieldName}");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try
        {
            action();
        }
        catch (T)
        {
            return;
        }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }
}
