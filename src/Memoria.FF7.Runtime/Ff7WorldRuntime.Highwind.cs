using System.Numerics;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7WorldRuntime
{
    private void BeginHighwindFlight(int direction)
    {
        if (direction is not (1 or -1))
        {
            throw new ArgumentOutOfRangeException(nameof(direction));
        }
        vehicle ??= new([]);
        vehicle = vehicle with { FlightState = direction, FlightVelocity = 0 };
        if (direction > 0)
        {
            vehicle = vehicle with { FlightPreviousView = viewMode, FlightBasePitch = camera.Pitch, FlightBaseDistance = camera.Distance, FlightMaxHeight = controlledModel == 3 ? 4000 : 2000 };
            UpdateEntity(RequireEntity(controlledModel) with { Flags = RequireEntity(controlledModel).Flags | 128 });
        }
    }

    private void AdvanceHighwindInput(Ff7WorldEntity aircraft)
    {
        vehicle ??= new([]);
        ushort held = WorldControlHeld();
        ushort pressed = (ushort)(held & ~input.PreviousControlHeld);
        bool strafe = (held & 0x80) != 0;
        int target = input.CameraHeadingTarget ?? camera.Heading;
        if (!strafe)
        {
            int left = ((held & 4) != 0 ? 1 : 0) + ((held & 0x8000) != 0 ? 1 : 0);
            int right = ((held & 8) != 0 ? 1 : 0) + ((held & 0x2000) != 0 ? 1 : 0);
            target = (target + (right - left) * 16 * ((held & 0x5000) != 0 ? 1 : 2)) & 4095;
        }
        if ((pressed & 0x900) != 0)
        {
            radarMode = (radarMode + 1) % 3;
        }
        if ((held & 0xA00C) != 0)
        {
            WorldRandom.Next();
        }
        if ((pressed & 0x10) != 0 && mainState == 1 && aircraft.Model == 3)
        {
            Machine.StartSystem(6);
        }
        input = input with { CameraHeadingTarget = target, PreviousControlHeld = held };
        int delta = (aircraft.Model == 3 ? 240 : 120) * movementSpeedPercent / 100;
        int requestedSpeed = (held & (strafe ? 0xF000 : 0x20)) != 0 ? delta : 0;
        int speed = (vehicle.FlightSpeed * 3 + requestedSpeed) >> 2;
        int dx = strafe ? (held & 0x8000) != 0 ? -speed : (held & 0x2000) != 0 ? speed : 0 : 0;
        int dz = strafe ? (held & 0x1000) != 0 ? -speed : (held & 0x4000) != 0 ? speed : 0 : -speed;
        Vector3 step = inputMath.TransformRotation(new(dx, 0, dz), 0, -target, 0);
        int y = aircraft.Y;
        int verticalSpeed = 0;
        if (!strafe)
        {
            if (((held & 0x1000) != 0 && y > 500 && aircraft.TerrainResolved && y > aircraft.GroundHeight + 200 && (aircraft.Terrain & 31) != 27) || y > vehicle.FlightMaxHeight)
            {
                y -= 100;
                verticalSpeed = 10;
            }
            else if ((held & 0x4000) != 0 && y < vehicle.FlightMaxHeight - 100)
            {
                y += 100;
                verticalSpeed = -10;
            }
        }
        int rollTarget = (held & 0x8000) != 0 ? 128 : (held & 0x2000) != 0 ? -128 : 0;
        if ((held & 0x20) == 0)
        {
            rollTarget = 0;
        }
        int shift = (((aircraft.Flags & 1) != 0 && Math.Abs(vehicle.FlightRoll) >= 16 || rollTarget != 0) ? 2 : 0) + 3;
        int roll = (((1 << shift) - 1) * vehicle.FlightRoll + rollTarget + (1 << (shift - 1))) >> shift;
        int pitch = (vehicle.FlightPitch * 3 + verticalSpeed) >> 2;
        int bodyRoll = roll;
        if (rollTarget == 0 && Math.Abs(roll) <= 4)
        {
            roll = 0;
        }
        vehicle = vehicle with { FlightSpeed = speed, FlightRoll = roll, FlightPitch = pitch };
        aircraft = aircraft with { TargetDirection = (2048 - target + (input.FreeDirectionOffset ?? 0)) & 4095 };
        UpdateEntity(aircraft with { X = Wrap(aircraft.X + (int)step.X, 0x48000), Y = y, Z = Wrap(aircraft.Z + (int)step.Z, 0x38000), Flags = (aircraft.Flags | 128) & ~1 | ((int)step.X != 0 || (int)step.Z != 0 ? 1 : 0), TiltX = pitch + 4, TiltY = bodyRoll << 2, TiltZ = -bodyRoll << 1, ScriptDirection = null });
        camera = camera with { Roll = roll };
    }

    private void ResolveHighwindTerrain(Ff7WorldEntity aircraft)
    {
        (int Height, int Terrain)? ground = QueryTerrain(aircraft.X, aircraft.Z, aircraft.Y, aircraft.Terrain, aircraft.Model);
        if (ground is { } resolved)
        {
            aircraft = aircraft with { GroundHeight = resolved.Height, Terrain = resolved.Terrain, TerrainResolved = true, Flags = aircraft.Flags | 2 };
            if (viewMode == 3 && vehicle is not { FlightState: not 0 } && aircraft.Y <= resolved.Height + 200)
            {
                if (aircraft.Model == 3 || resolved.Height < 2000)
                {
                    aircraft = aircraft with { Y = resolved.Height + 200 };
                }
                else
                {
                    aircraft = aircraft with { X = input.PreviousX, Y = input.PreviousY, Z = input.PreviousZ, Flags = aircraft.Flags & ~1 };
                }
            }
            if ((aircraft.Flags & 128) == 0 && pendingWorldBattle is null)
            {
                int script = resolved.Terrain >> 5 & 7;
                if (script >= 3 && input.TerrainScript != script)
                {
                    Machine.StartMesh(aircraft.X >> 13, aircraft.Z >> 13, script - 3);
                }
                input = input with { TerrainScript = script >= 3 ? script : 0 };
            }
        }
        UpdateEntity(aircraft);
        AdvanceWorldModelPresentation();
        if (aircraft.Model == 3 && leaderAnimations.TryGetValue(3, out var resources))
        {
            vehicle ??= new([]);
            vehicle = vehicle with { HighwindPropellerPoseFrame = vehicle.HighwindPropellerFrame };
            int dx = WrappedDifference(aircraft.X - aircraft.PreviousX, 0x48000);
            int dz = WrappedDifference(aircraft.Z - aircraft.PreviousZ, 0x38000);
            if (dx * dx + dz * dz >= 200)
            {
                int animation = RequireEntity(3).PoseAnimation;
                int frames = resources.Models.Animations[resources.Descriptor.Animations[animation]].Frames.Count;
                int next = vehicle.HighwindPropellerFrame + 1;
                vehicle = vehicle with { HighwindPropellerFrame = next >= frames ? 0 : next };
            }
        }
    }

    private bool AdvanceHighwindFlightCamera()
    {
        if (vehicle is null || controlledModel is not (3 or 5) || viewMode != 3 && vehicle.FlightState == 0)
        {
            return false;
        }
        Ff7WorldEntity aircraft = RequireEntity(controlledModel);
        int progress = vehicle.FlightProgress;
        int state = vehicle.FlightState;
        int velocity = vehicle.FlightVelocity;
        int heading = vehicle.FlightSavedHeading;
        int distance = vehicle.FlightBaseDistance ?? camera.Distance;
        if (state != 0)
        {
            if (velocity == 0)
            {
                if (state > 0)
                {
                    SetWorldViewMode(3);
                }
                heading = input.CameraHeadingTarget ?? camera.Heading;
                if (heading >= 2048)
                {
                    heading -= 4096;
                }
            }
            velocity += progress < 128 ? 2 : -2;
            if (velocity != 0)
            {
                progress = Math.Clamp(progress + velocity, 0, 256);
                distance = 10000 - (progress * 6000 >> 8);
                int y = aircraft.GroundHeight + (((state > 0 ? vehicle.FlightMaxHeight : input.PreviousY) - aircraft.GroundHeight) * progress >> 8);
                UpdateEntity(aircraft with { Y = y });
                if (vehicle.FlightPreviousView is 0 or 1)
                {
                    input = input with { CameraHeadingTarget = progress * heading >> 8 };
                }
            }
            else
            {
                if (state < 0)
                {
                    SetWorldViewMode(vehicle.FlightPreviousView);
                    movementEnabled = false;
                    controlLock = 2;
                }
                state = 0;
            }
        }
        int profile = state < 0 && vehicle.FlightPreviousView == 0 ? 0 : viewMode;
        int? targetPitch = scriptCamera?.TargetPitch is > 0 ? scriptCamera.TargetPitch : cameraOcclusion.PitchOffset != 0 ? cameraController.Profile(profile).Pitch + cameraOcclusion.PitchOffset : null;
        Ff7WorldCameraState smoothed = cameraController.Advance(camera with { Pitch = vehicle.FlightBasePitch ?? camera.Pitch, Distance = distance }, profile, targetPitch: targetPitch, targetDistance: scriptCamera?.TargetDistance is > 0 ? scriptCamera.TargetDistance : null);
        vehicle = vehicle with { FlightState = state, FlightProgress = progress, FlightVelocity = velocity, FlightSavedHeading = heading, FlightBasePitch = smoothed.Pitch, FlightBaseDistance = smoothed.Distance };
        camera = smoothed with { Pitch = (progress * ((camera.GroundHeight >> 5) + 1750) + (256 - progress) * smoothed.Pitch) >> 8, Focal = 400 - (progress * 200 >> 8) };
        return true;
    }
}
