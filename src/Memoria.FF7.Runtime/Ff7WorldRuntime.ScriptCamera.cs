using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed record Ff7WorldScriptCameraState(int Mode = 0, int RotationSpeed = 0, int PitchSpeed = 0, int DistanceSpeed = 0, int TargetPitch = 0, int TargetDistance = 0);

public sealed partial class Ff7WorldRuntime
{
    private Ff7WorldScriptCameraState? scriptCamera;

    private bool ExecuteScriptCamera(WorldOperation operation, int[] arguments)
    {
        if (operation is not (WorldOperation.CameraMode or WorldOperation.CameraRotation or WorldOperation.CameraTilt or WorldOperation.CameraRotationSpeed or WorldOperation.CameraZoom or WorldOperation.CameraZoomSpeed))
        {
            return false;
        }
        scriptCamera ??= new();
        int value = arguments[0];
        switch (operation)
        {
            case WorldOperation.CameraMode:
                scriptCamera = scriptCamera with { Mode = value };
                if (value == 1)
                {
                    var profile = cameraController.Profile(viewMode);
                    scriptCamera = scriptCamera with { TargetPitch = profile.Pitch, TargetDistance = profile.Distance };
                }
                break;
            case WorldOperation.CameraRotation:
                input = input with { CameraHeadingTarget = value << 4 & 4095 };
                break;
            case WorldOperation.CameraTilt:
                int heading = value << 4 & 4095;
                input = input with { CameraHeadingTarget = heading };
                camera = camera with { Heading = heading };
                PresentationCameraEpoch++;
                break;
            case WorldOperation.CameraRotationSpeed:
                scriptCamera = scriptCamera with { RotationSpeed = value };
                break;
            case WorldOperation.CameraZoom:
                scriptCamera = scriptCamera with { PitchSpeed = value };
                break;
            case WorldOperation.CameraZoomSpeed:
                scriptCamera = scriptCamera with { DistanceSpeed = value };
                break;
        }
        return true;
    }

    private void AdvanceScriptCamera()
    {
        if (scriptCamera is not { } state || state.Mode is not (0 or 1))
        {
            return;
        }
        input = input with { CameraHeadingTarget = unchecked((input.CameraHeadingTarget ?? camera.Heading) + state.RotationSpeed) & 4095 };
        scriptCamera = state.Mode == 0
            ? state with { TargetPitch = 0, TargetDistance = 0 }
            : state with { TargetPitch = Math.Clamp(unchecked(state.TargetPitch + state.PitchSpeed), 1152, 1920), TargetDistance = Math.Clamp(unchecked(state.TargetDistance + state.DistanceSpeed), 1000, 15000) };
    }
}
