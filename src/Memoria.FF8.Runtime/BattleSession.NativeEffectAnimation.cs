namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private void RegisterNativeOriginalEffectAnimation(NativeBattleOriginalEffectState effect, NativeBattleEffectHeapAllocation animation, uint textureGlobal)
    {
        if ((effect.Parameters!.Flags79 & 1) != 0)
        {
            return;
        }
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native effect entry requires its retained effect memory.");
        NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native effect entry requires its texture queue.");
        textures = textures.Register5099A0(animation);
        int source = effect.ResourcePointers.Single(pointer => pointer.GlobalAddress == textureGlobal).ArenaOffset;
        textures = textures.EnqueueArenaTim505E30(memory.Arena!, source).State;
        NativeEffectMemory = memory with { Textures = textures };
        NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native effect entry requires its camera process.")).RegisterControlResource(textures.Animation);
    }
}
