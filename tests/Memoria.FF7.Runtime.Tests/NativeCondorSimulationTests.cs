using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeCondorSimulationTests
{
    internal static void Run()
    {
        byte[] bytes = Data();
        byte[] original = bytes.ToArray();
        Ff7CondorResources resources = Ff7CondorResources.Read(bytes);
        FieldHostRequest request = new(1, 11, 5, 0x1291, new FieldMinigameCommand(new(356, 9, 114, 17, 128), 3));
        Ff7CondorRuntime runtime = new(resources, request, 6, 12345, 7);
        Ff7CondorSnapshot initial = runtime.Capture();
        string initialJson = JsonSerializer.Serialize(initial);
        for (int frame = 0; frame < 126; frame++)
        {
            runtime.Advance(0x40, 0x40);
        }
        Check(runtime.Phase == Ff7CondorPhase.Deployment && runtime.Capture().Tick == 126, "Introduction timing or a prepaid start press skipped the native deployment boundary.");
        runtime.Advance(0x40, 0);
        Check(runtime.Phase == Ff7CondorPhase.Deployment, "Held input was incorrectly converted into a new native press.");
        runtime.Advance(0x40, 0x40);
        Check(runtime.Phase == Ff7CondorPhase.StartConfirmation && runtime.Frame.MenuSelection == 16, "The native start-menu input did not preserve its default selection.");
        Ff7CondorRuntime unsupportedCancel = new(resources, request, 6, 12345, 7, runtime.Capture());
        Expect<NotSupportedException>(() => unsupportedCancel.Advance(0x20, 0x20));
        Check(runtime.Phase == Ff7CondorPhase.StartConfirmation, "Checking an unsupported cancel branch changed the original native start selection.");
        runtime.Advance(0x1000, 0x1000);
        runtime.Advance(0x20, 0x20);
        Check(runtime.Phase == Ff7CondorPhase.Starting, "The actual select-start-confirm sequence did not enter its native phase.");
        runtime.Advance(0, 0);
        Check(runtime.Phase == Ff7CondorPhase.CombatIntroduction, "The starting callback was collapsed into combat.");
        for (int frame = 0; frame < 173; frame++)
        {
            runtime.Advance(0, 0);
        }
        Check(runtime.Phase == Ff7CondorPhase.Combat, "The native combat introduction lost its separate timing.");
        Ff7CondorSnapshot checkpoint = runtime.Capture();
        Ff7CondorSnapshot cold = JsonSerializer.Deserialize<Ff7CondorSnapshot>(JsonSerializer.Serialize(checkpoint))!;
        Ff7CondorRuntime restored = new(resources, request, 6, 12345, 7, cold);
        for (int frame = 0; frame < 120; frame++)
        {
            runtime.Advance(0, 0);
            restored.Advance(0, 0);
            Check(JsonSerializer.Serialize(runtime.Capture()) == JsonSerializer.Serialize(restored.Capture()), "Cold restoration changed native phase, clocks, funds or RNG advancement.");
        }
        Check(!runtime.Finished && runtime.Frame.Enemies.Count == 0 && runtime.Funds == 12345 && runtime.RandomSeed == 7, "A future native enemy wave fabricated an early spawn, outcome, funds or RNG use.");
        Check(initialJson == JsonSerializer.Serialize(initial) && bytes.SequenceEqual(original), "Simulation mutated a retained snapshot or native resource source.");
        CheckOutcome(resources, request, checkpoint);
        CheckGeometryAndUnit(resources);
        Expect<NotSupportedException>(() => new Ff7CondorRuntime(resources, request, 5, 0, 1));
        Expect<NotSupportedException>(() => new Ff7CondorRuntime(resources, request with { Command = new FieldMinigameCommand(new(356, 0, 0, 0, 0), 2) }, 6, 0, 1));
        Expect<InvalidDataException>(() => new Ff7CondorRuntime(resources, request, 6, 0, 1, checkpoint with { Request = request with { Id = 2 } }));
        Expect<InvalidDataException>(() => new Ff7CondorRuntime(resources, request, 6, 0, 1, checkpoint with { Tick = -1 }));
        Expect<InvalidDataException>(() => new Ff7CondorRuntime(resources, request, 6, 0, 1, checkpoint with { Phase = (Ff7CondorPhase)99 }));
        Expect<InvalidDataException>(() => new Ff7CondorRuntime(resources, request, 6, 0, 1, checkpoint with { NextEnemy = 2 }));
        Expect<InvalidDataException>(() => new Ff7CondorRuntime(resources, request, 6, 0, 1, checkpoint with { Phase = Ff7CondorPhase.Overrun }));
        Expect<InvalidDataException>(() => new Ff7CondorRuntime(resources, request, 6, 0, 1, checkpoint with { ClockFrames = 3600 }));
        Console.WriteLine("Condor native input phases, cold state, fixed-point unit timing and delayed outcome acknowledgement remain explicit.");
    }

    private static void CheckOutcome(Ff7CondorResources resources, FieldHostRequest request, Ff7CondorSnapshot checkpoint)
    {
        Ff7CondorUnitState actualOverrun = new(20, 18, 304, 416, 0, 0, 0, 0, 0, 0, 0, 0, 56, 56, -1, 1, 0);
        Ff7CondorSnapshot source = checkpoint with { Phase = Ff7CondorPhase.Overrun, Enemies = [actualOverrun], NextEnemy = 1, TextY = -32, OutcomeCounter = 0 };
        Ff7CondorRuntime overrun = new(resources, request, 6, 0, 1, source);
        overrun.Advance(0x20, 0x20);
        for (int frame = 1; frame < 96; frame++)
        {
            overrun.Advance(0, 0);
        }
        Check(overrun.Phase == Ff7CondorPhase.Overrun && overrun.Frame.CanAcknowledge, "A premature press was retained until the outcome text became acknowledgeable.");
        overrun.Advance(0x20, 0x20);
        Check(overrun.Phase == Ff7CondorPhase.Returning && !overrun.Finished, "Outcome acknowledgement skipped the separate return phase.");
        overrun.Advance(0, 0);
        Check(overrun.Finished, "The completed native return callback did not finish.");
        long tick = overrun.Capture().Tick;
        overrun.Advance(0, 0);
        Check(overrun.Capture().Tick == tick, "A retired module continued native callbacks.");
        Ff7CondorRuntime automatic = new(resources, request, 6, 0, 1, source with { TextY = 64, OutcomeCounter = 599 });
        automatic.Advance(0, 0);
        Check(automatic.Phase == Ff7CondorPhase.Overrun, "The native timeout retired at600 instead of after600.");
        automatic.Advance(0, 0);
        Check(automatic.Phase == Ff7CondorPhase.Returning, "The native601 outcome timeout was not preserved.");
    }

    private static void CheckGeometryAndUnit(Ff7CondorResources resources)
    {
        Check(Ff7CondorGeometry.Integrate(10, 0, -256) == ((short)9, (byte)0), "Negative Q8 integration changed its signed high-byte carry.");
        Check(Ff7CondorGeometry.Integrate(10, 0, -1) == ((short)9, (byte)255), "Fractional negative velocity lost its native carry.");
        Check(Ff7CondorGeometry.Integrate(short.MinValue, 0, -256) == (short.MaxValue, (byte)0), "Native position WORD wrap was replaced with clamping.");
        Check(Ff7CondorGeometry.Normalize(0, 0, -10, 0) == ((short)-256, (short)1), "Native normalization discarded the minimum minor-axis component.");
        Ff7CondorUnitState unit = Ff7CondorUnit.Spawn(resources, new(18, 18, 2, 0, 0, 0), 20, 7);
        Check(unit.Timer == 77 && unit.Interval == 56, "Native slot-dependent initial unit timer changed.");
        Ff7CondorUnitState next = Ff7CondorUnit.Advance(resources, unit);
        Check(next.Timer == 75 && next.X == unit.X && next.Y == unit.Y && unit.Timer == 77, "Native timer decrement moved early or mutated retained unit state.");
        Expect<InvalidDataException>(() => Ff7CondorGeometry.Contains(new byte[1], 0, 0));
    }

    private static byte[] Data()
    {
        byte[] data = new byte[720];
        Write(data, 0, 80);
        Write(data, 4, 32);
        for (int attack = 0; attack < 7; attack++)
        {
            Write(data, 10 + attack * 2, 56);
        }
        Write(data, 32, 34);
        data[34] = 4;
        data[35] = 2;
        data[36] = 0xFF;
        new byte[] { 18, 18, 2, 0, 0xFF, 0xFF, 0, 0, 0xFF }.CopyTo(data, 56);
        data[80 + 18 * 32 + 6] = 7;
        data[80 + 18 * 32 + 0x0C] = 56;
        return data;
    }

    private static void Write(byte[] bytes, int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), value);

    private static void Expect<T>(Action action) where T : Exception
    {
        bool rejected = false;
        try
        {
            action();
        }
        catch (T)
        {
            rejected = true;
        }
        Check(rejected, "An unsupported native owner or malformed snapshot was admitted.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
