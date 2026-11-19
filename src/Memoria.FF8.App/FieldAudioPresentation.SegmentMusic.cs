using Memoria.FF8.Audio;

namespace Memoria.FF8;

internal sealed partial class FieldAudioPresentation
{
    private AudioDecodeQueue? segmentDecodeQueue;
    private SegmentMusicBank? segmentBank;
    private SegmentMusicSource? music;
    private int segmentMusicSlot = -1;

    internal void PumpPendingSegmentMusic()
    {
        TryOutput(() => segmentDecodeQueue?.Pump());
        if (music?.Error is Exception failure)
        {
            MusicDecodeFailed(segmentMusicSlot, failure);
        }
    }

    private void QueueSegmentMusic(string[] paths, int slot, int track, int volume, bool concert = false, int startPoint = 0, int previewMeasure = 0)
    {
        ObjectDisposedException.ThrowIf(audioDisposed, this);
        ResetMidiMusic();
        string bankPath = gameFiles.ResolveDataFile("music/dmusic/FF8.dls");
        int sampleRate = Sound.SampleRate;
        MusicPlaybackPosition? saved = ResumeSegmentPosition(slot, track);
        midiVolume = volume;
        midiRamp = null;
        nativeMidiPaused = false;
        try
        {
            segmentDecodeQueue ??= new AudioDecodeQueue(ReportOutputFailure);
            segmentDecodeQueue.Queue(MusicKind.Midi, async token =>
            {
                segmentBank ??= new SegmentMusicBank(await File.ReadAllBytesAsync(bankPath, token).ConfigureAwait(false));
                return await segmentBank.PrepareAsync(paths, sampleRate, 0, concert, startPoint, previewMeasure, saved, token).ConfigureAwait(false);
            }, prepared =>
            {
                prepared.Volume(OutputGain(Catalog.MusicVolume(Math.Clamp((int)midiVolume, 0, 127)), musicPreference));
                if (focusPaused || nativeMidiPaused)
                {
                    prepared.Pause();
                }
                else
                {
                    prepared.Resume();
                }
                Sound.AddSource(prepared, Sound.Music);
                music = prepared;
                segmentMusicSlot = slot;
                RegisterSegmentPosition(slot, prepared);
                DiscardMusicResumePosition(slot);
                Console.WriteLine($"field-music=Playing track={track} slot={slot} file={string.Join(',', paths.Select(Path.GetFileName))}");
            }, exception => MusicDecodeFailed(slot, exception));
        }
        catch
        {
            musicKinds[slot] = MusicKind.None;
            musicIds[slot] = -1;
            throw;
        }
        musicKinds[slot] = MusicKind.Midi;
        musicIds[slot] = track;
    }

    private void StopSegmentMusic()
    {
        if (music is { } active)
        {
            sound?.RemoveSource(active);
            musicPositionCaptures.Remove(segmentMusicSlot);
            segmentMusicSlot = -1;
            music = null;
        }
    }

    private void ResetMidiMusic()
    {
        CancelMusicDecode(MusicKind.Midi);
        StopSegmentMusic();
        StopExternalMusic(MusicKind.Midi);
    }
}
