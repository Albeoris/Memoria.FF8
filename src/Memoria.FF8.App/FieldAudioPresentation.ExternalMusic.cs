using Memoria.FF8.Mods;
using System.Diagnostics;

namespace Memoria.FF8;

internal sealed partial class FieldAudioPresentation
{
    private readonly ExternalMusicOutput?[] externalMusic = new ExternalMusicOutput?[3];
    private ExternalMusicResolver? externalResolver;
    private ModCatalogSnapshot? externalCatalog;

    private bool PlayExternalMusic(string nativeFilename, int slot, int track, int volume, bool noIntro)
    {
        ModCatalogSnapshot? snapshot = modCatalog?.Invoke();
        if (snapshot is null)
        {
            return false;
        }
        if (!ReferenceEquals(snapshot, externalCatalog))
        {
            externalResolver = new ExternalMusicResolver(snapshot);
            externalCatalog = snapshot;
        }
        ExternalMusicSelection? selection = externalResolver!.Resolve(nativeFilename, noIntro);
        if (selection is null)
        {
            return false;
        }
        MusicKind kind = Path.GetExtension(nativeFilename).Equals(".sgt", StringComparison.OrdinalIgnoreCase) ? MusicKind.Midi : MusicKind.Stream;
        byte[] encoded = selection.File.ReadBytes();
        PcmAudio pcm = new(PcmAudioDecoder.Decode(toolsDirectory, "pipe:0", encoded, 44100, 2), 44100, 2);
        ExternalMusicLoop loop = ExternalMusicLoop.Read(encoded, pcm);
        int offset = ExternalMusicPcm.OffsetBytes(pcm, selection.OffsetSeconds);
        if (kind == MusicKind.Midi)
        {
            music?.Stop();
        }
        else
        {
            streamedMusic?.Dispose();
            streamedMusic = null;
        }
        if (offset >= loop.EndByte)
        {
            throw new InvalidDataException("External music offset exceeds its loop end.");
        }
        PcmAudioOutput.Buffer buffer = Sound.Create(pcm.Bytes.AsSpan(0, loop.EndByte).ToArray(), pcm.SampleRate, pcm.Channels);
        PcmAudioOutput.Buffer? repeat = null;
        ExternalMusicOutput output;
        try
        {
            buffer.Volume(ExternalGain(kind, volume, selection.Volume));
            buffer.Position(offset);
            if (loop.StartByte > 0)
            {
                repeat = Sound.Create(pcm.Bytes.AsSpan(loop.StartByte, loop.EndByte - loop.StartByte).ToArray(), pcm.SampleRate, pcm.Channels);
                repeat.Volume(ExternalGain(kind, volume, selection.Volume));
            }
            buffer.Play(repeat is null);
            output = new ExternalMusicOutput(buffer, repeat, selection.Volume, (loop.EndByte - offset) / (double)(pcm.SampleRate * pcm.Channels * sizeof(short)), pcm.SampleRate * pcm.Channels * sizeof(short), loop.EndByte - loop.StartByte);
            if (focusPaused)
            {
                output.Pause(true);
            }
        }
        catch
        {
            buffer.Dispose();
            repeat?.Dispose();
            throw;
        }
        externalMusic[(int)kind]?.Dispose();
        externalMusic[(int)kind] = output;
        musicKinds[slot] = kind;
        musicIds[slot] = track;
        if (kind == MusicKind.Midi)
        {
            midiVolume = volume;
            midiRamp = null;
            nativeMidiPaused = false;
        }
        else
        {
            streamVolume = volume;
            streamRamp = null;
            streamedMusicPaused = false;
        }
        Console.WriteLine($"field-music=Playing track={track} slot={slot} package={selection.File.PackageId} file={selection.File.RelativePath} offset={selection.OffsetSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)} loop={loop.StartByte}:{loop.EndByte}");
        return true;
    }

    private int ExternalGain(MusicKind kind, int volume, int percentage)
    {
        int nativeGain = kind == MusicKind.Midi ? Catalog.MusicVolume(volume) : Catalog.SoundVolume(volume);
        return OutputGain(OutputGain(nativeGain, percentage), musicPreference);
    }

    private void UpdateExternalGain(MusicKind kind, int volume)
    {
        if (externalMusic[(int)kind] is ExternalMusicOutput output)
        {
            output.SetGain(ExternalGain(kind, volume, output.Volume));
        }
    }

    private void StopExternalMusic(MusicKind kind)
    {
        if (kind != MusicKind.None)
        {
            externalMusic[(int)kind]?.Dispose();
            externalMusic[(int)kind] = null;
        }
    }

    private void PauseExternalMusic(bool paused)
    {
        foreach (ExternalMusicOutput? output in externalMusic)
        {
            if (output is not null)
            {
                output.Pause(paused);
            }
        }
    }

    private sealed class ExternalMusicOutput(PcmAudioOutput.Buffer primary, PcmAudioOutput.Buffer? loop, int volume, double initialSeconds, int bytesPerSecond, int loopBytes) : IDisposable
    {
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private bool loopStarted;
        private bool paused;
        public int Volume { get; } = volume;

        public void Update()
        {
            if (!paused && loop is not null && !loopStarted && clock.Elapsed.TotalSeconds >= initialSeconds)
            {
                int elapsedBytes = (int)((clock.Elapsed.TotalSeconds - initialSeconds) * bytesPerSecond % loopBytes);
                loop.Position(elapsedBytes / 4 * 4);
                loop.Play(true);
                primary.Stop();
                loopStarted = true;
            }
        }

        public void SetGain(int gain)
        {
            primary.Volume(gain);
            loop?.Volume(gain);
        }

        public void Pause(bool paused)
        {
            this.paused = paused;
            if (paused)
            {
                clock.Stop();
            }
            else
            {
                clock.Start();
            }
            SetBufferPaused(primary, paused);
            if (loop is not null)
            {
                SetBufferPaused(loop, paused);
            }
        }

        public void Dispose()
        {
            primary.Dispose();
            loop?.Dispose();
        }
    }
}

internal static class ExternalMusicPcm
{
    internal static int OffsetBytes(PcmAudio pcm, double offsetSeconds)
    {
        if (!double.IsFinite(offsetSeconds) || offsetSeconds < 0)
        {
            throw new InvalidDataException("External music offset must be finite and nonnegative.");
        }
        double frames = Math.Floor(offsetSeconds * pcm.SampleRate);
        int alignment = checked(pcm.Channels * sizeof(short));
        if (pcm.Bytes.Length == 0 || pcm.Bytes.Length % alignment != 0 || frames >= pcm.Bytes.Length / alignment)
        {
            throw new InvalidDataException("External music offset exceeds the decoded track, or the PCM data is incomplete.");
        }
        return checked((int)frames * alignment);
    }
}
