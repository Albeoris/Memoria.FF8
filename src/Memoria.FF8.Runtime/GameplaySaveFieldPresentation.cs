using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record GameplaySaveFieldBar([property: JsonRequired] short Mode, [property: JsonRequired] short X, [property: JsonRequired] short Y, [property: JsonRequired] short Minimum, [property: JsonRequired] short Maximum, [property: JsonRequired] short Value, [property: JsonRequired] short MirrorFlags, [property: JsonRequired] short? WidthWord, [property: JsonRequired] short Width, [property: JsonRequired] short Fill, [property: JsonRequired] byte DisplayFlags)
{
    internal static GameplaySaveFieldBar? Capture(NativeFieldBarSlot? slot) => slot is null ? null : new(slot.Mirror.Mode, slot.Mirror.X, slot.Mirror.Y, slot.Mirror.Minimum, slot.Mirror.Maximum, slot.Mirror.Value, slot.Mirror.Flags, slot.Mirror.WidthWord5A, slot.Display.Width, slot.Display.Fill, slot.Display.Flags);

    internal NativeFieldBarSlot Restore() => new(new(Mode, Value, Maximum, Minimum, MirrorFlags, Y, X, WidthWord), new(X, Y, Minimum, Maximum, Value, Width, Fill, DisplayFlags));
}

public sealed record GameplaySaveFieldShakeAxis([property: JsonRequired] bool Enabled, [property: JsonRequired] bool Oscillating, [property: JsonRequired] byte Cursor, [property: JsonRequired] sbyte Output, [property: JsonRequired] short Amplitude, [property: JsonRequired] short Previous, [property: JsonRequired] short Target, [property: JsonRequired] short Duration, [property: JsonRequired] short Counter)
{
    internal static GameplaySaveFieldShakeAxis Capture(NativeShakeAxis axis) => new(axis.Enabled, axis.Oscillating, axis.Cursor, axis.Output, axis.Amplitude, axis.Previous, axis.Target, axis.Duration, axis.Counter);

    internal NativeShakeAxis Restore() => new(Enabled, Oscillating, Cursor, Output, Amplitude, Previous, Target, Duration, Counter);
}

/// <summary>Logical bar geometry and shake phase after completed field commands; installed lookup tables and physical aliases are reconstructed separately.</summary>
public sealed record GameplaySaveFieldPresentation([property: JsonRequired] GameplaySaveFieldBar? FirstBar, [property: JsonRequired] GameplaySaveFieldBar? SecondBar, [property: JsonRequired] GameplaySaveFieldShakeAxis? ShakeX, [property: JsonRequired] GameplaySaveFieldShakeAxis? ShakeY)
{
    public static GameplaySaveFieldPresentation CreateDefaults()
    {
        // Hidden zeroed UI records also allow a later SETBAR to update a slot whose original display command was not saved.
        NativeFieldBarsState bars = NativeFieldBarsState.CreateHidden().DisplayWidth(0, 0, 0, 0, 0, 0, 0, 0).Hide(0).DisplayWidth(1, 0, 0, 0, 0, 0, 0, 0).Hide(1);
        return Capture(bars, NativeFieldShake.FromColdProcess())!;
    }

    public static GameplaySaveFieldPresentation? Capture(NativeFieldBarsState? bars, NativeFieldShake? shake)
    {
        bars?.Validate();
        shake?.Validate();
        if (bars?.First is null && bars?.Second is null && (shake is null || shake.IsColdProcess))
        {
            return null;
        }
        return new(GameplaySaveFieldBar.Capture(bars?.First), GameplaySaveFieldBar.Capture(bars?.Second), shake is null ? null : GameplaySaveFieldShakeAxis.Capture(shake.X), shake is null ? null : GameplaySaveFieldShakeAxis.Capture(shake.Y));
    }

    public NativeFieldBarsState RestoreBars(NativeFieldBarsState initialized)
    {
        NativeFieldBarsState result = initialized with { First = FirstBar?.Restore(), Second = SecondBar?.Restore() };
        result.Validate();
        return result;
    }

    public NativeFieldShake RestoreShake(NativeFieldShake initialized)
    {
        if ((ShakeX is null) != (ShakeY is null))
        {
            throw new InvalidDataException("Gameplay shake continuation requires both logical axes.");
        }
        NativeFieldShake result = ShakeX is null ? initialized : initialized with { X = ShakeX.Restore(), Y = ShakeY!.Restore() };
        result.Validate();
        return result;
    }

    public void Validate()
    {
        _ = RestoreBars(NativeFieldBarsState.CreateHidden());
        _ = RestoreShake(NativeFieldShake.FromColdProcess());
    }
}
