using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattlePresentationTextureBank(NativeBattleEffectTextureState Textures, NativeBattlePcTextureWindowCache Cache);

public sealed partial record NativeBattleEffectTextureState
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableDictionary<string, NativeBattlePresentationTextureBank>? PresentationTextures { get; init; }

    public NativeBattleEffectTextureState WithPresentationTexture(string actorId, NativeBattlePresentationTextureBank bank)
    {
        RequireWorking();
        Validate();
        ValidatePresentationTexture(actorId, bank);
        ImmutableDictionary<string, NativeBattlePresentationTextureBank> banks = PresentationTextures ?? ImmutableDictionary.Create<string, NativeBattlePresentationTextureBank>(StringComparer.Ordinal);
        object authority = bank.Textures.VramCaptureIdentity;
        if (ReferenceEquals(authority, VramCaptureIdentity) || banks.Any(entry => !StringComparer.Ordinal.Equals(actorId, entry.Key) && ReferenceEquals(authority, entry.Value.Textures.VramCaptureIdentity)))
        {
            bank = bank with { Textures = NativeBattleStorageSnapshots.Fork(bank.Textures) };
        }
        return this with { PresentationTextures = banks.SetItem(actorId, bank) };
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
        foreach ((string actorId, NativeBattlePresentationTextureBank bank) in banks)
        {
            ValidatePresentationTexture(actorId, bank);
            if (!authorities.Add(bank.Textures.VramCaptureIdentity))
            {
                throw new InvalidDataException("Global VRAM and each actor presentation texture bank require distinct VRAM owners.");
            }
        }
    }

    private void ValidatePresentationTexture(string actorId, NativeBattlePresentationTextureBank? bank)
    {
        ValidatePresentationTextureActorId(actorId);
        if (bank is not { Textures: { } textures, Cache: { } cache } || textures.PresentationTextures is not null || textures.textureSnapshot != textureSnapshot)
        {
            throw new InvalidDataException("A presentation texture bank requires a texture leaf and cache with matching storage ownership.");
        }
        textures.Validate();
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
