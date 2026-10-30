using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeWorldTerrainFlags109([property: JsonRequired] byte Value, [property: JsonRequired] byte KnownMask)
{
    public static NativeWorldTerrainFlags109 Exact(byte value)
    {
        return new(value, byte.MaxValue);
    }

    public NativeWorldTerrainFlags109 WithEntranceFlag(bool? entrance)
    {
        Validate();
        return entrance is bool value
            ? new((byte)((Value & 0xFE) | (value ? 1 : 0)), (byte)(KnownMask | 1))
            : new((byte)(Value & 0xFE), (byte)(KnownMask & 0xFE));
    }

    public bool Matches(ushort operand)
    {
        Validate();
        if ((KnownMask & 1) == 0)
        {
            throw new NotSupportedException("World predicate FF21 requires the saved terrain entrance flag.");
        }
        return (Value & 1) == unchecked((short)operand);
    }

    public void Validate()
    {
        if ((Value & ~KnownMask) != 0)
        {
            throw new InvalidDataException("Saved terrain flags contain values outside their authenticated mask.");
        }
    }
}

public sealed partial record NativeSavedWorldState
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeWorldTerrainFlags109? TerrainFlags109 { get; init; }

    public NativeSavedWorldState WithTerrainEntranceFlag(bool? entrance)
    {
        return this with { TerrainFlags109 = (TerrainFlags109 ?? new(0, 0)).WithEntranceFlag(entrance) };
    }

    public bool MatchesTerrainFlags109(ushort operand)
    {
        return (TerrainFlags109 ?? throw new NotSupportedException("World predicate FF21 requires its saved terrain owner.")).Matches(operand);
    }
}
