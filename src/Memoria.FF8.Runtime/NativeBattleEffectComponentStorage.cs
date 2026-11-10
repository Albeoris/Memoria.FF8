namespace Memoria.FF8.Runtime;

/// <summary>Detaches the native effect texture owner for an embedded action component.</summary>
public static class NativeBattleEffectComponentStorage
{
    public static NativeBattleEffectTextureState Capture(NativeBattleEffectTextureState source) => NativeBattleStorageSnapshots.Capture(source);
    public static NativeBattleEffectTextureState Restore(NativeBattleEffectTextureState source) => NativeBattleStorageSnapshots.Restore(source);
    public static NativeBattleOriginalEffectState Capture(NativeBattleOriginalEffectState source) => NativeBattleStorageSnapshots.Capture(source);
    public static NativeBattleOriginalEffectState Restore(NativeBattleOriginalEffectState source) => NativeBattleStorageSnapshots.Restore(source);
}
