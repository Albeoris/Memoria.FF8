using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7Session
{
    public Ff7ClockState Clock => Progress.Clock ?? Ff7NativeClock.RestoreRecorded(Machine.Variables);

    private bool CountUp => World is { } world ? !world.HasEncounterCountdown && (world.ReadPersistentByte(95) & 2) != 0 : (Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 95) & 2) != 0;

    public void AdvanceClock(TimeSpan elapsed, bool paused = false)
    {
        Progress = Progress with { Clock = Ff7NativeClock.Advance(Clock, elapsed, CountUp, paused) };
        AdvanceAudioClock(elapsed, paused);
    }

    private void AdvanceNominalClockCallbacks()
    {
        Progress = Progress with { Clock = Ff7NativeClock.AdvanceCallbacks(Clock, 2, CountUp) };
        AdvanceAudioClock(TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 30));
    }
}
