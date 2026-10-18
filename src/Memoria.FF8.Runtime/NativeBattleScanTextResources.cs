using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Original Scan description bytecode; presentation uses the application's system fonts.</summary>
public sealed record NativeBattleScanTextResources(ImmutableArray<ushort> DescriptionOffsets, ImmutableArray<byte> DescriptionBytes)
{
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
