using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeAnimationTests
{
    public static void Run()
    {
        DefaultAndBaseSpeed();
        SynchronousClockAndRestore();
        PartialFramesAndHold();
    }

    private static void DefaultAndBaseSpeed()
    {
        var host = Host();
        host.Execute(Request(1, new FieldAnimation(1, 2, false, false, true)));
        Equal(0, host.GetActor(0).Animation);
        Equal(8, host.GetActor(0).DefaultAnimationSpeed);
        host.AdvanceActors();
        Equal(1, host.GetActor(0).Animation);
        Equal(8, host.GetActor(0).AnimationFrame16);
        host.Execute(Request(2, new FieldAnimation(2, 4, false, false)));
        Equal(4, host.GetActor(0).AnimationSpeed);
        host.Execute(Request(3, new FieldActorProperty(FieldActorPropertyKind.AnimationSpeed, 32)));
        Equal(32, host.GetActor(0).BaseAnimationSpeed);
        // The running asynchronous clip keeps ownership until it completes.
        Equal(false, host.Execute(Request(4, new FieldAnimation(2, 2, true, false))).Completed);
        for (int tick = 0; tick < 100; tick++)
        {
            host.AdvanceActors();
        }
        host.Execute(Request(5, new FieldAnimation(2, 2, true, false)));
        Equal(16, host.GetActor(0).AnimationSpeed);
    }

    private static void SynchronousClockAndRestore()
    {
        var host = Host();
        var request = Request(1, new FieldAnimation(1, 1, true, false));
        Equal(false, host.Execute(request).Completed);
        for (int tick = 0; tick < 3; tick++)
        {
            host.AdvanceActors();
        }
        Equal(48, host.GetActor(0).AnimationFrame16);
        Equal(2, host.GetActor(0).AnimationStatus);
        // Completion is tested before the next frame increment, not when rendering reaches the last frame.
        host.AdvanceActors();
        Equal(4, host.GetActor(0).AnimationStatus);
        var restored = Host(host.Capture());
        Equal(true, restored.Execute(request).Completed);
        Equal(0, restored.GetActor(0).AnimationStatus);
        restored.AdvanceActors();
        Equal(0, restored.GetActor(0).Animation);
    }

    private static void PartialFramesAndHold()
    {
        var host = Host();
        var partial = Request(1, new FieldAnimation(2, 2, true, true, FirstFrame: 3, LastFrame: 20, DivideFirstFrameBySpeed: true));
        Equal(false, host.Execute(partial).Completed);
        Equal(16, host.GetActor(0).AnimationFrame16);
        Equal(10, host.GetActor(0).LastAnimationFrame);
        for (int tick = 0; tick < 25; tick++)
        {
            host.AdvanceActors();
        }
        Equal(true, host.Execute(partial).Completed);
        Equal(3, host.GetActor(0).AnimationStatus);
        Equal(160, host.GetActor(0).AnimationFrame16);
        host.Execute(Request(2, new FieldActorProperty(FieldActorPropertyKind.StopAnimation, 0)));
        Equal(10, host.GetActor(0).LastAnimationFrame);
        host.Execute(Request(3, new FieldAnimation(0, 1, false, false, true)));
        Equal(0, host.GetActor(0).AnimationStatus);
    }

    private static Ff7FieldHost Host(Ff7FieldProcess? process = null)
    {
        // This test exercises only restored actor animation state; unrelated resource sections are never accessed.
        var field = new Ff7FieldResources("animation-fixture", null!, null!, null!, [], [], null!, null!, null!);
        process ??= new Ff7FieldProcess { PlayerLocked = true, Actors = [new Ff7Actor { Entity = 0, Slot = 0, Model = 0 }] };
        return new Ff7FieldHost(field, (_, animation) => animation == 2 ? 30 : 4, process);
    }
    private static FieldHostRequest Request(long id, FieldCommand command) => new(id, 0, 7, 16, command);
    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new Exception($"Expected {expected}, got {actual}.");
        }
    }
}
