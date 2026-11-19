namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    internal readonly record struct DiagnosticHostedLedger(long Actions, long Completions, long Acknowledged);

    internal DiagnosticHostedLedger ReadDiagnosticHostedLedger()
    {
        return new(lastHostedActionIssued, lastHostedCompletionIssued, lastHostedCompletionAcknowledged);
    }
}
