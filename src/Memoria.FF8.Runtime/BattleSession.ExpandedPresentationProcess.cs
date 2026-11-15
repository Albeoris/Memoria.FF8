namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private ExpandedBattlePresentationProcess? expandedPresentationProcess;
    private bool dispatchingExpandedPresentation;

    public ExpandedBattlePresentationProcess? ExpandedPresentationProcess => expandedPresentationProcess;

    public void AttachExpandedPresentationProcess(ExpandedBattlePresentationProcess process)
    {
        ArgumentNullException.ThrowIfNull(process);
        process.RequireLogicalOwner(this);
        if (!UsesExpandedRoster || NativeGraphics is not null || expandedPresentationProcess is not null || Frame != 0 ||
            NativeHostCallbacks != 0 || ActiveAction is not null || process.Graphics.Callback != 0)
        {
            throw new InvalidOperationException("Hosted presentation can attach only before the expanded logical battle's first callback.");
        }

        InitializeExpandedPartyPresentation();
        NativeExamTimer = definition.NativeExamTimerAtEntry;
        expandedPresentationProcess = process;
    }

    private void RequireExpandedPresentationDispatchIdle()
    {
        if (dispatchingExpandedPresentation)
        {
            throw new InvalidOperationException("An expanded battle presentation frame is already advancing.");
        }
    }

    private void DispatchExpandedPresentationFrame(ExpandedBattlePresentationProcess process)
    {
        RequireExpandedPresentationDispatchIdle();
        process.RequireCompletionAcknowledged();
        dispatchingExpandedPresentation = true;
        try
        {
            AdvanceNativeHostTime();
            process.Advance(this, NativeExamTimer, DispatchExpandedCore, ApplyExpandedHostedFrameReceipts);
            NativeExamTimerState? presentedTimer = process.Timer;
            RequireSameHostedTimerClock(presentedTimer, NativeExamTimer);
            NativeExamTimer = presentedTimer;
            if (process.LastPostUiVitalsAdmitted)
            {
                AdvanceExpandedPresentedVitals4B0500();
            }
            RefreshPreparedPresentation();

            void DispatchExpandedCore()
            {
                if (process.Graphics.UiServices is { PauseDE9: false } ui && (ui.Flags942 & 0x40) != 0)
                {
                    for (int poll = 0; poll < 3; poll++)
                    {
                        AdvanceExpandedPresentedVitals4B0500();
                    }
                }
                if (process.EntryReady && Outcome == BattleOutcome.Active)
                {
                    DispatchTurnBasedFrameCore();
                }
            }
        }
        finally
        {
            dispatchingExpandedPresentation = false;
        }
    }
}
