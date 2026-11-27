using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    private WorldState StepRagnarok(WorldState before, int movementSpeedPercent, float cameraHorizontal, int priorGraphicsSpeed, uint buttons, NativeWorldEntryMemory memory, ref NativeWorldSalaryState salary, out bool consumeRequest, out WorldFieldSelection? fieldEntry, out uint movementUnits, out int graphicsSpeed, bool mapOnly = false, uint? motorButtons = null)
    {
        NativeRagnarokWorldRuntimeState runtime = before.NativeRagnarokRuntime!;
        runtime.RequireSceneState();
        NativeRagnarokWorldObject drawBody = runtime.Objects[runtime.Allocation.RagnarokIndex];
        byte scriptMode = runtime.Transfer is null ? salary.EventUi?.ScriptMode ?? 0 : runtime.Transfer.Kind == NativeRagnarokTransferKind.Landing ? (byte)5 : (byte)6;
        int cooldown = NativeWorldEntryControl.DelayAfterCallback(before.Frame + 1, 0);
        NativeRagnarokInputPacket input = NativeRagnarokWorldInput.ReadDigitalButtons(cooldown > 0 ? 0 : motorButtons ?? buttons);
        NativeRagnarokMotorState motorBefore = runtime.Motor with { FlightHeight = unchecked((short)before.NativeHeight) };
        NativeRagnarokMotorResult motor = scriptMode is 0 or 12 ? NativeRagnarokMotor.Step(motorBefore, input.Steering13, input.Throttle11, input.Vertical12, variant: runtime.AnimationVariant, scriptMode: scriptMode) : NativeRagnarokMotor.StepScripted(motorBefore, scriptMode, scriptMode == 5 ? 16 : 120);
        int nativeGraphicsSpeed = NativeWorldVectorMagnitude.Truncate(motor.MotionX, 0, motor.MotionGraphicsZ);
        if (scriptMode == 0)
        {
            int heightDelta = unchecked((short)(motor.State.FlightHeight - motorBefore.FlightHeight));
            motor = motor with { MotionX = ScalePlayerMovement(motor.MotionX, movementSpeedPercent), MotionGraphicsZ = ScalePlayerMovement(motor.MotionGraphicsZ, movementSpeedPercent), State = motor.State with { FlightHeight = unchecked((short)(motorBefore.FlightHeight + ScalePlayerMovement(heightDelta, movementSpeedPercent))) } };
        }
        int cameraVelocity = runtime.ManualCameraVelocity;
        if (runtime.AnimationVariant == 1 && scriptMode == 0)
        {
            (int yaw, int velocity) = NativeVehicleCamera.Step(before.CameraYaw, cameraVelocity, cooldown > 0 ? 0 : cameraHorizontal);
            cameraVelocity = velocity;
            motor = motor with { State = motor.State with { CameraYaw = checked((ushort)yaw) } };
        }
        runtime = runtime with { ManualCameraVelocity = cameraVelocity };
        NativeWorldInputHistory history = salary.InputHistory ?? throw new InvalidDataException("Ragnarok flight requires the current world input sample.");
        NativeGardenTransferAdmission admission = NativeRagnarokTransferInput.Evaluate(new(history.CurrentButtons, history.PreviousButtons, 50, 0, scriptMode, runtime.Transfer is not null, -1, cooldown, memory.ReplacementFlags264, false));
        consumeRequest = admission.ConsumeScriptRequest;
        int targetX = (before.X + NearWrapped(before.CameraTargetX, before.X, 262144)) >> 1;
        int targetZ = (before.Z + NearWrapped(before.CameraTargetZ, before.Z, 196608)) >> 1;
        bool frozenHeight = runtime.Transfer is { Kind: NativeRagnarokTransferKind.Takeoff, Counter: >= 1 and <= 41 };
        int targetHeight = frozenHeight ? before.CameraTargetNativeHeight : (3 * before.CameraTargetNativeHeight + before.NativeHeight - 256) >> 2;
        NativeWorldFootCameraState oldCamera = runtime.ProjectionCamera!;
        NativeWorldForestState forest = runtime.Forest ?? throw new NotSupportedException("Ragnarok requires its original555B20 particle/random pool owner.");
        if (!mapOnly)
        {
            // The preceding callback records the same pre-terrain height in both owners.
            int previousHeight = runtime.PreviousParticleNativeHeight ?? oldCamera.NativeHeight;
            // Both drawing and emission precede dense-body publication. Particle follow
            // consumes the preceding accepted request, not this callback's new motor input.
            NativeWorldPathPosition previousMotion = runtime.PreviousParticleMotion ?? new(0, 0, 0);
            NativeWorldPathPosition follow = new(previousMotion.X, before.NativeHeight - previousHeight, previousMotion.Z);
            forest = forest.AdvanceRagnarok(before.GroundType ?? throw new InvalidDataException("Ragnarok particle update lost its current native ground type."), drawBody, runtime.CurrentGroundHeight!.Value, priorGraphicsSpeed, follow, scriptMode, assets.ForestTables.RagnarokThrustOffsets, assets.ForestTables.Random, assets.ForestTables.Effects);
            runtime = runtime with { DrawBody = drawBody, DrawGroundHeight = runtime.CurrentGroundHeight };
        }
        forest = forest with { CameraPitch = motor.State.CameraPitch };
        runtime = runtime with { Motor = motor.State, Forest = forest, PreviousParticleNativeHeight = before.NativeHeight, ProjectionCurveThreshold = runtime.TransferCamera?.CurveThreshold ?? 6144, ProjectionCamera = new(before.X, before.Z, before.NativeHeight, targetX, targetZ, targetHeight, motor.State.CameraYaw, motor.State.CameraPitch, oldCamera.OffsetY, runtime.TransferCamera?.OffsetZ ?? oldCamera.OffsetZ) };
        WorldState cameraState = before with { BodyYaw = motor.State.BodyYaw, CameraYaw = motor.State.CameraYaw, CameraTargetX = targetX, CameraTargetZ = targetZ, CameraTargetNativeHeight = targetHeight, NativeRagnarokRuntime = runtime };
        if (runtime.BackgroundRails is { } background)
        {
            runtime = runtime with { BackgroundRails = StepBackgroundRails(background, runtime.ProjectionCamera!, 50, scriptMode, runtime.ProjectionCurveThreshold ?? 6144) };
            cameraState = cameraState with { NativeRagnarokRuntime = runtime };
        }
        if (mapOnly)
        {
            consumeRequest = false;
            movementUnits = 0;
            graphicsSpeed = nativeGraphicsSpeed;
            runtime = runtime.PublishBody(new(before.X, before.NativeHeight, -before.Z), motor.State.BodyPitch, checked((short)motor.State.BodyYaw), motor.State.BodyRoll, companionFollows: before.ChocoboDismount is null);
            if (runtime.BackgroundRails is { } mapRails)
            {
                runtime = runtime with { BackgroundRails = CompleteBackgroundRails(mapRails, before.X, before.Z, before.NativeHeight) };
            }
            WorldState mapped = cameraState with { Frame = before.Frame + 1, NativeRagnarokRuntime = runtime };
            fieldEntry = SelectWorldCallback(ref mapped, cameraState, scriptMode, ref salary, buttons, false);
            return mapped;
        }
        NativeWorldPathPosition? landingPlacement = admission.Action ? PrepareRagnarokLanding(cameraState) : null;
        bool activity = runtime.Transfer is not null || landingPlacement is not null;
        if (landingPlacement is not null)
        {
            scriptMode = 5;
        }
        int? group = scriptMode != 0 ? null : (buttons & 0x8000) != 0 ? 0 : (buttons & 0x2000) != 0 ? 1 : null;
        WorldNativeRagnarokGroundResult ground = assets.ResolveNativeRagnarokGround(before.X, before.Z, checked((short)motor.MotionX), checked((short)motor.MotionGraphicsZ), unchecked((short)before.NativeHeight), motor.State.FlightHeight, group, runtime.GroundCache!, replacements, (probes, padding) => RagnarokContact(cameraState, probes, padding), scriptMode);
        runtime = runtime with { PreviousParticleMotion = ground.GroundCommitted ? new(motor.MotionX, 0, motor.MotionGraphicsZ) : new(0, 0, 0) };
        WorldNativeTerrainOcclusionResult occlusion = assets.CertifyNativeTerrainCameraOcclusion(runtime.ProjectionCamera!, ground.GroundCommitted ? ground.GroundHeight : runtime.CurrentGroundHeight!.Value, replacements, 50, runtime.ProjectionCurveThreshold);
        runtime = runtime with { GroundCache = ground.Cache, GroundYaw = checked((short)ground.GroundYaw), CurrentGroundHeight = ground.GroundCommitted ? ground.GroundHeight : runtime.CurrentGroundHeight, PrimaryContact = ground.PrimaryContact, BroadContact = ground.BroadContact, PriorTerrainOcclusion = occlusion.Occluded ?? throw new NotSupportedException(occlusion.Reason), Motor = motor.State with { FlightHeight = ground.FlightHeight, Speed = ground.ResetSpeed ? 0 : motor.State.Speed } };
        WorldState after = cameraState with { Frame = before.Frame + 1, X = WrapCoordinate(ground.X, 262144), Z = WrapCoordinate(ground.Z, 196608), NativeHeight = ground.BodyHeight, GroundType = ground.Center?.Ground.Triangle.GroundType ?? before.GroundType, NativeRagnarokRuntime = runtime };
        runtime = runtime.PublishBody(new(after.X, after.NativeHeight, -after.Z), runtime.Motor.BodyPitch, checked((short)after.BodyYaw), runtime.Motor.BodyRoll, companionFollows: before.ChocoboDismount is null);
        if (runtime.BackgroundRails is { } completedRails)
        {
            runtime = runtime with { BackgroundRails = CompleteBackgroundRails(completedRails, after.X, after.Z, after.NativeHeight) };
        }
        after = after with { NativeRagnarokRuntime = runtime };
        graphicsSpeed = nativeGraphicsSpeed;
        //53FAC0 excludes entity50 from6519D0 movement aging and salary distance.
        movementUnits = 0;
        fieldEntry = SelectWorldCallback(ref after, cameraState, scriptMode, ref salary, buttons, activity);
        if (fieldEntry is null)
        {
            if (landingPlacement is not null)
            {
                runtime = after.NativeRagnarokRuntime!;
                NativeRagnarokTransferCamera initial = runtime.TransferCamera ?? throw new NotSupportedException("Landing requires projection globals produced by a preceding actual554860 callback.");
                initial = initial with { Pitch = motor.State.CameraPitch, Yaw = checked((short)after.CameraYaw), OffsetZ = runtime.ProjectionCamera!.OffsetZ };
                NativeRagnarokTransferCamera target = assets.ReadRagnarokTransferCameraTargets(NativeRagnarokTransferKind.Landing, runtime.CurrentGroundHeight!.Value, checked((short)after.BodyYaw));
                runtime = runtime with { Transfer = new(NativeRagnarokTransferKind.Landing, new(after.X, after.NativeHeight, -after.Z), runtime.CurrentGroundHeight.Value, checked((short)after.BodyYaw), initial, target), LandingPlacement = landingPlacement };
                after = after with { NativeRagnarokRuntime = runtime };
            }
            if (after.NativeRagnarokRuntime!.Transfer is not null)
            {
                after = AdvanceRagnarokTransfer(after);
            }
            else
            {
                runtime = after.NativeRagnarokRuntime!;
                NativeRagnarokTransferCamera globals = assets.ReadRagnarokTransferCameraTargets(NativeRagnarokTransferKind.Takeoff, runtime.CurrentGroundHeight!.Value, checked((short)after.BodyYaw));
                globals = globals with { Pitch = runtime.Motor.CameraPitch, Yaw = checked((short)after.CameraYaw), OffsetZ = runtime.ProjectionCamera!.OffsetZ };
                after = after with { NativeRagnarokRuntime = runtime with { TransferCamera = globals } };
            }
            after = StepRagnarokCactuar(after, ref salary);
        }
        return after;
    }
}
