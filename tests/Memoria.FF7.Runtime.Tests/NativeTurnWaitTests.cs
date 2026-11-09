using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeTurnWaitTests
{
    public static void Run()
    {
        var request = new FieldHostRequest(1, 0, 6, 16, new FieldActorProperty(FieldActorPropertyKind.TurnWait, 0));
        foreach (int mode in new[] { 0, 1, 2, 3, 4, 255 })
        {
            Ff7Actor actor = Actor() with { TurnMode = mode, TurnStep = 5, TurnSteps = 7, Direction = 37 };
            Ff7FieldHost host = Build(new() { Actors = [actor], BlinkStateInitialized = true });
            Equal(mode is 0 or 3, host.Execute(request).Completed);
            Equal(mode == 3 ? actor with { TurnMode = 0, TurnStep = 0, TurnSteps = 0 } : actor, host.GetActor(0));
        }
        Ff7FieldHost running = Build(new() { Actors = [Actor() with { TurnMode = 1, TurnStart = 0, TurnEnd = 64, TurnSteps = 2 }], BlinkStateInitialized = true });
        Equal(false, running.Execute(request).Completed);
        running.AdvanceActors();
        Ff7FieldHost cold = Build(JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(running.Capture()))!);
        for (int tick = 0; tick < 2; tick++)
        {
            Equal(false, running.Execute(request).Completed);
            Equal(false, cold.Execute(request).Completed);
            running.AdvanceActors();
            cold.AdvanceActors();
            Equal(JsonSerializer.Serialize(running.Capture()), JsonSerializer.Serialize(cold.Capture()));
        }
        Equal(3, running.GetActor(0).TurnMode);
        Equal(64, running.GetActor(0).Direction);
        Equal(true, running.Execute(request).Completed);
        Equal(true, cold.Execute(request).Completed);
        Equal(0, running.GetActor(0).TurnMode);
        Equal(0, running.GetActor(0).TurnStep);
        Equal(0, running.GetActor(0).TurnSteps);
        Equal(64, running.GetActor(0).Direction);
        Equal(JsonSerializer.Serialize(running.Capture()), JsonSerializer.Serialize(cold.Capture()));
    }

    private static Ff7Actor Actor()
    {
        return new() { Entity = 0, Slot = 0, Model = 0 };
    }

    private static Ff7FieldHost Build(Ff7FieldProcess process)
    {
        PcWalkmeshTriangle triangle = new(0, Vector3.Zero, new(0, 100, 0), new(100, 0, 0), -1, -1, -1);
        var field = new Ff7FieldResources("turn-wait-fixture", null!, null!, null!, [], [triangle], null!, null!, null!);
        return new(field, (_, _) => 4, process) { Movement = new([triangle], new short[512], new byte[129]) };
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new Exception($"Expected {expected}, got {actual}.");
        }
    }
}
