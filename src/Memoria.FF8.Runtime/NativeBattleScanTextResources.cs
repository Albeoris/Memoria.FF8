using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleScanCameraProfile(ushort Category, uint Address, ImmutableArray<byte> Bytes);

/// <summary>Original Scan description bytecode; presentation uses the application's system fonts.</summary>
public sealed record NativeBattleScanTextResources(ImmutableArray<ushort> DescriptionOffsets, ImmutableArray<byte> DescriptionBytes)
{
    public required ImmutableArray<ushort> KernelMiscOffsets { get; init; }
    public required ImmutableArray<byte> KernelMiscBytes { get; init; }
    public required int KernelMiscDirectoryFileOffset { get; init; }
    public required int KernelMiscTextFileOffset { get; init; }

    public int? MiscTextOffset(int id)
    {
        if (KernelMiscOffsets.IsDefault || (uint)id >= KernelMiscOffsets.Length)
        {
            throw new NotSupportedException("Native Scan requires its actual kernel miscellaneous text directory entry.");
        }
        ushort offset = KernelMiscOffsets[id];
        return offset == ushort.MaxValue ? null : offset;
    }

    public byte ReadMiscTextByte(int offset)
    {
        if (KernelMiscBytes.IsDefault || (uint)offset >= KernelMiscBytes.Length)
        {
            throw new NotSupportedException("Native Scan miscellaneous text offset leaves its original encoded kernel bank.");
        }
        return KernelMiscBytes[offset];
    }

    public ImmutableArray<byte>? ReadEncodedMiscText(int id)
    {
        int? offset = MiscTextOffset(id);
        if (offset is not int start)
        {
            return null;
        }
        if (KernelMiscBytes.IsDefault || (uint)start >= KernelMiscBytes.Length)
        {
            throw new InvalidDataException("Native Scan miscellaneous text directory leaves its original encoded kernel bank.");
        }
        int length = KernelMiscBytes.AsSpan()[start..].IndexOf((byte)0);
        if (length < 0)
        {
            throw new InvalidDataException("Native Scan miscellaneous text has no bounded original terminator.");
        }
        return KernelMiscBytes.Slice(start, length + 1);
    }

    public uint DescriptionPointer(int modelIndex)
    {
        if (DescriptionOffsets.IsDefault || DescriptionOffsets.Length != 160 || (uint)modelIndex >= 160)
        {
            throw new NotSupportedException("Native Scan requires its original model-indexed description directory.");
        }
        return checked(0x18875B4u + DescriptionOffsets[modelIndex]);
    }

    public byte ReadDescriptionByte(uint pointer)
    {
        uint offset = unchecked(pointer - 0x18875B4u);
        if (DescriptionBytes.IsDefault || offset >= (uint)DescriptionBytes.Length)
        {
            throw new NotSupportedException("Native Scan text pointer leaves its original description bank.");
        }
        return DescriptionBytes[(int)offset];
    }

}
