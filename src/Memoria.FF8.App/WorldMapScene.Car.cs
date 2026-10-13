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
        // 557A90 computes this callback's motion before applying the prior selected terrain yaw.
        bodyYaw = WrapYaw(bodyYaw + (car.GroundYaw!.Value >> 3));
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
        // Drawing uses the prior ground pointers and lookup cache before this callback's movement query.
        (int tilt, int tiltBlend) = CarModelTilt(before, bodyYaw, car);
        int proposedMoveX = NearWrapped(x, before.X, 262144) - before.X;
        int proposedGraphicsZ = before.Z - NearWrapped(z, before.Z, 196608);
        int? selectedGroup = horizontal > 0 ? 1 : horizontal < 0 ? 0 : null;
        WorldCarGroundResult ground = assets.ResolveCarGround(before.X, before.Z, proposedMoveX, proposedGraphicsZ, previousHeight, selectedGroup, car.GroundCache!, replacements);
        candidate = candidate with { X = ground.X, Z = ground.Z };
        EnsureCarContactCertificate(candidate);
        WorldGround? accepted = ground.Center?.Ground;
        if (accepted is WorldGround entrance && entrance.Triangle.FieldEntrance)
        {
            WorldEntryVariables inputs = WorldEntryVariables.Read(session.State);
            fieldEntry = assets.FieldEntries.Select(new WorldFieldSelectionInput(candidate.X, candidate.Z, inputs.StoryWord, 33), entrance.Triangle.TextureFlags);
        }
        int height = accepted is WorldGround selected ? -(int)selected.Height + (selected.Triangle.GroundType <= 5 ? 320 : 0) : before.NativeHeight;
        return candidate with { NativeHeight = height, GroundType = accepted?.Triangle.GroundType ?? before.GroundType, NativeCarRuntime = car with { Speed = speed, CameraFollowSpeed = follow, TerrainTiltBlend = tiltBlend, ModelTilt = tilt + tiltBlend, GroundCache = ground.Cache, GroundYaw = ground.GroundYaw } };
    }

    private (int Tilt, int Blend) CarModelTilt(WorldState before, int bodyYaw, NativeCarWorldRuntimeState car)
    {
        (int aheadX, int aheadGraphicsZ) = Rotate(200, 0, bodyYaw - 1024);
        (int Tilt, int Blend)? result = null;
        foreach (NativeCarGroundAlternative alternative in car.GroundCache!.Alternatives)
        {
            byte currentType = assets.ResolveCarGroundIdentity(alternative.CurrentGround, replacements).GroundType;
            byte previousType = assets.ResolveCarGroundIdentity(alternative.PreviousGround, replacements).GroundType;
            int blend = Math.Max(0, (currentType == previousType ? car.TerrainTiltBlend : 64) - 16);
            WorldGroundSelection? ahead = assets.LookupCarGround(before.X + aheadX, before.Z - aheadGraphicsZ, replacements, alternative.Entries);
            int tilt = ahead is not null && (ahead.Ground.Triangle.VertexFlags & 0x40) != 0 && Math.Abs(before.NativeHeight + ahead.Ground.Height) < 200 ? Math.Clamp(NativeRound(Math.Atan2(-before.NativeHeight - ahead.Ground.Height, 200) * NativeAtanScale) >> 1, -128, 128) : 0;
            if (result is not null && result.Value != (tilt, blend))
            {
                throw new NotSupportedException("Retained native car cache alternatives disagree on model ground tilt.");
            }
            result = (tilt, blend);
        }
        return result!.Value;
    }
}
