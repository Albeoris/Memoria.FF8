using System.Buffers.Binary;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeGrowthTests
{
    public static void Run()
    {
        byte[] kernel = Tables();
        Ff7NativeGrowth growth = new(KernelGrowthTables.Read(kernel));
        byte[] record = Character();
        Ff7BattleRng rng = new(kernel, new(new byte[8]));
        byte[] unchanged = growth.Apply(record, 0, rng);
        Check(unchanged[1] == 1 && rng.Capture().Indices.All(value => value == 0), "No-level rewards consumed growth randomness.");
        byte[] gained = growth.Apply(record, 1, rng);
        Check(gained[1] == 2 && gained.Skip(2).Take(6).All(value => value == 3), "The six native stat distributions changed.");
        Check(U16(gained, 0x2E) == 220 && U16(gained, 0x32) == 18, "Native HP/MP proportional growth changed.");
        Check(U32(gained, 0x3C) == 1 && U32(gained, 0x80) == 4 && gained[0x21] == 0, "Level EXP thresholds or bar changed.");
        Check(rng.Capture().Indices[0] == 8 && rng.Capture().Indices.Skip(1).All(value => value == 0), "A gained level did not consume exactly eight bytes on the selected stream.");
        Check(record.SequenceEqual(Character()), "Growth mutated the caller's native record.");
        int[] edited = [1, 2, 3, 4, 5, 6, 7, 0x21, 0x2E, 0x2F, 0x32, 0x33, 0x3C, 0x3D, 0x3E, 0x3F, 0x80, 0x81, 0x82, 0x83];
        Check(Enumerable.Range(0, 132).Where(index => !edited.Contains(index)).All(index => gained[index] == record[index]), "Growth changed current vitals, bonuses, equipment or another unrelated native field.");
        Ff7BattleRng resumed = new(kernel, rng.Capture());
        byte[] afterResume = growth.Apply(gained, 4, resumed);
        byte[] uninterrupted = growth.Apply(gained, 4, rng);
        Check(afterResume.SequenceEqual(uninterrupted) && resumed.Capture().Indices.SequenceEqual(rng.Capture().Indices), "Growth continuation changed the next level or RNG state.");
        record[1] = 99;
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(0x3C), 999999990);
        byte[] capped = growth.Apply(record, 100, rng);
        Check(capped[1] == 99 && U32(capped, 0x3C) == 999999999 && U32(capped, 0x80) == 0 && capped[0x21] == 61, "Maximum-level EXP policy changed.");
        kernel[0x21C + 6 * 16 + 1] = 255;
        byte[] signed = new Ff7NativeGrowth(KernelGrowthTables.Read(kernel)).Apply(Character(), 1, new(kernel, new(new byte[8])));
        Check(U16(signed, 0x2E) == 200, "Growth curve intercept must remain a signed byte.");
        for (int band = 0; band < 8; band++)
        {
            kernel[0x21C + 8 * 16 + band * 2] = (byte)((band + 1) * 10);
        }
        growth = new(KernelGrowthTables.Read(kernel));
        Check(growth.Threshold(0, 10) == 385 && growth.Threshold(0, 11) == 1012 && growth.Threshold(0, 60) == 442860 && growth.Threshold(0, 61) == 542717 && growth.Threshold(0, 81) == 1443528, "Native level-band boundaries changed.");
        Throws<InvalidDataException>(() => KernelGrowthTables.Read(new byte[3987]));
        kernel[8] = 192;
        Throws<InvalidDataException>(() => KernelGrowthTables.Read(kernel));
        Throws<InvalidDataException>(() => growth.Apply(new byte[132], 1, rng));
        RunInstalled();
    }

    private static void RunInstalled()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("SKIP installed growth: MEMORIA_FF7_GAME_DIR is unset");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(root);
        var sections = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        Ff7NativeGrowth growth = new(KernelGrowthTables.Read(sections[2].Data.Span));
        KernelInitialData initial = KernelInitialData.Read(sections[3].Data.Span);
        foreach (KernelCharacterData character in initial.Characters.Where(character => character.Level is >= 1 and < 99 && character.Id < 9))
        {
            Ff7BattleRng rng = new(sections[2].Data.Span, new(new byte[8]));
            uint needed = growth.Threshold(character.Id, character.Level) - character.Experience;
            byte[] result = growth.Apply(character.Raw.Span, needed, rng);
            Check(result[1] == character.Level + 1 && rng.Capture().Indices[0] == 8, "Original character did not gain exactly one level at its native threshold.");
            Check(U16(result, 0x2C) == character.CurrentHp && U16(result, 0x30) == character.CurrentMp, "Original current HP/MP changed during growth.");
        }
    }

    private static byte[] Tables()
    {
        byte[] kernel = new byte[3988];
        for (int character = 0; character < 9; character++)
        {
            for (int statistic = 0; statistic < 9; statistic++)
            {
                kernel[character * 56 + statistic] = (byte)statistic;
            }
        }
        for (int statistic = 0; statistic < 9; statistic++)
        {
            for (int band = 0; band < 8; band++)
            {
                kernel[0x21C + statistic * 16 + band * 2] = statistic == 8 ? (byte)10 : (byte)100;
            }
        }
        for (int index = 0; index < 12; index++)
        {
            kernel[0x1F8 + index] = (byte)index;
            kernel[0x204 + index] = index == 0 ? (byte)100 : (byte)120;
            kernel[0x210 + index] = 80;
        }
        return kernel;
    }

    private static byte[] Character()
    {
        byte[] record = Enumerable.Repeat((byte)123, 132).ToArray();
        record[0] = 0;
        record[1] = 1;
        Array.Fill(record, (byte)1, 2, 6);
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(0x2E), 100);
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(0x32), 10);
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(0x3C), 0);
        return record;
    }

    private static ushort U16(byte[] value, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(value.AsSpan(offset));
    private static uint U32(byte[] value, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(value.AsSpan(offset));

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidDataException(message);
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
        throw new InvalidDataException($"Expected {typeof(T).Name}.");
    }
}
