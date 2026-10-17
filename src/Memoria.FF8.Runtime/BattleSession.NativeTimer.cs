namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    public NativeExamTimerState? NativeExamTimer { get; private set; }

    private void InitializeNativeBattleTimer47CCB0()
    {
        if (definition.NativeGraphics is not null && definition.NativeExamTimerAtEntry is { } timer)
        {
            NativeExamTimer = timer.SetDisplayIntensity4A6CB0(0).SetDisplayEnabled4A6CC0(false);
        }
    }

    private void InitializeNativeTimerDisplay47D910()
    {
        if ((ResolvedNativeFlags & 4) != 0)
        {
            NativeExamTimerState timer = NativeExamTimer ?? throw new NotSupportedException("Native47D910 requires its current canonical timer owner; explicitly migrate the named older snapshot.");
            NativeExamTimer = timer.SetDisplayIntensity4A6CB0(4096).SetDisplayEnabled4A6CC0(true);
        }
    }

    private void AdvanceNativeTimerDisplay4A9160(NativeBattleGraphicsState graphics)
    {
        if (NativeExamTimer is not { } timer || (graphics.Flags & 4) == 0)
        {
            return;
        }
        uint count = definition.NativeGraphics!.Rules.UiPublicationDuration ?? throw new NotSupportedException("Native4A9160 requires its actual B8A3E4 UI iteration count.");
        for (uint index = 0; index < count; index++)
        {
            timer = timer.AdvanceDisplay4A6D00();
        }
        NativeExamTimer = timer;
    }
}
