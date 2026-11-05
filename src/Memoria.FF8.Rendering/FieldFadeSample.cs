using System.Numerics;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Rendering;

/// <summary>Fractional display color kept separate from the native fade's physical WORDs and drawing bytes.</summary>
public sealed record FieldFadeSample(NativeFadeDrawMode Mode, Vector3 Color)
{
    public static FieldFadeSample FromNative(NativeFadeDraw draw) => new(draw.Mode, new Vector3(draw.Red, draw.Green, draw.Blue));

    public void Validate()
    {
        if (!Enum.IsDefined(Mode) || new[] { Color.X, Color.Y, Color.Z }.Any(value => !float.IsFinite(value) || value < 0 || value > 255))
        {
            throw new ArgumentOutOfRangeException(nameof(Color), "Presentation fade requires a valid blend mode and finite RGB channels.");
        }
    }

    public static FieldFadeSample Interpolate(NativeFieldFadeState previous, NativeFieldFadeState current, float fraction)
    {
        if (!float.IsFinite(fraction) || fraction is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(fraction));
        }
        previous.Validate();
        current.Validate();
        FieldFadeSample before = FromNative(previous.Draw);
        FieldFadeSample after = FromNative(current.Draw);
        if (fraction == 0)
        {
            return before;
        }
        if (fraction == 1)
        {
            return after;
        }
        if (before.Mode == after.Mode)
        {
            return after with { Color = Vector3.Lerp(before.Color, after.Color, fraction) };
        }
        if (before.Mode == NativeFadeDrawMode.None && after.Mode is NativeFadeDrawMode.Add or NativeFadeDrawMode.Subtract)
        {
            return after with { Color = after.Color * fraction };
        }
        if (after.Mode == NativeFadeDrawMode.None && before.Mode is NativeFadeDrawMode.Add or NativeFadeDrawMode.Subtract)
        {
            return before with { Color = before.Color * (1 - fraction) };
        }
        return after;
    }
}
