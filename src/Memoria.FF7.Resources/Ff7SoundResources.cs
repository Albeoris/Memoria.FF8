using System.Buffers.Binary;

namespace Memoria.FF7.Resources;

public sealed record Ff7SoundSample(int SampleIndex, bool Loop, uint LoopStartPcmByte, uint LoopEndPcmByte, byte[] WaveBytes)
{
    public uint CompressedByteLength { get; init; }
    public int NativeSampleRate { get; init; }
}

public static class Ff7SoundResources
{
    public static Ff7SoundSample Read(Ff7Installation installation, int sampleIndex)
    {
        ArgumentNullException.ThrowIfNull(installation);
        using FileStream format = File.OpenRead(installation.GetRequired(Ff7ResourceRole.SoundIndex).Path);
        using FileStream data = File.OpenRead(installation.GetRequired(Ff7ResourceRole.SoundData).Path);
        return Read(format, data, sampleIndex);
    }

    public static Ff7SoundSample Read(Stream format, Stream data, int sampleIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sampleIndex);
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(data);
        if (!format.CanRead || !data.CanRead || !data.CanSeek)
        {
            throw new ArgumentException("FFVII sound input requires readable metadata and seekable sample data.");
        }
        Span<byte> descriptor = stackalloc byte[42];
        try
        {
            for (int index = 0; index <= sampleIndex; index++)
            {
                format.ReadExactly(descriptor);
                ushort tag = BinaryPrimitives.ReadUInt16LittleEndian(descriptor[24..]);
                // The native loader consumes cbSize only for Microsoft ADPCM, including while traversing unused entries.
                int extraSize = tag == 2 ? BinaryPrimitives.ReadUInt16LittleEndian(descriptor[40..]) : 0;
                if (extraSize > 4096)
                {
                    throw new InvalidDataException("FFVII sound WAVEFORMATEX extra data exceeds its bounded descriptor.");
                }
                byte[] extra = new byte[extraSize];
                format.ReadExactly(extra);
                if (index != sampleIndex)
                {
                    continue;
                }
                uint length = BinaryPrimitives.ReadUInt32LittleEndian(descriptor);
                uint offset = BinaryPrimitives.ReadUInt32LittleEndian(descriptor[4..]);
                uint looping = BinaryPrimitives.ReadUInt32LittleEndian(descriptor[8..]);
                uint loopStart = BinaryPrimitives.ReadUInt32LittleEndian(descriptor[16..]);
                uint loopEnd = BinaryPrimitives.ReadUInt32LittleEndian(descriptor[20..]);
                ushort channels = BinaryPrimitives.ReadUInt16LittleEndian(descriptor[26..]);
                uint rate = BinaryPrimitives.ReadUInt32LittleEndian(descriptor[28..]);
                if (length == 0 || length > 64 * 1024 * 1024 || (long)offset + length > data.Length || tag is not (1 or 2) || channels is not (1 or 2) || rate == 0 || rate > int.MaxValue || looping > 1 || looping != 0 && loopEnd <= loopStart)
                {
                    throw new InvalidDataException("FFVII sound descriptor has unsupported encoding or invalid sample/loop bounds.");
                }
                data.Position = offset;
                byte[] compressed = new byte[length];
                data.ReadExactly(compressed);
                using MemoryStream wave = new();
                using BinaryWriter writer = new(wave);
                int formatSize = 18 + extra.Length;
                writer.Write("RIFF"u8);
                writer.Write(checked(20 + formatSize + compressed.Length + (compressed.Length & 1)));
                writer.Write("WAVEfmt "u8);
                writer.Write(formatSize);
                BinaryPrimitives.WriteUInt16LittleEndian(descriptor[40..], checked((ushort)extraSize));
                writer.Write(descriptor[24..]);
                writer.Write(extra);
                writer.Write("data"u8);
                writer.Write(compressed.Length);
                writer.Write(compressed);
                if ((compressed.Length & 1) != 0)
                {
                    writer.Write((byte)0);
                }
                return new Ff7SoundSample(index, looping != 0, loopStart, loopEnd, wave.ToArray()) { CompressedByteLength = length, NativeSampleRate = (int)rate };
            }
        }
        catch (EndOfStreamException exception)
        {
            throw new InvalidDataException("FFVII sound metadata or data is truncated before the selected sample.", exception);
        }
        throw new InvalidDataException("FFVII selected sound sample was not read.");
    }
}
