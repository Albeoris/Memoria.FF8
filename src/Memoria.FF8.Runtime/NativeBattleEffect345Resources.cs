using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect345NodeResourceBank(uint Address, ImmutableArray<byte> Data)
{
    public void Validate()
    {
        int length = Address switch { 0x1643698 => 18932, 0x164808C => 9708, _ => throw new InvalidDataException("Effect345 node source has no original address.") };
        int nodes = Address == 0x1643698 ? 5 : 3;
        if (Data.IsDefault || Data.Length != length || Dword(0) != 0)
        {
            throw new InvalidDataException("Effect345 requires its complete cold relative node source.");
        }
        int found = 0;
        for (int table = 0; table < 4; table++)
        {
            for (int index = 0; index < 16; index++)
            {
                uint pointer = Directory(table, index);
                if (pointer == 0)
                {
                    continue;
                }
                if (pointer >= Data.Length || table == 0 && pointer > Data.Length - 344)
                {
                    throw new InvalidDataException("Effect345 node directory leaves its original source.");
                }
                if (table != 0)
                {
                    continue;
                }
                found++;
                for (int field = 0xC8; field < 0x148; field += 4)
                {
                    if (Dword(checked((int)pointer + field)) >= Data.Length)
                    {
                        throw new InvalidDataException("Effect345 node channel relocation leaves its source.");
                    }
                }
            }
        }
        if (found != nodes)
        {
            throw new InvalidDataException("Effect345 node count differs from its original source.");
        }
    }

    public uint Directory(int table, int index)
    {
        if ((uint)table >= 4 || (uint)index >= 16)
        {
            throw new InvalidDataException("Effect345 node selector leaves its four physical directories.");
        }
        return Dword(4 + table * 64 + index * 4);
    }

    private uint Dword(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(Data.AsSpan().Slice(offset, 4));
}

public sealed record NativeBattleEffect345Resources(NativeBattleEffectInitializerAsset MainTim, NativeBattleEffectHeapAllocation EmbeddedTim0, NativeBattleEffectHeapAllocation EmbeddedTim1, NativeBattleEffectHeapAllocation Camera, NativeBattleEffectOwnedModelResource Model, ImmutableArray<NativeBattleEffect345NodeResourceBank> Nodes, NativeBattleStaticMeshResource Mesh, ImmutableDictionary<uint, ImmutableArray<byte>> StaticSpans)
{
    [JsonRequired]
    public ImmutableDictionary<uint, uint> ColdPointerSeeds { get; init; } = ImmutableDictionary<uint, uint>.Empty;

    public const uint TextureNameAddress = 0x15297D8;
    public const string TextureName = "mag013-073-344.tim";
    public const int TextureLength = 167456;
    public const uint CameraAddress = 0x1524774;
    public const int CameraLength = 4064;
    public const uint ModelAddress = 0x1525754;
    public const int ModelLength = 16332;
    public const uint MeshAddress = 0x1524664;
    public const int MeshLength = 272;
    public const uint EmbeddedTim0Address = 0x164A678;
    public const int EmbeddedTim0Length = 33824;
    public const uint EmbeddedTim1Address = 0x1652A98;
    public const int EmbeddedTim1Length = 16928;

    public void Validate()
    {
        if (MainTim is null || MainTim.Name != TextureName || MainTim.Data.IsDefault || MainTim.Data.Length != TextureLength || EmbeddedTim0 is null || EmbeddedTim0.Data.IsDefault || EmbeddedTim0.Data.Length != EmbeddedTim0Length || EmbeddedTim1 is null || EmbeddedTim1.Data.IsDefault || EmbeddedTim1.Data.Length != EmbeddedTim1Length || Camera is null || Camera.Data.IsDefault || Camera.Data.Length != CameraLength || Model is null || Model.Address != ModelAddress || Model.Bytes.IsDefault || Model.Bytes.Length != ModelLength || Mesh is null || Mesh.Bytes.IsDefault || Mesh.Bytes.Length != MeshLength || Nodes.IsDefault || Nodes.Length != 2 || StaticSpans is null)
        {
            throw new InvalidDataException("Effect345 requires its exact original selected resource identities and extents.");
        }
        ValidateTim(MainTim.Data, [320, 240, 256, 7, 384, 256, 320, 256]);
        ValidateTim(EmbeddedTim0.Data, [320, 224, 256, 2, 832, 256, 64, 256]);
        ValidateTim(EmbeddedTim1.Data, [320, 226, 256, 1, 768, 256, 64, 128]);
        if (!Enumerable.Range(0, 4).Select(index => BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(index * 2, 2))).SequenceEqual(new ushort[] { 2, 8, 324, 4064 }) || !Enumerable.Range(0, 5).Select(Model.Section).SequenceEqual(new[] { 24, 1384, 11816, 16332, 16332 }))
        {
            throw new InvalidDataException("Effect345 camera or model directory differs from its original source.");
        }
        Model.PoseSource.Validate();
        ImmutableArray<byte> frames = NativeBattlePoseDecoder.ReadFrameCounts(Model.PoseSource.Animations.AsSpan());
        if (Model.PoseSource.BoneCount != 28 || frames.Length != 7 || frames[5] != 24 || frames[6] != 18 || Model.Geometry.IsDefault || Model.Geometry.Length != 1 || Model.Geometry[0].Vertices.Length != 302 || Model.Geometry[0].Polygons.Length != 526 || Nodes[0].Address != 0x1643698 || Nodes[1].Address != 0x164808C)
        {
            throw new InvalidDataException("Effect345 owned model or particle sources differ from their selected native fronts.");
        }
        Model.Geometry[0].Validate();
        foreach (NativeBattleEffect345NodeResourceBank node in Nodes)
        {
            node.Validate();
        }
        foreach ((uint address, int length) in new (uint, int)[] { (0x152465C, 4), (0x1524660, 4), (0x1529720, 30), (0x1529740, 30), (0x1529760, 30), (0x1529780, 30) })
        {
            if (!StaticSpans.TryGetValue(address, out ImmutableArray<byte> bytes) || bytes.IsDefault || bytes.Length != length)
            {
                throw new InvalidDataException("Effect345 requires its two sounds and four bounded frame tables.");
            }
        }
        if (StaticSpans.Count != 6)
        {
            throw new InvalidDataException("Effect345 static spans must exclude live colors and mutable pointer owners.");
        }
        if (ColdPointerSeeds is null || ColdPointerSeeds.Count != 2 || !ColdPointerSeeds.TryGetValue(0x1524658, out uint workspace) || workspace != 0x255E618 || !ColdPointerSeeds.TryGetValue(0x15297A0, out uint target) || target != 0x255EA58)
        {
            throw new InvalidDataException("Effect345 cold pointer seeds differ from their original current-owner bindings.");
        }
    }

    public ImmutableArray<byte> ReadStatic(uint address, int length)
    {
        if (length < 0)
        {
            throw new InvalidDataException("Effect345 immutable resource read has a negative extent.");
        }
        foreach ((uint start, ImmutableArray<byte> bytes) in StaticSpans.Concat(new Dictionary<uint, ImmutableArray<byte>> { [CameraAddress] = Camera.Data, [ModelAddress] = Model.Bytes, [MeshAddress] = Mesh.Bytes, [EmbeddedTim0Address] = EmbeddedTim0.Data, [EmbeddedTim1Address] = EmbeddedTim1.Data }))
        {
            if (address >= start && (ulong)address + (uint)length <= (ulong)start + (uint)bytes.Length)
            {
                return bytes.Slice(checked((int)(address - start)), length);
            }
        }
        throw new NotSupportedException("Effect345 static read leaves its selected immutable fronts; node data and pointers require their current owners.");
    }

    private static void ValidateTim(ImmutableArray<byte> data, ushort[] rectangles)
    {
        if (data.Length < 8 || BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan()) != 16 || BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan().Slice(4)) != 9)
        {
            throw new InvalidDataException("Effect345 texture requires its original indexed TIM header.");
        }
        int cursor = 8;
        for (int block = 0; block < 2; block++)
        {
            if (cursor > data.Length - 12)
            {
                throw new InvalidDataException("Effect345 texture block header leaves its source.");
            }
            int length = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan().Slice(cursor, 4));
            int width = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan().Slice(cursor + 8, 2));
            int height = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan().Slice(cursor + 10, 2));
            for (int field = 0; field < 4; field++)
            {
                if (BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan().Slice(cursor + 4 + field * 2, 2)) != rectangles[block * 4 + field])
                {
                    throw new InvalidDataException("Effect345 texture rectangle differs from its original VRAM placement.");
                }
            }
            if (length != 12 + 2 * width * height || length > data.Length - cursor)
            {
                throw new InvalidDataException("Effect345 texture block leaves its exact source.");
            }
            cursor += length;
        }
        if (cursor != data.Length)
        {
            throw new InvalidDataException("Effect345 texture contains bytes outside its two native blocks.");
        }
    }
}
