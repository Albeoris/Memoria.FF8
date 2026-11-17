namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    private Dictionary<int, long>? diagnosticHeaderGenerations;
    private long diagnosticHeaderGeneration;

    private void ObserveDiagnosticPreparedHeader(BattleActionHeaderReference reference)
    {
        diagnosticHeaderGenerations ??= [];
        diagnosticHeaderGenerations[reference.Ordinal] = checked(++diagnosticHeaderGeneration);
    }
}
