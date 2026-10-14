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
public sealed record NativeWorldGroundAlternative([property: JsonRequired] int PreferredGroup, [property: JsonRequired] NativeWorldGroundIdentity[] Entries, [property: JsonRequired] NativeWorldGroundIdentity CurrentGround, [property: JsonRequired] NativeWorldGroundIdentity PreviousGround)
{
    public int? FailedSelections { get; init; }
}

public sealed record NativeWorldGroundCacheState
{
    public const string CacheRule = "ff8-en-world-ground-53e200-53eb80-53e2a0-v1";
    [JsonRequired]
    public required int Version { get; init; }
    [JsonRequired]
    public required string Rule { get; init; }
    [JsonRequired]
    public required NativeWorldGroundAlternative[] Alternatives { get; init; }

    public static NativeWorldGroundCacheState CreateFresh(NativeWorldGroundIdentity initialGround, NativeWorldGroundCacheState? retainedSelection = null)
    {
        initialGround.Validate();
        // 53E200 proves an empty cache.2036AD0 is retained across prior scenes;
        // preserve both possible groups until current inputs/query outcomes establish one result.
        retainedSelection?.Validate();
        NativeWorldGroundAlternative[] alternatives = retainedSelection is null ? [new(0, [], initialGround, initialGround), new(1, [], initialGround, initialGround)] : retainedSelection.Alternatives.Select(alternative => new NativeWorldGroundAlternative(alternative.PreferredGroup, [], initialGround, initialGround) { FailedSelections = alternative.FailedSelections }).ToArray();
        return new NativeWorldGroundCacheState { Version = 1, Rule = CacheRule, Alternatives = alternatives };
    }

    public void Validate()
    {
        if (Version != 1 || Rule != CacheRule || Alternatives is not { Length: > 0 and <= 64 })
        {
            throw new InvalidDataException("Native world ground cache lacks its executed loader/query authority.");
        }
        foreach (NativeWorldGroundAlternative alternative in Alternatives)
        {
            if (alternative is null || alternative.PreferredGroup is < 0 or > 1 || alternative.Entries is not { Length: <= 12 } || alternative.CurrentGround is null || alternative.PreviousGround is null || alternative.Entries.Any(entry => entry is null))
            {
                throw new InvalidDataException("Invalid native world ground cache alternative.");
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
