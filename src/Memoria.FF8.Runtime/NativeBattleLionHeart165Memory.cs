using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleLionHeart165Controller
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

    private static NativeBattleFileStaging Forget(NativeBattleFileStaging staging, uint address, int length) => staging.MarkUnknown(address, length);

    private static NativeBattleFileStaging CopyTaskToStaging(NativeBattleFileStaging staging, uint destination, NativeBattleModulePoolTask task, int source, int length)
    {
        task.RequirePayloadRange(source, length);
        staging = staging.Write(destination, task.Payload.AsSpan().Slice(source - 12, length));
        if (task.UnknownPayloadRanges is { } ranges)
        {
            foreach (NativeBattleEffectUnknownRegion range in ranges)
            {
                int first = Math.Max(source, range.Offset);
                int end = Math.Min(source + length, range.Offset + range.Length);
                if (end > first)
                {
                    staging = staging.MarkUnknown(destination + (uint)(first - source), end - first);
                }
            }
        }
        return staging;
    }

    private static NativeBattleModulePoolTask CopyStagingToTask(NativeBattleFileStaging staging, uint source, NativeBattleModulePoolTask task, int destination, int length)
    {
        (byte[] bytes, bool[] known) = NativeBattleTemporaryMemory.ReadView(staging, source, length).ReadSnapshot(0, length);
        task = task.WriteBytes(destination, bytes);
        for (int index = 0; index < length; index++)
        {
            if (!known[index])
            {
                task = task.MarkPayloadUnknown(destination + index, 1);
            }
        }
        return task;
    }
}
