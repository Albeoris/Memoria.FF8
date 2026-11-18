using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public partial record NativeBattleEffectArena
{
    private sealed record DeferredImport(long Version);
    private static readonly ConditionalWeakTable<NativeBattleEffectArena, DeferredImport> DeferredImports = new();

    [JsonIgnore]
    internal bool HasUnhydratedImport => !snapshot && working is null && !borrowed && Transaction is null && retainedAuthority is null && (!initialRegions.IsDefaultOrEmpty || !initialUnknown.IsDefaultOrEmpty || initialPointers is { Count: > 0 } || initialTextureFiles is not null);

    private long DeferredImportVersion => DeferredImports.TryGetValue(this, out DeferredImport? import) ? import.Version : 0;

    private long ValidatedImportVersion
    {
        get
        {
            Validate();
            return DeferredImportVersion;
        }
    }

    private int ValidatedImportExtent
    {
        get
        {
            Validate();
            return ImportExtent();
        }
    }

    /// <summary>Transfers fresh detached bytes into a writable import whose reads do not require mutable working storage.</summary>
    internal static NativeBattleEffectArena CreateOwnedRead(byte[] ownedBytes, ReadOnlySpan<bool> known, ImmutableDictionary<int, NativeBattleResourcePointer?>? pointers, long version)
    {
        ArgumentNullException.ThrowIfNull(ownedBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(version);
        if (ownedBytes.Length > Length || known.Length != ownedBytes.Length)
        {
            throw new InvalidDataException("Native detached read requires bounded bytes and matching knownness.");
        }
        ImmutableArray<NativeBattleEffectUnknownRegion>.Builder? unknown = null;
        for (int index = 0; index < known.Length;)
        {
            if (known[index])
            {
                index++;
                continue;
            }
            int start = index++;
            while (index < known.Length && !known[index])
            {
                index++;
            }
            (unknown ??= ImmutableArray.CreateBuilder<NativeBattleEffectUnknownRegion>()).Add(new(start, index - start));
        }
        NativeBattleEffectArena result = new(ownedBytes.Length == 0 ? [] : [new(0, ImmutableCollectionsMarshal.AsImmutableArray(ownedBytes))])
        {
            UnknownRegions = unknown?.ToImmutable() ?? [],
            ResourcePointers = pointers
        };
        result.Validate();
        if (version != 0)
        {
            DeferredImports.Add(result, new(version));
        }
        return result;
    }
}
