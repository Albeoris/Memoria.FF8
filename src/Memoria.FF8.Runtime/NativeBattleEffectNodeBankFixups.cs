using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Normalized87EA20/88DD40/883EE0: four nullable16-pointer directories, then32 unconditional channels per non-null node.</summary>
internal static class NativeBattleEffectNodeBankFixups
{
    internal static byte[] Relocate(ImmutableArray<byte> source, uint address, int directoryOffset = 0)
    {
        if (source.IsDefault || directoryOffset < 0 || directoryOffset > source.Length - 0x104)
        {
            throw new InvalidDataException("Native node-bank fixup requires all four original directories.");
        }
        byte[] data = source.ToArray();
        for (int table = 0; table < 4; table++)
        {
            for (int index = 0; index < 16; index++)
            {
                int offset = directoryOffset + 4 + table * 64 + index * 4;
                uint pointer = Read(offset);
                if (pointer != 0)
                {
                    Write(offset, unchecked(pointer + address + (uint)directoryOffset));
                }
            }
        }
        for (int index = 0; index < 16; index++)
        {
            uint pointer = Read(directoryOffset + 4 + index * 4);
            if (pointer == 0)
            {
                continue;
            }
            if (pointer < address || pointer - address > data.Length - 0x148)
            {
                throw new InvalidDataException("Native fixed node leaves its original executable resource.");
            }
            int node = checked((int)(pointer - address));
            for (int field = 0xC8; field <= 0x144; field += 4)
            {
                Write(node + field, unchecked(Read(node + field) + address + (uint)directoryOffset));
            }
        }
        return data;

        uint Read(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset));
        void Write(int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset), value);
    }
}
