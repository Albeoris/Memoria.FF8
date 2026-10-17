namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    public bool CanClearDiagnosticHistory => activeActionIndex is null && !synchronousFieldInitialization;

    /// <summary>Starts a fresh host observation history between operations without changing native state.</summary>
    public void ClearDiagnosticHistory()
    {
        if (!CanClearDiagnosticHistory)
        {
            throw new InvalidOperationException("Diagnostic history cannot be cleared during an active player action or field initialization.");
        }
        actionLog.Clear();
        branchCoverage.Clear();
        decisions.Clear();
        dialogueOutcomes.Clear();
        lineCrossings.Clear();
    }
}
