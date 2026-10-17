using System.Diagnostics;
using Memoria.FF8.Assets;
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
        byte[] pcm = WindowsAcmDecoder.Decode(sample.WaveFormat, compressed);
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

    public static PcmAudio ReadFile(string toolsDirectory, string path) => new(Decode(toolsDirectory, path, null, 44100, 2), 44100, 2);

    internal static byte[] Decode(string toolsDirectory, string path, byte[]? input, int rate, int channels)
    {
        var start = new ProcessStartInfo(Path.Combine(toolsDirectory, "ffmpeg.exe")) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = input is not null, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string argument in new[] { "-v", "error", "-i", path, "-vn", "-f", "s16le", "-acodec", "pcm_s16le", "-ar", rate.ToString(System.Globalization.CultureInfo.InvariantCulture), "-ac", channels.ToString(System.Globalization.CultureInfo.InvariantCulture), "pipe:1" })
        {
            start.ArgumentList.Add(argument);
        }
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("Could not start the PCM decoder.");
        using var output = new MemoryStream();
        Task<string> errors = process.StandardError.ReadToEndAsync();
        Task read = process.StandardOutput.BaseStream.CopyToAsync(output);
        if (input is not null)
        {
            process.StandardInput.BaseStream.Write(input);
            process.StandardInput.Close();
        }
        read.GetAwaiter().GetResult();
        process.WaitForExit();
        string error = errors.GetAwaiter().GetResult();
        if (process.ExitCode != 0)
        {
            throw new InvalidDataException($"Audio decode failed: {error}");
        }
        return output.ToArray();
    }
}
