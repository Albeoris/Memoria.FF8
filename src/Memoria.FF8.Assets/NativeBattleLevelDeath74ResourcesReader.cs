using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleLevelDeath74ResourcesReader
{
    public static NativeBattleLevelDeath74Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(0x1587F34, 19)) != "mag013-073-344.tim\0")
        {
            throw new InvalidDataException("LV?Death requires its original initializer texture name.");
        }
        ImmutableArray<byte> bytes = memory.Read(0x158233C, 0x3FCC).ToImmutableArray();
        int[] sections = Enumerable.Range(0, 5).Select(index => NativeBattleDatDirectory.Section(bytes.AsSpan(), index)).ToArray();
        if (!sections.SequenceEqual(new[] { 0x18, 0x568, 0x2E28, 0x3FCC, 0x3FCC }))
        {
            throw new InvalidDataException("LV?Death requires its original four-section model directory.");
        }
        NativeBattlePoseSource pose = new(bytes.Slice(sections[0], sections[1] - sections[0]), bytes.Slice(sections[2], sections[3] - sections[2]));
        pose.Validate();
        NativeBattleEffectOwnedModelResource model = new(0x158233C, bytes, pose, NativeBattleRawGeometryReader.Read(bytes.Slice(sections[1], sections[2] - sections[1]).ToArray(), pose.BoneCount));
        NativeBattleLevelDeath74Resources resources = new(new("FF8_EN:1582184", memory.Read(0x1582184, 440).ToImmutableArray()), model, new(memory.Read(0x1771F5C, 0x49F4).ToImmutableArray()) { ResourceAddress = 0x1771F5C }, new(memory.Read(0x1776950, 0x25EC).ToImmutableArray()) { ResourceAddress = 0x1776950 }, Tim(memory, 0x1778F3C), Tim(memory, 0x178135C), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0x1582074u), memory.Read(0x1586308, 128).ToImmutableArray(), new(0, 0x15863B0, 0, 0, memory.Read(0x15863B0, 0x1B74).ToImmutableArray()), U32(memory, 0x158206C), U32(memory, 0x1582070));
        resources.Validate();
        ValidateNodes(memory, resources.AuraNodes, [5, 3, 2, 2]);
        ValidateNodes(memory, resources.DarkNodes, [3, 0, 4, 0]);
        ImmutableArray<byte> caster = resources.CasterResource.Bytes;
        _ = NativeBattlePoseDecoder.ReadFrameCounts(caster.Slice(0x14, 0x1648 - 0x14).AsSpan());
        if (!NativeBattlePoseDecoder.ReadFrameCounts(pose.Animations.AsSpan()).Select(count => (int)count).SequenceEqual(new[] { 1, 16, 6, 14, 1, 24, 18 }))
        {
            throw new InvalidDataException("LV?Death requires all seven original effect-owned animation programmes.");
        }
        return resources;
    }

    private static void ValidateNodes(NativeExecutableMemory memory, NativeBattleCuragaExecutableSource source, int[] expectedCounts)
    {
        ImmutableArray<byte> data = source.Data;
        int[][] directories = Enumerable.Range(0, 4).Select(table => Enumerable.Range(0, 16).Select(index => Read(data, 4 + table * 64 + index * 4)).ToArray()).ToArray();
        if (!directories.Select(directory => directory.Count(offset => offset != 0)).SequenceEqual(expectedCounts) || directories[0].Max() + 0x158 != data.Length)
        {
            throw new InvalidDataException("LV?Death requires its complete original node directories and definitions.");
        }
        int[] spriteFrames = new int[16];
        int[] meshVertices = new int[16];
        int[] morphVertices = new int[16];
        for (int index = 0; index < 16; index++)
        {
            int sprite = directories[1][index];
            if (sprite != 0)
            {
                ImmutableArray<byte> original = NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, checked(source.ResourceAddress + (uint)sprite));
                Require(data, sprite, original.Length);
                NativeBattleModuleResource resource = new(0, checked(source.ResourceAddress + (uint)sprite), 0, 0, original);
                int frames = BinaryPrimitives.ReadUInt16LittleEndian(original.AsSpan().Slice(8));
                for (int frame = 0; frame < frames; frame++)
                {
                    _ = resource.ProjectedFrame571C80(frame);
                }
                spriteFrames[index] = frames;
            }
            int mesh = directories[2][index];
            if (mesh != 0)
            {
                NativeBattleStaticMeshResource resource = NativeBattleEffectStaticResourcesReader.ReadMesh(memory, checked(source.ResourceAddress + (uint)mesh));
                Require(data, mesh, resource.Bytes.Length);
                meshVertices[index] = ValidateMesh(resource.Bytes);
            }
            int morph = directories[3][index];
            if (morph != 0)
            {
                Require(data, morph, 8);
                int vertices = Read(data, morph + 4);
                int length = checked(8 + vertices * 8);
                if (vertices != 84 || Read(data, morph) != length)
                {
                    throw new InvalidDataException("LV?Death requires its two original 84-vertex morph sources.");
                }
                Require(data, morph, length);
                morphVertices[index] = vertices;
            }
        }
        foreach ((int node, int index) in directories[0].Select((offset, index) => (offset, index)).Where(row => row.offset != 0))
        {
            Require(data, node, 0x158);
            int kind = unchecked((sbyte)data[node + 0x16]);
            int mode = unchecked((sbyte)data[node + 0x1E]);
            int samples = kind == 0 && mode == 0 ? spriteFrames[index] - 1 : mode is 1 or 2 ? Math.Max(unchecked((sbyte)data[node + 0x1F]), unchecked((sbyte)data[node + 0x20])) : -1;
            int life = data[node + 0x12];
            if (samples is < 1 or > 45 || kind is not (0 or 4 or 5) || data[node + 0x18] != 0 || data[node + 0x1A] != 1)
            {
                throw new InvalidDataException("LV?Death requires its original fixed anchors and bounded sprite or mesh lifetimes.");
            }
            for (int field = 0xC8; field <= 0x144; field += 4)
            {
                Require(data, Read(data, node + field), Math.Max(80, checked(2 * Math.Max(life, samples))));
            }
            if (data[node + 0x36] is < 1 or > 4)
            {
                throw new InvalidDataException("LV?Death emission groups leave the original four-instance geometry layout.");
            }
            if (kind is 4 or 5)
            {
                for (int sample = 0; sample < samples; sample++)
                {
                    int selected = data[checked(Read(data, node + 0x12C) + sample)];
                    if (selected >= 16 || meshVertices[selected] == 0)
                    {
                        throw new InvalidDataException("LV?Death channels select an absent mesh.");
                    }
                    if (kind == 5)
                    {
                        int first = data[checked(Read(data, node + 0x130) + sample)];
                        int second = data[checked(Read(data, node + 0x134) + sample)];
                        if (first >= 16 || second >= 16 || morphVertices[first] != meshVertices[selected] || morphVertices[second] != meshVertices[selected])
                        {
                            throw new InvalidDataException("LV?Death morph channels do not match the selected mesh vertex count.");
                        }
                    }
                }
            }
            for (int child = 0; child < 5; child++)
            {
                int trigger = child == 4 ? 0x30 : 0x28 + child;
                int selector = child == 4 ? 0x31 : 0x2C + child;
                int selected = unchecked((sbyte)data[node + selector]);
                if (data[node + trigger] == 1 && (selected is < 0 or >= 16 || directories[0][selected] == 0))
                {
                    throw new InvalidDataException("LV?Death child source leaves its original node directory.");
                }
            }
        }
    }

    private static int ValidateMesh(ImmutableArray<byte> bytes)
    {
        int vertices = Read(bytes, 4);
        int cursor = Read(bytes, 0);
        if (vertices is < 1 or > 128 || cursor != 8 + vertices * 8)
        {
            throw new InvalidDataException("LV?Death mesh requires its original vertex rows.");
        }
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < 8; group++)
        {
            int count = Read(bytes, cursor);
            cursor = checked(cursor + 4);
            if (count is < 0 or > 96 || count != 0 && group is not (2 or 3 or 5 or 7))
            {
                throw new InvalidDataException("LV?Death requires its bounded original FT3, FT4, G4 and GT4 groups.");
            }
            Require(bytes, cursor, checked(count * strides[group]));
            for (int face = 0; face < count; face++)
            {
                for (int corner = 0; corner < ((group & 1) == 0 ? 3 : 4); corner++)
                {
                    int offset = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan().Slice(cursor + face * strides[group] + 4 + corner * 2)) * 4;
                    if ((offset & 7) != 0 || offset > vertices * 8 - 8)
                    {
                        throw new InvalidDataException("LV?Death mesh references a vertex outside its actual source.");
                    }
                }
            }
            cursor = checked(cursor + count * strides[group]);
        }
        if (cursor != bytes.Length)
        {
            throw new InvalidDataException("LV?Death mesh requires its exact original primitive extent.");
        }
        return vertices;
    }

    private static NativeBattleEffectHeapAllocation Tim(NativeExecutableMemory memory, uint address)
    {
        if (U32(memory, address) != 16)
        {
            throw new InvalidDataException("LV?Death requires its original TIM headers.");
        }
        int first = checked((int)U32(memory, address + 8));
        int length = checked(8 + first);
        if ((U32(memory, address + 4) & 8) != 0)
        {
            length = checked(length + (int)U32(memory, checked(address + (uint)length)));
        }
        if (first < 12 || length is < 20 or > 0x100000)
        {
            throw new InvalidDataException("LV?Death TIM blocks leave their actual original extents.");
        }
        return new($"FF8_EN:{address:X}", memory.Read(address, length).ToImmutableArray());
    }

    private static uint U32(NativeExecutableMemory memory, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(address, 4));
    private static int Read(ImmutableArray<byte> bytes, int offset)
    {
        Require(bytes, offset, 4);
        return BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan().Slice(offset));
    }
    private static void Require(ImmutableArray<byte> bytes, int offset, int length)
    {
        if (offset < 0 || length < 0 || offset > bytes.Length - length)
        {
            throw new InvalidDataException("LV?Death resources leave their exact original source bank.");
        }
    }
}

public sealed partial class GameAssets
{
    private NativeBattleLevelDeath74Resources? nativeLevelDeath74Resources;

    public NativeBattleLevelDeath74Resources ReadNativeLevelDeath74Resources()
    {
        return nativeLevelDeath74Resources ??= NativeBattleLevelDeath74ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
    }
}
