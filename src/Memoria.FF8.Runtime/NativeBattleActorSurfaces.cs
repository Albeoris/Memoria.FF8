using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public enum NativeBattleActorSurfaceKind
{
    IntrinsicHands,
    LauncherMount
}

public sealed record NativeBattleActorSurface
{
    public NativeBattleActorSurfaceKind Kind { get; }
    public int AuthoredMeshBase => ((int)Kind + 2) << 16;
    public ImmutableArray<NativeBattleRawMesh> Geometry { get; }
    public NativeBattleEffectHeapAllocation TexturePack { get; }

    [System.Text.Json.Serialization.JsonIgnore]
    public NativeBattleModelTextureLoad PreparedMaterial { get; }

    [System.Text.Json.Serialization.JsonIgnore]
    public string MaterialKey { get; }

    public NativeBattleActorSurface(NativeBattleActorSurfaceKind kind, ImmutableArray<NativeBattleRawMesh> geometry, NativeBattleEffectHeapAllocation texturePack)
    {
        if (geometry.IsDefaultOrEmpty)
        {
            throw new InvalidDataException("Actor surfaces require their recipient-owned native surfaces.");
        }
        foreach (NativeBattleRawMesh mesh in geometry)
        {
            mesh.Validate();
        }
        Kind = kind;
        Geometry = geometry;
        TexturePack = texturePack;
        PreparedMaterial = NativeBattleModelTextures.Prepare507400(texturePack, 0);
        MaterialKey = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(texturePack.Data.AsSpan()));
    }
}

public sealed partial record NativeBattleGraphicsResource
{
    public ImmutableArray<NativeBattleActorSurface> ActorSurfaces { get; init; } = [];
}

public sealed partial class BattlePresentationProcess
{
    private NativeBattleModelTextureOwner? PrepareActorSurfaceMaterials(NativeBattleGraphicsState graphics, BattleModelOwner owner, NativeBattleGraphicsResource resource)
    {
        if (resource.ActorSurfaces.IsDefaultOrEmpty)
        {
            return null;
        }
        foreach (NativeBattleActorSurface hands in resource.ActorSurfaces)
        {
            _ = PrepareActorSurfaceMaterial(owner, hands);
        }
        return SelectNativeModelMaterial(graphics, owner) ?? throw new NotSupportedException("Actor surfaces require their actor's native presentation material namespace.");
    }

    private (NativeBattleModelTextureBinding Binding, NativeBattleModelTextureOwner Material) PrepareActorSurfaceMaterial(BattleModelOwner owner, NativeBattleActorSurface hands)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new InvalidDataException("Actor surfaces require their retained native material owner.");
        NativeBattleEffectTextureState textures = memory.Textures ?? throw new InvalidDataException("Actor surfaces require the actor presentation texture banks.");
        string materialPrefix = ReadGraphicsPresentationId(owner) + "/actor-surface/" + hands.Kind + "/";
        string materialId = materialPrefix + hands.MaterialKey;
        NativeBattleModelTextureLoad upload = hands.PreparedMaterial;
        if (textures.PresentationTextures?.ContainsKey(materialId) != true)
        {
            if (textures.PresentationTextures is { } previousBanks)
            {
                textures = textures with { PresentationTextures = previousBanks.RemoveRange(previousBanks.Keys.Where(key => key.StartsWith(materialPrefix, StringComparison.Ordinal))) };
            }
            NativeBattlePcTextureWindowCache cache = memory.PrimitiveProcess?.TextureWindowCache ?? throw new InvalidDataException("Actor surfaces require an initialized native material cache.");
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
