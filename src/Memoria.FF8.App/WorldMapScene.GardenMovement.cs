using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    private WorldState StepGarden(WorldState before, float horizontal, float vertical, float cameraHorizontal, int priorGraphicsSpeed, uint buttons, NativeWorldEntryMemory memory, out bool consumeGardenRequest, out WorldFieldSelection? fieldEntry, out uint salaryMovementUnits, out int graphicsSpeed)
    {
        NativeGardenWorldRuntimeState garden = before.NativeGardenRuntime!;
        garden.Validate();
        if (cameraHorizontal != 0)
        {
            throw new NotSupportedException("Ordinary Garden variant0 has no native manual camera input.");
        }
        bool lowering = garden.Transfer is { Counter: < 31 };
        if (lowering || NativeWorldEntryControl.DelayAfterCallback(before.Frame + 1, 0) > 0)
        {
            horizontal = 0;
            vertical = 0;
        }
        sbyte steering = checked((sbyte)ReadSignedMovementComponent(-horizontal));
        sbyte throttle = checked((sbyte)ReadSignedMovementComponent(-vertical));
        NativeGardenMotorResult motor = NativeGardenMotor.Step(before.BodyYaw, before.CameraYaw, garden.Speed, garden.GroundYaw, garden.CameraFollowSpeed, steering, throttle);
        if (lowering)
        {
            //557A90's nonzero script-mode branch skips vehicle auto-follow.
            motor = motor with { CameraYaw = before.CameraYaw, CameraFollowSpeed = garden.CameraFollowSpeed };
        }
        (int moveX, int graphicsZ) = NativeGardenTransform.Offset(checked((short)motor.Speed), checked((short)motor.MotionYaw));
        graphicsSpeed = NativeWorldVectorMagnitude.Truncate(moveX, 0, graphicsZ);
        int targetX = (before.X + NearWrapped(before.CameraTargetX, before.X, 262144)) >> 1;
        int targetZ = (before.Z + NearWrapped(before.CameraTargetZ, before.Z, 196608)) >> 1;
        int targetHeight = (3 * before.CameraTargetNativeHeight + before.NativeHeight - 256) >> 2;
        NativeGardenWorldObject priorBody = garden.Objects[garden.Allocation.Garden64Index];
        if (priorBody.RotationX != 0 || priorBody.RotationZ != 0)
        {
            throw new NotSupportedException("Garden ordinary motor reached a nonzero retained body pitch/roll requiring its native damping owner.");
        }
        byte priorGroundType = GardenCurrentGroundType(garden.GroundCache);
        NativeWorldProcess processOwner = session.State.WorldProcess ?? throw new NotSupportedException("Garden camera requires its current native process owner.");
        bool priorOcclusion = garden.PriorTerrainOcclusion ?? throw new NotSupportedException("Garden camera requires its preceding original terrain-render occlusion owner.");
        NativeForestCameraResult cameraStep = (processOwner with { CameraCooldown = garden.Forest.CameraCooldown }).StepOrdinaryCamera(garden.CameraPitch, priorGroundType, 48, priorOcclusion, held8: steering != 0);
        NativeWorldForestState forest = garden.Forest.AdvanceGarden(priorGroundType, priorBody, garden.CurrentGroundHeight!.Value, priorGraphicsSpeed, assets.ForestTables.Random, assets.ForestTables.Effects) with { CameraPitch = cameraStep.Pitch, CameraCooldown = cameraStep.Process.CameraCooldown };
        garden = garden with
        {
            Speed = motor.Speed,
            CameraFollowSpeed = motor.CameraFollowSpeed,
            CameraPitch = cameraStep.Pitch,
            Forest = forest,
            ProjectionCamera = new NativeWorldFootCameraState(before.X, before.Z, before.NativeHeight, targetX, targetZ, targetHeight, motor.CameraYaw, cameraStep.Pitch, garden.CameraDefaults.OffsetY, garden.CameraDefaults.OffsetZ)
        };
        WorldState cameraState = before with { BodyYaw = motor.BodyYaw, CameraYaw = motor.CameraYaw, CameraTargetX = targetX, CameraTargetZ = targetZ, CameraTargetNativeHeight = targetHeight, NativeGardenRuntime = garden };
        NativeGardenTransferAdmission admission = garden.Buttons.Evaluate(garden.TransferControls.PrepareInput(buttons, 48, before.Frame + 1, garden.Transfer, memory));
        consumeGardenRequest = admission.ConsumeScriptRequest;
        if (admission.ToggleView)
        {
            throw new NotSupportedException("Garden view toggle requires its native variant1 camera transition.");
        }
        garden = garden with { Buttons = garden.Buttons.Observe(buttons) };
        cameraState = cameraState with { NativeGardenRuntime = garden };
        if (admission.Action)
        {
            if (lowering)
            {
                throw new NotSupportedException("A new scripted Garden transfer during lowering requires its native replacement transition lifecycle.");
            }
            garden = PrepareGardenDismount(cameraState);
            cameraState = cameraState with { NativeGardenRuntime = garden };
        }
        byte scriptMode = garden.Transfer is { Counter: < 31 } ? (byte)8 : (byte)0;
        int? selectedGroup = steering < 0 ? 1 : steering > 0 ? 0 : null;
        WorldNativeGardenGroundResult ground = assets.ResolveNativeGardenGround(before.X, before.Z, checked((short)moveX), checked((short)graphicsZ), unchecked((short)before.NativeHeight), selectedGroup, garden.GroundCache, replacements, (probes, padding) => GardenContact(cameraState, probes, 48, padding), scriptMode);
        WorldNativeTerrainOcclusionResult occlusion = assets.CertifyNativeTerrainCameraOcclusion(garden.ProjectionCamera!, garden.CurrentGroundHeight!.Value, replacements);
        bool currentOcclusion = occlusion.Occluded ?? throw new NotSupportedException(occlusion.Reason);
        garden = garden with
        {
            GroundCache = ground.Cache,
            GroundYaw = checked((short)ground.GroundYaw),
            CurrentGroundHeight = ground.GroundCommitted ? ground.GroundHeight : garden.CurrentGroundHeight,
            PrimaryContact = ground.PrimaryContact,
            BroadContact = ground.BroadContact,
            PriorTerrainOcclusion = currentOcclusion,
            MovementMagnitude20409F4 = ground.PositionCommitted ? graphicsSpeed : 0
        };
        WorldState after = cameraState with
        {
            Frame = before.Frame + 1,
            X = WrapCoordinate(ground.X, 262144),
            Z = WrapCoordinate(ground.Z, 196608),
            NativeHeight = ground.BodyHeight,
            GroundType = ground.Center?.Ground.Triangle.GroundType ?? before.GroundType,
            NativeGardenRuntime = garden
        };
        garden = garden.PublishBody(new(after.X, after.NativeHeight, -after.Z), 0, unchecked((short)after.BodyYaw), 0);
        fieldEntry = null;
        if (scriptMode == 0 && ground.Center?.Ground is WorldGround entrance && entrance.Triangle.FieldEntrance)
        {
            fieldEntry = assets.FieldEntries.Select(new WorldFieldSelectionInput(after.X, after.Z, memory.StoryWord, 48), entrance.Triangle.TextureFlags);
        }
        after = after with { NativeGardenRuntime = garden };
        if (fieldEntry is null && scriptMode == 0)
        {
            after = StepGardenCactuar(after, out fieldEntry);
            garden = after.NativeGardenRuntime!;
        }
        if (garden.Transfer is { Counter: < 31 } transfer)
        {
            NativeGardenTransferStep lower = transfer.StepLowering(after.Frame, garden.Objects[garden.Allocation.Garden64Index].Position, garden.CurrentGroundHeight!.Value);
            NativeGardenWorldObject body = garden.Objects[garden.Allocation.Garden64Index] with { Position = lower.GardenPosition };
            garden = garden with { Speed = 0, Transfer = lower.State, TransferControls = garden.TransferControls.AfterLowering(), Objects = garden.Objects.SetItem(garden.Allocation.Garden64Index, body) };
            after = after with { X = body.Position.X, Z = -body.Position.Z, NativeHeight = body.Position.Y, BodyYaw = WrapYaw(body.RotationY), NativeGardenRuntime = garden };
            if (lower.Completed)
            {
                if (lower.State.DestinationEntity != garden.Objects[garden.Allocation.FootObjectIndex].EntityType || lower.State.DestinationEntity != 0)
                {
                    throw new NotSupportedException("Completed Garden transfer reached a non-foot entity requiring its actual vehicle allocation lifecycle.");
                }
                NativeWorldProcess process = GameSession.PrepareWorldProcess(session.State, after);
                NativeFootWorldRuntimeState foot = NativeFootWorldRuntimeState.CreateGardenDismount(garden, lower.State, process, null);
                NativeWorldPathPosition target = lower.State.TargetPosition;
                garden = (garden with { GroundCache = foot.GroundCache!, AnimationVariant = 0 }).PublishBody(target, 0, body.RotationY, 0, gardenControlled: false);
                after = after with { X = WrapCoordinate(target.X, 262144), Z = WrapCoordinate(-target.Z, 196608), NativeHeight = target.Y, BodyYaw = WrapYaw(body.RotationY), AnimationId = 0, AnimationCounter = 0, NativeFootRuntime = foot, NativeGardenRuntime = garden };
            }
        }
        salaryMovementUnits = NativeWorldSalaryMovement.AcceptedVectorUnits(ground.PositionCommitted ? moveX : 0, 0, ground.PositionCommitted ? graphicsZ : 0);
        return after with { NativeGardenRuntime = garden };
    }

    private byte GardenCurrentGroundType(NativeWorldGroundCacheState cache)
    {
        byte? type = null;
        foreach (NativeWorldGroundAlternative alternative in cache.Alternatives)
        {
            byte candidate = assets.ResolveNativeGroundIdentity(alternative.CurrentGround, replacements).GroundType;
            if (type is byte retained && retained != candidate)
            {
                throw new NotSupportedException("Garden current ground alternatives disagree on native wake and camera terrain type.");
            }
            type = candidate;
        }
        return type!.Value;
    }

    private int GardenContact(WorldState cameraState, IReadOnlyList<WorldNativeGardenProbe> probes, int selectedEntity, int padding, bool forceCircle = false)
    {
        NativeGardenWorldRuntimeState garden = cameraState.NativeGardenRuntime!;
        return WorldGardenContactCertificate.Evaluate(assets, garden.Allocation, garden.Objects, 48, selectedEntity, probes, GardenProjection(cameraState), padding, forceCircle);
    }

    private NativeGardenWorldRuntimeState PrepareGardenDismount(WorldState state)
    {
        NativeGardenWorldRuntimeState garden = state.NativeGardenRuntime!;
        NativeWorldPathPosition body = new(state.X, state.NativeHeight, -state.Z);
        uint flags = GardenFootCurrentPolygonFlags(garden.GroundCache);
        WorldGardenContactQuery contact = (probes, selected, padding, force) => GardenContact(state, probes, selected, padding, force);
        WorldGardenPlacementResult source = assets.ResolveNativeGardenPlacement(body, unchecked((short)state.BodyYaw), 48, 48, 0, garden.CurrentGroundHeight!.Value, flags, garden.GroundCache, replacements, contact);
        if (!source.Accepted)
        {
            return garden;
        }
        int destination = garden.Objects[garden.Allocation.FootObjectIndex].EntityType;
        WorldGardenPlacementResult target = assets.ResolveNativeGardenPlacement(body, unchecked((short)state.BodyYaw), 48, destination, 700, garden.CurrentGroundHeight.Value, flags, garden.GroundCache, replacements, contact);
        if (!target.Accepted)
        {
            int hit = target.LastContactObjectIndex;
            if (hit >= 0 && (hit == garden.Allocation.FootObjectIndex || hit == garden.Allocation.Garden64Index || hit == garden.Allocation.Garden65Index))
            {
                throw new NotSupportedException("Garden placement reached native contact-fallback boarding requiring its destination validation.");
            }
            return garden;
        }
        NativeGardenTransferState transfer = NativeGardenTransferState.Create(state.Frame + 1, garden.Allocation.Garden64Index, destination, target.Position!.Value);
        return garden with { Transfer = transfer, TransferControls = garden.TransferControls.BeginLowering() };
    }
}
