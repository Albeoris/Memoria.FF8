using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffectTaskMemory
{
    public static NativeBattleEffectTaskPool WriteBankSpan(NativeBattleEffectTaskPool pool, int offset, NativeBattleEffectArena source, int length)
    {
        pool.Validate();
        if (offset < 0 || length < 0 || offset > pool.Stride * pool.Slots.Length - length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }
        int copied = 0;
        while (copied < length)
        {
            int position = offset + copied;
            int slot = position / pool.Stride;
            int field = position % pool.Stride;
            int count = Math.Min(length - copied, (field < 12 ? 12 : pool.Stride) - field);
            NativeBattleModulePoolTask task = pool.Slots[slot];
            if (field < 12)
            {
                byte[] header = new byte[12];
                BinaryPrimitives.WriteUInt16LittleEndian(header, task.Flags);
                BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(2), task.State);
                BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4), task.Next is int next ? checked(pool.BankAddress + (uint)(next * pool.Stride)) : 0);
                BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(8), task.Callback);
                NativeBattleTelemetry.Copy(1149, source.Read(copied, count).AsSpan(), header.AsSpan(field));
                uint physicalNext = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(4));
                int? linked = null;
                if (physicalNext != 0)
                {
                    long relative = (long)physicalNext - pool.BankAddress;
                    if (relative < 0 || relative >= (long)pool.Stride * pool.Slots.Length || relative % pool.Stride != 0)
                    {
                        throw new InvalidDataException("Native task header link leaves its physical bank.");
                    }
                    linked = checked((int)(relative / pool.Stride));
                }
                task = task with { Flags = BinaryPrimitives.ReadUInt16LittleEndian(header), State = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(2)), Next = linked, Callback = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(8)) };
            }
            else
            {
                (byte[] bytes, bool[] known) = source.ReadSnapshot(copied, count);
                task = task.WriteBytes(field, bytes);
                for (int index = 0; index < count; index++)
                {
                    if (!known[index])
                    {
                        task = task.MarkPayloadUnknown(field + index, 1);
                    }
                }
                var pointers = source.Transaction is { } transaction ? transaction.ResourcePointers : source.ResourcePointers;
                if (pointers is not null)
                {
                    foreach ((int pointerOffset, NativeBattleResourcePointer? pointer) in pointers)
                    {
                        if (pointerOffset >= copied && pointerOffset + 4 <= copied + count)
                        {
                            task = task.WriteResourcePointer(field + pointerOffset - copied, pointer ?? throw new NotSupportedException("Native task copies require an observed resource pointer owner."));
                        }
                    }
                }
            }
            pool = pool with { Slots = pool.Slots.SetItem(slot, task) };
            copied += count;
        }
        pool.Validate();
        return pool;
    }
}
