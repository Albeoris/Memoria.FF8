using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Saved Limit learning bytes1CFE76C/76E/772 and Angelo's native training counters.</summary>
public sealed record NativeLimitProgress([property: JsonRequired] string InitOutSha256, [property: JsonRequired] ushort BlueMagicFlags, [property: JsonRequired] ushort DuelFlags, [property: JsonRequired] byte AngeloFlags, [property: JsonRequired] byte TrainingSelection, [property: JsonRequired] ImmutableArray<byte> TrainingCounters)
{
    public byte? SpecialMagicFlags { get; init; }

    public void Validate()
    {
        if (InitOutSha256 is not { Length: 64 } || InitOutSha256.Any(value => !Uri.IsHexDigit(value)) || TrainingCounters.IsDefault || TrainingCounters.Length != 8)
        {
            throw new InvalidDataException("Native Limit progression requires its authentic saved flags and eight Angelo counters.");
        }
    }
}
