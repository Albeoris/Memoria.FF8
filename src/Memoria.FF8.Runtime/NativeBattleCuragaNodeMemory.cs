using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>One callback's byte-faithful view of the two original cleared auxiliary ranges; unknown bytes stay unknown until a native store.</summary>
internal sealed class NativeBattleCuragaNodeMemory
{
    internal const uint ArenaAddress = 0x20DFAB8;
    internal const int ContextOffset = 0x34;
    private readonly NativeBattleCuragaFrame frame;
    private readonly uint taskAddress;
    private readonly NativeBattleModulePoolTask originalTask;
    private readonly int first;
    private readonly byte[] bytes;
    private readonly bool[] unknown;
    private readonly byte[] taskBytes;
    private bool dirty;

    internal NativeBattleCuragaState State { get; set; }

    internal NativeBattleCuragaNodeMemory(NativeBattleCuragaFrame frame, uint taskAddress)
    {
        this.frame = frame;
        this.taskAddress = taskAddress;
        State = frame.State;
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        originalTask = hierarchy.Read(taskAddress);
        if (originalTask.Callback != 0x87E1A0 || originalTask.Payload.Length != 676 - 12)
        {
            throw new InvalidDataException("Native Curaga node context requires its original676-byte task.");
        }
        taskBytes = originalTask.Payload.ToArray();
        first = State.Auxiliary25F8 ?? throw new NotSupportedException("Native Curaga emitter storage requires the executed87DE10 allocation/clear.");
        int second = State.Auxiliary16F80 ?? throw new NotSupportedException("Native Curaga geometry storage requires the executed87DE10 allocation/clear.");
        if (second != first + 0x25F8 || first < 0 || second > NativeBattleEffectArena.Length - 0x16F80)
        {
            throw new InvalidDataException("Native Curaga auxiliary ranges contradict87DDC0's actual contiguous allocations.");
        }
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Curaga nodes require the571870 arena.");
        arena.Validate();
        bytes = new byte[0x25F8 + 0x16F80];
        unknown = new bool[bytes.Length];
        foreach (NativeBattleEffectMemoryRegion region in arena.Regions)
        {
            int begin = Math.Max(first, region.Offset);
            int end = Math.Min(first + bytes.Length, region.Offset + region.Data.Length);
            if (end > begin)
            {
                region.Data.AsSpan().Slice(begin - region.Offset, end - begin).CopyTo(bytes.AsSpan(begin - first));
            }
        }
        foreach (NativeBattleEffectUnknownRegion region in arena.UnknownRegions)
        {
            int begin = Math.Max(first, region.Offset);
            int end = Math.Min(first + bytes.Length, region.Offset + region.Length);
            if (end > begin)
            {
                Array.Fill(unknown, true, begin - first, end - begin);
            }
        }
    }

    internal byte Byte(int offset) => Read(offset, 1)[0];
    internal ushort Word(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(Read(offset, 2));
    internal uint Dword(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(Read(offset, 4));
    internal ushort ContextWord(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(taskBytes.AsSpan(ContextOffset + offset - 12));
    internal uint ContextDword(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(taskBytes.AsSpan(ContextOffset + offset - 12));
    internal void ContextWord(int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(taskBytes.AsSpan(ContextOffset + offset - 12), value);
    internal void ContextDword(int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(taskBytes.AsSpan(ContextOffset + offset - 12), value);

    internal void Byte(int offset, byte value) => Write(offset, 1)[0] = value;
    internal void Word(int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(Write(offset, 2), value);
    internal void Dword(int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(Write(offset, 4), value);
    internal void Clear(int offset, int count) => Write(offset, count).Clear();

    internal uint Pointer(int offset)
    {
        Require(offset, 1);
        return checked(ArenaAddress + (uint)offset);
    }

    internal int Offset(uint pointer)
    {
        if (pointer < ArenaAddress)
        {
            throw new InvalidDataException("Native Curaga node link is not an effect-arena pointer.");
        }
        int offset = checked((int)(pointer - ArenaAddress));
        Require(offset, 1);
        return offset;
    }

    internal void Link882930(int node, ushort kind)
    {
        Word(node + 8, kind);
        uint pointer = Pointer(node);
        if (ContextDword(0x2C) == 0)
        {
            ContextDword(0x2C, pointer);
            ContextDword(0x30, pointer);
        }
        else
        {
            uint previous = ContextDword(0x30);
            Dword(node, previous);
            Dword(Offset(previous) + 4, pointer);
            ContextDword(0x30, pointer);
        }
    }

    internal void Unlink882960(int node)
    {
        uint previous = Dword(node);
        uint next = Dword(node + 4);
        if (previous == 0)
        {
            ContextDword(0x2C, next);
        }
        else
        {
            Dword(Offset(previous) + 4, next);
        }
        if (next == 0)
        {
            ContextDword(0x30, previous);
        }
        else
        {
            Dword(Offset(next), previous);
        }
    }

    internal NativeBattleCuragaFrame Flush()
    {
        NativeBattleEffectArena arena = frame.Memory.Arena!;
        if (dirty)
        {
            //One committed range preserves untouched values and their original unknown masks, avoiding a full sparse-array clone per DWORD store.
            arena = arena.Write(first, bytes);
            for (int index = 0; index < unknown.Length;)
            {
                if (!unknown[index])
                {
                    index++;
                    continue;
                }
                int end = index + 1;
                while (end < unknown.Length && unknown[end])
                {
                    end++;
                }
                arena = arena.MarkUnknown(first + index, end - index);
                index = end;
            }
        }
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        hierarchy = hierarchy.Write(taskAddress, originalTask with { Payload = taskBytes.ToImmutableArray() });
        return frame with { Memory = frame.Memory with { Arena = arena }, Effect = frame.Effect with { Pools = hierarchy.Pools }, State = State };
    }

    private ReadOnlySpan<byte> Read(int offset, int count)
    {
        Require(offset, count);
        int index = offset - first;
        if (unknown.AsSpan(index, count).Contains(true))
        {
            throw new NotSupportedException("Native Curaga node read requires its actual prior bytes at the selected pointer.");
        }
        return bytes.AsSpan(index, count);
    }

    private Span<byte> Write(int offset, int count)
    {
        Require(offset, count);
        int index = offset - first;
        unknown.AsSpan(index, count).Clear();
        dirty = true;
        return bytes.AsSpan(index, count);
    }

    private void Require(int offset, int count)
    {
        if (count < 0 || offset < first || offset - first > bytes.Length - count)
        {
            throw new InvalidDataException("Native Curaga node access leaves its two original auxiliary ranges.");
        }
    }
}
