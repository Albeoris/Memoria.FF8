namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    /// <summary>Copies bounded scalar owners only, without retaining the resource payload.</summary>
    public object ObserveDiagnosticOwners()
    {
        BattlePresentationProcess process = Presentation ?? throw new InvalidOperationException("Effect preview observations require the attached presentation process.");
        return new
        {
            LifecycleId = DiagnosticLifecycleId,
            Callback = NativeHostCallbacks,
            SimulationFrame = Frame,
            Outcome,
            Presentation = process.ObserveDiagnosticPresentationOwners()
        };
    }
}
