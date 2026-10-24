using System.Collections.Immutable;
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

public sealed record NativeWorldGroundSelectionAlternative([property: JsonRequired] int PreferredGroup, [property: JsonRequired] int? FailedSelections);

/// <summary>Process-owned53BB90 selection metadata, independent of loaded terrain pointers.</summary>
public sealed record NativeWorldGroundSelectionState([property: JsonRequired] ImmutableArray<NativeWorldGroundSelectionAlternative> Alternatives)
{
    public static NativeWorldGroundSelectionState CreateColdProcess()
    {
        return new([new(0, 0)]);
    }

    public static NativeWorldGroundSelectionState FromCache(NativeWorldGroundCacheState cache)
    {
        cache.Validate();
        return new(cache.Alternatives.Select(alternative => new NativeWorldGroundSelectionAlternative(alternative.PreferredGroup, alternative.FailedSelections)).Distinct().ToImmutableArray());
    }

    public void Validate()
    {
        if (Alternatives.IsDefaultOrEmpty || Alternatives.Length > 64 || Alternatives.Any(alternative => alternative is null || alternative.PreferredGroup is < 0 or > 1))
        {
            throw new InvalidDataException("Invalid retained native world ground selection metadata.");
        }
    }
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

    public static bool ContentEquals(NativeWorldGroundCacheState? left, NativeWorldGroundCacheState? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }
        if (left.Version != right.Version || left.Rule != right.Rule)
        {
            return false;
        }
        if (left.Alternatives is null || right.Alternatives is null)
        {
            return left.Alternatives is null && right.Alternatives is null;
        }
        if (left.Alternatives.Length != right.Alternatives.Length)
        {
            return false;
        }
        for (int index = 0; index < left.Alternatives.Length; index++)
        {
            NativeWorldGroundAlternative? first = left.Alternatives[index];
            NativeWorldGroundAlternative? second = right.Alternatives[index];
            if (first is null || second is null)
            {
                if (first is not null || second is not null)
                {
                    return false;
                }
                continue;
            }
            if (first.PreferredGroup != second.PreferredGroup || first.FailedSelections != second.FailedSelections || first.CurrentGround != second.CurrentGround || first.PreviousGround != second.PreviousGround)
            {
                return false;
            }
            if (first.Entries is null || second.Entries is null)
            {
                if (first.Entries is not null || second.Entries is not null)
                {
                    return false;
                }
            }
            else if (!first.Entries.SequenceEqual(second.Entries))
            {
                return false;
            }
        }
        return true;
    }

    public static NativeWorldGroundCacheState CreateFresh(NativeWorldGroundIdentity initialGround, NativeWorldGroundCacheState? retainedSelection = null, NativeWorldGroundSelectionState? processSelection = null)
    {
        initialGround.Validate();
        // 53E200 proves an empty cache.2036AD0 is retained across prior scenes;
        // preserve both possible groups until current inputs/query outcomes establish one result.
        retainedSelection?.Validate();
        processSelection?.Validate();
        NativeWorldGroundSelectionState? selection = retainedSelection is null ? processSelection : NativeWorldGroundSelectionState.FromCache(retainedSelection);
        NativeWorldGroundAlternative[] alternatives = selection is null ? [new(0, [], initialGround, initialGround), new(1, [], initialGround, initialGround)] : selection.Alternatives.Select(alternative => new NativeWorldGroundAlternative(alternative.PreferredGroup, [], initialGround, initialGround) { FailedSelections = alternative.FailedSelections }).ToArray();
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
