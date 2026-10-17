using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>The current and prior mapped low WORDs published by actual world input callbacks.</summary>
public sealed record NativeWorldInputHistory([property: JsonRequired] ushort CurrentButtons, [property: JsonRequired] ushort? PreviousButtons, [property: JsonRequired] long Samples)
{
    public void Validate()
    {
        if (Samples <= 0 || Samples == 1 && PreviousButtons is not null || Samples > 1 && PreviousButtons is null)
        {
            throw new InvalidDataException("World input history requires an actual sampled callback.");
        }
    }

    public static NativeWorldInputHistory Sample(NativeWorldInputHistory? previous, uint buttons)
    {
        previous?.Validate();
        if (buttons > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(buttons), "World mapped input requires an unsigned WORD.");
        }
        return new NativeWorldInputHistory((ushort)buttons, previous?.CurrentButtons, checked((previous?.Samples ?? 0) + 1));
    }
}
