using System.Buffers.Binary;

namespace Memoria.FF7.Formats;

public sealed record PcWorldFunction(ushort Header, ushort Offset)
{
    public int Kind => Header >> 14;
    public int Event => Kind == 2 ? Header & 15 : Header & 255;
    public int Model => (Header >> 8) & 63;
    public int Mesh => (Header >> 4) & 1023;
}

public sealed record PcWorldEvents(IReadOnlyList<PcWorldFunction> Functions, IReadOnlyList<ushort> Code)
{
    public static PcWorldEvents Read(ReadOnlySpan<byte> source)
    {
        if (source.Length < 0x402 || (source.Length & 1) != 0)
        {
            throw new InvalidDataException("FF7 world events require a dispatch table and complete instruction words.");
        }
        ushort[] code = new ushort[(source.Length - 0x400) / 2];
        for (int i = 0; i < code.Length; i++)
        {
            code[i] = BinaryPrimitives.ReadUInt16LittleEndian(source[(0x400 + i * 2)..]);
        }
        List<PcWorldFunction> functions = [];
        for (int i = 1; i < 256; i++)
        {
            ushort header = BinaryPrimitives.ReadUInt16LittleEndian(source[(i * 4)..]);
            ushort offset = BinaryPrimitives.ReadUInt16LittleEndian(source[(i * 4 + 2)..]);
            if (header == 0xFFFF)
            {
                continue;
            }
            if ((header >> 14) == 3 || offset >= code.Length)
            {
                throw new InvalidDataException("FF7 world dispatch entry has an unsupported kind or invalid code offset.");
            }
            functions.Add(new(header, offset));
        }
        return new(functions.AsReadOnly(), Array.AsReadOnly(code));
    }
}
