namespace Memoria.FF8.Runtime;

public sealed partial class ExpandedBattlePresentationProcess
{
    private long terminalSequence;

    internal bool TerminalFinished => presentation.HostedTerminalFinished;

    internal sealed class PreparedTerminal(ExpandedBattlePresentationProcess owner, HostedBattleTerminal terminal, NativeBattleGraphicsState originalGraphics, NativeBattleGraphicsState graphics, BattleSession.PreparedHostedGuardianFormChange? guardianForm)
    {
        internal ExpandedBattlePresentationProcess Owner { get; } = owner;
        internal HostedBattleTerminal Terminal { get; } = terminal;
        internal NativeBattleGraphicsState OriginalGraphics { get; } = originalGraphics;
        internal NativeBattleGraphicsState Graphics { get; } = graphics;
        internal BattleSession.PreparedHostedGuardianFormChange? GuardianForm { get; } = guardianForm;
    }

    internal PreparedTerminal PrepareTerminal(BattleSession owner, BattleOutcome outcome, BattleSession.PreparedHostedGuardianFormChange? guardianForm = null)
    {
        RequireLogicalOwner(owner);
        RequireAdmissionWindow();
        if (!ReferenceEquals(owner.ExpandedPresentationProcess, this) || owner.Outcome != BattleOutcome.Active)
        {
            throw new InvalidOperationException("Hosted terminal admission requires its active attached logical owner.");
        }
        HostedBattleTerminal terminal = new(checked(terminalSequence + 1), outcome);
        return new(this, terminal, Graphics, presentation.PrepareHostedTerminalGraphics(terminal, guardianForm), guardianForm);
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
            presentation.ValidatePreparedHostedGuardianFormChange(form);
        }
    }

    internal void CommitTerminal(PreparedTerminal prepared)
    {
        if (prepared.GuardianForm is { } form)
        {
            presentation.CommitHostedGuardianFormChange(form);
        }
        presentation.CommitHostedTerminal(prepared.Terminal, prepared.Graphics);
        terminalSequence = prepared.Terminal.Token;
    }

    internal void SetEscapePresentationHeld(BattleSession owner, bool held)
    {
        RequireLogicalOwner(owner);
        RequireAdmissionWindow();
        presentation.SetHostedEscapePresentationHeld(held);
    }
}
