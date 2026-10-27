namespace Memoria.FF8.Assets;

/// <summary>Selects the original concert segment identities from an executed 26-bit instrument mask.</summary>
public static class NativeConcertSelection
{
    private static readonly (uint Group, string Filename)[] Instruments =
    [
        (0x2, "043a-concert-tap.sgt"),
        (0x4, "043b-concert-flute.sgt"),
        (0x8, "043c-concert-fiddle.sgt"),
        (0xFF0, "043d-concert-aguitar.sgt"),
        (0x1000, "043e-concert-sax.sgt"),
        (0x7E000, "043f-concert-piano.sgt"),
        (0x380000, "043g-concert-eguitar.sgt"),
        (0x400000, "043h-concert-ebass.sgt")
    ];

    public static IReadOnlyList<string> Tracks(uint mask)
    {
        ValidateMask(mask);
        List<string> tracks = [];
        foreach ((uint group, string filename) in Instruments)
        {
            if ((mask & group) == group)
            {
                tracks.Add(filename);
            }
        }
        return tracks.AsReadOnly();
    }

    public static bool IsPreview(uint mask)
    {
        ValidateMask(mask);
        return Instruments.Any(instrument => instrument.Group == mask);
    }

    private static void ValidateMask(uint mask)
    {
        if (mask > 0x03FFFFFF)
        {
            throw new ArgumentOutOfRangeException(nameof(mask), "Concert selection requires an executed 26-bit instrument mask.");
        }
    }
}
