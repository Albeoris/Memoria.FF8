namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    private Dictionary<int, long>? diagnosticHeaderGenerations;
    private long diagnosticHeaderGeneration;

    private void ObserveDiagnosticPreparedHeader(BattleActionHeaderReference reference)
    {
        if (OriginalDiagnosticHeaderIndex(reference) is not int header)
        {
            return;
        }
        diagnosticHeaderGenerations ??= [];
        diagnosticHeaderGenerations[header] = checked(++diagnosticHeaderGeneration);
    }
}
