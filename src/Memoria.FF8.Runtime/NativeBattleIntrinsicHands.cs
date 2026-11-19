using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleIntrinsicHandSurface
{
    public ImmutableArray<NativeBattleRawMesh> Geometry { get; }
    public NativeBattleEffectHeapAllocation TexturePack { get; }

    [System.Text.Json.Serialization.JsonIgnore]
    public NativeBattleModelTextureLoad PreparedMaterial { get; }

    [System.Text.Json.Serialization.JsonIgnore]
    public string MaterialKey { get; }

    public NativeBattleIntrinsicHandSurface(ImmutableArray<NativeBattleRawMesh> geometry, NativeBattleEffectHeapAllocation texturePack)
    {
        if (geometry.IsDefaultOrEmpty)
        {
            throw new InvalidDataException("Intrinsic hands require their recipient-owned native surfaces.");
        }
        foreach (NativeBattleRawMesh mesh in geometry)
        {
            mesh.Validate();
        }
        Geometry = geometry;
        TexturePack = texturePack;
        PreparedMaterial = NativeBattleModelTextures.Prepare507400(texturePack, 0);
        MaterialKey = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(texturePack.Data.AsSpan()));
    }
}

public sealed partial record NativeBattleGraphicsResource
{
    public NativeBattleIntrinsicHandSurface? IntrinsicHands { get; init; }
}

public sealed partial class BattlePresentationProcess
{
    private NativeBattleModelTextureOwner? PrepareIntrinsicBodyMaterials(NativeBattleGraphicsState graphics, BattleModelOwner owner, NativeBattleGraphicsResource resource)
    {
        if (resource.IntrinsicHands is not { } hands)
        {
            return null;
        }
        _ = PrepareIntrinsicHandMaterial(owner, hands);
        return SelectNativeModelMaterial(graphics, owner) ?? throw new NotSupportedException("Intrinsic hand surfaces require their actor's native presentation material namespace.");
    }

    private (NativeBattleModelTextureBinding Binding, NativeBattleModelTextureOwner Material) PrepareIntrinsicHandMaterial(BattleModelOwner owner, NativeBattleIntrinsicHandSurface hands)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new InvalidDataException("Intrinsic hands require their retained native material owner.");
        NativeBattleEffectTextureState textures = memory.Textures ?? throw new InvalidDataException("Intrinsic hands require the actor presentation texture banks.");
        string materialPrefix = ReadGraphicsPresentationId(owner) + "/intrinsic-hands/";
        string materialId = materialPrefix + hands.MaterialKey;
        NativeBattleModelTextureLoad upload = hands.PreparedMaterial;
        if (textures.PresentationTextures?.ContainsKey(materialId) != true)
        {
            if (textures.PresentationTextures is { } previousBanks)
            {
                textures = textures with { PresentationTextures = previousBanks.RemoveRange(previousBanks.Keys.Where(key => key.StartsWith(materialPrefix, StringComparison.Ordinal))) };
            }
            NativeBattlePcTextureWindowCache cache = memory.PrimitiveProcess?.TextureWindowCache ?? throw new InvalidDataException("Intrinsic hands require an initialized native material cache.");
            NativeBattleEffectTextureState leaf = textures with { PresentationTextures = null, Count = 0, Slots = NativeBattleEffectTextureState.FromColdProcess().Slots, Animation = null };
            leaf = NativeBattleModelTextures.Enqueue507400(leaf, upload);
            textures = textures.WithPresentationTexture(materialId, new(leaf, NativeBattlePcTextureWindowCache.Reset4647A0(cache)));
            NativeBattlePresentationTextureBank bank = textures.RequirePresentationTexture(materialId);
            NativeBattleEffectTextureDrain uploaded = bank.Textures.Drain505D20(memory.Arena, NativeFileStaging, address => NativeBattleProcessTim.Resolve(memory, address), memory.ScriptedEffects);
            textures = textures.WithPresentationTexture(materialId, bank with { Textures = uploaded.State });
            NativeEffectMemory = memory with { Textures = textures, Arena = uploaded.Arena ?? memory.Arena };
        }
        return (upload.Binding, new(materialId));
    }
}
