using Memoria.FF7.Formats;
using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Memoria.FF7.Events;

public readonly record struct WorldInstruction(int Address, ushort Opcode, ushort Operand, int WordLength)
{
    public int NextProgramCounter => Address + WordLength;
}

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

    public WorldInstruction ReadInstruction(int address)
    {
        ushort opcode = Read(address);
        bool operand = opcode is >= 0x110 and <= 0x11F or 0x200 or 0x201;
        return new(address, opcode, operand ? Read(address + 1) : (ushort)0, operand ? 2 : 1);
    }

    public static int MeshDispatchHeader(int chunkX, int chunkZ, int eventId)
    {
        if (chunkX is < 0 or >= 36 || chunkZ is < 0 or >= 28 || eventId is < 0 or > 15)
        {
            throw new ArgumentOutOfRangeException(nameof(eventId));
        }
        return 0x8000 | ((chunkZ * 36 + chunkX) << 4 & 0x3FF0) | eventId;
    }

    internal ushort Read(int address)
    {
        if ((uint)address >= code.Length)
        {
            throw new InvalidDataException($"FFVII world instruction address {address:X4} is outside the program.");
        }
        return code[address];
    }
}
