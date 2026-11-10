using Memoria.FF7.Formats;
using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Memoria.FF7.Events;

public sealed class WorldProgram
{
    private readonly ushort[] code;
    private readonly Dictionary<int, int> functions;

    public WorldProgram(PcWorldEvents events)
    {
        ArgumentNullException.ThrowIfNull(events);
        code = events.Code.ToArray();
        functions = new();
        foreach (PcWorldFunction function in events.Functions)
        {
            if (!functions.TryAdd(function.Header, function.Offset) || function.Offset == 0 || function.Offset >= code.Length)
            {
                throw new InvalidDataException("FFVII world dispatch contains duplicate or invalid functions.");
            }
        }
        byte[] identity = new byte[checked(code.Length * 2 + functions.Count * 4)];
        int cursor = 0;
        foreach ((int header, int offset) in functions.OrderBy(value => value.Key))
        {
            BinaryPrimitives.WriteUInt16LittleEndian(identity.AsSpan(cursor), checked((ushort)header));
            BinaryPrimitives.WriteUInt16LittleEndian(identity.AsSpan(cursor + 2), checked((ushort)offset));
            cursor += 4;
        }
        foreach (ushort word in code)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(identity.AsSpan(cursor), word);
            cursor += 2;
        }
        Identity = Convert.ToHexString(SHA256.HashData(identity));
    }

    public int Find(int header) => functions.GetValueOrDefault(header);
    public int WordCount => code.Length;
    public string Identity { get; }

    internal ushort Read(int address)
    {
        if ((uint)address >= code.Length)
        {
            throw new InvalidDataException($"FFVII world instruction address {address:X4} is outside the program.");
        }
        return code[address];
    }
}
