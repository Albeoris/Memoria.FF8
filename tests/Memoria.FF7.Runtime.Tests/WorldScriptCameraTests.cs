using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class WorldScriptCameraTests
{
    public static void Run(Ff7WorldRuntime world, Ff7WorldRuntime cold, Ff7WorldProcess ready, Ff7Installation installation)
    {
        world.Restore(ready);
        world.SetMovement(0, 0, true);
        world.SetButtons(0, 0);
        Apply(WorldOperation.CameraMode, 1);
        var profile = Ff7PcExecutableData.LoadWorldCameraProfiles(installation)[ready.ViewMode];
        Check(world.Process.ScriptCamera is { Mode: 1 } && world.Process.ScriptCamera.TargetPitch == profile.Pitch && world.Process.ScriptCamera.TargetDistance == profile.Distance, "Camera mode one must initialize the original view's target pitch and distance.");
        Apply(WorldOperation.CameraRotation, 255);
        Check(world.Process.Input!.CameraHeadingTarget == 4080 && world.Process.Camera == ready.Camera, "Rotation command31E must update the target without replacing the visible camera endpoint.");
        Apply(WorldOperation.CameraTilt, 1);
        Check(world.Process.Input!.CameraHeadingTarget == 16 && world.Process.Camera!.Heading == 16, "Camera command32F must place the current and target heading together.");
        Apply(WorldOperation.CameraRotationSpeed, -32);
        Apply(WorldOperation.CameraZoom, 10000);
        Apply(WorldOperation.CameraZoomSpeed, -20000);
        cold.Restore(JsonSerializer.Deserialize<Ff7WorldProcess>(JsonSerializer.Serialize(world.Process))!);
        foreach (Ff7WorldRuntime runtime in new[] { world, cold })
        {
            runtime.Advance();
        }
        Check(world.Process.ScriptCamera is { TargetPitch: 1920, TargetDistance: 1000 } && world.Process.Input!.CameraHeadingTarget == 4080, "Native scripted camera rates must wrap heading and clamp pitch1152..1920 and distance1000..15000.");
        Check(JsonSerializer.Serialize(world.Process) == JsonSerializer.Serialize(cold.Process), "Script camera rates and targets must cold-continue exactly.");
        Apply(WorldOperation.CameraMode, 2);
        Ff7WorldProcess frozen = world.Process;
        world.Restore(frozen with { Camera = frozen.Camera! with { GroundHeight = 12345 } });
        world.Advance();
        Check(world.Process.Camera!.GroundHeight == 12345 && world.Process.ScriptCamera == frozen.ScriptCamera, "Camera mode two must freeze ground following and scripted rates.");
        Apply(WorldOperation.CameraMode, 0);
        world.Advance();
        Check(world.Process.ScriptCamera is { TargetPitch: 0, TargetDistance: 0 }, "Camera mode zero must return pitch and distance targets to the native view profile.");
        world.Restore(ready);
        cold.Restore(ready);

        void Apply(WorldOperation operation, int value)
        {
            world.Execute(new(world.Process.Vm.NextRequestId, ready.ControlledModel, ready.ControlledModel, 0, new(operation, [value])));
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
