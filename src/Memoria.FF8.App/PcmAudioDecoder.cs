using System.Diagnostics;
using Memoria.FF8.Assets;
using Memoria.FF8.Audio;
using Memoria.FF8.Resources;

namespace Memoria.FF8;

internal sealed record PcmAudio(byte[] Bytes, int SampleRate, int Channels);

internal static class PcmAudioDecoder
{
    public static PcmAudio ReadSample(string gameDirectory, int sampleIndex)
    {
        using GameFileResources files = new(gameDirectory);
        return ReadSample(files.Resources, sampleIndex);
    }

    public static PcmAudio ReadSample(ResourceService resources, int sampleIndex)
    {
        GlobalAudioSampleDescriptor sample = GlobalAudioFormat.Read(resources, sampleIndex);
        byte[] compressed = new byte[checked((int)sample.CompressedLength)];
        using (Stream data = resources.Get(new ResourceKey("files/sound/audio.dat")).OpenRead())
        {
            data.Position = sample.CompressedOffset;
            data.ReadExactly(compressed);
        }
        byte[] pcm = PortableWaveDecoder.Decode(sample.WaveFormat, compressed);
        if (sample.LoopStartPcmByte != 0 && sample.LoopEndPcmByte > pcm.Length)
        {
            throw new InvalidDataException($"Sample {sampleIndex} decoded shorter than its original PCM loop range.");
        }
        return new PcmAudio(pcm, checked((int)sample.WaveFormat.SampleRate), sample.WaveFormat.Channels);
    }

    internal static byte[] CreateWave(GlobalAudioWaveFormat format, byte[] data)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        int extra = format.Tag == 2 ? format.ExtraData.Count : 0;
        int formatSize = extra == 0 && format.Tag == 1 ? 16 : 18 + extra;
        writer.Write("RIFF"u8);
        writer.Write(checked(20 + formatSize + data.Length + (data.Length & 1)));
        writer.Write("WAVEfmt "u8);
        writer.Write(formatSize);
        writer.Write(format.Tag);
        writer.Write(format.Channels);
        writer.Write(format.SampleRate);
        writer.Write(format.AverageBytesPerSecond);
        writer.Write(format.BlockAlign);
        writer.Write(format.BitsPerSample);
        if (formatSize != 16)
        {
            writer.Write((ushort)extra);
            writer.Write(format.ExtraData.ToArray());
        }
        writer.Write("data"u8);
        writer.Write(data.Length);
        writer.Write(data);
        if ((data.Length & 1) != 0)
        {
            writer.Write((byte)0);
        }
        return stream.ToArray();
    }

    public static PcmAudio ReadFile(string toolsDirectory, string path)
    {
        return ReadFileAsync(toolsDirectory, path, CancellationToken.None).GetAwaiter().GetResult();
    }

    public static async Task<PcmAudio> ReadFileAsync(string toolsDirectory, string path, CancellationToken cancellationToken)
    {
        byte[] pcm = await DecodeAsync(toolsDirectory, path, null, 44100, 2, cancellationToken).ConfigureAwait(false);
        return new PcmAudio(pcm, 44100, 2);
    }

    internal static PcmAudio ReadWave(byte[] source)
    {
        DecodedWave wave = WaveFileDecoder.Read(source);
        if (wave.SampleRate == 44100 && wave.Channels == 2)
        {
            return new PcmAudio(wave.Pcm, wave.SampleRate, wave.Channels);
        }
        int frames = wave.Pcm.Length / (wave.Channels * 2);
        int outputFrames = checked((int)(((long)frames * 44100 + wave.SampleRate - 1) / wave.SampleRate));
        byte[] output = new byte[checked(outputFrames * 4)];
        var mixer = new PcmMixer();
        using PcmMixer.Voice voice = mixer.Create(wave.Pcm, wave.SampleRate, wave.Channels);
        voice.Play(false);
        short[] block = new short[882];
        for (int offset = 0; offset < outputFrames; offset += block.Length / 2)
        {
            int count = Math.Min(block.Length, (outputFrames - offset) * 2);
            mixer.Render(block.AsSpan(0, count));
            for (int i = 0; i < count; i++)
            {
                System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(output.AsSpan(offset * 4 + i * 2), block[i]);
            }
        }
        return new PcmAudio(output, 44100, 2);
    }

    internal static byte[] Decode(string toolsDirectory, string path, byte[]? input, int rate, int channels)
    {
        return DecodeAsync(toolsDirectory, path, input, rate, channels, CancellationToken.None).GetAwaiter().GetResult();
    }

    internal static Task<byte[]> DecodeAsync(string toolsDirectory, string path, byte[]? input, int rate, int channels, CancellationToken cancellationToken, TimeSpan? timeout = null)
    {
        var start = new ProcessStartInfo(MediaToolsInstallation.ResolveExecutable(toolsDirectory, "ffmpeg")) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = input is not null, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string argument in new[] { "-nostdin", "-v", "error", "-i", path, "-vn", "-f", "s16le", "-acodec", "pcm_s16le", "-ar", rate.ToString(System.Globalization.CultureInfo.InvariantCulture), "-ac", channels.ToString(System.Globalization.CultureInfo.InvariantCulture), "pipe:1" })
        {
            start.ArgumentList.Add(argument);
        }
        return AudioDecodeProcess.RunAsync(start, input, cancellationToken, timeout);
    }
}
