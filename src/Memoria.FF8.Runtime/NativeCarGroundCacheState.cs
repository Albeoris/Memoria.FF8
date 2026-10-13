using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeWorldGroundIdentity([property: JsonRequired] int MapId, [property: JsonRequired] int SourceId, [property: JsonRequired] int BlockIndex, [property: JsonRequired] int PolygonIndex)
{
    public int Cell => (MapId / 32 * 4 + BlockIndex / 4) * 128 + MapId % 32 * 4 + BlockIndex % 4;

    public void Validate()
    {
        if (MapId is < 0 or >= 768 || SourceId is < 0 or >= 835 || BlockIndex is < 0 or >= 16 || PolygonIndex is < 0 or > 255)
        {
            throw new InvalidDataException("Invalid original world ground identity.");
        }
    }
}

/// <summary>One possible retained native2036AD0 group, twelve-entry2035CA0 cache and current/previous ground pointers.</summary>
public sealed record NativeCarGroundAlternative([property: JsonRequired] int PreferredGroup, [property: JsonRequired] NativeWorldGroundIdentity[] Entries, [property: JsonRequired] NativeWorldGroundIdentity CurrentGround, [property: JsonRequired] NativeWorldGroundIdentity PreviousGround)
{
    public int? FailedSelections { get; init; }
}

public sealed record NativeCarGroundCacheState
{
    public const string CacheRule = "ff8-en-car-ground-53e200-53eb80-53e2a0-v1";
    [JsonRequired]
    public required int Version { get; init; }
    [JsonRequired]
    public required string Rule { get; init; }
    [JsonRequired]
    public required NativeCarGroundAlternative[] Alternatives { get; init; }

    public static NativeCarGroundCacheState CreateFresh(NativeWorldGroundIdentity initialGround)
    {
        initialGround.Validate();
        // 53E200 proves an empty cache.2036AD0 is retained across prior scenes;
        // preserve both possible groups until current inputs/query outcomes establish one result.
        return new NativeCarGroundCacheState { Version = 1, Rule = CacheRule, Alternatives = [new(0, [], initialGround, initialGround), new(1, [], initialGround, initialGround)] };
    }

    public void Validate()
    {
        if (Version != 1 || Rule != CacheRule || Alternatives is not { Length: > 0 and <= 2 })
        {
            throw new InvalidDataException("Native car ground cache lacks its executed loader/query authority.");
        }
        foreach (NativeCarGroundAlternative alternative in Alternatives)
        {
            if (alternative is null || alternative.PreferredGroup is < 0 or > 1 || alternative.Entries is not { Length: <= 12 } || alternative.CurrentGround is null || alternative.PreviousGround is null || alternative.Entries.Any(entry => entry is null) || alternative.FailedSelections < 0)
            {
                throw new InvalidDataException("Invalid native car ground cache alternative.");
            }
            alternative.CurrentGround.Validate();
            alternative.PreviousGround.Validate();
            foreach (NativeWorldGroundIdentity entry in alternative.Entries)
            {
                entry.Validate();
            }
        }
    }
}
