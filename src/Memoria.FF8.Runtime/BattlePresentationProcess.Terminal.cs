namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    private long terminalSequence;

    internal bool TerminalFinished => HostedTerminalFinished;

    internal sealed class PreparedTerminal(BattlePresentationProcess owner, HostedBattleTerminal terminal, NativeBattleGraphicsState originalGraphics, NativeBattleGraphicsState graphics, BattlePresentationProcess.PreparedHostedGuardianFormChange? guardianForm)
    {
        internal BattlePresentationProcess Owner { get; } = owner;
        internal HostedBattleTerminal Terminal { get; } = terminal;
        internal NativeBattleGraphicsState OriginalGraphics { get; } = originalGraphics;
        internal NativeBattleGraphicsState Graphics { get; } = graphics;
        internal BattlePresentationProcess.PreparedHostedGuardianFormChange? GuardianForm { get; } = guardianForm;
    }

    internal PreparedTerminal PrepareTerminal(BattleOutcome outcome, ushort escapeFlags, BattlePresentationProcess.PreparedHostedGuardianFormChange? guardianForm = null)
    {
        RequireAdmissionWindow();
        HostedBattleTerminal terminal = new(checked(terminalSequence + 1), outcome);
        return new(this, terminal, Graphics, PrepareHostedTerminalGraphics(terminal, escapeFlags, guardianForm), guardianForm);
    }

    internal void ValidateTerminal(PreparedTerminal prepared)
    {
        RequireAdmissionWindow();
        if (!ReferenceEquals(prepared.Owner, this) || !ReferenceEquals(prepared.OriginalGraphics, Graphics) || prepared.Terminal.Token != checked(terminalSequence + 1))
        {
            throw new InvalidOperationException("Prepared hosted terminal admission no longer owns its original graphics boundary.");
        }
        if (prepared.GuardianForm is { } form)
        {
            ValidatePreparedHostedGuardianFormChange(form);
        }
    }

    internal void CommitTerminal(PreparedTerminal prepared)
    {
        if (prepared.GuardianForm is { } form)
        {
            CommitHostedGuardianFormChange(form);
        }
        CommitHostedTerminal(prepared.Terminal, prepared.Graphics);
        terminalSequence = prepared.Terminal.Token;
    }

    internal void SetEscapePresentationHeld(bool held)
    {
        RequireAdmissionWindow();
        SetHostedEscapePresentationHeld(held);
    }
}
