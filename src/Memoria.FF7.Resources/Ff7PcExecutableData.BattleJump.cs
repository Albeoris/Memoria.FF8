using System.Buffers.Binary;
using System.Reflection.PortableExecutable;

namespace Memoria.FF7.Resources;

public sealed partial class Ff7PcExecutableData
{
    public static IReadOnlyList<short[]> LoadBattleJumpDeltas(Ff7Installation installation)
    {
        const int patternCount = 8;
        const int rowSize = 8 * sizeof(short);
        using FileStream stream = OpenSupportedImage(installation);
        using PEReader pe = new(stream, PEStreamOptions.LeaveOpen);
        byte[] selectors = ReadVirtual(stream, pe, 0x424AE6, patternCount);
        byte[] table = ReadVirtual(stream, pe, 0x7C1178, patternCount * rowSize);
        short[][] patterns = new short[patternCount][];
        for (int pattern = 0; pattern < patterns.Length; pattern++)
        {
            int steps = selectors[pattern] switch
            {
                0 => 5,
                1 => 8,
                _ => throw new InvalidDataException($"FFVII battle jump pattern {pattern} has an invalid step selector.")
            };
            short[] samples = new short[steps];
            for (int sample = 0; sample < samples.Length; sample++)
            {
                samples[sample] = BinaryPrimitives.ReadInt16LittleEndian(table.AsSpan(pattern * rowSize + sample * sizeof(short)));
            }
            patterns[pattern] = samples;
        }
        return Array.AsReadOnly(patterns);
    }
}
