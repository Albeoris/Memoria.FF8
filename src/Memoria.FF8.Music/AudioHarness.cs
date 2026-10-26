using System.Globalization;
using Memoria.FF8.Audio;

namespace Memoria.FF8;

internal static class AudioHarness
{
    public static int Run(string[] args)
    {
        if (args.Length < 4)
        {
            throw new ArgumentException("Usage: --render bank.dls segment.sgt output.wav [seconds] [startTick], or --play bank.dls segment.sgt seconds.");
        }
        var bank = new DlsBank(File.ReadAllBytes(args[1]));
        var segment = new MusicSegment(File.ReadAllBytes(args[2]));
        int seconds = args[0] == "--play" ? int.Parse(args[3], CultureInfo.InvariantCulture) : args.Length > 4 ? int.Parse(args[4], CultureInfo.InvariantCulture) : 10;
        if (seconds is < 1 or > 3600)
        {
            throw new ArgumentOutOfRangeException(nameof(seconds));
        }
        int start = args.Length > 5 ? int.Parse(args[5], CultureInfo.InvariantCulture) : 0;
        if (args[0] == "--play")
        {
            using var output = new SdlAudioOutput();
            var synth = new SegmentSynthesizer(bank, output.Mixer.SampleRate);
            output.Mixer.Add(synth);
            synth.Play(segment, startPoint: start);
            for (int elapsed = 0; elapsed < seconds * 10 && synth.IsPlaying; elapsed++)
            {
                Thread.Sleep(100);
                output.ThrowIfFailed();
            }
            return 0;
        }
        var renderer = new SegmentSynthesizer(bank);
        renderer.Play(segment, startPoint: start);
        var mixer = new PcmMixer();
        mixer.Add(renderer);
        int frames = checked(seconds * mixer.SampleRate);
        using var file = File.Create(args[3]);
        using var writer = new BinaryWriter(file);
        writer.Write("RIFF"u8);
        writer.Write(checked(36 + frames * 4));
        writer.Write("WAVEfmt "u8);
        writer.Write(16);
        writer.Write((ushort)1);
        writer.Write((ushort)2);
        writer.Write(mixer.SampleRate);
        writer.Write(mixer.SampleRate * 4);
        writer.Write((ushort)4);
        writer.Write((ushort)16);
        writer.Write("data"u8);
        writer.Write(frames * 4);
        short[] block = new short[882];
        double energy = 0;
        int peak = 0;
        for (int offset = 0; offset < frames; offset += block.Length / 2)
        {
            int count = Math.Min(block.Length, (frames - offset) * 2);
            mixer.Render(block.AsSpan(0, count));
            for (int i = 0; i < count; i++)
            {
                writer.Write(block[i]);
                energy += (double)block[i] * block[i];
                peak = Math.Max(peak, Math.Abs((int)block[i]));
            }
        }
        Console.WriteLine($"frames={frames}; peak={peak}; rms={Math.Sqrt(energy / (frames * 2)):F3}; lengthTicks={segment.Length}; loop={segment.LoopStart}:{segment.LoopEnd}; repeats={segment.Repeats}; events={segment.Events.Count}");
        if (peak == 0)
        {
            throw new InvalidDataException("The selected render interval contains no audible samples.");
        }
        return 0;
    }
}
