using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeNamingGrowthTests
{
    public static void Run()
    {
        CheckAverageAndAdditiveExperience();
        CheckSignedModifierAndEmptyAverage();
        CheckSavedLevelReset();
        CheckStrictBoundaries();
        CheckInstalledModifiers();
    }

    private static void CheckAverageAndAdditiveExperience()
    {
        byte[] kernel = Tables();
        kernel[6 * 56 + 9] = 0x7E;
        kernel[6 * 56 + 11] = 0x35;
        Ff7NativeGrowth growth = new(KernelGrowthTables.Read(kernel));
        byte[] joining = Record(6, 1, 14);
        Ff7CharacterState[] characters = [Character(7, 0, 70), Character(0, 1, 18), Character(2, 0, 16), Character(6, 6, 99), Character(3, 2, 90)];
        string before = JsonSerializer.Serialize(characters);
        byte[] original = joining.ToArray();
        Ff7BattleRng random = Random(kernel);
        byte[] result = growth.InitializeJoin(joining, characters, 0x43, random);
        Check(KernelGrowthTables.Read(kernel).JoinModifier(6) == 0 && result[0] == 6 && result[1] == 17 && U32(result, 0x3C) == 1510, "Native joining lost its exact modifier offset, available-persona average, physical-first match or additive template EXP.");
        Check(result[2] == joining[2] + 32 && U16(result, 0x2C) == U16(joining, 0x2C) && U16(result, 0x30) == U16(joining, 0x30), "Even-modifier joining reset template stats or changed current vitals before their completion owner.");
        Check(random.Capture().Indices[3] == 128 && random.Capture().Indices.Where((_, index) => index != 3).All(value => value == 0), "Native join levels did not consume eight bytes each from the caller's selected stream.");
        Check(joining.SequenceEqual(original) && before == JsonSerializer.Serialize(characters), "Join growth changed its input record or available-character facts.");

        byte[] late = Record(6, 20, 0);
        byte[] retained = growth.InitializeJoin(late, [Character(0, 0, 3)], 1, Random(kernel));
        Check(retained[1] == 20 && retained[2] == late[2] && U32(retained, 0x3C) == 5, "An even-modifier join lowered an existing level or replaced EXP instead of adding the target threshold.");
    }

    private static void CheckSignedModifierAndEmptyAverage()
    {
        byte[] kernel = Tables();
        kernel[6 * 56 + 10] = unchecked((byte)-6);
        Ff7NativeGrowth growth = new(KernelGrowthTables.Read(kernel));
        Check(KernelGrowthTables.Read(kernel).JoinModifier(6) == -6, "The native join modifier was decoded as an unsigned byte.");
        byte[] result = growth.InitializeJoin(Record(6, 1, 0), [Character(0, 0, 16), Character(1, 1, 19)], 3, Random(kernel));
        Check(result[1] == 14 && U32(result, 0x3C) == growth.Threshold(6, 13), "Native joining lost integer-average truncation or signed modifier division.");
        kernel[6 * 56 + 10] = 0;
        growth = new(KernelGrowthTables.Read(kernel));
        Ff7BattleRng random = Random(kernel);
        string before = JsonSerializer.Serialize(random.Capture());
        byte[] empty = growth.InitializeJoin(Record(6, 1, 0), [Character(6, 9, 99), Character(7, 10, 99)], 0x300, random);
        Check(empty[1] == 1 && U32(empty, 0x3C) == 0 && before == JsonSerializer.Serialize(random.Capture()), "No eligible persona must use the existing join level without aliasing flashback storage or consuming extra RNG.");
        byte[] existing = growth.InitializeJoin(Record(6, 5, 0), [], 0, random);
        Check(existing[1] == 5 && U32(existing, 0x3C) == 30 && before == JsonSerializer.Serialize(random.Capture()), "An empty eligible roster did not use the joining record's existing level as its target.");
        kernel[6 * 56 + 10] = unchecked((byte)-128);
        growth = new(KernelGrowthTables.Read(kernel));
        Check(growth.InitializeJoin(Record(6, 1, 0), [Character(0, 0, 0)], 1, Random(kernel))[1] == 1, "Native join target did not clamp its signed result to minimum level1.");
    }

    private static void CheckSavedLevelReset()
    {
        byte[] kernel = Tables();
        kernel[6 * 56 + 10] = 1;
        Ff7NativeGrowth growth = new(KernelGrowthTables.Read(kernel));
        byte[] joining = Record(6, 8, 14);
        Ff7BattleRng random = Random(kernel);
        byte[] result = growth.InitializeJoin(joining, [Character(0, 0, 80)], 1, random, savedLevel: 3);
        Check(result[1] == 3 && U32(result, 0x3C) == 5 && result.Skip(2).Take(6).All(value => value == 5) && U16(result, 0x2E) == 201 && U16(result, 0x32) == 17, "Saved-level joining did not reset native EXP, level and eight base values before normal growth initialization.");
        Check(U16(result, 0x2C) == U16(joining, 0x2C) && U16(result, 0x30) == U16(joining, 0x30) && result.AsSpan(16, 12).SequenceEqual(joining.AsSpan(16, 12)), "Join growth changed name or current HP/MP outside its native context.");
        Check(random.Capture().Indices[3] == 16 && random.Capture().WordCounter == 7 && random.Capture().CrtSeed == 1234, "Join growth drew from the wrong RNG owner or reset caller stream state.");
        kernel[6 * 56 + 10] = unchecked((byte)-3);
        byte[] signedReset = new Ff7NativeGrowth(KernelGrowthTables.Read(kernel)).InitializeJoin(joining, [Character(0, 0, 80)], 1, Random(kernel), savedLevel: 3);
        Check(signedReset.SequenceEqual(result), "An odd signed modifier applied average arithmetic instead of selecting the required saved-level input.");
        Ff7BattleRng cold = new(kernel, JsonSerializer.Deserialize<Ff7BattleRngState>(JsonSerializer.Serialize(random.Capture()))!);
        byte[] continued = growth.Apply(result, 9, random);
        byte[] restored = growth.Apply(JsonSerializer.Deserialize<byte[]>(JsonSerializer.Serialize(result))!, 9, cold);
        Check(continued.SequenceEqual(restored) && JsonSerializer.Serialize(random.Capture()) == JsonSerializer.Serialize(cold.Capture()), "Cold continuation after naming growth changed native levels or RNG.");
        Ff7BattleRng minimumRandom = Random(kernel);
        byte[] minimum = growth.InitializeJoin(joining, [], 0, minimumRandom, savedLevel: 0);
        Check(minimum[1] == 1 && U32(minimum, 0x3C) == 0 && minimum.Skip(2).Take(6).All(value => value == 1) && U16(minimum, 0x2E) == 1 && U16(minimum, 0x32) == 1 && minimumRandom.Capture().Indices.All(value => value == 0), "Saved-level zero did not use native minimum1 and zero-base normalization without growth draws.");
    }

    private static void CheckStrictBoundaries()
    {
        byte[] kernel = Tables();
        kernel[6 * 56 + 10] = 1;
        Ff7NativeGrowth growth = new(KernelGrowthTables.Read(kernel));
        Ff7BattleRng random = Random(kernel);
        string before = JsonSerializer.Serialize(random.Capture());
        Reject<NotSupportedException>(() => growth.InitializeJoin(Record(6, 1, 0), [], 0, random));
        Reject<NotSupportedException>(() => growth.InitializeJoin(Record(6, 1, 0), [], 0, random, 100));
        Reject<InvalidDataException>(() => growth.InitializeJoin(Record(9, 1, 0), [], 0, random, 3));
        Reject<InvalidDataException>(() => growth.InitializeJoin(Record(6, 0, 0), [], 0, random, 3));
        Reject<InvalidDataException>(() => growth.InitializeJoin(new byte[131], [], 0, random, 3));
        Reject<ArgumentOutOfRangeException>(() => KernelGrowthTables.Read(kernel).JoinModifier(9));
        kernel[6 * 56 + 10] = 126;
        growth = new(KernelGrowthTables.Read(kernel));
        Reject<NotSupportedException>(() => growth.InitializeJoin(Record(6, 1, 0), [Character(0, 0, 99)], 1, random));
        Check(before == JsonSerializer.Serialize(random.Capture()), "Unsupported join operands consumed the caller's RNG before validation.");
    }

    private static void CheckInstalledModifiers()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            Console.WriteLine("SKIP original naming growth: MEMORIA_FF7_GAME_DIR is unset.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(directory);
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        KernelGrowthTables tables = KernelGrowthTables.Read(kernel[2].Data.Span);
        Check(tables.JoinModifier(6) == 0 && tables.JoinModifier(7) == 6, "The selected original Cait/Vincent join modifiers differ from their recognized resource records.");
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
        for (int distribution = 0; distribution < 12; distribution++)
        {
            kernel[0x1F8 + distribution] = 2;
            kernel[0x204 + distribution] = 100;
            kernel[0x210 + distribution] = 80;
        }
        return kernel;
    }

    private static byte[] Record(byte persona, byte level, uint experience)
    {
        byte[] record = Enumerable.Repeat((byte)123, 132).ToArray();
        record[0] = persona;
        record[1] = level;
        for (int index = 0; index < 6; index++)
        {
            record[2 + index] = (byte)(20 + index);
        }
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(0x2C), 111);
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(0x2E), 224);
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(0x30), 7);
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(0x32), 18);
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(0x3C), experience);
        return record;
    }

    private static Ff7CharacterState Character(int storage, byte persona, byte level)
    {
        return new(storage, "Authored", 99, 0, 111, 7, Record(persona, level, 0));
    }

    private static Ff7BattleRng Random(byte[] kernel)
    {
        return new(kernel, new(new byte[8], Stream: 3, WordCounter: 7, CrtSeed: 1234));
    }

    private static ushort U16(byte[] record, int offset)
    {
        return BinaryPrimitives.ReadUInt16LittleEndian(record.AsSpan(offset));
    }

    private static uint U32(byte[] record, int offset)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(record.AsSpan(offset));
    }

    private static void Reject<T>(Action action) where T : Exception
    {
        try
        {
            action();
        }
        catch (T)
        {
            return;
        }
        throw new InvalidOperationException("An unsupported native join-growth operand was accepted.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
