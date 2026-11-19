using System.Buffers.Binary;
using Memoria.FF8.Audio;
using Memoria.FF8.Mods;

namespace Memoria.FF8;

/// <summary>Uses the gameplay decoders to present native music on the regular preview timeline.</summary>
internal static class CatalogNativeMedia
{
    private const int SampleRate = 44100;
    private const int StereoFrameBytes = 4;

    public static ModPreviewMediaInput? WithNativeDecoder(ModPreviewMediaInput? input)
    {
        if (input is null)
        {
            return null;
        }
        string extension = Path.GetExtension(input.Path ?? input.Name);
        if (extension.Equals(".sgt", StringComparison.OrdinalIgnoreCase))
        {
            return input with { WriteWave = (stream, token) => WriteSegmentAsync(input, stream, token) };
        }
        if (extension.Equals(".psf", StringComparison.OrdinalIgnoreCase) || extension.Equals(".minipsf", StringComparison.OrdinalIgnoreCase))
        {
            return input with { WriteWave = (stream, token) => WritePsfAsync(input, stream, token) };
        }
        return input;
    }

    private static Task WriteSegmentAsync(ModPreviewMediaInput input, Stream output, CancellationToken token)
    {
        return Task.Run(() =>
        {
            token.ThrowIfCancellationRequested();
            if (input.InstrumentBank is not { } bank)
            {
                throw new NotSupportedException("The installed DirectMusic instrument bank could not be resolved.");
            }
            byte[] segment = input.Path is { } path ? File.ReadAllBytes(path) : input.Read?.Invoke() ?? throw new InvalidDataException("The DirectMusic segment is missing.");
            MusicSegment music = new(segment);
            SegmentSynthesizer synthesizer = new(new DlsBank(File.ReadAllBytes(bank)), SampleRate);
            // The authored repeats belong to gameplay looping. One full pass also includes any tail after the loop point.
            synthesizer.PlayPreview(music);
            PcmMixer mixer = new(SampleRate);
            mixer.Add(synthesizer);
            short[] samples = new short[4096];
            byte[] bytes = new byte[samples.Length * sizeof(short)];
            double duration = music.SecondsAt(music.Length) - music.SecondsAt(music.PlayStart);
            long frames = checked((long)Math.Ceiling(duration * SampleRate));
            ValidateWaveLength(checked(frames * StereoFrameBytes));
            WriteWaveHeader(output, frames * StereoFrameBytes);
            while (frames > 0)
            {
                token.ThrowIfCancellationRequested();
                int count = (int)Math.Min(frames, samples.Length / 2);
                mixer.Render(samples.AsSpan(0, count * 2));
                for (int index = 0; index < count * 2; index++)
                {
                    BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * 2), samples[index]);
                }
                output.Write(bytes.AsSpan(0, count * StereoFrameBytes));
                frames -= count;
            }
        }, token);
    }

    private static async Task WritePsfAsync(ModPreviewMediaInput input, Stream output, CancellationToken token)
    {
        if (input.ExternalMusic is not { BiosFile: { } bios } track)
        {
            throw new NotSupportedException("PSF preview requires a Junction music selection and its configured BIOS resource.");
        }
        using PsfPcmStream pcm = await PsfDecoder.OpenAsync(track.File.RelativePath, track.File.ReadBytes(),
            (requester, dependency) => track.ResolveDependency(requester, dependency).ReadBytes(),
            bios.ReadBytes(), repeat: false, token).ConfigureAwait(false);

        long start = output.Position;
        WriteWaveHeader(output, 0);
        byte[] block = new byte[16 * 1024];
        long total = 0;
        int count;
        while ((count = await pcm.ReadAsync(block, token).ConfigureAwait(false)) != 0)
        {
            total += count;
            ValidateWaveLength(total);
            await output.WriteAsync(block.AsMemory(0, count), token).ConfigureAwait(false);
        }
        long end = output.Position;
        output.Position = start;
        WriteWaveHeader(output, total);
        output.Position = end;
    }

    private static void ValidateWaveLength(long bytes)
    {
        if (bytes <= 0 || bytes > uint.MaxValue - 36L)
        {
            throw new NotSupportedException("The native track cannot be represented as one PCM WAV stream.");
        }
    }

    private static void WriteWaveHeader(Stream output, long bytes)
    {
        using BinaryWriter writer = new(output, System.Text.Encoding.ASCII, leaveOpen: true);
        writer.Write("RIFF"u8);
        writer.Write(checked((uint)(36 + bytes)));
        writer.Write("WAVEfmt "u8);
        writer.Write(16);
        writer.Write((ushort)1);
        writer.Write((ushort)2);
        writer.Write(SampleRate);
        writer.Write(SampleRate * StereoFrameBytes);
        writer.Write((ushort)StereoFrameBytes);
        writer.Write((ushort)16);
        writer.Write("data"u8);
        writer.Write(checked((uint)bytes));
    }
}
