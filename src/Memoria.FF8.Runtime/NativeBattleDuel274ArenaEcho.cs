using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleDuel274ArenaEcho
{
    public const int Length = 1160;

    public static NativeBattleModulePoolTask Read(NativeBattleDuel274Frame frame, uint address)
    {
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Duel echo requires its physical arena owner.");
        (byte[] data, bool[] known) = arena.ReadSnapshot(NativeBattleWaterExecutionFields.ArenaOffset(address, Length), Length);
        NativeBattleModulePoolTask task = new(0, 0, null, 0, data.ToImmutableArray());
        for (int index = 0; index < known.Length; index++)
        {
            if (!known[index])
            {
                task = task.MarkPayloadUnknown(12 + index, 1);
            }
        }
        return task;
    }

    public static NativeBattleDuel274Frame StoreRanges(NativeBattleDuel274Frame frame, uint address, NativeBattleModulePoolTask task, IReadOnlyList<(int Offset, int Length)> writes)
    {
        if (task.PayloadLength != Length)
        {
            throw new InvalidDataException("Native Duel echo write changed its actual physical extent.");
        }
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Duel echo requires its physical arena owner.");
        int start = NativeBattleWaterExecutionFields.ArenaOffset(address, Length);
        foreach ((int offset, int length) in writes)
        {
            if (offset < 0 || length < 0 || offset > Length - length)
            {
                throw new InvalidDataException("Native Duel echo write leaves its actual arena extent.");
            }
            arena = arena.Write(checked(start + offset), task.ReadRawPayloadSpan().Slice(offset, length));
            if (task.UnknownPayloadRanges is { } unknown)
            {
                foreach (NativeBattleEffectUnknownRegion range in unknown)
                {
                    int first = Math.Max(offset, range.Offset - 12);
                    int end = Math.Min(offset + length, range.Offset - 12 + range.Length);
                    if (end > first)
                    {
                        arena = arena.MarkUnknown(checked(start + first), end - first);
                    }
                }
            }
        }
        return frame with { Memory = frame.Memory with { Arena = arena } };
    }

    public static NativeBattleDuel274Frame Initialize6A1230(NativeBattleDuel274Frame frame, uint address, ImmutableArray<byte> bones)
    {
        NativeBattleModulePoolTask task = NativeBattleDuelEcho.Initialize6A1230(Read(frame, address), 12, bones);
        return StoreRanges(frame, address, task, [(0, 133)]);
    }

    public static NativeBattleDuel274Frame Capture68ADF0(NativeBattleDuel274Frame frame, uint address, int boneCount, Func<int, (byte[] Data, bool[] Known)> readCurrentMatrix32)
    {
        if ((uint)boneCount > 128)
        {
            throw new InvalidDataException("Native Duel echo requires its actual skeleton count.");
        }
        NativeBattleModulePoolTask task = Read(frame, address);
        int bank = 136 + (task.ReadByte(14) << 9);
        List<(int Offset, int Length)> writes = [(0, 4)];
        for (int bone = 0; bone < boneCount; bone++)
        {
            int selected = unchecked((sbyte)task.ReadByte(17 + bone));
            if (selected >= 0)
            {
                writes.Add((checked(bank + selected * 32), 32));
            }
        }
        task = NativeBattleDuelEcho.Capture68ADF0(task, 12, boneCount, readCurrentMatrix32);
        return StoreRanges(frame, address, task, writes);
    }
}
