namespace Memoria.FF7.Runtime;

public static class Ff7CondorAutoPlay
{
    public static ushort ChooseInput(Ff7CondorRuntime condor)
    {
        return condor.Frame switch
        {
            { Phase: Ff7CondorPhase.Deployment } => 0x40,
            { Phase: Ff7CondorPhase.StartConfirmation, MenuSelection: not 0 } => 0x1000,
            { Phase: Ff7CondorPhase.StartConfirmation, MenuSelection: 0 } => 0x20,
            { Phase: Ff7CondorPhase.Overrun, CanAcknowledge: true } => 0x20,
            _ => 0
        };
    }
}
