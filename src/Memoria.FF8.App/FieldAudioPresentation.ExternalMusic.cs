using Memoria.FF8.Mods;
using Memoria.FF8.Runtime;

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
            ResetMidiMusic();
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
                PreparedPcmMusic music = PreparedPcmMusic.FromVorbis(decoded, encoded, PcmMusicRepeat.TaggedRange);
                int offset = ExternalMusicPcm.OffsetBytes(decoded, selection.OffsetSeconds);
                if (offset >= music.Loop.EndByte)
                {
                    throw new InvalidDataException("External music offset exceeds its loop end.");
                }
                return (Music: music, Offset: offset);
            }, prepared => StartExternalMusic(selection, kind, slot, track, prepared.Music, prepared.Offset), exception => MusicDecodeFailed(slot, exception));
        }
        catch
        {
            musicKinds[slot] = MusicKind.None;
            musicIds[slot] = -1;
            throw;
        }
        return true;
    }

    private void StartExternalMusic(ExternalMusicSelection selection, MusicKind kind, int slot, int track, PreparedPcmMusic prepared, int offset)
    {
        PcmAudio pcm = prepared.Pcm;
        ExternalMusicLoop loop = prepared.Loop;
        double positionFrames = offset / (pcm.Channels * sizeof(short));
        string fingerprint = PcmFingerprint(pcm, loop.StartByte, loop.EndByte);
        GameplaySaveAudioPosition? savedPosition = ResumeMusicPosition(slot, track, 0, "pcm16-v1", fingerprint);
        bool restoreLoop = false;
        if (ValidPcmPosition(savedPosition, pcm, loop.StartByte, loop.EndByte))
        {
            positionFrames = savedPosition!.Position + savedPosition.FractionalFrame;
            restoreLoop = savedPosition.StartPoint == 1;
        }
        int volume = Math.Clamp((int)(kind == MusicKind.Midi ? midiVolume : streamVolume), 0, 127);
        if (kind == MusicKind.Stream)
        {
            streamedMusic?.Dispose();
            streamedMusic = null;
        }
        PcmLoopPlayback output = prepared.Start(Sound, positionFrames, restoreLoop, ExternalGain(kind, volume, selection.Volume), selection.Volume, focusPaused || nativeMidiPaused);
        externalMusic[(int)kind]?.Dispose();
        externalMusic[(int)kind] = output;
        DiscardMusicResumePosition(slot);
        musicPositionCaptures[slot] = queued =>
        {
            (double frames, bool repeating) = output.CaptureFramePosition(queued, prepared.LoopStartFrames);
            return [CreatePcmPosition(frames, repeating, pcm, fingerprint, loop.StartByte, loop.EndByte)];
        };
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
