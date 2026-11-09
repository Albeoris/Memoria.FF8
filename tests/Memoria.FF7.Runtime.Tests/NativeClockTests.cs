using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeClockTests
{
    public static void Run()
    {
        Ff7ClockState clock = new(CountdownSeconds: 10);
        clock = Ff7NativeClock.AdvanceCallbacks(clock, 60, false);
        Equal((uint)10, clock.CountdownSeconds);
        Equal((ushort)65520, clock.CountdownPhase);
        clock = Ff7NativeClock.AdvanceCallbacks(clock, 1, false);
        Equal((uint)9, clock.CountdownSeconds);
        Equal((uint)1, clock.ElapsedSeconds);
        Equal((ushort)1076, clock.CountdownPhase);
        clock = Ff7NativeClock.AdvanceCallbacks(clock, 60, false);
        Equal((uint)8, clock.CountdownSeconds);
        Equal((ushort)1060, clock.CountdownPhase);
        Ff7ClockState fractional = Ff7NativeClock.Advance(new(CountdownSeconds: 10), TimeSpan.FromMilliseconds(1028), false);
        Equal((uint)10, fractional.CountdownSeconds);
        Equal(TimeSpan.FromMilliseconds(8).Ticks, fractional.CallbackRemainderTicks);
        Ff7ClockState resumed = Ff7NativeClock.Advance(fractional, TimeSpan.FromMilliseconds(9), false);
        Equal((uint)9, resumed.CountdownSeconds);
        Ff7ClockState paused = Ff7NativeClock.Advance(fractional, TimeSpan.FromMilliseconds(1700), false, paused: true);
        Equal(fractional, paused);
        Ff7ClockState pausedFraction = Ff7NativeClock.Advance(fractional, TimeSpan.FromMilliseconds(25), false, paused: true);
        Equal(fractional.CountdownSeconds, pausedFraction.CountdownSeconds);
        Equal(fractional.CountdownPhase, pausedFraction.CountdownPhase);
        Equal(TimeSpan.FromMilliseconds(16).Ticks, pausedFraction.CallbackRemainderTicks);
        Ff7ClockState cold = JsonSerializer.Deserialize<Ff7ClockState>(JsonSerializer.Serialize(fractional))!;
        Equal(resumed, Ff7NativeClock.Advance(cold, TimeSpan.FromMilliseconds(9), false));
        Equal((uint)0, Ff7NativeClock.AdvanceCallbacks(new(CountdownSeconds: 1), 1000, false).CountdownSeconds);
        Equal((uint)0, Ff7NativeClock.AdvanceCallbacks(new(CountdownSeconds: uint.MaxValue, CountdownPhase: 65520), 1, true).CountdownSeconds);
        Equal((uint)1, Ff7NativeClock.AdvanceCallbacks(new(CountdownPhase: 65520), 1, true).CountdownSeconds);
        FieldVariables variables = new();
        Ff7NativeClock.PublishField(new(ElapsedSeconds: 3661, CountdownSeconds: 599), variables);
        Equal((byte)1, variables.ReadByte(FieldBank.Persistent0Byte, 16));
        Equal((byte)1, variables.ReadByte(FieldBank.Persistent0Byte, 17));
        Equal((byte)1, variables.ReadByte(FieldBank.Persistent0Byte, 18));
        Equal((byte)0, variables.ReadByte(FieldBank.Persistent0Byte, 19));
        Equal((byte)9, variables.ReadByte(FieldBank.Persistent0Byte, 21));
        Equal((byte)59, variables.ReadByte(FieldBank.Persistent0Byte, 22));
        Equal((byte)30, variables.ReadByte(FieldBank.Persistent0Byte, 23));
        Ff7NativeClock.PublishField(new(ElapsedSeconds: 3661, CountdownSeconds: 599), variables);
        Equal((byte)1, variables.ReadByte(FieldBank.Persistent0Byte, 19));
        Equal((byte)29, variables.ReadByte(FieldBank.Persistent0Byte, 23));
        Equal(new Ff7ClockState(3661, 599), Ff7NativeClock.RestoreRecorded(variables));
        InstalledContinuation();
    }

    private static void InstalledContinuation()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_CHECKPOINT_FILE");
        if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP original-resource clock continuation: optional resources are unset.");
            return;
        }
        CombinedSessionSave envelope;
        using (FileStream input = File.OpenRead(path))
        {
            envelope = CombinedSessionCodec.Read(input);
        }
        Ff7SessionSave saved = envelope.Ff7!;
        Ff7Installation installation = Ff7Installation.Discover(directory);
        using Ff7Session original = Ff7Session.Restore(installation, saved);
        Equal(JsonSerializer.Serialize(saved.Vm), JsonSerializer.Serialize(original.Capture().Vm));
        if (saved.Progress.Clock is null)
        {
            Equal(Ff7NativeClock.RestoreRecorded(original.Machine.Variables), original.Clock);
        }
        original.AdvanceClock(TimeSpan.FromMilliseconds(1020));
        Ff7ClockState before = original.Clock;
        original.Host.Execute(new(long.MaxValue - 17, 0, 7, 0, new FieldTimerCommand(600)));
        Equal(before with { CountdownSeconds = 600 }, original.Clock);
        original.AdvanceClock(TimeSpan.FromMilliseconds(1028));
        using Ff7Session cold = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(original.Capture()))!);
        original.AdvanceClock(TimeSpan.FromMilliseconds(9));
        cold.AdvanceClock(TimeSpan.FromMilliseconds(9));
        Equal(original.Clock, cold.Clock);
        Equal(JsonSerializer.Serialize(original.Capture().Vm), JsonSerializer.Serialize(cold.Capture().Vm));
        Equal(JsonSerializer.Serialize(saved.Progress with { Clock = original.Clock }), JsonSerializer.Serialize(original.Progress));
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new Exception($"Expected {expected}, got {actual}.");
        }
    }
}
