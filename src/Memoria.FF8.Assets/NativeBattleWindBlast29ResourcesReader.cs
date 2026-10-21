using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleWindBlast29ResourcesReader
{
    public static NativeBattleWindBlast29Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(0x160E094, 11)) != "mag028.tim\0")
        {
            throw new InvalidDataException("Wind Blast requires its original initializer texture name.");
        }
        NativeBattleWindBlast29Resources result = new(new("FF8_EN:160DF80", memory.Read(0x160DF80, 276).ToImmutableArray()), new(memory.Read(0x17D27DC, 14192).ToImmutableArray()) { ResourceAddress = 0x17D27DC }, BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x160DF7C, 4)));
        result.Validate();
        ImmutableArray<byte> bytes = result.Executable.Data;
        int[][] directories = Enumerable.Range(0, 4).Select(table => Enumerable.Range(0, 16).Select(index => Read(bytes, 4 + table * 64 + index * 4)).ToArray()).ToArray();
        if (!directories.Select(table => table.Count(offset => offset != 0)).SequenceEqual(new[] { 7, 3, 4, 0 }) || directories[0].Max() + 0x158 != bytes.Length)
        {
            throw new InvalidDataException("Wind Blast requires its complete original node, sprite and mesh directories.");
        }
        int[] spriteFrames = new int[16];
        for (int index = 0; index < 16; index++)
        {
            int relative = directories[1][index];
            if (relative != 0)
            {
                uint address = checked(result.Executable.ResourceAddress + (uint)relative);
                ImmutableArray<byte> sprite = NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address);
                Require(bytes, relative, sprite.Length);
                NativeBattleModuleResource resource = new(0, address, 0, 0, sprite);
                int count = BinaryPrimitives.ReadUInt16LittleEndian(sprite.AsSpan().Slice(8));
                for (int frame = 0; frame < count; frame++)
                {
                    _ = resource.ProjectedFrame571C80(frame);
                }
                spriteFrames[index] = count;
            }
            int mesh = directories[2][index];
            if (mesh != 0)
            {
                ValidateMesh(bytes, mesh);
            }
        }
        foreach ((int node, int index) in directories[0].Select((offset, index) => (offset, index)).Where(item => item.offset != 0))
        {
            Require(bytes, node, 0x158);
            for (int field = 0xC8; field <= 0x144; field += 4)
            {
                Require(bytes, Read(bytes, node + field), 80);
            }
            int kind = unchecked((sbyte)bytes[node + 0x16]);
            int mode = unchecked((sbyte)bytes[node + 0x1E]);
            int samples = mode is 1 or 2 ? Math.Max(unchecked((sbyte)bytes[node + 0x1F]), unchecked((sbyte)bytes[node + 0x20])) : mode == 0 && kind == 0 ? spriteFrames[index] - 1 : -1;
            if (samples is < 0 or > 40 || kind == 0 && spriteFrames[index] == 0 || kind is not (0 or 4))
            {
                throw new InvalidDataException("Wind Blast channels leave their original sprite and fixed-lifetime mesh contracts.");
            }
            if (kind == 4)
            {
                int stream = Read(bytes, node + 0x12C);
                for (int frame = 0; frame < samples; frame++)
                {
                    int selected = bytes[stream + frame];
                    if (selected >= 16 || directories[2][selected] == 0)
                    {
                        throw new InvalidDataException("Wind Blast channel selects an absent mesh.");
                    }
                }
            }
            for (int child = 0; child < 5; child++)
            {
                int trigger = child == 4 ? 0x30 : 0x28 + child;
                int selector = child == 4 ? 0x31 : 0x2C + child;
                if (bytes[node + trigger] != 1)
                {
                    continue;
                }
                int selected = unchecked((sbyte)bytes[node + selector]);
                if (selected is < 0 or >= 16 || directories[0][selected] == 0)
                {
                    throw new InvalidDataException("Wind Blast child source leaves its original node directory.");
                }
            }
        }
        return result;
    }

    private static void ValidateMesh(ImmutableArray<byte> bytes, int start)
    {
        int vertices = Read(bytes, start + 4);
        int relative = Read(bytes, start);
        if (vertices is < 1 or > 80 || relative != 8 + vertices * 8)
        {
            throw new InvalidDataException("Wind Blast mesh has invalid original vertex rows.");
        }
        Require(bytes, start, relative);
        int cursor = checked(start + relative);
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < strides.Length; group++)
        {
            int count = Read(bytes, cursor);
            cursor = checked(cursor + 4);
            if (count is < 0 or > 64 || count != 0 && group is not (3 or 4 or 7))
            {
                throw new InvalidDataException("Wind Blast requires its bounded original FT4, G3 and GT4 groups.");
            }
            Require(bytes, cursor, checked(count * strides[group]));
            for (int face = 0; face < count; face++)
            {
                for (int corner = 0; corner < ((group & 1) == 0 ? 3 : 4); corner++)
                {
                    int offset = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan().Slice(cursor + face * strides[group] + 4 + corner * 2)) * 4;
                    if ((offset & 7) != 0 || offset > vertices * 8 - 8)
                    {
                        throw new InvalidDataException("Wind Blast face reads beyond its actual vertex rows.");
                    }
                }
            }
            cursor = checked(cursor + count * strides[group]);
        }
    }

    private static int Read(ImmutableArray<byte> bytes, int offset)
    {
        Require(bytes, offset, 4);
        return BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan().Slice(offset));
    }

    private static void Require(ImmutableArray<byte> bytes, int offset, int length)
    {
        if (offset < 0 || length < 0 || offset > bytes.Length - length)
        {
            throw new InvalidDataException("Wind Blast resource reads beyond its original executable bank.");
        }
    }
}

public sealed partial class GameAssets
{
    private NativeBattleWindBlast29Resources? nativeWindBlast29Resources;

    public NativeBattleWindBlast29Resources ReadNativeWindBlast29Resources()
    {
        return nativeWindBlast29Resources ??= NativeBattleWindBlast29ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
    }
}
