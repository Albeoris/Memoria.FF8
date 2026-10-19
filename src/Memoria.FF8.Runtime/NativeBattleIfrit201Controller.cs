namespace Memoria.FF8.Runtime;

/// <summary>The retained single-task bridge for the selected Ifrit cinematic callback.</summary>
public static class NativeBattleIfrit201Controller
{
    public const uint MainDescriptor = 0x2796E18;
    public const uint MainBank = 0x2796E28;
    public const uint Callback = 0xB25DF0;

    public static NativeBattleIfrit201Frame AdvanceB25DF0(NativeBattleIfrit201Frame frame, NativeBattleScriptedCallbackInput input, NativeBattleScriptedCallbackServices services, uint temporaryFrameAddress, Func<NativeBattleEffectMemory> readCurrentMemory, int instructionBudget = 4096, int traversalBudget = 256)
    {
        NativeBattleCinematicFrame result = NativeBattleCinematicController.Advance(NativeBattleCinematicProfile.ForEffect(201), new(frame.Memory, frame.Effect, frame.Random), input, services, temporaryFrameAddress, readCurrentMemory, instructionBudget, traversalBudget);
        return new(result.Memory, result.Effect, result.Random);
    }
}
