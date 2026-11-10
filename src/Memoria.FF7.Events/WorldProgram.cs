using Memoria.FF7.Formats;

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
    }

    public int Find(int header) => functions.GetValueOrDefault(header);
    public int WordCount => code.Length;

    internal ushort Read(int address)
    {
        if ((uint)address >= code.Length)
        {
            throw new InvalidDataException($"FFVII world instruction address {address:X4} is outside the program.");
        }
        return code[address];
    }
}
