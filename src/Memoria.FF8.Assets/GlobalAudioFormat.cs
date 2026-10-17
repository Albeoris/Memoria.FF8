using Memoria.FF8.Resources;
using System.Buffers.Binary;

namespace Memoria.FF8.Assets;

public sealed record GlobalAudioWaveFormat(ushort Tag, ushort Channels, uint SampleRate, uint AverageBytesPerSecond, ushort BlockAlign, ushort BitsPerSample, ushort DeclaredExtraSize, IReadOnlyList<byte> ExtraData)
{
    public int? SamplesPerBlock => Tag == 2 && ExtraData.Count >= 2 ? ExtraData[0] | ExtraData[1] << 8 : null;
}

public sealed record GlobalAudioSampleDescriptor(int SampleIndex, long DescriptorOffset, uint CompressedOffset, uint CompressedLength, uint PlaybackFlags, uint LoopStartPcmByte, uint LoopEndPcmByte, GlobalAudioWaveFormat WaveFormat);

/// <summary>Streams original audio.fmt metadata through one selected descriptor; never reads audio.dat payloads.</summary>
public static class GlobalAudioFormat
{
    public static GlobalAudioSampleDescriptor Read(string gameDirectory, int sampleIndex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameDirectory);
        using IResourceSource source = GameDataSourceFactory.Open(gameDirectory);
        return Read(new ResourceService(source), sampleIndex);
    }

    public static GlobalAudioSampleDescriptor Read(ResourceService resources, int sampleIndex)
    {
        using Stream data = resources.Get(new ResourceKey("files/sound/audio.dat")).OpenRead();
        using Stream format = resources.Get(new ResourceKey("files/sound/audio.fmt")).OpenRead();
        return Read(format, data.Length, sampleIndex);
    }

    /// <summary>Reads from the current format-stream position and leaves the stream open. Descriptor offsets are relative to that position.</summary>
    public static GlobalAudioSampleDescriptor Read(Stream format, long dataLength, int sampleIndex)
    {
        ArgumentNullException.ThrowIfNull(format);
        if (!format.CanRead)
        {
            throw new ArgumentException("The audio format stream must be readable.", nameof(format));
        }
        ArgumentOutOfRangeException.ThrowIfNegative(dataLength);
        ArgumentOutOfRangeException.ThrowIfNegative(sampleIndex);
        Span<byte> header = stackalloc byte[2];
        Span<byte> entry = stackalloc byte[38];
        Span<byte> skipped = stackalloc byte[512];
        try
        {
            format.ReadExactly(header);
            int lastSampleIndex = BinaryPrimitives.ReadUInt16LittleEndian(header);
            if (sampleIndex > lastSampleIndex)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleIndex), $"The original audio format contains sample indices 0 through {lastSampleIndex}.");
            }
            long descriptorOffset = 2;
            for (int index = 0; index <= sampleIndex; index++)
            {
                format.ReadExactly(entry);
                ushort tag = BinaryPrimitives.ReadUInt16LittleEndian(entry[20..]);
                ushort declaredExtraSize = BinaryPrimitives.ReadUInt16LittleEndian(entry[36..]);
                // Native 0x46D610 advances over extra bytes only for Microsoft ADPCM format tag 2.
                int extraSize = tag == 2 ? declaredExtraSize : 0;
                if (index != sampleIndex)
                {
                    int remaining = extraSize;
                    while (remaining > 0)
                    {
                        int count = Math.Min(remaining, skipped.Length);
                        format.ReadExactly(skipped[..count]);
                        remaining -= count;
                    }
                    descriptorOffset += entry.Length + extraSize;
                    continue;
                }
                byte[] extra = new byte[extraSize];
                format.ReadExactly(extra);
                uint compressedLength = BinaryPrimitives.ReadUInt32LittleEndian(entry);
                uint compressedOffset = BinaryPrimitives.ReadUInt32LittleEndian(entry[4..]);
                if ((long)compressedOffset + compressedLength > dataLength)
                {
                    throw new InvalidDataException($"Audio sample {sampleIndex} lies outside audio.dat.");
                }
                uint loopStart = BinaryPrimitives.ReadUInt32LittleEndian(entry[12..]);
                uint loopEnd = BinaryPrimitives.ReadUInt32LittleEndian(entry[16..]);
                if (loopStart != 0 && loopEnd <= loopStart)
                {
                    throw new InvalidDataException($"Audio sample {sampleIndex} has an invalid decoded PCM loop range.");
                }
                if (tag == 2)
                {
                    if (extra.Length < 4)
                    {
                        throw new InvalidDataException($"Audio sample {sampleIndex} has incomplete Microsoft ADPCM metadata.");
                    }
                    int samplesPerBlock = BinaryPrimitives.ReadUInt16LittleEndian(extra);
                    int coefficients = BinaryPrimitives.ReadUInt16LittleEndian(extra.AsSpan(2));
                    if (samplesPerBlock == 0 || coefficients == 0 || 4 + coefficients * 4 > extra.Length)
                    {
                        throw new InvalidDataException($"Audio sample {sampleIndex} has invalid Microsoft ADPCM block or coefficient metadata.");
                    }
                }
                GlobalAudioWaveFormat wave = new(tag, BinaryPrimitives.ReadUInt16LittleEndian(entry[22..]), BinaryPrimitives.ReadUInt32LittleEndian(entry[24..]), BinaryPrimitives.ReadUInt32LittleEndian(entry[28..]), BinaryPrimitives.ReadUInt16LittleEndian(entry[32..]), BinaryPrimitives.ReadUInt16LittleEndian(entry[34..]), declaredExtraSize, Array.AsReadOnly(extra));
                return new GlobalAudioSampleDescriptor(sampleIndex, descriptorOffset, compressedOffset, compressedLength, BinaryPrimitives.ReadUInt32LittleEndian(entry[8..]), loopStart, loopEnd, wave);
            }
        }
        catch (EndOfStreamException exception)
        {
            throw new InvalidDataException($"Truncated audio.fmt before selected sample {sampleIndex} was complete.", exception);
        }
        throw new InvalidDataException("No selected audio descriptor was read.");
    }
}
