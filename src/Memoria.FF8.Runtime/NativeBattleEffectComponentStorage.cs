namespace Memoria.FF8.Runtime;

/// <summary>Detaches native storage used by the mixed-battle host and its private action forks.</summary>
public static class NativeBattleEffectComponentStorage
{
    public static NativeBattleAuxiliaryState Fork(NativeBattleAuxiliaryState source) => NativeBattleStorageSnapshots.Fork(source);
    public static NativeBattleModuleState Fork(NativeBattleModuleState source) => NativeBattleStorageSnapshots.Fork(source);
    public static NativeBattleEffectTextureState Capture(NativeBattleEffectTextureState source) => NativeBattleStorageSnapshots.Capture(source);
    public static NativeBattleEffectTextureState Restore(NativeBattleEffectTextureState source) => NativeBattleStorageSnapshots.Restore(source);
    public static NativeBattleRenzokuken160Frame Capture(NativeBattleRenzokuken160Frame source) => NativeBattleStorageSnapshots.Capture(source);
    public static NativeBattleRenzokuken160Frame Restore(NativeBattleRenzokuken160Frame source) => NativeBattleStorageSnapshots.Restore(source);
    public static NativeBattleLogicalOrdering Capture(NativeBattleLogicalOrdering source) => NativeBattleStorageSnapshots.Capture(source);
    public static NativeBattleLogicalOrdering Restore(NativeBattleLogicalOrdering source) => NativeBattleStorageSnapshots.Restore(source);
}
