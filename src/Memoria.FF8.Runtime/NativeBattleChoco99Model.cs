namespace Memoria.FF8.Runtime;

public static class NativeBattleChoco99Model
{
    public static NativeBattleEffectOwnedModelStep Initialize(NativeBattleModulePoolTask task, uint taskAddress, NativeBattleChoco97ModelResources resources) => NativeBattleChoco97Model.Initialize(task, taskAddress, resources);

    public static NativeBattleChoco97ModelAdvance Advance71E8D0(NativeBattleModulePoolTask task, NativeBattleEffectOwnedModelState state, NativeBattleChoco97ModelResources resources, NativeBattleChoco97ModelInputs inputs) => NativeBattleChoco97Model.AdvanceProfiled(task, state, resources, inputs, NativeBattleChocoModelProfile.Choco99);
}
