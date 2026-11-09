using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed record Ff7ClockState(uint ElapsedSeconds = 0, uint CountdownSeconds = 0, ushort ElapsedPhase = 0, ushort CountdownPhase = 0, long CallbackRemainderTicks = 0)
{
    public void Validate()
    {
        if (CallbackRemainderTicks < 0 || CallbackRemainderTicks >= Ff7NativeClock.CallbackPeriod.Ticks)
        {
            throw new InvalidDataException("FFVII global clock callback phase is invalid.");
        }
    }
}

public static class Ff7NativeClock
{
    public static TimeSpan CallbackPeriod { get; } = TimeSpan.FromMilliseconds(17);
    private const uint PhaseIncrement = 0x444;

    public static Ff7ClockState Advance(Ff7ClockState state, TimeSpan elapsed, bool countUp, bool paused = false)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentOutOfRangeException.ThrowIfNegative(elapsed.Ticks);
        state.Validate();
        long callbacks = elapsed.Ticks / CallbackPeriod.Ticks;
        long remainder = state.CallbackRemainderTicks + elapsed.Ticks % CallbackPeriod.Ticks;
        callbacks += remainder / CallbackPeriod.Ticks;
        state = state with { CallbackRemainderTicks = remainder % CallbackPeriod.Ticks };
        return paused ? state : AdvanceCallbacks(state, callbacks, countUp);
    }

    public static Ff7ClockState AdvanceCallbacks(Ff7ClockState state, long callbacks, bool countUp)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentOutOfRangeException.ThrowIfNegative(callbacks);
        state.Validate();
        ulong elapsedPhase = checked((ulong)callbacks * PhaseIncrement + state.ElapsedPhase);
        ulong countdownPhase = checked((ulong)callbacks * PhaseIncrement + state.CountdownPhase);
        ulong elapsedSeconds = elapsedPhase >> 16;
        ulong countdownSeconds = countdownPhase >> 16;
        return state with
        {
            ElapsedSeconds = unchecked(state.ElapsedSeconds + (uint)elapsedSeconds),
            CountdownSeconds = countUp ? unchecked(state.CountdownSeconds + (uint)countdownSeconds) : countdownSeconds >= state.CountdownSeconds ? 0 : state.CountdownSeconds - (uint)countdownSeconds,
            ElapsedPhase = unchecked((ushort)elapsedPhase),
            CountdownPhase = unchecked((ushort)countdownPhase)
        };
    }

    public static Ff7ClockState RestoreRecorded(FieldVariables variables)
    {
        uint ReadSeconds(byte address) => (uint)(variables.ReadByte(FieldBank.Persistent0Byte, address) * 3600 + variables.ReadByte(FieldBank.Persistent0Byte, (byte)(address + 1)) * 60 + variables.ReadByte(FieldBank.Persistent0Byte, (byte)(address + 2)));
        return new(ReadSeconds(16), ReadSeconds(20));
    }

    public static void PublishField(Ff7ClockState state, FieldVariables variables)
    {
        Publish(state.ElapsedSeconds, 16, false);
        Publish(state.CountdownSeconds, 20, true);

        void Publish(uint seconds, byte address, bool countdown)
        {
            variables.WriteByte(FieldBank.Persistent0Byte, address, (byte)Math.Min((int)unchecked((ushort)(seconds / 3600)), 255));
            variables.WriteByte(FieldBank.Persistent0Byte, (byte)(address + 1), (byte)(seconds % 3600 / 60));
            byte current = (byte)(seconds % 60);
            byte previous = variables.ReadByte(FieldBank.Persistent0Byte, (byte)(address + 2));
            byte frame = variables.ReadByte(FieldBank.Persistent0Byte, (byte)(address + 3));
            variables.WriteByte(FieldBank.Persistent0Byte, (byte)(address + 2), current);
            byte nextFrame;
            if (countdown)
            {
                nextFrame = current != previous ? (byte)30 : frame == 0 ? (byte)0 : (byte)(frame - 1);
            }
            else
            {
                nextFrame = current != previous ? (byte)0 : unchecked((byte)(frame + 1));
            }
            variables.WriteByte(FieldBank.Persistent0Byte, (byte)(address + 3), nextFrame);
        }
    }
}
