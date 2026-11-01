using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Process-retained texture and palette animation clocks, advanced by the world input callback.</summary>
public sealed record NativeWorldTextureAnimationState([property: JsonRequired] int TextureClock, [property: JsonRequired] int PaletteClock)
{
    public const int ClockPeriod = 384;

    public static NativeWorldTextureAnimationState FromColdProcess() => new(0, 0);

    public void Validate()
    {
        if (TextureClock is < 0 or >= ClockPeriod || PaletteClock is < 0 or >= ClockPeriod)
        {
            throw new InvalidDataException("Native world texture animation clocks are outside their modulo-384 range.");
        }
    }

    public NativeWorldTextureAnimationState AdvanceWorldCallback(int entityType, byte scriptMode, bool textureEnabled = true, bool paletteEnabled = true)
    {
        Validate();
        int step = entityType == 50 && scriptMode != 12 ? 3 : 2;
        return new(textureEnabled ? (TextureClock + step) % ClockPeriod : TextureClock, paletteEnabled ? (PaletteClock + step) % ClockPeriod : PaletteClock);
    }
}
