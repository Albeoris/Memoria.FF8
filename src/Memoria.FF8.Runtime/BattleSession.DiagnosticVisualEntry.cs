namespace Memoria.FF8.Runtime;

public sealed record BattleDiagnosticVisualEntryBoundary(byte ActorIndex, byte Command, ushort Action, ushort Effect, long Callback);

public sealed partial class BattleSession
{
    private sealed record DiagnosticVisualEntry(byte Actor, byte Command, ushort Action, ushort Effect);
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
        diagnosticVisualEntry = new(checked((byte)actor.NativeIndex), command, action, effect);
        diagnosticVisualEntryAdmission = admission;
    }

    private void AdmitDiagnosticVisualEntry(NativeBattleActionHeaderSlot header)
    {
        if (!MatchesDiagnosticVisualEntry(header) || diagnosticVisualEntryAdmission is not { } admission)
        {
            return;
        }
        diagnosticVisualEntryAdmission = null;
        DiagnosticVisualEntryBoundary = new(header.ReadByte(0), header.ReadByte(1), NativeBattleActionGraphics.Word(header, 4), NativeBattleActionGraphics.Word(header, 6), NativeHostCallbacks ?? Frame);
        admission(DiagnosticVisualEntryBoundary);
    }

    private bool MatchesDiagnosticVisualEntry(NativeBattleActionHeaderSlot header)
    {
        return diagnosticVisualEntry is { } entry && header.ReadByte(0) == entry.Actor && header.ReadByte(1) == entry.Command && NativeBattleActionGraphics.Word(header, 4) == entry.Action && NativeBattleActionGraphics.Word(header, 6) == entry.Effect;
    }
}
