using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeAnimationTests
{
    public static void Run()
    {
        DefaultAndBaseSpeed();
        SynchronousClockAndRestore();
        PartialFramesAndHold();
        PlayerUnlockAndDefaultOwnership();
        InstalledMovementSelectsNativeGait();
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

    private static void InstalledMovementSelectsNativeGait()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            return;
        }
        var installation = Ff7Installation.Discover(root);
        var native = Ff7PcExecutableData.Load(installation);
        foreach (string name in new[] { "md1stin", "md1_1" })
        {
            var field = Ff7FieldResources.Load(installation, name);
            using var characters = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.CharacterArchive).Path);
            var model = Ff7FieldModelResources.Load(characters, field.Models.Models[0]);
            int Length(int modelIndex, int animation) => model.Animations[Path.GetFileNameWithoutExtension(field.Models.Models[modelIndex].Animations[animation]) + ".a"].Frames.Count;
            var triangle = field.Walkmesh[0];
            int x = ((int)triangle.A.X + (int)triangle.B.X + (int)triangle.C.X) / 3;
            int y = ((int)triangle.A.Y + (int)triangle.B.Y + (int)triangle.C.Y) / 3;
            var initial = new Ff7Actor { Slot = 0, Entity = 0, Model = 0, X4096 = x * 4096, Y4096 = y * 4096, Triangle = 0, MoveSpeed = 1536 * field.Scripts.Scale / 512 };
            var process = new Ff7FieldProcess { PlayerLocked = true, Actors = [initial] };
            var host = new Ff7FieldHost(field, Length, process);
            host.Movement = new Ff7NativeMovement(field.Walkmesh, native.NativeSinCos, native.NativeArcAngles);
            var request = Request(100, new FieldActorMove(checked((short)(x + 30)), checked((short)y)));
            Equal(false, host.Execute(request).Completed);
            Equal(1, host.GetActor(0).Animation);
            Equal(1, host.GetActor(0).AnimationStatus);
            host.AdvanceActors();
            Equal(16, host.GetActor(0).AnimationFrame16);
            var restored = new Ff7FieldHost(field, Length, host.Capture());
            Equal(false, restored.Execute(request).Completed);
            Equal(16, restored.GetActor(0).AnimationFrame16);
            var run = new Ff7FieldHost(field, Length, process with { Actors = [initial with { MoveSpeed = initial.MoveSpeed + 1 }] });
            Equal(false, run.Execute(request).Completed);
            Equal(2, run.GetActor(0).Animation);
            var custom = new Ff7FieldHost(field, Length, process with { Actors = [initial with { Animation = 3, AnimationFrame16 = 32, AnimationSpeed = 8 }] });
            Equal(false, custom.Execute(Request(101, new FieldActorMove(checked((short)(x + 30)), checked((short)y), Animated: false))).Completed);
            Equal(3, custom.GetActor(0).Animation);
            Equal(32, custom.GetActor(0).AnimationFrame16);
            Equal(8, custom.GetActor(0).AnimationSpeed);
            var complete = new Ff7FieldHost(field, Length, process);
            Equal(true, complete.Execute(Request(102, new FieldActorMove(checked((short)x), checked((short)y)))).Completed);
            Equal(0, complete.GetActor(0).AnimationStatus);
            Equal(0, complete.Process.Motions.Length);
        }
    }

    private static void PlayerUnlockAndDefaultOwnership()
    {
        var field = new Ff7FieldResources("animation-fixture", null!, null!, null!, [], [], null!, null!, null!);
        var partial = new Ff7Actor { Entity = 0, Slot = 0, Model = 0, PlayerControlled = true, Animation = 3, AnimationStatus = 6, AnimationFrame16 = 69 << 4, FirstAnimationFrame = 15, LastAnimationFrame = 69, HoldAnimation = true };
        var host = new Ff7FieldHost(field, (_, animation) => animation == 3 ? 70 : 15, new() { PlayerLocked = true, Actors = [partial] });
        host.Execute(Request(20, new FieldControlCommand(FieldControlKind.PlayerLocked, 0)));
        Equal(0, host.GetActor(0).AnimationStatus);
        host.ApplyPlayerMovement(new(host.GetActor(0) with { Animation = 2 }, false, true, []));
        Equal(0, host.GetActor(0).AnimationFrame16);
        host.AdvanceActors();
        Equal(16, host.GetActor(0).AnimationFrame16);
        Equal(14, host.GetActor(0).LastAnimationFrame);
        host.Execute(Request(21, new FieldControlCommand(FieldControlKind.PlayerLocked, 1)));
        host.Execute(Request(22, new FieldAnimation(3, 1, false, false, true)));
        host.AdvanceActors();
        Equal(3, host.GetActor(0).Animation);
        Equal(16, host.GetActor(0).AnimationFrame16);
        Equal(69, host.GetActor(0).LastAnimationFrame);
        Equal(0, host.GetActor(0).AnimationStatus);
        Equal(false, host.GetActor(0).HoldAnimation);

        // Locking alone preserves an active partial owner; only unlocking clears it.
        var locked = new Ff7FieldHost(field, (_, _) => 70, new() { PlayerLocked = true, Actors = [partial with { AnimationFrame16 = 20 << 4, LastAnimationFrame = 21 }] });
        locked.AdvanceActors();
        locked.AdvanceActors();
        Equal(21 << 4, locked.GetActor(0).AnimationFrame16);
        Equal(3, locked.GetActor(0).AnimationStatus);
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
