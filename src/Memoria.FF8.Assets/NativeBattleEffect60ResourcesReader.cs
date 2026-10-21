using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleEffect60ResourcesReader
{
    public static NativeBattleEffect60Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(0x1597830, 11)) != "mag059.tim\0")
        {
            throw new InvalidDataException("Effect60 requires its original initializer texture name.");
        }
        ImmutableArray<byte> ratios = memory.Read(0x1597820, 16).ToImmutableArray();
        NativeBattleEffect60Resources result = new(new("FF8_EN:159750C", memory.Read(0x159750C, 788).ToImmutableArray()), new(memory.Read(0x1797CC0, 19672).ToImmutableArray()) { ResourceAddress = 0x1797CC0 }, new(memory.Read(0x179C998, 17260).ToImmutableArray()) { ResourceAddress = 0x179C998 }, new(0x17A0D04, memory.Read(0x17A0D04, 9292).ToImmutableArray()), new(0x17A3150, memory.Read(0x17A3150, 3200).ToImmutableArray()), new(0, 0x1597360, 0, 0, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, 0x1597360)), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x159735C, 4)), Enumerable.Range(0, 8).Select(index => BinaryPrimitives.ReadInt16LittleEndian(ratios.AsSpan().Slice(index * 2))).ToImmutableArray());
        result.Validate();
        ValidateNodes(memory, result.PrimaryNodes, [4, 1, 6, 5]);
        ValidateNodes(memory, result.SecondaryNodes, [2, 0, 6, 5]);
        ValidateAct(result.ActPrimary, 228);
        ValidateAct(result.ActSecondary, 36);
        return result;
    }

    private static void ValidateNodes(IGameNativeMemorySource memory, NativeBattleCuragaExecutableSource source, int[] expectedCounts)
    {
        int[] counts = new int[4];
        int[][] directories = Enumerable.Range(0, 4).Select(table => Enumerable.Range(0, 16).Select(index => Read(source.Data, 4 + table * 64 + index * 4)).ToArray()).ToArray();
        int[] vertices = new int[16];
        for (int table = 0; table < 4; table++)
        {
            for (int index = 0; index < 16; index++)
            {
                int relative = directories[table][index];
                if (relative == 0)
                {
                    continue;
                }
                counts[table]++;
                uint address = checked(source.ResourceAddress + (uint)relative);
                if (table == 0)
                {
                    Require(source.Data, relative, 0x158);
                    for (int field = 0xC8; field <= 0x144; field += 4)
                    {
                        Require(source.Data, Read(source.Data, relative + field), 80);
                    }
                }
                else if (table == 1)
                {
                    Require(source.Data, relative, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address).Length);
                }
                else if (table == 2)
                {
                    NativeBattleStaticMeshResource mesh = NativeBattleEffectStaticResourcesReader.ReadMesh(memory, address);
                    Require(source.Data, relative, mesh.Bytes.Length);
                    vertices[index] = ValidateMesh(source.Data, relative, 8, 1);
                }
                else
                {
                    int count = Read(source.Data, relative + 4);
                    int length = Read(source.Data, relative);
                    if (count is < 1 or > 121 || length != 8 + count * 8)
                    {
                        throw new InvalidDataException("Effect60 morph source has an invalid vertex envelope.");
                    }
                    Require(source.Data, relative, length);
                }
            }
        }
        if (!counts.SequenceEqual(expectedCounts))
        {
            throw new InvalidDataException("Effect60 requires each original bank's distinct node, sprite, mesh and morph directories.");
        }
        foreach (int node in directories[0].Where(offset => offset != 0))
        {
            int kind = unchecked((sbyte)source.Data[node + 0x16]);
            int lifetime = unchecked((sbyte)source.Data[node + 0x12]);
            int mode = unchecked((sbyte)source.Data[node + 0x1E]);
            int samples = Math.Max(unchecked((sbyte)source.Data[node + 0x1F]), unchecked((sbyte)source.Data[node + 0x20]));
            if (lifetime is < 1 or > 40 || kind is 4 or 5 && (mode is not (1 or 2) || samples is < 1 or > 40))
            {
                throw new InvalidDataException("Effect60 channel lifetimes exceed their bounded native WORD streams.");
            }
            if (kind is not (4 or 5))
            {
                continue;
            }
            for (int frame = 0; frame < samples; frame++)
            {
                int meshIndex = source.Data[Read(source.Data, node + 0x12C) + frame];
                if (meshIndex >= 16 || vertices[meshIndex] == 0)
                {
                    throw new InvalidDataException("Effect60 channel selects an absent native mesh.");
                }
                if (kind == 5)
                {
                    foreach (int field in new[] { 0x130, 0x134 })
                    {
                        int shapeIndex = source.Data[Read(source.Data, node + field) + frame];
                        if (shapeIndex >= 16 || directories[3][shapeIndex] == 0)
                        {
                            throw new InvalidDataException("Effect60 channel selects an absent native morph shape.");
                        }
                        int shape = directories[3][shapeIndex];
                        int mesh = directories[2][meshIndex];
                        int count = vertices[meshIndex];
                        if (Read(source.Data, shape + 4) < count || shape != mesh && shape + 8 < mesh + 8 + count * 8 && mesh + 8 < shape + 8 + count * 8)
                        {
                            throw new InvalidDataException("Effect60 morph spans overlap with a shifted alias or lack required vertices.");
                        }
                    }
                }
            }
        }
    }

    private static int ValidateMesh(ImmutableArray<byte> bytes, int start, int headerLength, int shapes)
    {
        int vertices = Read(bytes, start + 4);
        int relative = Read(bytes, start);
        if (vertices is < 1 or > 121 || shapes is < 1 or > 3 || relative != headerLength + vertices * shapes * 8)
        {
            throw new InvalidDataException("Effect60 mesh has invalid original shape or vertex rows.");
        }
        Require(bytes, start, relative);
        int cursor = checked(start + relative);
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < strides.Length; group++)
        {
            int count = Read(bytes, cursor);
            cursor = checked(cursor + 4);
            if (count is < 0 or > 4096)
            {
                throw new InvalidDataException("Effect60 mesh face count exceeds its original bounded resource.");
            }
            Require(bytes, cursor, checked(count * strides[group]));
            for (int face = 0; face < count; face++)
            {
                for (int corner = 0; corner < ((group & 1) == 0 ? 3 : 4); corner++)
                {
                    int offset = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan().Slice(cursor + face * strides[group] + 4 + corner * 2)) * 4;
                    if ((offset & 7) != 0 || offset > vertices * 8 - 8)
                    {
                        throw new InvalidDataException("Effect60 face reads outside its actual vertex rows.");
                    }
                }
            }
            cursor = checked(cursor + count * strides[group]);
        }
        return vertices;
    }

    private static void ValidateAct(NativeBattleGfAnimationSource source, int expectedWorkspace)
    {
        ImmutableArray<byte> bytes = source.Data;
        int geometries = Read(bytes, 0);
        if (geometries is < 1 or > 3)
        {
            throw new InvalidDataException("Effect60 ACT geometry directory is invalid.");
        }
        for (int index = 0; index < geometries; index++)
        {
            int start = Read(bytes, 8 + index * 4);
            _ = ValidateMesh(bytes, start, 12, Read(bytes, start + 8));
        }
        int table = Read(bytes, 4);
        int nodes = Read(bytes, table);
        int duration = Read(bytes, table + 4);
        int workspace = 8;
        for (int index = 0; index < nodes; index++)
        {
            int node = checked(table + Read(bytes, table + 8 + index * 4));
            int flags = Read(bytes, node + 4);
            if ((Read(bytes, node) & 255) >= geometries || (flags & ~0x3FFF) != 0)
            {
                throw new InvalidDataException("Effect60 ACT node leaves its original geometry or channel flags.");
            }
            int cursor = node + 8;
            for (int bit = 0; bit < 14; bit++)
            {
                if ((flags & (1 << bit)) == 0)
                {
                    continue;
                }
                int stride = bit < 9 || bit == 12 ? 16 : bit == 13 ? 20 : 12;
                int key = checked(node + Read(bytes, cursor));
                cursor += 4;
                int prior = -1;
                while (true)
                {
                    int frame = Read(bytes, key);
                    if (frame == -1)
                    {
                        break;
                    }
                    if (frame < prior || frame >= duration)
                    {
                        throw new InvalidDataException("Effect60 ACT key leaves its ordered native frame interval.");
                    }
                    Require(bytes, key, stride);
                    prior = frame;
                    key = checked(key + stride);
                }
            }
            workspace += VectorSize(flags) + VectorSize(flags >> 3) + VectorSize(flags >> 6) + ((flags & 0x800) != 0 ? 24 : (flags & 0x400) != 0 ? 16 : (flags & 0x200) != 0 ? 8 : 0) + ((flags & 0x1000) != 0 ? 12 : 0) + ((flags & 0x2000) != 0 ? 16 : 0);
        }
        if (workspace != expectedWorkspace)
        {
            throw new InvalidDataException("Effect60 ACT channels contradict its actual workspace extent.");
        }
    }

    private static int VectorSize(int flags) => (flags & 4) != 0 ? 36 : (flags & 2) != 0 ? 24 : (flags & 1) != 0 ? 12 : 0;

    private static int Read(ImmutableArray<byte> bytes, int offset)
    {
        Require(bytes, offset, 4);
        return BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan().Slice(offset));
    }

    private static void Require(ImmutableArray<byte> bytes, int offset, int length)
    {
        if (offset < 0 || length < 0 || offset > bytes.Length - length)
        {
            throw new InvalidDataException("Effect60 resource reads beyond its original retained extent.");
        }
    }
}

public sealed partial class GameAssets
{
    private NativeBattleEffect60Resources? nativeEffect60Resources;

    public NativeBattleEffect60Resources ReadNativeEffect60Resources()
    {
        return nativeEffect60Resources ??= NativeBattleEffect60ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
    }
}
