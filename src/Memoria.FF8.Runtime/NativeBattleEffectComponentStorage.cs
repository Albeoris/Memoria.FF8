namespace Memoria.FF8.Runtime;

/// <summary>Detaches the native effect texture owner for an embedded action component.</summary>
public static class NativeBattleEffectComponentStorage
{
    public static NativeBattleEffectTextureState Capture(NativeBattleEffectTextureState source) => NativeBattleStorageSnapshots.Capture(source);
    public static NativeBattleEffectTextureState Restore(NativeBattleEffectTextureState source) => NativeBattleStorageSnapshots.Restore(source);
    public static NativeBattleOriginalEffectState Capture(NativeBattleOriginalEffectState source) => NativeBattleStorageSnapshots.Capture(source);
    public static NativeBattleOriginalEffectState Restore(NativeBattleOriginalEffectState source) => NativeBattleStorageSnapshots.Restore(source);
    public static NativeBattleCureFrame Capture(NativeBattleCureFrame source) => NativeBattleStorageSnapshots.Capture(source);
    public static NativeBattleCureFrame Restore(NativeBattleCureFrame source) => NativeBattleStorageSnapshots.Restore(source);
    public static NativeBattleBlizzardFrame Capture(NativeBattleBlizzardFrame source) => NativeBattleStorageSnapshots.Capture(source);
    public static NativeBattleBlizzardFrame Restore(NativeBattleBlizzardFrame source) => NativeBattleStorageSnapshots.Restore(source);
    public static NativeBattleLifeFrame Capture(NativeBattleLifeFrame source) => NativeBattleStorageSnapshots.Capture(source);
    public static NativeBattleLifeFrame Restore(NativeBattleLifeFrame source) => NativeBattleStorageSnapshots.Restore(source);
    public static NativeBattleRestorationFrame Capture(NativeBattleRestorationFrame source) => NativeBattleStorageSnapshots.Capture(source);
    public static NativeBattleRestorationFrame Restore(NativeBattleRestorationFrame source) => NativeBattleStorageSnapshots.Restore(source);
    public static NativeBattleRenzokuken160Frame Capture(NativeBattleRenzokuken160Frame source) => NativeBattleStorageSnapshots.Capture(source);
    public static NativeBattleRenzokuken160Frame Restore(NativeBattleRenzokuken160Frame source) => NativeBattleStorageSnapshots.Restore(source);
    public static NativeBattleLogicalOrdering Capture(NativeBattleLogicalOrdering source) => NativeBattleStorageSnapshots.Capture(source);
    public static NativeBattleLogicalOrdering Restore(NativeBattleLogicalOrdering source) => NativeBattleStorageSnapshots.Restore(source);
}
