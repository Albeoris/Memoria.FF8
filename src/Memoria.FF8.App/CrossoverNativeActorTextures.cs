using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal static class CrossoverNativeActorTextures
{
    public static (NativeBattleGraphicsModel Model, NativeBattleEffectTextureState Textures) Allocate(NativeBattleGraphicsModel model, NativeBattleGraphicsResource resource)
    {
        NativeBattleModelTextureLoad body = NativeBattleModelTextures.Prepare507400(resource.BodyTexturePack ?? throw new InvalidDataException("The source model has no original body textures."), 0);
        NativeBattleEffectTextureState textures = NativeBattleModelTextures.Enqueue507400(NativeBattleEffectTextureState.FromColdProcess(), body);
        NativeBattleModelTextureBinding? linked = null;
        ushort allocation = body.AllocatedSlots;
        if (resource.LinkedGeometry is { IsEmpty: false })
        {
            NativeBattleModelTextureLoad weapon = NativeBattleModelTextures.Prepare507400(resource.LinkedTexturePack ?? throw new InvalidDataException("The linked source model has no original weapon textures."), allocation);
            textures = NativeBattleModelTextures.Enqueue507400(textures, weapon);
            linked = weapon.Binding;
            allocation |= weapon.AllocatedSlots;
        }
        return (model with { BodyTextureSlots = body.Binding, LinkedTextureSlots = linked, TextureAllocationMask = allocation }, textures);
    }
}
