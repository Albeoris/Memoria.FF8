namespace Memoria.FF8.Runtime;

public static class NativeBattleChoco100Aux
{
    public static NativeBattleChoco97StageFadeStep AdvanceStage(NativeBattleModulePoolTask task)
    {
        if (task.Callback is not (0x716A60 or 0x716B50))
        {
            throw new InvalidDataException("Native Choco stage requires its actual auxiliary callback.");
        }
        return NativeBattleChoco97StageFade.Advance(task);
    }

    public static NativeBattleChoco99CameraYStep Advance716300(NativeBattleModulePoolTask task, NativeBattleChoco99CameraYProgram program)
    {
        if (program.Address != 0x1529F70 || program.Values.IsDefault || program.Values.Length != 9)
        {
            throw new InvalidDataException("Native Choco view translation requires its complete original program.");
        }
        return NativeBattleChoco99Meteo.AdvanceCameraYProfiled(task, program, 0x716300);
    }
}
