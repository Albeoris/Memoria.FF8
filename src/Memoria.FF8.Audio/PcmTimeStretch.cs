using System.Buffers.Binary;

namespace Memoria.FF8.Audio;

/// <summary>Offline PCM16 tempo adjustment using waveform similarity overlap-add.</summary>
/// <remarks>
/// Compression removes waveform regions and can omit isolated transients. When the
/// compressed duration fits within one 20 ms hop, only the initial unresampled waveform
/// is retained rather than increasing pitch.
/// </remarks>
public static class PcmTimeStretch
{
    public static byte[] Compress(byte[] pcm, int sampleRate, int channels, double speed)
    {
        ArgumentNullException.ThrowIfNull(pcm);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        if (channels is not (1 or 2))
        {
            throw new ArgumentOutOfRangeException(nameof(channels));
        }
        if (!double.IsFinite(speed) || speed is < 0.5 or > 30)
        {
            throw new ArgumentOutOfRangeException(nameof(speed));
        }
        if (pcm.Length % (channels * 2) != 0)
        {
            throw new ArgumentException("PCM must contain complete PCM16 frames.", nameof(pcm));
        }
        if (speed == 1)
        {
            return (byte[])pcm.Clone();
        }

        int frames = pcm.Length / (channels * 2);
        int outputFrames = checked((int)Math.Floor(frames / speed + 0.5));
        short[] input = new short[frames * channels];
        short[] output = new short[outputFrames * channels];
        for (int i = 0; i < input.Length; i++)
        {
            input[i] = BinaryPrimitives.ReadInt16LittleEndian(pcm.AsSpan(i * 2));
        }

        // Twenty millisecond hops retain several periods of low musical notes.
        // The same source offset and crossfade are always used for both channels.
        int hop = Math.Max(1, sampleRate / 50);
        int window = (int)Math.Min(Math.Min((long)hop * 2, frames), outputFrames);
        hop = Math.Min(hop, Math.Max(1, frames / 2));
        Array.Copy(input, output, window * channels);
        int search = Math.Max(1, sampleRate / 66);
        int stride = Math.Max(1, sampleRate / 11025);
        for (int destination = hop; destination < outputFrames; destination += hop)
        {
            int count = Math.Min(window, outputFrames - destination);
            int overlap = Math.Min(window - hop, count);
            int nominal = (int)Math.Min(destination * speed, frames - count);
            int low = Math.Max(0, nominal - search);
            int high = (int)Math.Min((long)frames - count, (long)nominal + search);
            int best = nominal;
            double bestScore = Correlation(input, output, best, destination, overlap, channels, stride);
            for (int candidate = low; candidate <= high; candidate += stride)
            {
                double score = Correlation(input, output, candidate, destination, overlap, channels, stride);
                if (score > bestScore + 1e-9)
                {
                    best = candidate;
                    bestScore = score;
                }
            }
            int refineLow = Math.Max(low, best - stride);
            int refineHigh = Math.Min(high, best + stride);
            bestScore = Correlation(input, output, best, destination, overlap, channels, 1);
            for (int candidate = refineLow; candidate <= refineHigh; candidate++)
            {
                double score = Correlation(input, output, candidate, destination, overlap, channels, 1);
                if (score > bestScore + 1e-9)
                {
                    best = candidate;
                    bestScore = score;
                }
            }

            for (int i = 0; i < count; i++)
            {
                double weight = i < overlap ? (i + 1d) / (overlap + 1d) : 1;
                for (int channel = 0; channel < channels; channel++)
                {
                    int target = (destination + i) * channels + channel;
                    output[target] = (short)Math.Round(output[target] * (1 - weight) + input[(best + i) * channels + channel] * weight);
                }
            }
        }

        byte[] result = new byte[output.Length * 2];
        for (int i = 0; i < output.Length; i++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(result.AsSpan(i * 2), output[i]);
        }
        return result;
    }

    private static double Correlation(short[] input, short[] output, int source, int destination, int count, int channels, int stride)
    {
        double dot = 0;
        double sourceEnergy = 0;
        double targetEnergy = 0;
        for (int i = 0; i < count; i += stride)
        {
            for (int channel = 0; channel < channels; channel++)
            {
                double a = input[(source + i) * channels + channel];
                double b = output[(destination + i) * channels + channel];
                dot += a * b;
                sourceEnergy += a * a;
                targetEnergy += b * b;
            }
        }
        return sourceEnergy > 0 && targetEnergy > 0 ? dot / Math.Sqrt(sourceEnergy * targetEnergy) : 0;
    }
}
