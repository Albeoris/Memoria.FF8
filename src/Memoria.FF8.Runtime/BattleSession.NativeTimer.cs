namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    public NativeExamTimerState? NativeExamTimer { get; private set; }

    internal void CopyHostedPresentationTimerFrom(BattleSession owner)
    {
        NativeExamTimer = owner.NativeExamTimer;
    }

    internal static void RequireSameHostedTimerClock(NativeExamTimerState? observed, NativeExamTimerState? canonical)
    {
        if (canonical is null ? observed is not null : observed is null || !observed.HasSameClock(canonical))
        {
            throw new InvalidDataException("Hosted presentation must preserve the logical battle's timer clock.");
        }
    }

    private void AdvanceNativeHostTime()
    {
        if (Outcome == BattleOutcome.Active && NativeHostCallbacks is long callbacks)
        {
            NativeHostCallbacks = checked(callbacks + 1);
            if (NativeExamTimer is { } timer)
            {
                NativeExamTimer = timer.AdvanceCallbacks(NativeTimerCallbackKind.Battle, 1);
            }
        }
    }

    private void InitializeNativeBattleTimer47CCB0()
    {
        if (definition.NativeGraphics is not null && definition.NativeExamTimerAtEntry is { } timer)
        {
            NativeExamTimer = timer.SetDisplayIntensity4A6CB0(0).SetDisplayEnabled4A6CC0(false);
        }
    }
}
