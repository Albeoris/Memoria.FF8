namespace Memoria.FF8.Runtime;

internal sealed record HostedBattleTerminal(long Token, BattleOutcome Outcome);
internal sealed record NativeBattleTerminalEntered(HostedBattleTerminal Terminal) : NativeBattlePresentationReceipt;
internal sealed record NativeBattleTerminalFinished(HostedBattleTerminal Terminal) : NativeBattlePresentationReceipt;

public sealed partial class BattleSession
{
    private HostedBattleTerminal? expandedHostedTerminal;
    private bool expandedTerminalEntered;
    private bool deferExpandedHostedTerminal;
    private BattleOutcome? deferredExpandedTerminalOutcome;

    private HostedBattleTerminal? hostedTerminal;
    private bool hostedTerminalEntered;
    private bool hostedTerminalFadePublished;
    private bool hostedTerminalFinished;

    internal bool HostedTerminalFinished => hostedTerminalFinished;
    private bool HasPendingExpandedTerminal => expandedHostedTerminal is not null;
    private bool OwnsExpandedTerminal => expandedPresentationProcess is not null || deferExpandedHostedTerminal;

    private void AdmitExpandedHostedTerminal(BattleOutcome outcome)
    {
        if (deferExpandedHostedTerminal)
        {
            deferredExpandedTerminalOutcome = outcome;
            StopTurnAdvance();
            return;
        }
        ExpandedBattlePresentationProcess process = expandedPresentationProcess ?? throw new InvalidOperationException("Expanded terminal admission requires its attached presentation owner.");
        ExpandedBattlePresentationProcess.PreparedTerminal preparation = process.PrepareTerminal(this, outcome);
        process.ValidateTerminal(preparation);
        PublishExpandedHostedTerminal(process, preparation);
    }

    private void PublishExpandedHostedTerminal(ExpandedBattlePresentationProcess process, ExpandedBattlePresentationProcess.PreparedTerminal preparation)
    {
        process.CommitTerminal(preparation);
        expandedHostedTerminal = preparation.Terminal;
        StopTurnAdvance();
    }

    private void AcknowledgeHostedTerminalEntry(NativeBattleTerminalEntered receipt)
    {
        if (!ReferenceEquals(expandedHostedTerminal, receipt.Terminal) || expandedTerminalEntered || Outcome != BattleOutcome.Active)
        {
            throw new InvalidDataException("Hosted terminal entry does not match the pending canonical battle.");
        }
        foreach (BattleCombatant actor in allies)
        {
            actor.NativeCombatantFlags &= 0x7FFFFFFFu;
        }
        if (nativeBattleClock is { } clock)
        {
            nativeBattleClock = clock with { Active = 0, AtbAdmitted = 0 };
        }
        expandedTerminalEntered = true;
    }

    private void AcknowledgeHostedTerminalFinish(NativeBattleTerminalFinished receipt)
    {
        if (!ReferenceEquals(expandedHostedTerminal, receipt.Terminal) || !expandedTerminalEntered || Outcome != BattleOutcome.Active)
        {
            throw new InvalidDataException("Hosted terminal completion does not match the pending canonical battle.");
        }
        if (receipt.Terminal.Outcome == BattleOutcome.Escaped)
        {
            nativeEscape = nativeEscape with { Completed = true };
        }
        FinalizeBattleOutcome(receipt.Terminal.Outcome);
        expandedHostedTerminal = null;
    }

    internal NativeBattleGraphicsState PrepareHostedTerminalGraphics(HostedBattleTerminal terminal)
    {
        NativeBattleGraphicsState graphics = RequireNativeGraphics();
        if (!hostedBattlePresentation || !graphics.EntryReady || hostedFrameServices is not null || hostedTerminal is not null ||
            HostedActiveActionToken is not null || hostedCompletionInvocation is not null || graphics.NativeOutcome?.ResultByte6E7 is > 0)
        {
            throw new InvalidOperationException("Hosted terminal admission requires its entered graphics owner and a drained action.");
        }
        return BuildNativeTerminalGraphics(graphics, terminal.Outcome);
    }

    internal void CommitHostedTerminal(HostedBattleTerminal terminal, NativeBattleGraphicsState graphics)
    {
        hostedTerminal = terminal;
        NativeGraphics = graphics;
    }

    internal void SetHostedEscapePresentationHeld(bool held)
    {
        NativeBattleGraphicsState graphics = RequireNativeGraphics();
        if (!hostedBattlePresentation || !graphics.EntryReady || hostedFrameServices is not null || hostedTerminal is not null)
        {
            throw new InvalidOperationException("Hosted escape motion requires its ready presentation admission boundary.");
        }
        NativeGraphics = QueueNativeDescriptor(graphics, held ? (ushort)108 : (ushort)109, 0, priority: 240);
    }

    private void ReachHostedTerminalEntry()
    {
        HostedBattleTerminal terminal = hostedTerminal ?? throw new InvalidDataException("Hosted47DF60 requires its admitted terminal owner.");
        NativeBattleModelLifecycleServices services = hostedFrameServices ?? throw new InvalidOperationException("Hosted47DF60 requires its ordered frame receipts.");
        if (hostedTerminalEntered)
        {
            throw new InvalidDataException("Hosted47DF60 ran twice for one terminal owner.");
        }
        services.EmitTerminal(new NativeBattleTerminalEntered(terminal));
        hostedTerminalEntered = true;
    }

    private void ObserveHostedTerminalDrain(NativeBattleModelLifecycleServices services)
    {
        if (hostedTerminal is not { } terminal || !hostedTerminalEntered || hostedTerminalFinished)
        {
            return;
        }
        NativeBattleGraphicsState graphics = RequireNativeGraphics();
        if (graphics.NativeOutcome?.FadeDurationB0C != 0 || !hostedTerminalFadePublished ||
            NativeEffectMemory?.PrimitiveProcess?.ScreenFadeEB8 != 0 ||
            nativeScriptState.Tasks.Tail != 255 ||
            !graphics.Descriptors.Queue.IsEmpty ||
            graphics.Descriptors.Tasks.Any(task => (task.Flags & 1) != 0 && task.Kind != NativeBattleDescriptorTaskKind.Pump500CC0) ||
            graphics.Auxiliary.Slots.Any(task => (task.Flags & 1) != 0 && task.Kind is NativeBattleAuxiliaryKind.OutcomeFade501D10 or NativeBattleAuxiliaryKind.MusicLoad501A70) ||
            NativeFileIo?.ActiveCount != 0)
        {
            return;
        }
        services.EmitTerminal(new NativeBattleTerminalFinished(terminal));
        hostedTerminalFinished = true;
    }
}
