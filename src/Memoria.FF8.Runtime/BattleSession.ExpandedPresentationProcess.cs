namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private ExpandedBattlePresentationProcess? expandedPresentationProcess;
    private bool dispatchingExpandedPresentation;

    internal ExpandedBattlePresentationProcess? ExpandedPresentationProcess => expandedPresentationProcess;

    internal void AttachExpandedPresentationProcess(ExpandedBattlePresentationProcess process)
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
        dispatchingExpandedPresentation = true;
        try
        {
            AdvanceNativeHostTime();
            process.Advance(this, NativeExamTimer, DispatchExpandedCore);
            NativeExamTimerState? timer = NativeExamTimer;
            NativeExamTimerState? presentedTimer = process.Timer;
            if (timer is null ? presentedTimer is not null : presentedTimer is null || !presentedTimer.HasSameClock(timer))
            {
                throw new InvalidDataException("Hosted presentation must preserve the logical battle's timer clock.");
            }
            NativeExamTimer = presentedTimer;
            ApplyExpandedHostedFrameReceipts(process.LastFrameServices!, process.LastStepActionToken);
            AdvanceExpandedPresentedVitals4B0500();
            RefreshPreparedPresentation();

            void DispatchExpandedCore()
            {
                for (int poll = 0; poll < 3; poll++)
                {
                    AdvanceExpandedPresentedVitals4B0500();
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
