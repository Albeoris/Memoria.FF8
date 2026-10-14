using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Menu WORD1D2BAD2, independent of the script/world52D210 generator.</summary>
public sealed record NativeMenuRandomState([property: JsonRequired] ushort Seed)
{
    public static NativeMenuRandomState FromColdProcess() => new(0x4611);

    public void Validate()
    {
        // The complete WORD admits every bit pattern;4C2560 MOVZXes it.
    }

    public NativeMenuRandomState Advance(int calls)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(calls);
        ushort seed = Seed;
        for (int index = 0; index < calls; index++)
        {
            seed = (ushort)((125 * seed + 14) & 0x7FFF);
        }
        return this with { Seed = seed };
    }
}
