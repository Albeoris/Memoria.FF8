namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsModel CompleteNativeLinkedWeaponTexture507BF0(NativeBattleGraphicsModel model, NativeBattleGraphicsResource resource)
    {
        byte id = model.ModelId4 ?? throw new NotSupportedException("Native507BF0/507E20 requires the actual model ID BYTE4.");
        if (id == 7)
        {
            return model;
        }
        if (resource.LinkedTexturePack is not { } pack)
        {
            throw new NotSupportedException("Native507BF0/507E20 requires the original selected weapon DAT texture pack.");
        }
        NativeBattleModelTextureCache cache = NativeModelTextureCache ?? throw new NotSupportedException("Native507BF0/507E20 requires its original eleven-row resource cache.");
        NativeBattleModelTextureCacheLoad load = cache.LoadLinkedWeapon(id, model.Index, pack);
        if (load.Upload is { } upload)
        {
            NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native507BF0 requires its process VRAM/upload owner.");
            NativeEffectMemory = memory with { Textures = NativeBattleModelTextures.Enqueue507400(memory.Textures ?? throw new NotSupportedException("Native507BF0 requires observed process VRAM and upload slots."), upload) };
        }
        NativeModelTextureCache = load.Cache;
        return model with { LinkedTextureSlots = load.Binding, LinkedTextureResourceIndex = load.ResourceIndex, LinkedTransform = id is 1 or 9 ? null : new(new(0, 0, 0), new(0, 0, 0)) };
    }
}
