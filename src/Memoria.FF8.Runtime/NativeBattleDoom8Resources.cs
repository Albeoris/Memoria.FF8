using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDoom8Resources(NativeBattleEffectInitializerAsset MainTim, NativeBattleEffectHeapAllocation Camera, ImmutableArray<NativeBattleEffectFileAsset> Files, ImmutableArray<NativeBattleEffectHeapAllocation> DirectorTims, NativeBattleEffectHeapAllocation InitialIoScript, uint DirectorSound, NativeBattleCuragaExecutableSource NodeSource, NativeBattleEffectOwnedModelResource ReaperModel)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleDoom8StripTables? StripTables { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<short>? PlacementSelectors { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<uint>? OverlayColors { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public uint? ColdTargetPointer { get; init; }

    public NativeBattleDoom8StripTables RequireStripTables()
    {
        NativeBattleDoom8StripTables tables = StripTables ?? throw new NotSupportedException("Doom strips require their original placement and drawing tables.");
        tables.Validate();
        return tables;
    }

    public ImmutableArray<short> RequirePlacementSelectors()
    {
        ImmutableArray<short> selectors = PlacementSelectors ?? throw new NotSupportedException("Doom ACT placement requires its original selector table.");
        if (selectors.IsDefault || selectors.Length != 5 || selectors[^1] != 127 || selectors.Take(selectors.Length - 1).Any(selector => selector is < 0 or > 17))
        {
            throw new InvalidDataException("Doom ACT placement requires its complete terminated row selectors.");
        }
        return selectors;
    }

    public ImmutableArray<uint> RequireOverlayColors()
    {
        ImmutableArray<uint> colors = OverlayColors ?? throw new NotSupportedException("Doom overlay requires its original color programme.");
        if (colors.IsDefault || colors.Length != 4)
        {
            throw new InvalidDataException("Doom overlay requires its complete four-word color programme.");
        }
        return colors;
    }

    public uint RequireColdTargetPointer()
    {
        uint pointer = ColdTargetPointer ?? throw new NotSupportedException("Doom strips require their original cold target pointer.");
        if (pointer != NativeBattleDoom8Strips.TargetAddress)
        {
            throw new InvalidDataException("Doom cold target pointer does not identify its retained target allocation.");
        }
        return pointer;
    }

    public NativeBattleModuleResource? DustSprite { get; init; }
    public NativeBattleStaticMeshResource? SmokeMesh { get; init; }

    public NativeBattleModuleResource RequireDustSprite()
    {
        NativeBattleModuleResource sprite = DustSprite ?? throw new NotSupportedException("Doom dust requires its original sprite resource.");
        if (sprite.Address != 0x1634F28 || sprite.Bytes.IsDefault || sprite.Bytes.Length != 456 || BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8)) != 17)
        {
            throw new InvalidDataException("Doom dust requires all seventeen original sprite frames.");
        }
        return sprite;
    }

    public NativeBattleStaticMeshResource RequireSmokeMesh()
    {
        NativeBattleStaticMeshResource mesh = SmokeMesh ?? throw new NotSupportedException("Doom smoke requires its original mesh resource.");
        if (mesh.Address != 0x16350F0 || mesh.Bytes.IsDefault || mesh.Bytes.Length != 2024 || BinaryPrimitives.ReadUInt32LittleEndian(mesh.Bytes.AsSpan()) != 480 || BinaryPrimitives.ReadUInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4)) != 59)
        {
            throw new InvalidDataException("Doom smoke requires its complete original mesh.");
        }
        int cursor = 480;
        for (int group = 0; group < 8; group++)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(cursor));
            if (count != (group == 7 ? 42 : 0))
            {
                throw new InvalidDataException("Doom smoke requires its forty-two original textured Gouraud quads.");
            }
            cursor = checked(cursor + 4 + count * 36);
        }
        return mesh;
    }

    public NativeBattleEffectFileAsset File(short id)
    {
        return Files.Single(file => file.FileId == id);
    }

    public NativeBattleGfAnimationSource ActSource(short id, uint destination)
    {
        if (id is < 383 or > 385)
        {
            throw new ArgumentOutOfRangeException(nameof(id));
        }
        return new(destination, File(id).Data);
    }

    public void Validate()
    {
        if (StripTables is not null)
        {
            _ = RequireStripTables();
        }
        if (PlacementSelectors is not null)
        {
            _ = RequirePlacementSelectors();
        }
        if (OverlayColors is not null)
        {
            _ = RequireOverlayColors();
        }
        if (ColdTargetPointer is not null)
        {
            _ = RequireColdTargetPointer();
        }
        if (MainTim is null || !string.Equals(MainTim.Name, "mag007.tim", StringComparison.OrdinalIgnoreCase) || MainTim.Data.IsDefault || MainTim.Data.Length != 167968 || Camera is null || Camera.Identity != "FF8_EN:16358D8" || Camera.Data.IsDefault || Camera.Data.Length != 4908 || InitialIoScript is null || InitialIoScript.Identity != "FF8_EN:1636C78" || InitialIoScript.Data.IsDefault || InitialIoScript.Data.Length != 112)
        {
            throw new InvalidDataException("Doom requires its complete original initializer, camera and cold IO script.");
        }
        if (Files.IsDefault || Files.Length != 5 || DirectorTims.IsDefault || DirectorTims.Length != 2 || DirectorSound != 0x494A2)
        {
            throw new InvalidDataException("Doom requires its five script files, both director textures and original sound value.");
        }
        int[] sizes = [7072, 3204, 10984, 33312, 14880];
        string[] names = ["mag007_b.1s0", "mag007_b.1s1", "mag007_b.1s2", "mag007_b.1t0", "mag007_b.1t1"];
        for (int index = 0; index < Files.Length; index++)
        {
            NativeBattleEffectFileAsset file = Files[index];
            if (file is null || file.FileId != 383 + index || !string.Equals(file.Name, names[index], StringComparison.OrdinalIgnoreCase) || file.Data.IsDefault || file.Data.Length != sizes[index])
            {
                throw new InvalidDataException("Doom requires the exact ordered file selected by each original script row.");
            }
        }
        for (int index = 0; index < DirectorTims.Length; index++)
        {
            NativeBattleEffectHeapAllocation tim = DirectorTims[index];
            if (tim is null || tim.Identity != (index == 0 ? "FF8_EN:181CFAC" : "FF8_EN:18253CC") || tim.Data.IsDefault || tim.Data.Length != (index == 0 ? 33824 : 16928))
            {
                throw new InvalidDataException("Doom requires both complete director textures in upload order.");
            }
        }
        if (NodeSource is null || NodeSource.ResourceAddress != 0x1818B78 || NodeSource.Data.IsDefault || NodeSource.Data.Length != 17460 || BinaryPrimitives.ReadUInt32LittleEndian(NodeSource.Data.AsSpan()) != 0)
        {
            throw new InvalidDataException("Doom requires its complete uninitialized embedded node source.");
        }
        int[] directoryCounts = [4, 2, 2, 2];
        for (int table = 0; table < 4; table++)
        {
            int populated = 0;
            for (int index = 0; index < 16; index++)
            {
                uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(NodeSource.Data.AsSpan().Slice(4 + table * 64 + index * 4));
                if (pointer == 0)
                {
                    continue;
                }
                populated++;
                if (pointer < 0x104 || pointer > NodeSource.Data.Length - (table == 0 ? 344 : 8))
                {
                    throw new InvalidDataException("Doom node directory leaves its embedded source.");
                }
                if (table == 0)
                {
                    for (int field = 0xC8; field <= 0x144; field += 4)
                    {
                        uint channel = BinaryPrimitives.ReadUInt32LittleEndian(NodeSource.Data.AsSpan().Slice(checked((int)pointer + field)));
                        if (channel < 0x104 || channel >= NodeSource.Data.Length)
                        {
                            throw new InvalidDataException("Doom node channel leaves its embedded source.");
                        }
                    }
                }
            }
            if (populated != directoryCounts[table])
            {
                throw new InvalidDataException("Doom requires its four original populated resource directories.");
            }
        }
        if (ReaperModel is null || ReaperModel.Address != 0x1636CE8 || ReaperModel.Bytes.IsDefault || ReaperModel.Bytes.Length != 16332 || ReaperModel.PoseSource is null || ReaperModel.PoseSource.BoneCount != 28 || ReaperModel.PoseSource.Skeleton.Length != 1360 || ReaperModel.PoseSource.Animations.Length != 4516 || ReaperModel.Geometry.IsDefault || ReaperModel.Geometry.Length != 1 || ReaperModel.Geometry[0].Vertices.Length != 302 || ReaperModel.Geometry[0].Polygons.Length != 526)
        {
            throw new InvalidDataException("Doom requires its complete embedded skeletal model and typed source sections.");
        }
        int[] sections = [0x18, 0x568, 0x2E28, 0x3FCC, 0x3FCC];
        for (int index = 0; index < sections.Length; index++)
        {
            if (ReaperModel.Section(index) != sections[index])
            {
                throw new InvalidDataException("Doom model sections disagree with their original bounded directory.");
            }
        }
        ReaperModel.PoseSource.Validate();
        ReaperModel.Geometry[0].Validate();
        ReadOnlySpan<byte> script = InitialIoScript.Data.AsSpan();
        for (int row = 0; row < 7; row++)
        {
            ReadOnlySpan<byte> bytes = script.Slice(row * 16, 16);
            uint operation = row == 0 ? 0u : row == 6 ? 0xFFu : row <= 3 ? 3u : 1u;
            uint file = row is > 0 and < 6 ? (uint)(382 + row) : 0;
            uint selector = row is 2 or 3 ? (uint)(row - 1) : 0;
            uint length = row is > 0 and < 6 ? (uint)sizes[row - 1] : 0;
            if (BinaryPrimitives.ReadUInt32LittleEndian(bytes) != (operation | file << 16) || BinaryPrimitives.ReadUInt32LittleEndian(bytes[4..]) != selector || BinaryPrimitives.ReadUInt32LittleEndian(bytes[8..]) != length || BinaryPrimitives.ReadUInt32LittleEndian(bytes[12..]) != 0)
            {
                throw new InvalidDataException("Doom requires its unmodified cold IO script; mutable destinations belong to the active IO owner.");
            }
        }
    }
}
