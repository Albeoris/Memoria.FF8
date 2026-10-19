using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleBlastingZone164Controller
{
    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static short ReadWord(NativeBattleFileStaging staging, uint address) => BinaryPrimitives.ReadInt16LittleEndian(staging.Read(address, 2));
    private static int ReadDword(NativeBattleFileStaging staging, uint address) => BinaryPrimitives.ReadInt32LittleEndian(staging.Read(address, 4));

    private static NativeBattleFileStaging WriteWord(NativeBattleFileStaging staging, uint address, ushort value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return staging.Write(address, bytes);
    }

    private static NativeBattleFileStaging WriteDword(NativeBattleFileStaging staging, uint address, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return staging.Write(address, bytes);
    }

    private static NativeBattleFileStaging StoreMatrix(NativeBattleFileStaging staging, uint address, NativeBattleActionMatrix matrix)
    {
        for (int index = 0; index < 9; index++)
        {
            staging = WriteWord(staging, address + (uint)index * 2, unchecked((ushort)matrix.Rotation[index]));
        }
        return WriteDword(WriteDword(WriteDword(staging, address + 20, unchecked((uint)matrix.X)), address + 24, unchecked((uint)matrix.Y)), address + 28, unchecked((uint)matrix.Z));
    }

    private static NativeBattleFileStaging Point(NativeBattleFileStaging staging, uint address, NativeBattleVector point)
    {
        return WriteWord(WriteWord(WriteWord(staging, address, unchecked((ushort)point.X)), address + 2, unchecked((ushort)point.Y)), address + 4, unchecked((ushort)point.Z));
    }

    private static NativeBattleFileStaging Forget(NativeBattleFileStaging staging, uint address, int length)
    {
        int first = checked((int)(address - staging.BaseAddress));
        int end = checked(first + length);
        ImmutableArray<NativeBattleEffectMemoryRegion>.Builder ranges = ImmutableArray.CreateBuilder<NativeBattleEffectMemoryRegion>();
        foreach (NativeBattleEffectMemoryRegion region in staging.KnownRanges)
        {
            int regionEnd = checked(region.Offset + region.Data.Length);
            if (regionEnd <= first || region.Offset >= end)
            {
                ranges.Add(region);
                continue;
            }
            if (region.Offset < first)
            {
                ranges.Add(new(region.Offset, region.Data.AsSpan(0, first - region.Offset).ToArray().ToImmutableArray()));
            }
            if (regionEnd > end)
            {
                ranges.Add(new(end, region.Data.AsSpan().Slice(end - region.Offset).ToArray().ToImmutableArray()));
            }
        }
        return staging with { KnownRanges = ranges.ToImmutable() };
    }
}
