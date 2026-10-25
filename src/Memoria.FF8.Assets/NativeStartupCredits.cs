namespace Memoria.FF8.Assets;

/// <summary>One displayed callback of original52F300. OverlayValue is PSX subtractive black or additive white intensity.</summary>
public sealed record NativeStartupCreditsFrame(int ResourceIndex, byte OverlayValue, bool OverlayIsWhite, bool Finished, int NativeState);

/// <summary>FF8_EN52D970's60.5Hz clock and52F300's exact no-input picture sequence.</summary>
public static class NativeStartupCredits
{
    public const double FrameRate = 60.5;
    public const int MusicId = 79;
    private static readonly NativeStartupCreditsFrame[] square = BuildSquare();
    private static readonly NativeStartupCreditsFrame[] credits = BuildCredits();

    public static TimeSpan SquareDuration => TimeSpan.FromSeconds(square.Length / FrameRate);
    public static TimeSpan CreditsCycleDuration => GetCreditsCycleDuration();

    public static TimeSpan GetCreditsCycleDuration(bool skipInitialPause = false) => TimeSpan.FromSeconds((credits.Length - (skipInitialPause ? 180 : 0)) / FrameRate);

    public static NativeStartupCreditsFrame SampleSquare(TimeSpan elapsed)
    {
        long frame = FrameNumber(elapsed);
        return frame >= square.Length ? square[^1] with { Finished = true } : square[(int)frame];
    }

    /// <summary>Music79 starts on this stage's first callback. Finished requests the original state17 restart through the Square logo; only input enters completion state18.</summary>
    public static NativeStartupCreditsFrame SampleCredits(TimeSpan elapsed, bool skipInitialPause = false)
    {
        long frame = FrameNumber(elapsed);
        if (skipInitialPause && frame > 0)
        {
            frame = checked(frame + 180);
        }
        return frame >= credits.Length ? credits[^1] with { Finished = true } : credits[(int)frame];
    }

    private static long FrameNumber(TimeSpan elapsed)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(elapsed, TimeSpan.Zero);
        return checked((long)Math.Floor(elapsed.TotalSeconds * FrameRate));
    }

    private static NativeStartupCreditsFrame[] BuildSquare()
    {
        List<NativeStartupCreditsFrame> frames = [];
        for (int value = 255; value >= 0; value -= 4)
        {
            frames.Add(new(4, (byte)value, false, false, frames.Count == 0 ? 0 : 1));
        }
        frames.Add(new(4, 0, false, false, 1));
        for (int value = 0; value <= 180; value++)
        {
            frames.Add(new(4, 0, false, false, 2));
        }
        for (int value = 0; value <= 120; value++)
        {
            frames.Add(new(4, 0, false, false, 3));
        }
        for (int value = 0; value <= 255; value++)
        {
            frames.Add(new(4, (byte)value, false, false, 4));
        }
        return frames.ToArray();
    }

    private static NativeStartupCreditsFrame[] BuildCredits()
    {
        List<NativeStartupCreditsFrame> frames = [];
        // States5/6 leave the previous Square image hidden for181 callbacks, starting music immediately.
        for (int value = 0; value <= 180; value++)
        {
            frames.Add(new(4, 255, false, false, value == 0 ? 5 : 6));
        }
        int reference = frames.Count - 1;
        for (int resource = 5; resource <= 32; resource++)
        {
            int state = resource == 32 ? 9 : 7;
            for (int value = 255; value >= 0; value -= 8)
            {
                frames.Add(new(resource, (byte)value, false, false, state));
            }
            frames.Add(new(resource, 0, false, false, state));
            int panel = resource - 5;
            while (frames.Count - reference < 374 * (panel + 1))
            {
                frames.Add(new(resource, 0, false, false, resource == 32 ? 10 : 7));
            }
            // The threshold callback changes phase without drawing an overlay.
            frames.Add(new(resource, 0, false, false, resource == 32 ? 10 : 7));
            if (resource == 32)
            {
                break;
            }
            for (int value = 0; value < 255; value += 8)
            {
                frames.Add(new(resource, (byte)value, false, false, 7));
            }
            frames.Add(new(resource, 255, false, false, 7));
        }
        for (int value = 0; value <= 255; value++)
        {
            frames.Add(new(32, (byte)value, true, false, 11));
        }
        for (int value = 0; value <= 120; value++)
        {
            frames.Add(new(32, 255, true, false, 12));
        }
        for (int value = 255; value >= 0; value -= 2)
        {
            frames.Add(new(33, (byte)value, true, false, value == 255 ? 13 : 14));
        }
        frames.Add(new(33, 0, true, false, 14));
        frames.Add(new(33, 0, false, false, 15));
        for (int value = 0; value <= 3600; value++)
        {
            frames.Add(new(33, 0, false, false, 16));
        }
        for (int value = 0; value <= 255; value++)
        {
            frames.Add(new(33, (byte)value, false, false, 17));
        }
        return frames.ToArray();
    }
}
