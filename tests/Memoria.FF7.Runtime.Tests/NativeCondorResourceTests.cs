using System.Buffers.Binary;
using Memoria.FF7.Resources;

internal static class NativeCondorResourceTests
{
    internal static void Run()
    {
        byte[] bytes = Fixture();
        byte[] before = bytes.ToArray();
        Ff7CondorResources resources = Ff7CondorResources.Read(bytes);
        Check(resources.PathCount == 4, "The native path directory did not retain all relative-base offsets.");
        foreach (int attack in Enumerable.Range(0, Ff7CondorResources.AttackCount))
        {
            var rows = resources.ReadWave(attack);
            Check(rows.Count == 2 && rows[0] == new Ff7CondorEnemyWave(18, 18, 2, 0, 0, 0) && rows[1] == new Ff7CondorEnemyWave(19, 4, 2, 3, 0x1234, 1) { Reserved = 0xFE }, "Eight-byte wave decoding changed row order, unsigned time, byte extra or reserved source data.");
        }
        Check(resources.ReadPath(0).SequenceEqual(new[] { new Ff7CondorPathPoint(4, 2) }) && resources.ReadPath(3).SequenceEqual(new[] { new Ff7CondorPathPoint(10, 5) }), "Native packed XY path points or absolute member-relative offsets changed.");
        Check(bytes.SequenceEqual(before), "Resource reading mutated the supplied native allocation.");
        bytes[56] = 99;
        Check(resources.ReadWave(6)[0].RowType == 18, "The resource retained mutable caller-owned native bytes.");
        Ff7CondorResources shortSentinel = Ff7CondorResources.Read(Fixture()[..73]);
        Check(shortSentinel.ReadWave(6).Count == 2, "A native final-byte wave sentinel was mistaken for a truncated eight-byte row.");
        byte[] emptyPath = Fixture();
        Write(emptyPath, 34, 79);
        emptyPath[79] = 0xFF;
        Check(Ff7CondorResources.Read(emptyPath).ReadPath(1).Count == 0, "An X=FF final-byte path sentinel incorrectly required an unused Y byte.");
        Expect<ArgumentOutOfRangeException>(() => resources.ReadWave(-1));
        Expect<ArgumentOutOfRangeException>(() => resources.ReadWave(7));
        Expect<ArgumentOutOfRangeException>(() => resources.ReadPath(-1));
        Expect<ArgumentOutOfRangeException>(() => resources.ReadPath(4));
        Expect<InvalidDataException>(() => Ff7CondorResources.Read(new byte[23]));
        foreach ((int address, ushort value) in new[] { (4, (ushort)0), (4, (ushort)80), (32, (ushort)39), (32, (ushort)80) })
        {
            byte[] malformed = Fixture();
            Write(malformed, address, value);
            Expect<InvalidDataException>(() => Ff7CondorResources.Read(malformed));
        }
        foreach (ushort offset in new ushort[] { 0, 80 })
        {
            byte[] malformed = Fixture();
            Write(malformed, 10, offset);
            Expect<InvalidDataException>(() => Ff7CondorResources.Read(malformed).ReadWave(0));
        }
        byte[] badPath = Fixture();
        badPath[59] = 4;
        Expect<InvalidDataException>(() => Ff7CondorResources.Read(badPath).ReadWave(0));
        Expect<InvalidDataException>(() => Ff7CondorResources.Read(Fixture()[..60]).ReadWave(0));
        byte[] missingWaveEnd = Fixture()[..74];
        missingWaveEnd[72] = 0;
        Expect<InvalidDataException>(() => Ff7CondorResources.Read(missingWaveEnd).ReadWave(0));
        foreach (ushort pointer in new ushort[] { 0, 80 })
        {
            byte[] malformed = Fixture();
            Write(malformed, 34, pointer);
            Expect<InvalidDataException>(() => Ff7CondorResources.Read(malformed).ReadPath(1));
        }
        byte[] truncatedPoint = Fixture();
        Write(truncatedPoint, 34, 79);
        truncatedPoint[79] = 1;
        Expect<InvalidDataException>(() => Ff7CondorResources.Read(truncatedPoint).ReadPath(1));
        byte[] missingPathEnd = Fixture();
        Write(missingPathEnd, 34, 78);
        Expect<InvalidDataException>(() => Ff7CondorResources.Read(missingPathEnd).ReadPath(1));
        CheckTypesAndTerrain();
        Console.WriteLine("Condor resources preserve native wave/path records and short sentinels while rejecting invalid allocation references.");
    }

