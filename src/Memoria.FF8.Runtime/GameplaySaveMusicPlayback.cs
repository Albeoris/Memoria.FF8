using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Current logical channel selection and gain, independently of decoder/device resources and process-first-load history.</summary>
public sealed record GameplaySaveMusicPlayback([property: JsonRequired] int? Gain0, [property: JsonRequired] int? Gain1, [property: JsonRequired] int? CurrentMusicId0, [property: JsonRequired] int? CurrentMusicId1, [property: JsonRequired] NativeMusicPlaybackMode? Mode0, [property: JsonRequired] NativeMusicPlaybackMode? Mode1, [property: JsonRequired] uint? ConcertMask)
{
    public static GameplaySaveMusicPlayback? Capture(NativeMusicPlaybackState? playback)
    {
        playback?.Validate();
        return playback is null || playback.IsColdProcess ? null : new(playback.Gain0, playback.Gain1, playback.CurrentMusicId0, playback.CurrentMusicId1, playback.Mode0, playback.Mode1, playback.ConcertMask);
    }

    public NativeMusicPlaybackState Restore(NativeMusicPlaybackState initialized)
    {
        NativeMusicPlaybackState result = initialized with { Gain0 = Gain0, Gain1 = Gain1, CurrentMusicId0 = CurrentMusicId0, CurrentMusicId1 = CurrentMusicId1, Mode0 = Mode0, Mode1 = Mode1, ConcertMask = ConcertMask };
        result.Validate();
        return result;
    }

    public void Validate()
    {
        _ = Restore(NativeMusicPlaybackState.FromColdProcess());
    }
}
