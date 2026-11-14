using System.Buffers.Binary;
using System.Reflection.PortableExecutable;

namespace Memoria.FF7.Resources;

public sealed record Ff7WorldEffectSequence(int FirstSprite, bool GroundAligned);
public sealed record Ff7WorldEffectSprite(int Duration, int Flags, int Width, int Height, int Left, int Top, int Right, int Bottom, int Palette);
public sealed record Ff7WorldEffectTable(IReadOnlyList<Ff7WorldEffectSequence> Sequences, IReadOnlyList<Ff7WorldEffectSprite> Sprites);

public sealed partial class Ff7PcExecutableData
{
    public static Ff7WorldEffectTable LoadWorldEffects(Ff7Installation installation)
    {
        using FileStream stream = OpenSupportedImage(installation);
        using PEReader pe = new(stream, PEStreamOptions.LeaveOpen);
        byte[] sequences = ReadVirtual(stream, pe, 0x96D5B8, 60 * 4);
        byte[] sprites = ReadVirtual(stream, pe, 0x96D6A8, 145 * 12);
        Ff7WorldEffectSequence[] starts = Enumerable.Range(0, 60).Select(index => new Ff7WorldEffectSequence(sequences[index * 4], sequences[index * 4 + 1] != 0)).ToArray();
        Ff7WorldEffectSprite[] frames = new Ff7WorldEffectSprite[145];
        for (int index = 0; index < frames.Length; index++)
        {
            ReadOnlySpan<byte> row = sprites.AsSpan(index * 12, 12);
            frames[index] = new(row[0], row[1], row[2], row[3], row[4], row[5], row[6], row[7], BinaryPrimitives.ReadInt16LittleEndian(row[8..]));
        }
        if (starts.Any(sequence => sequence.FirstSprite >= frames.Length) || frames.Any(frame => frame.Palette < 0 || frame.Palette >= 26))
        {
            throw new InvalidDataException("FFVII world effects have an invalid original sprite or palette reference.");
        }
        return new(Array.AsReadOnly(starts), Array.AsReadOnly(frames));
    }
}
