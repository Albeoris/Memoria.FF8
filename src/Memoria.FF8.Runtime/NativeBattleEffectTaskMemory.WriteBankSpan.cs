using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffectTaskMemory
{
    private readonly record struct TaskHeader(ushort Flags, ushort State, int? Next, uint Callback);

    public static NativeBattleEffectTaskPool WriteBankSpan(NativeBattleEffectTaskPool pool, int offset, NativeBattleEffectArena source, int length)
    {
        NativeBattleTaskBankOwner owner = pool.RequireOwner();
        if (offset < 0 || length < 0 || offset > owner.BankLength - length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }
        //The native copy observes source bytes/knownness before any overlapping destination stores.
        (byte[] bytes, bool[] known) = source.ReadSnapshot(0, length);
        KeyValuePair<int, NativeBattleResourcePointer?>[] sourcePointers = source.EnumerateResourcePointers().ToArray();
        KeyValuePair<int, BattleExecutionReferenceEntry>[] sourceExecution = source.EnumerateExecutionReferences().ToArray();
        return WriteBankSpan(pool, owner, offset, bytes, known, sourcePointers, sourceExecution);
    }

    internal static NativeBattleEffectTaskPool WriteBankSpan(NativeBattleEffectTaskPool pool, int offset, NativeBattleEffect314WriteSourceSnapshot source)
    {
        NativeBattleTaskBankOwner owner = pool.RequireOwner();
        if (offset < 0 || offset > owner.BankLength - source.Data.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }
        return WriteBankSpan(pool, owner, offset, source.Data, source.Known, source.Pointers.ToArray(), []);
    }

    private static NativeBattleEffectTaskPool WriteBankSpan(NativeBattleEffectTaskPool pool, NativeBattleTaskBankOwner owner, int offset, byte[] bytes, bool[] known, KeyValuePair<int, NativeBattleResourcePointer?>[] sourcePointers, KeyValuePair<int, BattleExecutionReferenceEntry>[] sourceExecution)
    {
        int length = bytes.Length;
        List<(int Offset, BattleExecutionReferenceEntry Entry, ImmutableArray<byte>? Encoding)> execution = [];
        Dictionary<int, TaskHeader> headers = [];
        List<(int Offset, NativeBattleResourcePointer Pointer)> pointers = [];
        int copied = 0;
        while (copied < length)
        {
            int position = offset + copied;
            int slot = position / pool.Stride;
            int field = position % pool.Stride;
            int count = Math.Min(length - copied, (field < 12 ? 12 : pool.Stride) - field);
            if (field < 12)
            {
                for (int index = copied; index < copied + count; index++)
                {
                    if (!known[index])
                    {
                        throw new NotSupportedException("Native task headers require their observed physical bytes.");
                    }
                }
                byte[] header = new byte[12];
                owner.Body.ReadInto(owner.StorageOffset + slot * pool.Stride, header);
                bytes.AsSpan(copied, count).CopyTo(header.AsSpan(field));
                uint physicalNext = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(4));
                int? linked = null;
                if (physicalNext != 0)
                {
                    long relative = (long)physicalNext - pool.BankAddress;
                    if (relative < 0 || relative >= owner.BankLength || relative % pool.Stride != 0)
                    {
                        throw new InvalidDataException("Native task header link leaves its physical bank.");
                    }
                    linked = checked((int)(relative / pool.Stride));
                }
                TaskHeader candidate = new(BinaryPrimitives.ReadUInt16LittleEndian(header), BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(2)), linked, BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(8)));
                if ((candidate.Flags & 1) != 0 && candidate.Callback == 0)
                {
                    throw new InvalidDataException("Native task active header has no callback.");
                }
                headers.Add(slot, candidate);
            }
            else
            {
                foreach ((int referenceOffset, BattleExecutionReferenceEntry entry) in sourceExecution)
                {
                    if (referenceOffset >= copied && referenceOffset <= copied + count - entry.Width)
                    {
                        execution.Add((offset + referenceOffset, entry, ExecutionReferenceEncoding.Capture(bytes.AsSpan(referenceOffset, entry.Width), known.AsSpan(referenceOffset, entry.Width))));
                    }
                }
                foreach ((int pointerOffset, NativeBattleResourcePointer? pointer) in sourcePointers)
                {
                    if (pointerOffset >= copied && pointerOffset + 4 <= copied + count)
                    {
                        if (pointer is null || !pointer.HasValidOwner())
                        {
                            throw new NotSupportedException("Native task copies require an observed resource pointer owner.");
                        }
                        uint? numeric = pointer.NativeByteValue;
                        for (int lane = 0; lane < 4; lane++)
                        {
                            if (known[pointerOffset + lane] != numeric.HasValue)
                            {
                                throw new InvalidDataException("Native copied task pointer knownness differs from its physical provenance.");
                            }
                        }
                        if (numeric is uint address && BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(pointerOffset, 4)) != address)
                        {
                            throw new InvalidDataException("Native copied task pointer differs from its physical DWORD.");
                        }
                        pointers.Add((offset + pointerOffset, pointer));
                    }
                }
            }
            copied += count;
        }
        //Validate the prospective linked descriptor before publishing any source segment.
        HashSet<int> seen = [];
        int? last = null;
        for (int? next = pool.Head; next is int index;)
        {
            if (!seen.Add(index))
            {
                throw new InvalidDataException("Native copied task links form a cycle.");
            }
            NativeBattleModulePoolTask task = pool.Slots[index];
            TaskHeader header = headers.GetValueOrDefault(index, new(task.Flags, task.State, task.Next, task.Callback));
            if (header.Callback == 0)
            {
                throw new InvalidDataException("Native copied task queue has an absent callback.");
            }
            last = index;
            next = header.Next;
        }
        if (last != pool.Tail)
        {
            throw new InvalidDataException("Native copied task descriptor tail contradicts its linked bank.");
        }
        if (owner.HasCheckpoint)
        {
            int stores = checked(pointers.Count + execution.Count);
            owner.Storage.ReserveCheckpointCapacity(checked(length + 4 * stores), checked(owner.Storage.ResourcePointerCount + owner.Storage.ExecutionReferenceCount + 2 * stores));
        }
        owner.Body.WriteWithKnownness(owner.StorageOffset + offset, bytes, known);
        foreach ((int target, NativeBattleResourcePointer pointer) in pointers)
        {
            owner.Body.SetResourcePointer(owner.StorageOffset + target, pointer);
        }
        foreach (var write in execution)
        {
            owner.Body.WriteExecutionReference(owner.StorageOffset + write.Offset, write.Entry.Width, write.Entry.Reference, write.Encoding);
        }
        return pool;
    }
}
