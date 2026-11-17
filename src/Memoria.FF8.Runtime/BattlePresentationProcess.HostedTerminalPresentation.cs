namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{

    private HostedBattleTerminal? hostedTerminal;
    private bool hostedTerminalEntered;
    private bool hostedTerminalFadePublished;
    private bool hostedTerminalFinished;

    internal bool HostedTerminalFinished => hostedTerminalFinished;

    internal NativeBattleGraphicsState PrepareHostedTerminalGraphics(HostedBattleTerminal terminal, ushort escapeFlags, PreparedHostedGuardianFormChange? guardianForm = null)
    {
        NativeBattleGraphicsState graphics = RequireNativeGraphics();
        if (!HasBattleEntry || !graphics.EntryReady || hostedFrameServices is not null || hostedTerminal is not null ||
            HostedActiveActionToken is not null || hostedCompletionInvocation is not null || graphics.NativeOutcome?.ResultByte6E7 is > 0)
        {
            throw new InvalidOperationException("Hosted terminal admission requires its entered graphics owner and a drained action.");
        }
        if (guardianForm is not null)
        {
            ValidatePreparedHostedGuardianFormChange(guardianForm);
            graphics = guardianForm.Staged.RequireNativeGraphics();
        }
        return BuildNativeTerminalGraphics(graphics, terminal.Outcome, escapeFlags);
    }

    internal void CommitHostedTerminal(HostedBattleTerminal terminal, NativeBattleGraphicsState graphics)
    {
        hostedTerminal = terminal;
        NativeGraphics = graphics;
    }

    internal void SetHostedEscapePresentationHeld(bool held)
    {
        NativeBattleGraphicsState graphics = RequireNativeGraphics();
        if (!HasBattleEntry || !graphics.EntryReady || hostedFrameServices is not null || hostedTerminal is not null)
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
            terminalScriptTasks.Tail != 255 ||
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
