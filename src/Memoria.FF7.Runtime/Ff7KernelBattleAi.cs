using System.Buffers.Binary;

namespace Memoria.FF7.Runtime;

internal static class Ff7KernelBattleAi
{
    private const int Directory = 0x61C;

    public static ReadOnlyMemory<byte> ReadScript(ReadOnlyMemory<byte> kernel, int identity, byte callback)
    {
        if (kernel.Length != 3988 || identity is < 0 or > 10 || callback >= 16)
        {
            throw new InvalidDataException("FFVII party AI requires its original battle kernel and logical callback owner.");
        }
        ushort offset = BinaryPrimitives.ReadUInt16LittleEndian(kernel.Span[(Directory + identity * 2)..]);
        if (offset == ushort.MaxValue)
        {
            return default;
        }
        int bank = Directory + offset;
        int end = kernel.Length;
        for (int id = 0; id <= 10; id++)
        {
            int candidate = Directory + BinaryPrimitives.ReadUInt16LittleEndian(kernel.Span[(Directory + id * 2)..]);
            if (candidate > bank && candidate < end)
            {
                end = candidate;
            }
        }
        if (bank < Directory + 22 || bank + 32 > end)
        {
            throw new InvalidDataException("FFVII party AI bank directory is invalid.");
        }
        ushort script = BinaryPrimitives.ReadUInt16LittleEndian(kernel.Span[(bank + callback * 2)..]);
        if (script == ushort.MaxValue)
        {
            return default;
        }
        int start = bank + script;
        for (int index = 0; index < 16; index++)
        {
            ushort other = BinaryPrimitives.ReadUInt16LittleEndian(kernel.Span[(bank + index * 2)..]);
            if (other != ushort.MaxValue && bank + other > start && bank + other < end)
            {
                end = bank + other;
            }
        }
        if (script < 32 || start >= end)
        {
            throw new InvalidDataException("FFVII party AI callback exceeds its bank.");
        }
        return kernel[start..end];
    }
}
