using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Current logical channel selection and gain, independently of decoder/device resources and process-first-load history.</summary>
public sealed record GameplaySaveMusicPlayback([property: JsonRequired] int? Gain0, [property: JsonRequired] int? Gain1, [property: JsonRequired] int? CurrentMusicId0, [property: JsonRequired] int? CurrentMusicId1, [property: JsonRequired] NativeMusicPlaybackMode? Mode0, [property: JsonRequired] NativeMusicPlaybackMode? Mode1, [property: JsonRequired] uint? ConcertMask)
{
    public static GameplaySaveMusicPlayback? FromSceneDefaults(GameplaySaveSceneMusic? music)
    {
        if (music is null || !music.Playing)
        {
            return null;
        }
        int primary = music.PrimaryChannel is { ChannelId: >= 0, MusicId: byte first } && first != 255 ? first : music.LoadedMusic;
        int? secondary = music.SecondaryChannel is { ChannelId: >= 0, MusicId: byte second } && second != 255 ? second : null;
        int gain = Math.Clamp(music.Volume, 0, 127);
        GameplaySaveMusicPlayback result = new(gain, secondary is null ? 0 : gain, primary, secondary ?? 0, NativeMusicPlaybackMode.Midi, secondary is null ? NativeMusicPlaybackMode.Stopped : NativeMusicPlaybackMode.Midi, music.PrimaryChannel?.ChoiceMask);
        result.Validate();
        return result;
    }

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
