using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattlePsychoBlast23ResourcesReader
{
    public static NativeBattlePsychoBlast23Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(0x16146D8, 11)) != "mag022.tim\0")
        {
            throw new InvalidDataException("Psycho Blast requires its original initializer texture name.");
        }
        ImmutableArray<byte> overlay = memory.Read(0x16146C8, 16).ToImmutableArray();
        NativeBattlePsychoBlast23Resources result = new(new("FF8_EN:1614384", memory.Read(0x1614384, 832).ToImmutableArray()), new(memory.Read(0x17EA4F0, 15716).ToImmutableArray()) { ResourceAddress = 0x17EA4F0 }, BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x1614288, 4)), new(0x16146C8, Enumerable.Range(0, 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(overlay.AsSpan().Slice(index * 4))).ToImmutableArray()), new(0, 0x161428C, 0, 0, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, 0x161428C)), new(0x17EE254, memory.Read(0x17EE254, 8028).ToImmutableArray()), new(0x17F01B0, memory.Read(0x17F01B0, 5524).ToImmutableArray()), new(0x17F1744, memory.Read(0x17F1744, 8028).ToImmutableArray()));
        result.Validate();
        ValidateNodes(memory, result.Executable);
        ValidateAct(result.ActPrimary, 216);
        ValidateAct(result.ActDelayed, 88);
        ValidateAct(result.ActFollowup, 216);
        return result;
    }

    private static void ValidateNodes(IGameNativeMemorySource memory, NativeBattleCuragaExecutableSource source)
    {
        int[] counts = new int[4];
        for (int table = 0; table < 4; table++)
        {
            for (int index = 0; index < 16; index++)
            {
                int relative = Read(source.Data, 4 + table * 64 + index * 4);
                if (relative == 0)
                {
                    continue;
                }
                counts[table]++;
                uint address = checked(source.ResourceAddress + (uint)relative);
                switch (table)
                {
                    case 0:
                        Require(source.Data, relative, 0x158);
                        for (int field = 0xC8; field <= 0x144; field += 4)
                        {
                            Require(source.Data, Read(source.Data, relative + field), 80);
                        }
                        break;
                    case 1:
                        Require(source.Data, relative, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address).Length);
                        break;
                    case 2:
                        NativeBattleStaticMeshResource mesh = NativeBattleEffectStaticResourcesReader.ReadMesh(memory, address);
                        Require(source.Data, relative, mesh.Bytes.Length);
                        break;
                    default:
                        throw new InvalidDataException("Psycho Blast has no original generic morph directory.");
                }
            }
        }
        if (!counts.SequenceEqual(new[] { 5, 1, 5, 0 }))
        {
            throw new InvalidDataException("Psycho Blast requires its original node, sprite and mesh directories.");
        }
    }

    private static void ValidateAct(NativeBattleGfAnimationSource source, int expectedWorkspace)
    {
        ImmutableArray<byte> bytes = source.Data;
        int geometries = Read(bytes, 0);
        if (geometries is < 1 or > 3)
        {
            throw new InvalidDataException("Psycho Blast ACT geometry directory is invalid.");
        }
        for (int index = 0; index < geometries; index++)
        {
            int start = Read(bytes, 8 + index * 4);
            int vertices = Read(bytes, start + 4);
            int shapes = Read(bytes, start + 8);
            if (vertices is < 1 or > 84 || shapes is < 1 or > 2 || Read(bytes, start) != 12 + vertices * shapes * 8)
            {
                throw new InvalidDataException("Psycho Blast ACT vertex rows leave their original shape directory.");
            }
            int cursor = checked(start + 12 + vertices * shapes * 8);
            int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
            for (int group = 0; group < strides.Length; group++)
            {
                int count = Read(bytes, cursor);
                cursor = checked(cursor + 4);
                if (count is < 0 or > 128)
                {
                    throw new InvalidDataException("Psycho Blast ACT polygon count exceeds its original bounds.");
                }
                Require(bytes, cursor, checked(count * strides[group]));
                for (int face = 0; face < count; face++)
                {
                    for (int corner = 0; corner < ((group & 1) == 0 ? 3 : 4); corner++)
                    {
                        int offset = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan().Slice(cursor + face * strides[group] + 4 + corner * 2)) * 4;
                        if ((offset & 7) != 0 || offset > vertices * 8 - 8)
                        {
                            throw new InvalidDataException("Psycho Blast ACT face leaves its actual vertex rows.");
                        }
                    }
                }
                cursor = checked(cursor + count * strides[group]);
            }
        }
        int table = Read(bytes, 4);
        int nodes = Read(bytes, table);
        int duration = Read(bytes, table + 4);
        int workspace = 8;
        for (int index = 0; index < nodes; index++)
        {
            int node = checked(table + Read(bytes, table + 8 + index * 4));
            int header = Read(bytes, node);
            int flags = Read(bytes, node + 4);
            if ((header & 255) >= geometries || (flags & ~0x3FFF) != 0)
            {
                throw new InvalidDataException("Psycho Blast ACT node leaves its original geometry or channel flags.");
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
                        throw new InvalidDataException("Psycho Blast ACT key leaves its ordered native frame interval.");
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
            throw new InvalidDataException("Psycho Blast ACT channels contradict its actual workspace extent.");
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
            throw new InvalidDataException("Psycho Blast resource reads beyond its original retained extent.");
        }
    }
}

public sealed partial class GameAssets
{
    private NativeBattlePsychoBlast23Resources? nativePsychoBlast23Resources;

    public NativeBattlePsychoBlast23Resources ReadNativePsychoBlast23Resources()
    {
        return nativePsychoBlast23Resources ??= NativeBattlePsychoBlast23ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
    }
}