    private static void CheckTypesAndTerrain()
    {
        byte[] data = new byte[720];
        Fixture().CopyTo(data, 0);
        Write(data, 0, 80);
        byte[] intervals = [48, 26, 56, 48];
        for (int type = 16; type <= 19; type++)
        {
            data[80 + type * 32 + 6] = 7;
            data[80 + type * 32 + 0x0C] = intervals[type - 16];
        }
        byte[] identifiers = [2, 0];
        byte[] vertices = Enumerable.Range(0, 2 * 0x4C).Select(index => (byte)index).ToArray();
        byte[] before = vertices.ToArray();
        Ff7CondorResources resources = Ff7CondorResources.Read(data, identifiers, vertices);
        Check(resources.TerrainCells.Count == 2 && resources.TerrainCells[0].Span.SequenceEqual(vertices.AsSpan(0, 0x4C)) && resources.TerrainCells[1].Span.SequenceEqual(vertices.AsSpan(0x4C)), "Native terrain rows were reinterpreted or copied at a different stride.");
        for (int type = 16; type <= 19; type++)
        {
            Ff7CondorUnitType unit = resources.ReadUnitType(type);
            Check(unit.Behavior == 7 && unit.Interval == intervals[type - 16] && unit.Data.Length == 32, "Native unit type table offset, stride or unsigned behavior/interval bytes changed.");
        }
        Check(vertices.SequenceEqual(before) && identifiers.SequenceEqual(new byte[] { 2, 0 }), "Loading native terrain changed caller-owned cells or identifiers.");
        vertices[0] ^= 0xFF;
        identifiers[0] = 0;
        data[80 + 16 * 32 + 6] = 99;
        Check(resources.TerrainCells[0].Span[0] == before[0] && resources.ReadUnitType(16).Behavior == 7, "Native resource projections retained mutable caller-owned allocations.");
        Check(Ff7CondorResources.Read(Fixture()).TerrainCells.Count == 0, "The optional authored terrain seam fabricated native cell rows.");
        Expect<InvalidDataException>(() => Ff7CondorResources.Read(Fixture(), [2], new byte[2 * 0x4C]));
        Expect<InvalidDataException>(() => Ff7CondorResources.Read(Fixture(), [2, 0], new byte[0x4C]));
        Expect<InvalidDataException>(() => Ff7CondorResources.Read(Fixture(), default, new byte[0x4C]));
        Expect<ArgumentOutOfRangeException>(() => resources.ReadUnitType(-1));
        Expect<ArgumentOutOfRangeException>(() => resources.ReadUnitType(256));
        Expect<InvalidDataException>(() => resources.ReadUnitType(20));
        byte[] badDirectory = Fixture();
        Write(badDirectory, 0, 1);
        Expect<InvalidDataException>(() => Ff7CondorResources.Read(badDirectory).ReadUnitType(0));
    }

    private static byte[] Fixture()
    {
        byte[] data = new byte[80];
        Write(data, 4, 32);
        foreach (int attack in Enumerable.Range(0, 7))
        {
            Write(data, 10 + attack * 2, 56);
        }
        for (int path = 0; path < 4; path++)
        {
            int offset = 40 + path * 4;
            Write(data, 32 + path * 2, (ushort)offset);
            data[offset] = (byte)(4 + path * 2);
            data[offset + 1] = (byte)(2 + path);
            data[offset + 2] = 0xFF;
        }
        new byte[] { 18, 18, 2, 0, 0, 0, 0, 0, 19, 4, 2, 3, 0x34, 0x12, 1, 0xFE, 0xFF, 0xFF }.CopyTo(data, 56);
        return data;
    }

    private static void Write(byte[] data, int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset), value);

    private static void Expect<T>(Action action) where T : Exception
    {
        bool rejected = false;
        try
        {
            action();
        }
        catch (T)
        {
            rejected = true;
        }
        Check(rejected, "A malformed native resource reference was accepted.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
