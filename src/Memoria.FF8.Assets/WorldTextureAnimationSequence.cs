using System.Buffers.Binary;

namespace Memoria.FF8.Assets;

/// <summary>A WMSET animation header and its relative frame table.</summary>
public sealed record WorldTextureAnimationSequence(byte Phase, byte Delay, byte FrameCount, bool PingPong, short DestinationX, short DestinationY, int[] FrameOffsets)
{
    public int SelectFrame(int clock, bool palette = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(clock);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(clock, 384);
        if (palette && FrameCount != 0 && Delay == 0)
        {
            throw new InvalidDataException("World palette animation has a zero frame delay.");
        }
        if (FrameCount == 0 || Delay == 0)
        {
            return 0;
        }
        int period = PingPong ? unchecked((byte)(2 * FrameCount - (palette ? 1 : 2))) : FrameCount;
        int frame = period == 0 ? 0 : (clock + Phase) / Delay % period;
        return frame < FrameCount ? frame : 2 * FrameCount - frame - 2;
    }

    public static WorldTextureAnimationSequence Read(ReadOnlySpan<byte> data, int offset, bool palette = false)
    {
        int headerSize = palette ? 12 : 8;
        ReadOnlySpan<byte> header = data.Slice(offset, headerSize);
        byte count = header[2];
        int table = checked(offset + headerSize);
        int[] frames = new int[count];
        for (int index = 0; index < count; index++)
        {
            frames[index] = checked(table + BinaryPrimitives.ReadInt32LittleEndian(data.Slice(table + index * 4, 4)));
            if (frames[index] < table + count * 4 || frames[index] > data.Length - 20)
            {
                throw new InvalidDataException("World animation frame is outside its resource.");
            }
        }
        return new(header[0], header[1], count, header[3] != 0, BinaryPrimitives.ReadInt16LittleEndian(header[4..]), BinaryPrimitives.ReadInt16LittleEndian(header[6..]), frames);
    }
}
