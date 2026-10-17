namespace Memoria.FF8.Runtime;

public enum NativeMusicPlaybackMode
{
    Stopped = 0,
    Midi = 1,
    Wave = 2
}

/// <summary>The original46B530 route switch. A selected route does not establish successful device playback.</summary>
public static class NativeMusicRouting
{
    public static NativeMusicPlaybackMode Mode46B530(int decodedMusicId)
    {
        return GetStreamFileName(decodedMusicId) is null ? NativeMusicPlaybackMode.Midi : NativeMusicPlaybackMode.Wave;
    }

    public static string? GetStreamFileName(int decodedMusicId)
    {
        return decodedMusicId switch
        {
            3 => "003-combat.wav",
            6 => "006-funsui.wav",
            10 => "010-ante.wav",
            11 => "011-wind.wav",
            30 => "030-flangchorus.wav",
            31 => "031-dubchorus.wav",
            32 => "032-solochorus.wav",
            33 => "033-femalechorus.wav",
            34 => "034-chorus.wav",
            37 => "037-reet.wav",
            38 => "038-soyo.wav",
            39 => "039-rouka.wav",
            40 => "040-night.wav",
            44 => "044-sea.wav",
            74 => "074-train.wav",
            87 => "087-mdmotor.wav",
            94 => "094-laswhite.wav",
            95 => "095-lasbl.wav",
            _ => null
        };
    }
}
