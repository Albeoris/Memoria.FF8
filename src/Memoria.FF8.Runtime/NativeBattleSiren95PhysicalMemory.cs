using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

internal static class NativeBattleSiren95PhysicalMemory
{
    internal static byte[] Read(NativeBattleSiren95Frame frame, uint address, int length, Func<uint, int, byte[]>? readGlobals = null)
    {
        if (length < 0 || (ulong)address + (uint)length > uint.MaxValue + 1UL)
        {
            throw new InvalidDataException("Native Siren physical read exceeds its addressed process span.");
        }
        byte[] result = new byte[length];
        Span<byte> header = stackalloc byte[12];
        Span<byte> payloadByte = stackalloc byte[1];
        Span<bool> payloadKnown = stackalloc bool[1];
        for (int index = 0; index < length; index++)
        {
            uint pointer = unchecked(address + (uint)index);
            bool taskByte = false;
            foreach (NativeBattleEffectTaskPool pool in frame.Effect.Pools)
            {
                ulong end = (ulong)pool.BankAddress + (ulong)(uint)pool.Stride * (uint)pool.Slots.Length;
                if (pointer >= pool.BankAddress && pointer < end)
                {
                    int relative = checked((int)(pointer - pool.BankAddress));
                    int slot = relative / pool.Stride;
                    int field = relative % pool.Stride;
                    NativeBattleModulePoolTask task = pool.Slots[slot];
                    if (field >= 12)
                    {
                        task.ReadWithKnownness(field, payloadByte, payloadKnown);
                        result[index] = payloadByte[0];
                    }
                    else
                    {
                        BinaryPrimitives.WriteUInt16LittleEndian(header, task.Flags);
                        BinaryPrimitives.WriteUInt16LittleEndian(header[2..], task.State);
                        BinaryPrimitives.WriteUInt32LittleEndian(header[4..], task.Next is int next ? unchecked(pool.BankAddress + (uint)(next * pool.Stride)) : 0);
                        BinaryPrimitives.WriteUInt32LittleEndian(header[8..], task.Callback);
                        result[index] = header[field];
                    }
                    taskByte = true;
                    break;
                }
                if (pointer >= pool.DescriptorAddress && (ulong)pointer < (ulong)pool.DescriptorAddress + 16)
                {
                    throw new NotSupportedException("Native Siren descriptor bytes require their actual descriptor field publisher.");
                }
            }
            if (!taskByte)
            {
                result[index] = readGlobals is not null ? readGlobals(pointer, 1)[0] : (frame.Memory.Siren95 ?? throw new NotSupportedException("Native Siren physical read requires its retained module owner.")).Read(pointer, 1)[0];
            }
        }
        return result;
    }
}
