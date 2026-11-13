using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal static class NativeEffectActorTextures
{
    public static (NativeBattleGraphicsModel Model, NativeBattleEffectTextureState Textures) Allocate(NativeBattleGraphicsModel model, NativeBattleGraphicsResource resource)
    {
        return Allocate(model, resource, NativeBattleEffectTextureState.FromColdProcess(), 0);
    }

    public static (NativeBattleGraphicsModel Model, NativeBattleEffectTextureState Textures) Allocate(NativeBattleGraphicsModel model, NativeBattleGraphicsResource resource, NativeBattleEffectTextureState textures, ushort occupiedSlots)
    {
        NativeBattleModelTextureLoad body = NativeBattleModelTextures.Prepare507400(resource.BodyTexturePack ?? throw new InvalidDataException("The source model has no original body textures."), occupiedSlots);
        textures = NativeBattleModelTextures.Enqueue507400(textures, body);
        NativeBattleModelTextureBinding? linked = null;
        ushort allocation = body.AllocatedSlots;
        if (resource.LinkedGeometry is { IsEmpty: false })
        {
            NativeBattleModelTextureLoad weapon = NativeBattleModelTextures.Prepare507400(resource.LinkedTexturePack ?? throw new InvalidDataException("The linked source model has no original weapon textures."), (ushort)(occupiedSlots | allocation));
            textures = NativeBattleModelTextures.Enqueue507400(textures, weapon);
            linked = weapon.Binding;
            allocation |= weapon.AllocatedSlots;
        }
        return (model with { BodyTextureSlots = body.Binding, LinkedTextureSlots = linked, TextureAllocationMask = allocation }, textures);
    }
}
