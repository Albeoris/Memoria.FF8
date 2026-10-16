using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Saved PocketStation block1CFEFB8..1CFEFF7, retained with its original56D9F0 clear provenance.</summary>
public sealed record NativePocketStationState([property: JsonRequired] string InitOutSha256, [property: JsonRequired] ImmutableArray<byte> Raw)
{
    public byte Flags => Raw[0];
    public byte Level => Raw[1];
    public byte AttackVariant => Raw[45];

    public void Validate()
    {
        if (InitOutSha256 != NativeInputConfiguration.OriginalInitHash || Raw.IsDefault || Raw.Length != 64)
        {
            throw new InvalidDataException("Native PocketStation state requires its original cleared64-byte saved block.");
        }
    }
}
