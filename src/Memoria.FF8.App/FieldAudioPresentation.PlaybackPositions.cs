using System.Security.Cryptography;
using System.Text;
using Memoria.FF8.Runtime;
using Memoria.FF8.Audio;

namespace Memoria.FF8;

internal sealed partial class FieldAudioPresentation
{
    private readonly HashSet<int> segmentPositionSlots = [];
    private readonly Dictionary<int, Func<int, GameplaySaveAudioPosition[]?>> musicPositionCaptures = [];
    private GameplaySaveAudioPresentation? playbackResumePositions;

    public void SetPlaybackResumePositions(GameplaySaveAudioPresentation? saved)
    {
        playbackResumePositions = saved?.Version == 1 ? saved : null;
    }

    public GameplaySaveAudioPresentation? CapturePlaybackPositions()
    {
        try
        {
            List<GameplaySaveAudioTrack> segments = [];
            foreach (int slot in segmentPositionSlots)
            {
                if (musicIds[slot] >= 0 && musicPositionCaptures.TryGetValue(slot, out var capture) && capture(0) is { Length: > 0 } position)
                {
                    segments.Add(new(slot, musicIds[slot], position));
                }
            }
            GameplaySaveAudioPresentation Capture(int queued)
            {
                List<GameplaySaveAudioTrack> tracks = [.. segments];
                for (int slot = 0; slot < musicIds.Length; slot++)
                {
                    if (!segmentPositionSlots.Contains(slot) && musicIds[slot] >= 0 && musicPositionCaptures.TryGetValue(slot, out var capture) && capture(queued) is { Length: > 0 } positions)
                    {
                        tracks.Add(new(slot, musicIds[slot], positions));
                    }
                }
                List<GameplaySaveAudioVoice> ambient = [];
                foreach (var pair in engine?.Scene.GameplayLoopSounds ?? System.Collections.Immutable.ImmutableDictionary<int, GameplaySaveFieldLoopSound>.Empty)
                {
                    if (voices.TryGetValue(pair.Key, out Voice? voice) && voice.PlaybackPosition is { } read)
                    {
                        ambient.Add(new(pair.Key, pair.Value.EffectId, read(queued)));
                    }
                }
                return new(1, tracks.ToArray(), ambient.ToArray());
            }
            return sound is null ? Capture(0) : sound.CapturePlaybackPosition(Capture);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or ObjectDisposedException or ArgumentOutOfRangeException or TimeoutException or System.Text.Json.JsonException)
        {
            Console.WriteLine($"audio-position=NotCaptured reason={exception.Message}");
            return null;
        }
    }

    private GameplaySaveAudioPosition? ResumeMusicPosition(int slot, int track, int part, string backend, string fingerprint)
    {
        GameplaySaveAudioTrack? saved = playbackResumePositions?.Music?.FirstOrDefault(item => item is not null && item.Slot == slot && item.MusicId == track);
        GameplaySaveAudioPosition? position = saved?.Positions?.ElementAtOrDefault(part);
        return CompatiblePosition(position, backend, fingerprint) ? position : null;
    }

    private void DiscardMusicResumePosition(int slot)
    {
        if (playbackResumePositions is { } saved)
        {
            playbackResumePositions = saved with { Music = saved.Music?.Where(track => track is not null && track.Slot != slot).ToArray() ?? [] };
        }
    }

    private void DiscardAmbientResumePosition(int voice)
    {
        if (playbackResumePositions is { } saved)
        {
            playbackResumePositions = saved with { Ambient = saved.Ambient?.Where(sound => sound is not null && sound.Voice != voice).ToArray() ?? [] };
        }
    }

    private GameplaySaveAudioPosition? ResumeAmbientPosition(int voice, string fingerprint)
    {
        uint? effect = engine?.Scene.GameplayLoopSounds?.GetValueOrDefault(voice)?.EffectId;
        GameplaySaveAudioPosition? saved = playbackResumePositions?.Ambient?.FirstOrDefault(item => item is not null && item.Voice == voice && item.EffectId == effect)?.Position;
        return CompatiblePosition(saved, "pcm16-v1", fingerprint) ? saved : null;
    }

    internal static bool CompatiblePosition(GameplaySaveAudioPosition? saved, string backend, string fingerprint)
    {
        return saved is not null && saved.Backend == backend && saved.Fingerprint == fingerprint && saved.Unit == "source-frames" && saved.Position >= 0 && saved.SampleRate > 0 && saved.Channels is 1 or 2 && saved.LoopStart >= 0 && saved.LoopEnd >= saved.LoopStart;
    }

    internal static string PcmFingerprint(PcmAudio pcm, long loopStart, long loopEnd)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes($"pcm16-v1:{pcm.SampleRate}:{pcm.Channels}:{loopStart}:{loopEnd}:"));
        hash.AppendData(pcm.Bytes);
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private MusicPlaybackPosition? ResumeSegmentPosition(int slot, int track)
    {
        GameplaySaveAudioPosition? position = playbackResumePositions?.Music?.FirstOrDefault(item => item is not null && item.Slot == slot && item.MusicId == track)?.Positions?.FirstOrDefault();
        if (position is null || position.Backend != "sgt-dls-v1" || position.Unit != "rendered-frames" || position.Fingerprint is not { Length: 64 } || position.Position < 0 || position.SampleRate <= 0 || position.StartPoint < 0 || !double.IsFinite(position.InitialSystemGain))
        {
            return null;
        }
        return new(position.Fingerprint, new(position.Position, position.SampleRate, position.StartPoint, position.InitialSystemGain, focusPaused || nativeMidiPaused));
    }

    private void RegisterSegmentPosition(int slot)
    {
        segmentPositionSlots.Add(slot);
        musicPositionCaptures[slot] = _ =>
        {
            MusicPlaybackPosition? position = music?.CapturePosition();
            return position is null ? null : [new("sgt-dls-v1", position.ContentSha256, "rendered-frames", position.Position.Frames, position.Position.SampleRate, 2, 0, 0, position.Position.StartPoint, position.Position.InitialSystemGain, position.Position.Paused)];
        };
    }

    private static GameplaySaveAudioPosition CapturePcmPosition(PcmAudioOutput.Buffer primary, PcmAudioOutput.Buffer? loop, bool loopStarted, PcmAudio pcm, string fingerprint, int loopStart, int loopEnd, int queued)
    {
        bool repeating = loopStarted && loop?.HasStartedBeforeQueuedFrames(queued) == true;
        long bytes = repeating ? loopStart + loop!.PositionBytesBeforeQueuedFrames(queued) : primary.PositionBytesBeforeQueuedFrames(queued);
        return new("pcm16-v1", fingerprint, "source-frames", bytes / (pcm.Channels * 2), pcm.SampleRate, pcm.Channels, loopStart / (pcm.Channels * 2), loopEnd / (pcm.Channels * 2), repeating ? 1 : 0);
    }

    private static bool ValidPcmPosition(GameplaySaveAudioPosition? saved, PcmAudio pcm, int loopStart, int loopEnd)
    {
        int frameBytes = pcm.Channels * 2;
        return saved is not null && saved.SampleRate == pcm.SampleRate && saved.Channels == pcm.Channels && saved.Position < loopEnd / frameBytes && saved.LoopStart == loopStart / frameBytes && saved.LoopEnd == loopEnd / frameBytes && saved.StartPoint is 0 or 1 && (saved.StartPoint == 0 || loopStart > 0 && saved.Position >= loopStart / frameBytes);
    }
}
