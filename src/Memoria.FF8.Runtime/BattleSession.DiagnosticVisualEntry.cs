namespace Memoria.FF8.Runtime;

public sealed record BattleDiagnosticVisualEntryBoundary(byte ActorIndex, byte Command, ushort Action, ushort Effect, long Callback);

public sealed partial class BattleSession
{
    private sealed record DiagnosticVisualEntry(BattleModelOwner Actor, byte OriginalOperand, byte Command, ushort Action, ushort Effect);
    private DiagnosticVisualEntry? diagnosticVisualEntry;
    private Action<BattleDiagnosticVisualEntryBoundary>? diagnosticVisualEntryAdmission;
    public BattleDiagnosticVisualEntryBoundary? DiagnosticVisualEntryBoundary { get; private set; }

    /// <summary>Arms a single native casting entry boundary after real initialization and before invoking its effect.</summary>
    public void ArmDiagnosticVisualEntry(string actorId, byte command, ushort action, ushort effect, Action<BattleDiagnosticVisualEntryBoundary> admission)
    {
        ArgumentNullException.ThrowIfNull(admission);
        if (diagnosticVisualEntry is not null || ActiveAction is not null || actions.Count != 0 || effect == 0)
        {
            throw new InvalidOperationException("Visual entry must be armed once before its diagnostic action submission.");
        }
        BattleCombatant actor = GetActor(actorId);
        diagnosticVisualEntry = new(CreatePresentationModelOwner(actor), checked((byte)actor.RequireNativeActorIndex()), command, action, effect);
        diagnosticVisualEntryAdmission = admission;
    }

    private void AdmitDiagnosticVisualEntry(NativeBattleActionHeaderSlot header)
    {
        if (diagnosticVisualEntry is not { } entry || !MatchesDiagnosticVisualEntry(header) || diagnosticVisualEntryAdmission is not { } admission)
        {
            return;
        }
        diagnosticVisualEntryAdmission = null;
        DiagnosticVisualEntryBoundary = new(entry.OriginalOperand, header.Command, header.Action, header.Effect, NativeHostCallbacks ?? Frame);
        admission(DiagnosticVisualEntryBoundary);
    }

    private bool MatchesDiagnosticVisualEntry(NativeBattleActionHeaderSlot header)
    {
        return diagnosticVisualEntry is { } entry && header.Source == entry.Actor && header.Command == entry.Command && header.Action == entry.Action && header.Effect == entry.Effect;
    }
}
