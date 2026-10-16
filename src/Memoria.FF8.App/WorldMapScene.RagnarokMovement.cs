using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    private WorldState StepRagnarok(WorldState before, float cameraHorizontal, uint buttons, NativeWorldEntryMemory memory, ref NativeWorldSalaryState salary, out bool consumeRequest, out WorldFieldSelection? fieldEntry, out uint movementUnits, out int graphicsSpeed)
    {
        NativeRagnarokWorldRuntimeState runtime = before.NativeRagnarokRuntime!;
        runtime.RequireSceneState();
        if (cameraHorizontal != 0)
        {
            throw new NotSupportedException("Ragnarok ordinary variant0 has no manual camera input; WorldButtons supplies real throttle/steering/vertical masks.");
        }
        byte scriptMode = runtime.Transfer is null ? salary.EventUi?.ScriptMode ?? 0 : runtime.Transfer.Kind == NativeRagnarokTransferKind.Landing ? (byte)5 : (byte)6;
        int cooldown = NativeWorldEntryControl.DelayAfterCallback(before.Frame + 1, 0);
        NativeRagnarokInputPacket input = NativeRagnarokWorldInput.ReadDigitalButtons(cooldown > 0 || scriptMode != 0 ? 0 : buttons);
        NativeRagnarokMotorResult motor = scriptMode == 0 ? NativeRagnarokMotor.Step(runtime.Motor, input.Steering13, input.Throttle11, input.Vertical12) : NativeRagnarokMotor.StepScripted(runtime.Motor, scriptMode, scriptMode == 5 ? 16 : 120);
        uint? previous = runtime.PreviousButtons;
        if (previous is null && (buttons & 0x42) != 0 && cooldown <= 0)
        {
            throw new NotSupportedException("Ragnarok action/view edge requires a preceding actual released/observed button callback.");
        }
        NativeGardenTransferAdmission admission = NativeRagnarokTransferInput.Evaluate(new(buttons, previous ?? buttons, 50, 0, scriptMode, runtime.Transfer is not null, -1, cooldown, memory.ReplacementFlags264, false));
        if (admission.ToggleView)
        {
            throw new NotSupportedException("Ragnarok alternate view requires its original variant1 camera/motor owner.");
        }
        consumeRequest = admission.ConsumeScriptRequest;
        int targetX = (before.X + NearWrapped(before.CameraTargetX, before.X, 262144)) >> 1;
        int targetZ = (before.Z + NearWrapped(before.CameraTargetZ, before.Z, 196608)) >> 1;
        bool frozenHeight = runtime.Transfer is { Kind: NativeRagnarokTransferKind.Takeoff, Counter: >= 1 and <= 41 };
        int targetHeight = frozenHeight ? before.CameraTargetNativeHeight : (3 * before.CameraTargetNativeHeight + before.NativeHeight - 256) >> 2;
        NativeWorldFootCameraState oldCamera = runtime.ProjectionCamera!;
        NativeWorldForestState forest = runtime.Forest ?? throw new NotSupportedException("Ragnarok requires its original555B20 particle/random pool owner.");
        forest = forest.AdvanceParticlesOnly(assets.ForestTables.Effects) with { CameraPitch = motor.State.CameraPitch };
        runtime = runtime with { Motor = motor.State, Forest = forest, PreviousButtons = buttons, ProjectionCurveThreshold = runtime.TransferCamera?.CurveThreshold ?? 6144, ProjectionCamera = new(before.X, before.Z, before.NativeHeight, targetX, targetZ, targetHeight, motor.State.CameraYaw, motor.State.CameraPitch, oldCamera.OffsetY, runtime.TransferCamera?.OffsetZ ?? oldCamera.OffsetZ) };
        WorldState cameraState = before with { BodyYaw = motor.State.BodyYaw, CameraYaw = motor.State.CameraYaw, CameraTargetX = targetX, CameraTargetZ = targetZ, CameraTargetNativeHeight = targetHeight, NativeRagnarokRuntime = runtime };
        NativeWorldPathPosition? landingPlacement = admission.Action ? PrepareRagnarokLanding(cameraState) : null;
        bool activity = runtime.Transfer is not null || landingPlacement is not null;
        if (landingPlacement is not null)
        {
            scriptMode = 5;
        }
        int? group = scriptMode != 0 ? null : (buttons & 0x8000) != 0 ? 0 : (buttons & 0x2000) != 0 ? 1 : null;
        WorldNativeRagnarokGroundResult ground = assets.ResolveNativeRagnarokGround(before.X, before.Z, checked((short)motor.MotionX), checked((short)motor.MotionGraphicsZ), unchecked((short)before.NativeHeight), motor.State.FlightHeight, group, runtime.GroundCache!, replacements, (probes, padding) => RagnarokContact(cameraState, probes, padding), scriptMode);
        WorldNativeTerrainOcclusionResult occlusion = assets.CertifyNativeTerrainCameraOcclusion(runtime.ProjectionCamera!, ground.GroundCommitted ? ground.GroundHeight : runtime.CurrentGroundHeight!.Value, replacements, 50, runtime.ProjectionCurveThreshold);
        runtime = runtime with { GroundCache = ground.Cache, GroundYaw = checked((short)ground.GroundYaw), CurrentGroundHeight = ground.GroundCommitted ? ground.GroundHeight : runtime.CurrentGroundHeight, PrimaryContact = ground.PrimaryContact, BroadContact = ground.BroadContact, PriorTerrainOcclusion = occlusion.Occluded ?? throw new NotSupportedException(occlusion.Reason), Motor = motor.State with { FlightHeight = ground.FlightHeight, Speed = ground.ResetSpeed ? 0 : motor.State.Speed } };
        WorldState after = cameraState with { Frame = before.Frame + 1, X = WrapCoordinate(ground.X, 262144), Z = WrapCoordinate(ground.Z, 196608), NativeHeight = ground.BodyHeight, GroundType = ground.Center?.Ground.Triangle.GroundType ?? before.GroundType, NativeRagnarokRuntime = runtime };
        runtime = runtime.PublishBody(new(after.X, after.NativeHeight, -after.Z), runtime.Motor.BodyPitch, checked((short)after.BodyYaw), runtime.Motor.BodyRoll);
        after = after with { NativeRagnarokRuntime = runtime };
        graphicsSpeed = NativeWorldVectorMagnitude.Truncate(motor.MotionX, 0, motor.MotionGraphicsZ);
        //53FAC0 excludes entity50 from6519D0 movement aging and salary distance.
        movementUnits = 0;
        fieldEntry = SelectWorldCallback(after, cameraState, scriptMode, ref salary, buttons, previous, activity);
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
            after = StepRagnarokCactuar(after, out fieldEntry);
        }
        return after;
    }
}
