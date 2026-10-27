using System.Buffers.Binary;

namespace Memoria.FF8.Audio;

public sealed record DecodedWave(byte[] Pcm, int SampleRate, int Channels);

public static class WaveFileDecoder
{
    public static DecodedWave Read(byte[] bytes)
    {
        RiffChunk root = RiffChunk.Read(bytes, "WAVE");
        ReadOnlySpan<byte> format = root.Required("fmt ").Data.Span;
        if (format.Length < 16)
        {
            throw new InvalidDataException("Truncated WAVE format.");
        }
        ushort tag = BinaryPrimitives.ReadUInt16LittleEndian(format);
        int channels = BinaryPrimitives.ReadUInt16LittleEndian(format[2..]);
        int rate = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(format[4..]));
        int align = BinaryPrimitives.ReadUInt16LittleEndian(format[12..]);
        int bits = BinaryPrimitives.ReadUInt16LittleEndian(format[14..]);
        if (rate <= 0)
        {
            throw new InvalidDataException("WAVE sample rate must be positive.");
        }
        if (tag == 1 && bits is 8 or 16 && align != channels * bits / 8)
        {
            throw new InvalidDataException("WAVE PCM block alignment disagrees with its channel and sample width.");
        }
        int extraLength = format.Length >= 18 ? BinaryPrimitives.ReadUInt16LittleEndian(format[16..]) : 0;
        if (extraLength > format.Length - 18 && extraLength != 0)
        {
            throw new InvalidDataException("Truncated WAVE codec metadata.");
        }
        byte[] pcm = WaveDecoder.Decode(tag, channels, bits, align, extraLength == 0 ? [] : format.Slice(18, extraLength), root.Required("data").Data.Span);
        RiffChunk? fact = root.Children.SingleOrDefault(c => c.Id == "fact");
        if (tag != 1 && fact is not null)
        {
            if (fact.Data.Length < 4)
            {
                throw new InvalidDataException("Truncated WAVE sample count.");
            }
            long length = (long)BinaryPrimitives.ReadUInt32LittleEndian(fact.Data.Span) * channels * 2;
            if (length > pcm.Length)
            {
                throw new InvalidDataException("WAVE sample count exceeds decoded data.");
            }
            Array.Resize(ref pcm, checked((int)length));
        }
        if (pcm.Length == 0)
        {
            throw new InvalidDataException("WAVE contains no decoded PCM samples.");
        }
        return new DecodedWave(pcm, rate, channels);
    }
}
