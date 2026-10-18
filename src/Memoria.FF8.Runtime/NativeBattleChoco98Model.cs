namespace Memoria.FF8.Runtime;

public static class NativeBattleChoco98Model
{
    public static NativeBattleEffectOwnedModelStep Initialize(NativeBattleModulePoolTask task, uint taskAddress, NativeBattleChoco97ModelResources resources)
    {
        return NativeBattleChoco97Model.Initialize(task, taskAddress, resources);
    }

    public static NativeBattleChoco97ModelAdvance Advance728210(NativeBattleModulePoolTask task, NativeBattleEffectOwnedModelState state, NativeBattleChoco97ModelResources resources, NativeBattleChoco97ModelInputs inputs)
    {
        return NativeBattleChoco97Model.AdvanceProfiled(task, state, resources, inputs, NativeBattleChocoModelProfile.Choco98);
    }
}
