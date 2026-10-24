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
        if (state.NativeCarRuntime.EntityType == 32)
        {
            EnsureDenseCarContacts(state, inputs, saved);
            return;
        }
        // Section9 clause1 creates only stored shape4 in [34,205).548CD0 skips
        // that controlled foot record and the current shape73 car;94 is skipped universally.
        if (inputs.StoryWord is < 34 or >= 205 || (inputs.ReplacementFlags & 7) != 0 || saved.Slot4.ShapeId != 73 || saved.Slot5.ShapeId != 0 || (inputs.EventFlags265 & 128) == 0)
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

    private static NativeWorldPathProjectionCertificate CarProjection(WorldState world)
    {
        //557250/558C20 write the same ordinary camera words for32/33 as for
        //normal foot. Without an admitted collision,557A90 retains this pitch.
        NativeWorldFootCameraState camera = world.NativeCarRuntime?.ProjectionCamera ?? throw new InvalidDataException("Native car projection requires its executed pre-terrain camera words.");
        if (camera.TargetX != world.CameraTargetX || camera.TargetRawZ != world.CameraTargetZ || camera.TargetNativeHeight != world.CameraTargetNativeHeight || camera.Yaw != world.CameraYaw || camera.Pitch != -112 || camera.OffsetY != -341 || camera.OffsetZ != -7168)
        {
            throw new InvalidDataException("Native car projection differs from its actual selected camera and supported no-collision loader words.");
        }
        return NativeWorldPathProjectionCertificate.CreateFoot(camera);
    }

    private void EnsureDenseCarContacts(WorldState state, WorldEntryVariables inputs, NativeSavedWorldState saved)
    {
        NativeCarWorldRuntimeState car = state.NativeCarRuntime!;
        NativeTrainWorldAllocation allocation = car.Allocation!;
        if (saved.Slot4.ShapeId != car.ShapeId || saved.Slot5.ShapeId != 0 || (inputs.ReplacementFlags & 7) != 0 || (inputs.EventFlags265 & 1) == 0)
        {
            throw new NotSupportedException("Native car32 requires its actual selected vehicle, inactive auxiliary slots and disabled rail allocation; disembark actions remain unsupported.");
        }
        NativeTrainWorldAllocation actual = assets.ReadTrainWorldAllocation(inputs.StoryWord, inputs.EventFlags265);
        if (actual.EventFlags103 != allocation.EventFlags103 || actual.Story != allocation.Story || actual.ProgramHash != allocation.ProgramHash || actual.CoordinateHash != allocation.CoordinateHash || !actual.ClauseWords.SequenceEqual(allocation.ClauseWords) || actual.Objects.Length != allocation.Objects.Length || actual.Objects.Where((item, index) => !item.CoordinateBytes.SequenceEqual(allocation.Objects[index].CoordinateBytes)).Any())
        {
            throw new InvalidDataException("Native car32 retained allocation differs from its actual original clause and coordinate records.");
        }
        NativeWorldPathProjectionCertificate context = CarProjection(state);
        NativeWorldPathWords player = context.RelativeWords(new NativeWorldPathPosition(state.X, state.NativeHeight, -state.Z));
        NativeWorldContactShape selected = assets.ReadNativeContactShape(car.ShapeId);
        foreach (int padding in new[] { 0, 200 })
        {
            WorldStationContactResult contact = WorldStationContactCertificate.Evaluate(assets, allocation, [], player, state.NativeHeight, position => context.ProjectNode(position) ?? throw new NotSupportedException(context.UnsupportedReason), context.IsNear, padding, NativeWorldTerrain.ProbeRadius(car.EntityType), selected.Height);
            if (contact.Hit)
            {
                throw new NotSupportedException("Native car32 reached an original object collision/proximity response whose body/camera effects are unsupported.");
            }
        }
    }

    private IReadOnlyList<NativeWorldStaticModelDraw> ReadCarModels(WorldState world, Vector3 position, NativeCarWorldRuntimeState car)
    {
        List<NativeWorldStaticModelDraw> models = [new NativeWorldStaticModelDraw(assets.GetCarModel(car.EntityType), new NativeWorldStaticModelPose(position, new NativeWorldPathAngles(checked((short)car.ModelTilt), unchecked((short)(world.BodyYaw - 1024)), 0))) { ObjectId = new(WorldRenderObjectKind.Controlled, 0) }];
        if (car.Allocation is not { } allocation)
        {
            return models;
        }
        EnsureCarContactCertificate(world);
        NativeWorldPathProjectionCertificate context = CarProjection(world);
        for (int index = 0; index < allocation.Objects.Length; index++)
        {
            NativeTrainWorldObject item = allocation.Objects[index];
            if (index == allocation.ControlledObjectIndex || item.ShapeId == 94 || !context.IsNear(item.Position))
            {
                continue;
            }
            NativeWorldPathNodeProjection projection = context.ProjectNode(item.Position) ?? throw new NotSupportedException(context.UnsupportedReason);
            if (projection.Visible)
            {
                models.Add(new NativeWorldStaticModelDraw(assets.ReadScriptedStaticModel(item.ShapeId), new NativeWorldStaticModelPose(context.CanonicalModelAnchor(projection), WorldStaticModelTransform.AllocationAngles(item))) { ObjectId = new(WorldRenderObjectKind.Allocation, index) });
            }
        }
        return models;
    }

    private WorldState StepCar(WorldState before, float horizontal, float vertical, out WorldFieldSelection? fieldEntry, out uint salaryMovementUnits, out int graphicsSpeed, bool mapOnly = false)
    {
        NativeCarWorldRuntimeState car = before.NativeCarRuntime!;
        car.Validate();
        fieldEntry = null;
        // Original5573C0 supplies signed steering/throttle bytes to557A90.
        // Full digital input retains127; fractional input keeps stick magnitude.
        int steering = ReadSignedMovementComponent(-horizontal);
        int turnShift = Math.Abs(YawDelta(before.BodyYaw, before.CameraYaw)) > 511 ? 3 : 2;
        int bodyYaw = WrapYaw(before.BodyYaw + (-steering >> turnShift));
        int throttle = ReadSignedMovementComponent(-vertical);
        int maximumSpeed = 64;
        if (car.EntityType == 32 && throttle != 0)
        {
            NativeEconomy economy = session.State.Economy ?? throw new NotSupportedException("Native car32 throttle requires its actual fuel inventory.");
            economy.Validate();
            if (!economy.Items.Any(item => item.ItemId == 162 && item.Count != 0) && car.MovementMagnitude20409F4 != 0)
            {
                maximumSpeed >>= 2;
            }
        }
        int speed = throttle == 0 ? Math.Abs(car.Speed) <= 4 ? 0 : car.Speed + (car.Speed > 0 ? -4 : 4) : Math.Clamp(car.Speed + throttle / 48, -maximumSpeed, maximumSpeed);
        (int moveX, int graphicsZ) = Rotate(0, speed, bodyYaw);
        graphicsSpeed = NativeWorldVectorMagnitude.Truncate(moveX, 0, graphicsZ);
        // 557A90 computes this callback's motion before applying the prior selected terrain yaw.
        bodyYaw = WrapYaw(bodyYaw + (car.GroundYaw!.Value >> 3));
        int x = WrapCoordinate(before.X + moveX, 262144);
        int z = WrapCoordinate(before.Z - graphicsZ, 196608);
        int delta = YawDelta(bodyYaw, before.CameraYaw);
        int followLimit = Math.Min(128, Math.Abs(delta >> 1));
        int follow = Math.Clamp(car.CameraFollowSpeed + (Math.Abs(delta) >= 64 ? 8 : -2), 0, followLimit);
        int cameraYaw = WrapYaw(before.CameraYaw + (delta > 0 ? follow >> 3 : -(follow >> 3)));
        if (mapOnly)
        {
            follow = car.CameraFollowSpeed;
            cameraYaw = before.CameraYaw;
        }
        int targetX = (before.X + NearWrapped(before.CameraTargetX, before.X, 262144)) >> 1;
        int targetZ = (before.Z + NearWrapped(before.CameraTargetZ, before.Z, 196608)) >> 1;
        int targetHeight = (3 * before.CameraTargetNativeHeight + before.NativeHeight - 256) >> 2;
        if (car.EntityType == 32)
        {
            car = car with { ProjectionCamera = new NativeWorldFootCameraState(before.X, before.Z, before.NativeHeight, targetX, targetZ, targetHeight, cameraYaw, -112, -341, -7168) };
        }
        WorldState candidate = before with { Frame = before.Frame + 1, X = x, Z = z, BodyYaw = bodyYaw, CameraYaw = cameraYaw, CameraTargetX = targetX, CameraTargetZ = targetZ, CameraTargetNativeHeight = targetHeight, NativeCarRuntime = car };
        if (mapOnly)
        {
            salaryMovementUnits = (uint)car.MovementMagnitude20409F4!.Value / 8;
            return candidate with { X = before.X, Z = before.Z, NativeCarRuntime = car with { Speed = speed, CameraFollowSpeed = follow } };
        }
        EnsureCarContactCertificate(candidate);
        int previousHeight = -unchecked((short)(before.NativeHeight - (before.GroundType is <= 5 ? 320 : 0)));
        // Drawing uses the prior ground pointers and lookup cache before this callback's movement query.
        (int tilt, int tiltBlend) = CarModelTilt(before, bodyYaw, car);
        int proposedMoveX = NearWrapped(x, before.X, 262144) - before.X;
        int proposedGraphicsZ = before.Z - NearWrapped(z, before.Z, 196608);
        int? selectedGroup = horizontal > 0 ? 0 : horizontal < 0 ? 1 : null;
        WorldNativeGroundResult ground = assets.ResolveNativeGround(before.X, before.Z, proposedMoveX, proposedGraphicsZ, previousHeight, selectedGroup, car.GroundCache!, replacements);
        candidate = candidate with { X = ground.X, Z = ground.Z };
        EnsureCarContactCertificate(candidate);
        WorldGround? accepted = ground.Center?.Ground;
        int height = accepted is WorldGround selected ? NativeWorldTerrain.ResolveHeight(car.EntityType, -(int)selected.Height, selected.Triangle.GroundType, before.NativeHeight).Height : before.NativeHeight;
        // Accepted53BB90 retains the unrotated raw velocity, even when a sliding candidate wins.
        salaryMovementUnits = NativeWorldSalaryMovement.AcceptedVectorUnits(ground.Blocked ? 0 : moveX, 0, ground.Blocked ? 0 : graphicsZ);
        return candidate with { NativeHeight = height, GroundType = accepted?.Triangle.GroundType ?? before.GroundType, NativeCarRuntime = car with { Speed = speed, CameraFollowSpeed = follow, TerrainTiltBlend = tiltBlend, ModelTilt = tilt + tiltBlend, GroundCache = ground.Cache, GroundYaw = ground.GroundYaw, MovementMagnitude20409F4 = ground.Blocked ? 0 : graphicsSpeed } };
    }

    private (int Tilt, int Blend) CarModelTilt(WorldState before, int bodyYaw, NativeCarWorldRuntimeState car)
    {
        (int aheadX, int aheadGraphicsZ) = Rotate(200, 0, bodyYaw - 1024);
        (int Tilt, int Blend)? result = null;
        foreach (NativeWorldGroundAlternative alternative in car.GroundCache!.Alternatives)
        {
            byte currentType = assets.ResolveNativeGroundIdentity(alternative.CurrentGround, replacements).GroundType;
            byte previousType = assets.ResolveNativeGroundIdentity(alternative.PreviousGround, replacements).GroundType;
            int blend = Math.Max(0, (currentType == previousType ? car.TerrainTiltBlend : 64) - 16);
            WorldGroundSelection? ahead = assets.LookupNativeGround(before.X + aheadX, before.Z - aheadGraphicsZ, replacements, alternative.Entries);
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
