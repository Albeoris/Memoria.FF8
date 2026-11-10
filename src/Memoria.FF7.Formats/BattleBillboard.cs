namespace Memoria.FF7.Formats;

public readonly record struct BattleBillboardQuad(ushort TexturePage, ushort Flip, short X, short Y, short U, short V, ushort Command, ushort Clut, int Width, int Height);

public static class BattleBillboard
{
    public static IReadOnlyList<IReadOnlyList<BattleBillboardQuad>> Read(ReadOnlySpan<byte> data)
    {
        BinaryCursor source = new(data);
        if (source.U32() != 0x2023)
        {
            throw new InvalidDataException("FFVII battle billboard requires its native 0x2023 header.");
        }
        uint count = source.U32();
        if (count == 0 || count > source.Remaining / 4)
        {
            throw new InvalidDataException("FFVII battle billboard frame count exceeds its native data.");
        }
        IReadOnlyList<BattleBillboardQuad>[] frames = new IReadOnlyList<BattleBillboardQuad>[count];
        for (int frame = 0; frame < frames.Length; frame++)
        {
            source.U16();
            int quads = source.U16();
            if (quads > source.Remaining / 20)
            {
                throw new InvalidDataException("FFVII battle billboard quad count exceeds its native frame data.");
            }
            BattleBillboardQuad[] values = new BattleBillboardQuad[quads];
            for (int index = 0; index < values.Length; index++)
            {
                values[index] = new(source.U16(), source.U16(), source.I16(), source.I16(), source.I16(), source.I16(), source.U16(), source.U16(), source.U16() & 255, source.U16() & 255);
            }
            frames[frame] = Array.AsReadOnly(values);
        }
        if (source.Remaining != 0)
        {
            throw new InvalidDataException("FFVII battle billboard has trailing data beyond its declared frames.");
        }
        return Array.AsReadOnly(frames);
    }
}
