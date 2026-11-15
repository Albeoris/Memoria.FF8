namespace Memoria.FF8.Runtime;

/// <summary>The shared cinematic tick uses only its explicitly selected instruction, update and draw services.</summary>
public static class NativeBattleCinematicCallback
{
    public static NativeBattleWaterCallbackResult Run(NativeBattleWaterExecutionFrame frame, NativeBattleScriptedCallbackInput input, NativeBattleScriptedCallbackServices services, int instructionBudget = NativeBattleWaterScriptScheduler.DefaultDiagnosticInstructionBudget, int traversalBudget = NativeBattleWaterScriptScheduler.DefaultDiagnosticTraversalBudget)
    {
        return NativeBattleScriptedCallback.Run(frame, input, services, instructionBudget, traversalBudget);
    }
}
