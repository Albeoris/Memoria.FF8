using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7Session
{
    public Ff7ClockState Clock => Progress.Clock ?? Ff7NativeClock.RestoreRecorded(Machine.Variables);

    public void AdvanceClock(TimeSpan elapsed, bool paused = false)
    {
        bool countUp = (Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 95) & 2) != 0;
        Progress = Progress with { Clock = Ff7NativeClock.Advance(Clock, elapsed, countUp, paused) };
    }

    private void AdvanceNominalClockCallbacks()
    {
        bool countUp = (Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 95) & 2) != 0;
        Progress = Progress with { Clock = Ff7NativeClock.AdvanceCallbacks(Clock, 2, countUp) };
    }
}
