namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    public NativeExamTimerState? NativeExamTimer { get; private set; }

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
}
