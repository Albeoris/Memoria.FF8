using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffectPoolBytes
{
    public static bool EqualDword(NativeBattleEffectTaskPool pool, uint address, uint expected)
    {
        bool unresolved = false;
        for (int index = 0; index < 4; index++)
        {
            uint selected = checked(address + (uint)index);
            if (selected >= pool.DescriptorAddress && selected - pool.DescriptorAddress < 16)
            {
                if (Descriptor(pool)[selected - pool.DescriptorAddress] != unchecked((byte)(expected >> (index * 8))))
                {
                    return false;
                }
                continue;
            }
            (int slot, int offset) = Locate(pool, selected);
            NativeBattleModulePoolTask task = pool.Slots[slot];
            if (offset >= 12)
            {
                task.RequirePayloadRange(offset, 1);
                task.ValidateUnknownPayloadRanges();
                if (task.ReadUnknownPayloadRanges() is { } ranges && ranges.Any(range => offset >= range.Offset && offset - range.Offset < range.Length))
                {
                    unresolved = true;
                    continue;
                }
            }
            byte actual = offset < 12 ? Header(pool, task)[offset] : task.ReadByte(offset);
            if (actual != unchecked((byte)(expected >> (index * 8))))
            {
                return false;
            }
        }
        if (unresolved)
        {
            throw new NotSupportedException("Native task DWORD equality is unresolved by its retained known bytes.");
        }
        return true;
    }

    public static bool Contains(NativeBattleEffectTaskPool pool, uint address) => address >= pool.DescriptorAddress && address - pool.DescriptorAddress < 16 || address >= pool.BankAddress && (ulong)address - pool.BankAddress < (ulong)pool.Stride * (uint)pool.Slots.Length;

    public static byte[] Read(NativeBattleEffectTaskPool pool, uint address, int length)
    {
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }
        byte[] result = new byte[length];
        for (int index = 0; index < length; index++)
        {
            uint selected = checked(address + (uint)index);
            if (selected >= pool.DescriptorAddress && selected - pool.DescriptorAddress < 16)
            {
                result[index] = Descriptor(pool)[selected - pool.DescriptorAddress];
                continue;
            }
            (int slot, int offset) = Locate(pool, selected);
            result[index] = offset < 12 ? Header(pool, pool.Slots[slot])[offset] : pool.Slots[slot].ReadBytes(offset, 1)[0];
        }
        return result;
    }

    public static NativeBattleEffectTaskPool Write(NativeBattleEffectTaskPool pool, uint address, ReadOnlySpan<byte> bytes)
    {
        int index = 0;
        while (index < bytes.Length)
        {
            uint selected = checked(address + (uint)index);
            if (selected >= pool.DescriptorAddress && selected - pool.DescriptorAddress < 16)
            {
                int offset = checked((int)(selected - pool.DescriptorAddress));
                int count = Math.Min(bytes.Length - index, 16 - offset);
                byte[] descriptor = Descriptor(pool);
                bytes.Slice(index, count).CopyTo(descriptor.AsSpan(offset));
                if (BinaryPrimitives.ReadUInt32LittleEndian(descriptor.AsSpan(8)) != pool.BankAddress || BinaryPrimitives.ReadUInt16LittleEndian(descriptor.AsSpan(12)) != pool.Stride || BinaryPrimitives.ReadUInt16LittleEndian(descriptor.AsSpan(14)) != pool.Slots.Length)
                {
                    throw new NotSupportedException("Native task descriptor mutation changes the physical bank dimensions.");
                }
                pool = pool.UpdateLinks(Index(pool, BinaryPrimitives.ReadUInt32LittleEndian(descriptor)), Index(pool, BinaryPrimitives.ReadUInt32LittleEndian(descriptor.AsSpan(4))));
                index += count;
                continue;
            }
            (int slot, int field) = Locate(pool, selected);
            int length = Math.Min(bytes.Length - index, pool.Stride - field);
            NativeBattleModulePoolTask task = pool.Slots[slot];
            if (field < 12)
            {
                length = Math.Min(length, 12 - field);
                byte[] header = Header(pool, task);
                bytes.Slice(index, length).CopyTo(header.AsSpan(field));
                task = task.UpdateHeader(BinaryPrimitives.ReadUInt16LittleEndian(header), BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(2)), Index(pool, BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(4))), BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(8)));
            }
            else
            {
                task = task.WriteBytes(field, bytes.Slice(index, length));
            }
            pool = pool.WriteSlot(slot, task);
            index += length;
        }
        return pool;
    }

    private static byte[] Header(NativeBattleEffectTaskPool pool, NativeBattleModulePoolTask task)
    {
        byte[] bytes = new byte[12];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, task.Flags);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), task.State);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), Pointer(pool, task.Next));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), task.Callback);
        return bytes;
    }

    private static byte[] Descriptor(NativeBattleEffectTaskPool pool)
    {
        byte[] bytes = new byte[16];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, Pointer(pool, pool.Head));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), Pointer(pool, pool.Tail));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), pool.BankAddress);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), pool.Stride);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(14), checked((ushort)pool.Slots.Length));
        return bytes;
    }

    private static uint Pointer(NativeBattleEffectTaskPool pool, int? slot) => slot is int index ? checked(pool.BankAddress + (uint)(index * pool.Stride)) : 0;

    private static int? Index(NativeBattleEffectTaskPool pool, uint address)
    {
        if (address == 0)
        {
            return null;
        }
        uint relative = unchecked(address - pool.BankAddress);
        if (address < pool.BankAddress || relative % pool.Stride != 0 || relative / pool.Stride >= pool.Slots.Length)
        {
            throw new NotSupportedException("Native task link leaves its physical bank.");
        }
        return checked((int)(relative / pool.Stride));
    }

    private static (int Slot, int Field) Locate(NativeBattleEffectTaskPool pool, uint address)
    {
        uint relative = unchecked(address - pool.BankAddress);
        if (address < pool.BankAddress || (ulong)relative >= (ulong)pool.Stride * (uint)pool.Slots.Length)
        {
            throw new NotSupportedException("Native task bytes leave their canonical descriptor and bank.");
        }
        return (checked((int)(relative / pool.Stride)), (int)(relative % pool.Stride));
    }
}
