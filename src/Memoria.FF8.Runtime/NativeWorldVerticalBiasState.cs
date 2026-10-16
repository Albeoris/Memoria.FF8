using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>557A90's retained signed WORD204DADC. Unknown older owners retain the full WORD domain, never an invented zero.</summary>
public sealed record NativeWorldVerticalBiasState([property: JsonRequired] short Minimum, [property: JsonRequired] short Maximum)
{
    [JsonIgnore]
    public bool IsExact => Minimum == Maximum;

    public static NativeWorldVerticalBiasState UnknownWord => new(short.MinValue, short.MaxValue);

    public static NativeWorldVerticalBiasState Exact(short value)
    {
        return new(value, value);
    }

    public void Validate()
    {
        if (Minimum > Maximum)
        {
            throw new InvalidDataException("Native vertical bias requires an ordered signed-WORD domain.");
        }
    }

    public NativeWorldVerticalBiasState Damp(int minimumRate, int maximumRate, long callbacks = 1)
    {
        Validate();
        if (minimumRate < 0 || maximumRate < minimumRate || callbacks < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumRate));
        }
        long minimumAmount = Math.Min(65536, minimumRate * Math.Min(callbacks, 65536));
        long maximumAmount = Math.Min(65536, maximumRate * Math.Min(callbacks, 65536));
        short minimum = (short)(Minimum < 0 ? Math.Min(0, Minimum + minimumAmount) : Math.Max(0, Minimum - maximumAmount));
        short maximum = (short)(Maximum > 0 ? Math.Max(0, Maximum - minimumAmount) : Math.Min(0, Maximum + maximumAmount));
        return new(minimum, maximum);
    }

    public NativeWorldVerticalBiasState StepRagnarok(sbyte vertical)
    {
        Validate();
        if (vertical == 0)
        {
            return Damp(15, 15);
        }
        int subtraction = (120 * vertical) >> 9;
        int low = Minimum - subtraction;
        int high = Maximum - subtraction;
        if (low < short.MinValue && high >= short.MinValue || high > short.MaxValue && low <= short.MaxValue)
        {
            // WORD wrap crosses from a negative clamp to a positive clamp.
            return new(-4096, 4096);
        }
        short first = unchecked((short)low);
        short last = unchecked((short)high);
        return new((short)Math.Clamp((int)first, -4096, 4096), (short)Math.Clamp((int)last, -4096, 4096));
    }
}
