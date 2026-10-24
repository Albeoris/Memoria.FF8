using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleStageReloadCopy(uint Source, uint Destination, int Length, uint ClutSource, uint ClutDestination);

public sealed partial record NativeBattleStageState
{
    public NativeBattleStageState Reload512AC0(int index, NativeBattleStageSource source)
    {
        Validate();
        NativeBattleStageReload512AC0.RequireIndex(index);
        if (SourceAddress1D9A820 is not > 0)
        {
            throw new NotSupportedException("Native stage reload requires its current physical source pointer.");
        }
        uint seed = ReloadColorSeed1D9A824 ?? throw new NotSupportedException("Native stage reload requires its retained source color DWORD.");
        if (source.Groups.IsDefault || source.Groups.Length != 4 || source.Geometry is not { IsDefault: false } geometry || source.TextureAnimations.IsDefault)
        {
            throw new InvalidDataException("Native stage reload requires its four decoded current groups and geometry.");
        }
        var groups = Groups.ToBuilder();
        for (int group = 0; group < groups.Count; group++)
        {
            NativeBattleStageGroup current = groups[group];
            groups[group] = source.Groups[group] is { } pose ? Select509440(current with { Source = pose, Flags = (byte)(current.Flags | 7) }) : current with { Flags = (byte)(current.Flags & 0xF8) };
        }
        NativeBattleStageState result = this with
        {
            Groups = groups.ToImmutable(),
            MutableGeometry = geometry,
            TextureAnimations = source.TextureAnimations.IsEmpty ? null : source.TextureAnimations,
            ModelColors = new(0x00606868, (seed & 0xFFFFFF) | 0x32000000),
            RotationSpeed = 8,
            FinalStage142 = FinalStage142 is { } finalStage ? finalStage with { SavedRotation = 8 } : null
        };
        result.Validate();
        return result;
    }

    public uint? CurrentGroupGeometryAddress(int group, Func<uint, int, byte[]> read)
    {
        if ((uint)group >= 4)
        {
            throw new ArgumentOutOfRangeException(nameof(group));
        }
        uint address = SourceAddress1D9A820 is > 0 ? SourceAddress1D9A820.Value : throw new NotSupportedException("Native stage geometry requires its current source pointer.");
        byte[] directory = read(address, 32);
        if (directory.Length != 32 || BinaryPrimitives.ReadUInt32LittleEndian(directory) != 6)
        {
            throw new InvalidDataException("Native stage geometry requires its current six-section directory.");
        }
        uint start = BinaryPrimitives.ReadUInt32LittleEndian(directory.AsSpan(4 + group * 4));
        uint end = BinaryPrimitives.ReadUInt32LittleEndian(directory.AsSpan(8 + group * 4));
        if (start > end || start < 32)
        {
            throw new InvalidDataException("Native stage group leaves its current directory.");
        }
        if (start == end)
        {
            return null;
        }
        uint section = checked(address + start);
        byte[] header = read(section, 16);
        if (header.Length != 16 || BinaryPrimitives.ReadUInt32LittleEndian(header) != 3)
        {
            throw new InvalidDataException("Native stage group requires its current three-section directory.");
        }
        uint geometry = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(8));
        uint animation = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(12));
        if (geometry < 16 || geometry > animation || animation > end - start)
        {
            throw new InvalidDataException("Native stage geometry leaves its current group.");
        }
        return checked(section + geometry);
    }
}

public static class NativeBattleStageReload512AC0
{
    public static NativeBattleStageReloadCopy Plan(NativeBattleStageState stage, int index, uint loadedFile, Func<uint, int, byte[]> read)
    {
        RequireIndex(index);
        uint destination = stage.SourceAddress1D9A820 is > 0 ? stage.SourceAddress1D9A820.Value : throw new NotSupportedException("Native stage reload requires its retained current source pointer.");
        uint source = checked(loadedFile + 0x1A64);
        byte[] directory = read(source, 32);
        if (directory.Length != 32 || BinaryPrimitives.ReadUInt32LittleEndian(directory) != 6)
        {
            throw new InvalidDataException("Native stage reload requires the addressed loaded six-section directory.");
        }
        uint tim = BinaryPrimitives.ReadUInt32LittleEndian(directory.AsSpan(24));
        int length = checked((int)tim + 0x2020);
        _ = checked(destination + (uint)length);
        return new(source, destination, length, checked(source + tim), checked(destination + tim + 20));
    }

    public static short File512AA0(int index)
    {
        RequireIndex(index);
        return 37;
    }

    internal static void RequireIndex(int index)
    {
        if (index != 0)
        {
            throw new NotSupportedException("Native stage replacement has no decoded table row for this index.");
        }
    }
}
