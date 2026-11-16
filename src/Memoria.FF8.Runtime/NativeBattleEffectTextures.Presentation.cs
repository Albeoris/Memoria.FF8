using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattlePresentationTextureBank(NativeBattleEffectTextureState Textures, NativeBattlePcTextureWindowCache Cache)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? EnemyAllocationId { get; init; }
}

public sealed partial record NativeBattleEffectTextureState
{
    private static readonly ConditionalWeakTable<NativeBattleEffectTextureState, object> validatedPresentationLeaves = new();

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableDictionary<string, NativeBattlePresentationTextureBank>? PresentationTextures { get; init; }

    public NativeBattleEffectTextureState WithPresentationTexture(string actorId, NativeBattlePresentationTextureBank bank)
    {
        RequireWorking();
        Validate();
        ValidatePresentationTexture(actorId, bank);
        ImmutableDictionary<string, NativeBattlePresentationTextureBank> banks = PresentationTextures ?? ImmutableDictionary.Create<string, NativeBattlePresentationTextureBank>(StringComparer.Ordinal);
        if (banks.TryGetValue(actorId, out NativeBattlePresentationTextureBank? current) && current.EnemyAllocationId is int allocation)
        {
            if (bank.EnemyAllocationId != allocation || !ReferenceEquals(current.Textures.VramCaptureIdentity, bank.Textures.VramCaptureIdentity))
            {
                throw new InvalidDataException("Shared enemy VRAM publication must retain its allocation and physical owner.");
            }
            if (!ReferenceEquals(current.Textures, bank.Textures))
            {
                foreach ((string member, NativeBattlePresentationTextureBank existing) in banks)
                {
                    if (existing.EnemyAllocationId == allocation)
                    {
                        banks = banks.SetItem(member, existing with { Textures = bank.Textures });
                    }
                }
            }
            return this with { PresentationTextures = banks.SetItem(actorId, bank) };
        }
        if (bank.EnemyAllocationId is not null)
        {
            throw new InvalidDataException("Enemy texture allocations can only acquire consumers through a native clone.");
        }
        object authority = bank.Textures.VramCaptureIdentity;
        if (ReferenceEquals(authority, VramCaptureIdentity) || banks.Any(entry => !StringComparer.Ordinal.Equals(actorId, entry.Key) && ReferenceEquals(authority, entry.Value.Textures.VramCaptureIdentity)))
        {
            bank = bank with { Textures = NativeBattleStorageSnapshots.Fork(bank.Textures) };
        }
        return this with { PresentationTextures = banks.SetItem(actorId, bank) };
    }

    internal NativeBattleEffectTextureState ClonePresentationEnemyTexture(string sourceId, string destinationId, int allocation)
    {
        RequireWorking();
        Validate();
        NativeBattlePresentationTextureBank source = RequirePresentationTexture(sourceId);
        NativeBattlePresentationTextureBank destination = RequirePresentationTexture(destinationId);
        if (allocation < 0 || sourceId == destinationId || destination.EnemyAllocationId is not null || source.EnemyAllocationId is int previous && previous != allocation ||
            source.EnemyAllocationId is null && PresentationTextures!.Values.Any(bank => bank.EnemyAllocationId == allocation))
        {
            throw new InvalidDataException("A shared enemy texture allocation requires its native source and an independent destination.");
        }
        return this with
        {
            PresentationTextures = PresentationTextures!
                .SetItem(sourceId, source with { EnemyAllocationId = allocation })
                .SetItem(destinationId, new(source.Textures, NativeBattlePcTextureWindowCache.Reset4647A0(destination.Cache)) { EnemyAllocationId = allocation })
        };
    }

    internal NativeBattleEffectTextureState ReleasePresentationEnemyTexture(string actorId)
    {
        RequireWorking();
        NativeBattlePresentationTextureBank bank = RequirePresentationTexture(actorId);
        if (bank.EnemyAllocationId is not int allocation)
        {
            return this;
        }
        ImmutableDictionary<string, NativeBattlePresentationTextureBank> banks = PresentationTextures!;
        NativeBattleEffectTextureState textures = bank.Textures;
        if (banks.Any(entry => entry.Key != actorId && entry.Value.EnemyAllocationId == allocation))
        {
            // Already issued commands can retain the retired actor's published image.
            textures = NativeBattleStorageSnapshots.Fork(textures with { Count = 0 });
        }
        return this with
        {
            PresentationTextures = banks.SetItem(actorId, bank with { Textures = textures, EnemyAllocationId = null })
        };
    }

    public NativeBattlePresentationTextureBank RequirePresentationTexture(string actorId)
    {
        ValidatePresentationTextureActorId(actorId);
        if (PresentationTextures is not { } banks)
        {
            throw new InvalidDataException("The actor has no bound presentation texture bank.");
        }
        ValidatePresentationTextureComparer(banks);
        if (!banks.TryGetValue(actorId, out NativeBattlePresentationTextureBank? bank))
        {
            throw new InvalidDataException("The actor has no bound presentation texture bank.");
        }
        ValidatePresentationTexture(actorId, bank);
        return bank!;
    }

    private void ValidatePresentationTextures()
    {
        if (PresentationTextures is not { } banks)
        {
            return;
        }
        ValidatePresentationTextureComparer(banks);
        HashSet<object> authorities = new(ReferenceEqualityComparer.Instance) { VramCaptureIdentity };
        Dictionary<int, NativeBattleEffectTextureState>? shared = null;
        foreach ((string actorId, NativeBattlePresentationTextureBank bank) in banks)
        {
            ValidatePresentationTexture(actorId, bank);
            if (bank.EnemyAllocationId is int allocation)
            {
                shared ??= new();
                if (shared.TryGetValue(allocation, out NativeBattleEffectTextureState? current))
                {
                    if (!ReferenceEquals(current, bank.Textures))
                    {
                        throw new InvalidDataException("Enemy texture consumers must share one texture publication.");
                    }
                    continue;
                }
                shared.Add(allocation, bank.Textures);
            }
            if (!authorities.Add(bank.Textures.VramCaptureIdentity))
            {
                throw new InvalidDataException("Global VRAM and each actor presentation texture bank require distinct VRAM owners.");
            }
        }
    }

    private void ValidatePresentationTexture(string actorId, NativeBattlePresentationTextureBank? bank)
    {
        ValidatePresentationTextureActorId(actorId);
        if (bank is not { Textures: { } textures, Cache: { } cache } || textures.PresentationTextures is not null || textures.textureSnapshot != textureSnapshot || bank.EnemyAllocationId is < 0)
        {
            throw new InvalidDataException("A presentation texture bank requires a texture leaf and cache with matching storage ownership.");
        }
        // A leaf record captures its immutable metadata and a fixed VRAM publication.
        // Any new record still validates independently, including one sharing its VRAM owner.
        _ = validatedPresentationLeaves.GetValue(textures, static leaf =>
        {
            leaf.Validate();
            return new object();
        });
        cache.Validate();
    }

    private static void ValidatePresentationTextureActorId(string actorId)
    {
        if (string.IsNullOrWhiteSpace(actorId))
        {
            throw new InvalidDataException("Presentation texture banks require a nonblank actor ID.");
        }
    }

    private static void ValidatePresentationTextureComparer(ImmutableDictionary<string, NativeBattlePresentationTextureBank> banks)
    {
        if (!ReferenceEquals(banks.KeyComparer, StringComparer.Ordinal) && !ReferenceEquals(banks.KeyComparer, EqualityComparer<string>.Default))
        {
            throw new InvalidDataException("Presentation texture banks require ordinal actor IDs.");
        }
    }
}
