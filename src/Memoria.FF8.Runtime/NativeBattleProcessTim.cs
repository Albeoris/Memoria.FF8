namespace Memoria.FF8.Runtime;

/// <summary>Late resolution of supported process-retained TIM pointers.</summary>
public static class NativeBattleProcessTim
{
    public static NativeBattleEffectHeapAllocation Resolve(NativeBattleEffectMemory memory, uint address)
    {
        if (address != 0x16146E8)
        {
            throw new NotSupportedException("Native process TIM has no supported retained physical owner.");
        }
        NativeBattleBio22Palette palette = memory.Bio22Palette ?? throw new NotSupportedException("Native process TIM requires its current retained Bio palette; explicitly migrate a needed snapshot.");
        palette.Validate();
        return new("FF8_EN:16146E8", palette.Bytes);
    }

    public static NativeBattleEffectMemory Enqueue505E30(NativeBattleEffectMemory memory, uint address)
    {
        NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native process TIM enqueue requires its retained texture queue.");
        return memory with { Textures = textures.EnqueueProcessTim505E30(address, Resolve(memory, address)).State };
    }
}
