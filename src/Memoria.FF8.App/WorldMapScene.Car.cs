using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    private void EnsureCarContactCertificate(WorldState state)
    {
        state.NativeCarRuntime!.Validate();
        WorldEntryVariables inputs = WorldEntryVariables.Read(session.State);
        NativeSavedWorldState saved = session.State.SavedWorld!;
        // Section9 clause1 creates only stored shape4 in [34,205).548CD0 skips
        // that controlled foot record and the current shape73 car;94 is skipped universally.
        if (inputs.StoryWord is < 34 or >= 205 || (inputs.ReplacementFlags & 7) != 0 || saved.Slot4.ShapeId != 73 || saved.Slot5.ShapeId != 0 || (session.State.Variables.GetValueOrDefault(265) & 128) == 0)
        {
            throw new NotSupportedException("Native car contacts require the proved ordinary story clause, current shape73, disabled object pairs and disabled disembark control.");
        }
        WorldCamera camera = WorldCamera.Foot(state.CameraYaw, 2048) with { TargetPosition = new Vector3(state.CameraTargetX, -state.CameraTargetNativeHeight, state.CameraTargetZ) };
        double eyeX = camera.FootFrame(new Vector3(state.X, -state.NativeHeight, state.Z)).Eye.X;
        if (!WorldFootContactCertificate.ExcludesOriginalTrainRoutes(eyeX))
        {
            throw new NotSupportedException("Native car camera enters train contact proximity whose phase and collision state are unrepresented.");
        }
    }

    private WorldState StepCar(WorldState before, float horizontal, float vertical, out WorldFieldSelection? fieldEntry, out ReplayWorldNavigationStimulus inputProposal, ReplayWorldNavigationStimulus? proposal)
    {
        NativeCarWorldRuntimeState car = before.NativeCarRuntime!;
        car.Validate();
        fieldEntry = null;
        // Ordinary Move: horizontal selects right/left steering, negative vertical
        // holds native0x80 forward throttle, positive vertical holds native0x10 reverse.
        int steering = -127 * Math.Sign(horizontal);
        int turnShift = Math.Abs(YawDelta(before.BodyYaw, before.CameraYaw)) > 511 ? 3 : 2;
        int bodyYaw = WrapYaw(before.BodyYaw + (-steering >> turnShift));
        int throttle = -127 * Math.Sign(vertical);
        int speed = throttle == 0 ? Math.Abs(car.Speed) <= 4 ? 0 : car.Speed + (car.Speed > 0 ? -4 : 4) : Math.Clamp(car.Speed + throttle / 48, -64, 64);
        (int moveX, int graphicsZ) = Rotate(0, speed, bodyYaw);
        int x = proposal?.X ?? WrapCoordinate(before.X + moveX, 262144);
        int z = proposal?.Z ?? WrapCoordinate(before.Z - graphicsZ, 196608);
        inputProposal = new ReplayWorldNavigationStimulus(x, z);
        int delta = YawDelta(bodyYaw, before.CameraYaw);
        int followLimit = Math.Min(128, Math.Abs(delta >> 1));
        int follow = Math.Clamp(car.CameraFollowSpeed + (Math.Abs(delta) >= 64 ? 8 : -2), 0, followLimit);
        int cameraYaw = WrapYaw(before.CameraYaw + (delta > 0 ? follow >> 3 : -(follow >> 3)));
        int targetX = (before.X + NearWrapped(before.CameraTargetX, before.X, 262144)) >> 1;
        int targetZ = (before.Z + NearWrapped(before.CameraTargetZ, before.Z, 196608)) >> 1;
        int targetHeight = (3 * before.CameraTargetNativeHeight + before.NativeHeight - 256) >> 2;
        WorldState candidate = before with { Frame = before.Frame + 1, X = x, Z = z, BodyYaw = bodyYaw, CameraYaw = cameraYaw, CameraTargetX = targetX, CameraTargetZ = targetZ, CameraTargetNativeHeight = targetHeight };
        EnsureCarContactCertificate(candidate);
        int previousHeight = -before.NativeHeight + (before.GroundType is <= 5 ? 320 : 0);
        WorldGround? ground = assets.FindNativeGround(x, z, replacements, previousHeight, 33);
        if (ground is null || new (int X, int Z)[] { (-64, 0), (0, 64), (64, 0), (0, -64) }.Any(offset => assets.FindNativeGround(x + offset.X, z + offset.Z, replacements, previousHeight, 33) is null))
        {
            throw new NotSupportedException("Native car direct terrain probes require alternate sliding/cache state at this boundary; current world action is preserved.");
        }
        WorldGround accepted = ground.Value;
        if (accepted.Triangle.FieldEntrance)
        {
            WorldEntryVariables inputs = WorldEntryVariables.Read(session.State);
            fieldEntry = assets.FieldEntries.Select(new WorldFieldSelectionInput(x, z, inputs.StoryWord, 33), accepted.Triangle.TextureFlags);
        }
        int height = -(int)accepted.Height + (accepted.Triangle.GroundType <= 5 ? 320 : 0);
        int tiltBlend = Math.Max(0, (accepted.Triangle.GroundType == before.GroundType ? car.TerrainTiltBlend : 64) - 16);
        (int aheadX, int aheadGraphicsZ) = Rotate(200, 0, bodyYaw - 1024);
        WorldGround? ahead = assets.FindNativeGround(x + aheadX, z - aheadGraphicsZ, replacements, entityType: 33);
        int tilt = ahead is WorldGround slope && Math.Abs(height + slope.Height) < 200 ? Math.Clamp(NativeRound(Math.Atan2(-height - slope.Height, 200) * NativeAtanScale) >> 1, -128, 128) : 0;
        return candidate with { NativeHeight = height, GroundType = accepted.Triangle.GroundType, NativeCarRuntime = car with { Speed = speed, CameraFollowSpeed = follow, TerrainTiltBlend = tiltBlend, ModelTilt = tilt + tiltBlend } };
    }
}
