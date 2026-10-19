namespace Memoria.FF8.Runtime;

/// <summary>B25DF0 uses the shared cinematic tick and schedulers with Ifrit's explicit command, update and lit draw services.</summary>
public static class NativeBattleIfritCallback
{
    public static NativeBattleWaterCallbackResult RunB25DF0(NativeBattleWaterExecutionFrame frame, NativeBattleScriptedCallbackInput input, NativeBattleScriptedCallbackServices services, int instructionBudget = 4096, int traversalBudget = 256)
    {
        return NativeBattleScriptedCallback.Run(frame, input, services, instructionBudget, traversalBudget);
    }
}
