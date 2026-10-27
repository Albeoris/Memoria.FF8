using Memoria.FF8.Mods;
using Memoria.FF8.Audio;
using System.Diagnostics;

namespace Memoria.FF8;

internal sealed partial class FieldAudioPresentation
{
    private readonly IExternalMusicOutput?[] externalMusic = new IExternalMusicOutput?[3];
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
        nativeMidiPaused = false;
        musicKinds[slot] = kind;
        musicIds[slot] = track;
        if (kind == MusicKind.Midi)
        {
            midiVolume = volume;
            midiRamp = null;
        }
        else
        {
            streamVolume = volume;
            streamRamp = null;
            streamedMusicPaused = false;
        }
        try
        {
            if (Path.GetExtension(selection.File.RelativePath).Equals(".minipsf", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(selection.File.RelativePath).Equals(".psf", StringComparison.OrdinalIgnoreCase))
            {
                QueuePsfMusic(selection, kind, slot, track);
                return true;
            }
            Decoder.Queue(kind, async token =>
            {
                byte[] encoded = selection.File.ReadBytes();
                token.ThrowIfCancellationRequested();
                PcmAudio decoded = await DecodeAsync("pipe:0", encoded, token).ConfigureAwait(false);
                ExternalMusicLoop loop = ExternalMusicLoop.Read(encoded, decoded);
                int offset = ExternalMusicPcm.OffsetBytes(decoded, selection.OffsetSeconds);
                if (offset >= loop.EndByte)
                {
                    throw new InvalidDataException("External music offset exceeds its loop end.");
                }
                PcmAudio primary = new(decoded.Bytes.AsSpan(0, loop.EndByte).ToArray(), decoded.SampleRate, decoded.Channels);
                byte[]? repeat = loop.StartByte > 0 ? decoded.Bytes.AsSpan(loop.StartByte, loop.EndByte - loop.StartByte).ToArray() : null;
                return new DecodedExternalMusic(primary, loop, offset, repeat);
            }, prepared => StartExternalMusic(selection, kind, slot, track, prepared), exception => MusicDecodeFailed(slot, exception));
        }
        catch
        {
            musicKinds[slot] = MusicKind.None;
            musicIds[slot] = -1;
            throw;
        }
        return true;
    }

    private sealed record DecodedExternalMusic(PcmAudio Primary, ExternalMusicLoop Loop, int Offset, byte[]? Repeat);

    private void StartExternalMusic(ExternalMusicSelection selection, MusicKind kind, int slot, int track, DecodedExternalMusic prepared)
    {
        PcmAudio pcm = prepared.Primary;
        ExternalMusicLoop loop = prepared.Loop;
        int offset = prepared.Offset;
        int volume = Math.Clamp((int)(kind == MusicKind.Midi ? midiVolume : streamVolume), 0, 127);
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
        PcmAudioOutput.Buffer buffer = Sound.Create(pcm.Bytes, pcm.SampleRate, pcm.Channels);
        PcmAudioOutput.Buffer? repeat = null;
        ExternalMusicOutput output;
        try
        {
            buffer.Volume(ExternalGain(kind, volume, selection.Volume));
            buffer.Position(offset);
            if (loop.StartByte > 0)
            {
                repeat = Sound.Create(prepared.Repeat!, pcm.SampleRate, pcm.Channels);
                repeat.Volume(ExternalGain(kind, volume, selection.Volume));
            }
            buffer.Play(repeat is null);
            output = new ExternalMusicOutput(buffer, repeat, selection.Volume, (loop.EndByte - offset) / (double)(pcm.SampleRate * pcm.Channels * sizeof(short)), pcm.SampleRate * pcm.Channels * sizeof(short), loop.EndByte - loop.StartByte);
            if (focusPaused || nativeMidiPaused)
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
        Console.WriteLine($"field-music=Playing track={track} slot={slot} package={selection.File.PackageId} file={selection.File.RelativePath} offset={selection.OffsetSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)} loop={loop.StartByte}:{loop.EndByte}");

    }

    private int ExternalGain(MusicKind kind, int volume, int percentage)
    {
        int nativeGain = kind == MusicKind.Midi ? Catalog.MusicVolume(volume) : Catalog.SoundVolume(volume);
        return OutputGain(OutputGain(nativeGain, percentage), musicPreference);
    }

    private void UpdateExternalGain(MusicKind kind, int volume)
    {
        if (externalMusic[(int)kind] is IExternalMusicOutput output)
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
        foreach (IExternalMusicOutput? output in externalMusic)
        {
            if (output is not null)
            {
                output.Pause(paused);
            }
        }
    }

    private interface IExternalMusicOutput : IDisposable
    {
        int Volume { get; }
        void Update();
        void SetGain(int gain);
        void Pause(bool paused);
    }

    private sealed class ExternalMusicOutput(PcmAudioOutput.Buffer primary, PcmAudioOutput.Buffer? loop, int volume, double initialSeconds, int bytesPerSecond, int loopBytes) : IExternalMusicOutput
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
