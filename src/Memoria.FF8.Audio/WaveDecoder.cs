using System.Buffers.Binary;

namespace Memoria.FF8.Audio;

/// <summary>Portable PCM and Microsoft ADPCM decoder. Each block owns its predictor history.</summary>
public static class WaveDecoder
{
    private static readonly int[] Adaptation = [230, 230, 230, 230, 307, 409, 512, 614, 768, 614, 512, 409, 307, 230, 230, 230];

    public static byte[] Decode(ushort tag, int channels, int bits, int blockAlign, ReadOnlySpan<byte> extra, ReadOnlySpan<byte> input)
    {
        if (channels is not (1 or 2))
        {
            throw new NotSupportedException("Audio requires mono or stereo samples.");
        }
        if (tag == 1 && bits == 16)
        {
            if (input.Length % (channels * 2) != 0)
            {
                throw new InvalidDataException("PCM16 data is not frame aligned.");
            }
            return input.ToArray();
        }
        if (tag == 1 && bits == 8)
        {
            if (input.Length % channels != 0)
            {
                throw new InvalidDataException("PCM8 data is not frame aligned.");
            }
            byte[] pcm = new byte[checked(input.Length * 2)];
            for (int i = 0; i < input.Length; i++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(pcm.AsSpan(i * 2), (short)((input[i] - 128) << 8));
            }
            return pcm;
        }
        if (tag != 2 || bits != 4)
        {
            throw new NotSupportedException($"Unsupported wave encoding {tag}, {bits} bits.");
        }
        if (extra.Length < 4 || blockAlign < channels * 7)
        {
            throw new InvalidDataException("Incomplete ADPCM format.");
        }
        int samplesPerBlock = BinaryPrimitives.ReadUInt16LittleEndian(extra);
        int coefficientCount = BinaryPrimitives.ReadUInt16LittleEndian(extra[2..]);
        if (samplesPerBlock < 2 || coefficientCount == 0 || extra.Length < 4 + coefficientCount * 4)
        {
            throw new InvalidDataException("Invalid ADPCM sample or coefficient count.");
        }
        using var output = new MemoryStream();
        using var writer = new BinaryWriter(output);
        for (int offset = 0; offset < input.Length; offset += blockAlign)
        {
            ReadOnlySpan<byte> block = input.Slice(offset, Math.Min(blockAlign, input.Length - offset));
            if (block.Length < channels * 7)
            {
                throw new InvalidDataException("Truncated ADPCM block header.");
            }
            int[] delta = new int[channels];
            int[] first = new int[channels];
            int[] second = new int[channels];
            int[] coefficient1 = new int[channels];
            int[] coefficient2 = new int[channels];
            for (int c = 0; c < channels; c++)
            {
                int predictor = block[c];
                if (predictor >= coefficientCount)
                {
                    throw new InvalidDataException("ADPCM predictor exceeds the coefficient table.");
                }
                coefficient1[c] = BinaryPrimitives.ReadInt16LittleEndian(extra[(4 + predictor * 4)..]);
                coefficient2[c] = BinaryPrimitives.ReadInt16LittleEndian(extra[(6 + predictor * 4)..]);
                delta[c] = BinaryPrimitives.ReadUInt16LittleEndian(block[(channels + c * 2)..]);
                first[c] = BinaryPrimitives.ReadInt16LittleEndian(block[(channels * 3 + c * 2)..]);
                second[c] = BinaryPrimitives.ReadInt16LittleEndian(block[(channels * 5 + c * 2)..]);
                if (delta[c] < 16)
                {
                    delta[c] = 16;
                }
            }
            for (int c = 0; c < channels; c++)
            {
                writer.Write((short)second[c]);
            }
            for (int c = 0; c < channels; c++)
            {
                writer.Write((short)first[c]);
            }
            int remaining = checked((samplesPerBlock - 2) * channels);
            for (int i = channels * 7; i < block.Length && remaining > 0; i++)
            {
                DecodeNibble(block[i] >> 4, channels == 2 ? 0 : (samplesPerBlock * channels - remaining) % channels);
                if (remaining > 0)
                {
                    DecodeNibble(block[i] & 15, channels == 2 ? 1 : 0);
                }
            }

            void DecodeNibble(int nibble, int channel)
            {
                int signed = nibble >= 8 ? nibble - 16 : nibble;
                int prediction = (first[channel] * coefficient1[channel] + second[channel] * coefficient2[channel]) >> 8;
                int sample = Math.Clamp(prediction + signed * delta[channel], short.MinValue, short.MaxValue);
                second[channel] = first[channel];
                first[channel] = sample;
                delta[channel] = Math.Max(16, Adaptation[nibble] * delta[channel] / 256);
                writer.Write((short)sample);
                remaining--;
            }
        }
        return output.ToArray();
    }
}
