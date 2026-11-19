using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleIntrinsicHandSurface(ImmutableArray<NativeBattleRawMesh> Geometry, NativeBattleEffectHeapAllocation TexturePack);

public sealed partial record NativeBattleGraphicsResource
{
    public NativeBattleIntrinsicHandSurface? IntrinsicHands { get; init; }
}

public sealed partial class BattlePresentationProcess
{
    private (NativeBattleModelTextureBinding Binding, NativeBattleModelTextureOwner Material) PrepareIntrinsicHandMaterial(BattleModelOwner owner, NativeBattleIntrinsicHandSurface hands)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new InvalidDataException("Intrinsic hands require their retained native material owner.");
        NativeBattleEffectTextureState textures = memory.Textures ?? throw new InvalidDataException("Intrinsic hands require the actor presentation texture banks.");
        string materialId = ReadGraphicsPresentationId(owner) + "/intrinsic-hands";
        NativeBattleModelTextureLoad upload = NativeBattleModelTextures.Prepare507400(hands.TexturePack, 0);
        if (textures.PresentationTextures?.ContainsKey(materialId) != true)
        {
            NativeBattlePcTextureWindowCache cache = memory.PrimitiveProcess?.TextureWindowCache ?? throw new InvalidDataException("Intrinsic hands require an initialized native material cache.");
            NativeBattleEffectTextureState leaf = textures with { PresentationTextures = null, Count = 0, Slots = NativeBattleEffectTextureState.FromColdProcess().Slots, Animation = null };
            leaf = NativeBattleModelTextures.Enqueue507400(leaf, upload);
            textures = textures.WithPresentationTexture(materialId, new(leaf, NativeBattlePcTextureWindowCache.Reset4647A0(cache)));
            NativeEffectMemory = DrainNativeTextureUploads(memory, textures);
        }
        return (upload.Binding, new(materialId));
    }
}
