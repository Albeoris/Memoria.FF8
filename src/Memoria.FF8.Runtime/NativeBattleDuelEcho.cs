using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>6A1230/68ADF0's physical two-frame ring of selected32-byte bone matrices.</summary>
public static class NativeBattleDuelEcho
{
    public static NativeBattleModulePoolTask Initialize6A1230(NativeBattleModulePoolTask task, int offset, ImmutableArray<byte> bones)
    {
        if (bones.IsDefault || bones.Length > 128 || bones.Any(bone => bone >= 128) || bones.Distinct().Count() != bones.Length)
        {
            throw new InvalidDataException("Native Duel echo requires its original bounded bone selectors.");
        }
        task = task.WriteDword(offset, 0).WriteByte(offset + 4, checked((byte)bones.Length));
        task = task.WriteBytes(offset + 5, Enumerable.Repeat((byte)255, 128).ToArray());
        for (int index = 0; index < bones.Length; index++)
        {
            task = task.WriteByte(offset + 5 + bones[index], checked((byte)index));
        }
        return task;
    }

    public static NativeBattleModulePoolTask Capture68ADF0(NativeBattleModulePoolTask task, int offset, int boneCount, Func<int, (byte[] Data, bool[] Known)> readCurrentMatrix32)
    {
        return Capture(task, offset, boneCount, readCurrentMatrix32, 512);
    }

    public static NativeBattleModulePoolTask Capture697850(NativeBattleModulePoolTask task, int offset, int boneCount, Func<int, (byte[] Data, bool[] Known)> readCurrentMatrix32)
    {
        return Capture(task, offset, boneCount, readCurrentMatrix32, 1152);
    }

    public static NativeBattleModulePoolTask Capture8200(NativeBattleModulePoolTask task, int offset, int boneCount, Func<int, (byte[] Data, bool[] Known)> readCurrentMatrix32)
    {
        return Capture(task, offset, boneCount, readCurrentMatrix32, 64);
    }

    private static NativeBattleModulePoolTask Capture(NativeBattleModulePoolTask task, int offset, int boneCount, Func<int, (byte[] Data, bool[] Known)> readCurrentMatrix32, int historyStride)
    {
        if ((uint)boneCount > 128)
        {
            throw new InvalidDataException("Native Duel echo requires its actual skeleton count.");
        }
        byte ring = task.ReadByte(offset + 2);
        task = task.WriteByte(offset, 1).WriteByte(offset + 3, ring);
        byte next = unchecked((byte)(ring + 1));
        task = task.WriteByte(offset + 2, next >= 2 ? (byte)0 : next);
        byte count = unchecked((byte)(task.ReadByte(offset + 1) + 1));
        task = task.WriteByte(offset + 1, count > 2 ? (byte)2 : count);
        int bank = checked(offset + 136 + ring * historyStride);
        for (int bone = 0; bone < boneCount; bone++)
        {
            int selected = unchecked((sbyte)task.ReadByte(offset + 5 + bone));
            if (selected < 0)
            {
                continue;
            }
            (byte[] bytes, bool[] known) = readCurrentMatrix32(bone);
            if (bytes.Length != 32 || known.Length != 32)
            {
                throw new InvalidDataException("Native Duel echo copy requires the actual complete eight DWORD matrix row.");
            }
            int destination = checked(bank + selected * 32);
            task = task.WriteBytes(destination, bytes);
            for (int index = 0; index < 32; index++)
            {
                if (!known[index])
                {
                    task = task.MarkPayloadUnknown(destination + index, 1);
                }
            }
        }
        return task;
    }

    public static NativeBattleModulePoolTask CompleteDraw6A2760(NativeBattleModulePoolTask task, int offset)
    {
        if (task.ReadByte(offset) == 0 && task.ReadByte(offset + 1) != 0)
        {
            task = task.WriteByte(offset + 1, unchecked((byte)(task.ReadByte(offset + 1) - 1)));
        }
        return task.WriteByte(offset, 0);
    }

    public static int ReadHistoryOffset(NativeBattleModulePoolTask task, int offset, int history)
    {
        return ReadHistoryOffset(task, offset, history, 512);
    }

    internal static int ReadHistoryOffset(NativeBattleModulePoolTask task, int offset, int history, int historyStride)
    {
        if (history < 0 || history >= task.ReadByte(offset + 1))
        {
            throw new InvalidDataException("Native Duel echo history leaves its actual retained ring count.");
        }
        return checked(offset + 136 + historyStride * ((task.ReadByte(offset + 3) - history) & 1));
    }

    public static NativeBattleActionMatrix ReadHistoryMatrix(NativeBattleModulePoolTask task, int offset, int history, int bone)
    {
        if ((uint)bone >= 128)
        {
            throw new InvalidDataException("Native Duel echo bone leaves its original selector map.");
        }
        int selected = unchecked((sbyte)task.ReadByte(offset + 5 + bone));
        if (selected < 0)
        {
            throw new InvalidDataException("Native Duel echo bone was not selected by its actual native caller.");
        }
        int address = checked(ReadHistoryOffset(task, offset, history) + selected * 32);
        ImmutableArray<short> rotation = Enumerable.Range(0, 9).Select(index => unchecked((short)task.ReadWord(address + index * 2))).ToImmutableArray();
        return new(rotation, unchecked((int)task.ReadDword(address + 20)), unchecked((int)task.ReadDword(address + 24)), unchecked((int)task.ReadDword(address + 28)), 4096);
    }
}
