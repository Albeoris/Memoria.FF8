using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    private sealed class RagnarokFootCallback(uint buttons, NativeWorldEntryMemory memory)
    {
        public uint Buttons { get; } = buttons;
        public NativeWorldEntryMemory Memory { get; } = memory;
        public bool ConsumeRequest { get; set; }
        public WorldState? CameraState { get; set; }
        public NativeRagnarokWorldObject? BoardingTarget { get; set; }
    }

    private RagnarokFootCallback? ragnarokFootCallback;
    private NativeRagnarokWorldObject? ragnarokFootBoardingTarget;

    private WorldState StepRagnarokFoot(WorldState before, float horizontal, float vertical, float cameraHorizontal, int priorGraphicsSpeed, uint buttons, NativeWorldEntryMemory memory, ref NativeWorldSalaryState salary, out bool consumeRequest, out WorldFieldSelection? fieldEntry, out uint salaryMovementUnits, out int graphicsSpeed)
    {
        if (ragnarokFootCallback is not null)
        {
            throw new InvalidOperationException("Ragnarok foot callbacks cannot be nested.");
        }
        ragnarokFootBoardingTarget = null;
        ragnarokFootCallback = new(buttons, memory);
        try
        {
            WorldState after = StepFoot(before, horizontal, vertical, cameraHorizontal, priorGraphicsSpeed, ref salary, out fieldEntry, out salaryMovementUnits, out graphicsSpeed);
            consumeRequest = ragnarokFootCallback.ConsumeRequest;
            ragnarokFootBoardingTarget = ragnarokFootCallback.BoardingTarget;
            return after;
        }
        finally
        {
            ragnarokFootCallback = null;
        }
    }

    private WorldState CompleteRagnarokLanding(WorldState state, NativeRagnarokTransferState completed, NativeWorldPathPosition placement, NativeRagnarokTransferCamera camera)
    {
        NativeRagnarokWorldRuntimeState runtime = state.NativeRagnarokRuntime!;
        NativeWorldProcess process = GameSession.PrepareWorldProcess(session.State, state);
        NativeFootWorldRuntimeState foot = NativeFootWorldRuntimeState.CreateRagnarokDismount(runtime, completed, camera, process);
        NativeRagnarokWorldObject ship = runtime.Objects[runtime.Allocation.RagnarokIndex];
        //54B460 changes20409E0 to546F90(shape0)=0, restores the retained
        //53E7A0 placement and548300 publishes foot/chocobo, leaving shape1 parked.
        runtime = PublishRagnarokFootBody(runtime with { GroundCache = foot.GroundCache, Forest = foot.Forest, TransferCamera = camera }, placement, ship.Pitch, ship.Yaw, ship.Roll);
        return state with { X = WrapCoordinate(placement.X, 262144), Z = WrapCoordinate(-placement.Z, 196608), NativeHeight = placement.Y, BodyYaw = WrapYaw(ship.Yaw), CameraYaw = WrapYaw(camera.Yaw), AnimationId = 0, AnimationCounter = 0, NativeFootRuntime = foot, NativeRagnarokRuntime = runtime };
    }

    private WorldState PrepareRagnarokFootCallback(WorldState cameraState)
    {
        RagnarokFootCallback callback = ragnarokFootCallback ?? throw new InvalidOperationException("Parked Ragnarok foot movement requires its current ordinary callback input.");
        NativeFootWorldRuntimeState foot = cameraState.NativeFootRuntime!;
        NativeRagnarokWorldRuntimeState runtime = cameraState.NativeRagnarokRuntime!;
        if (foot.RagnarokDismount is not null)
        {
            foot.RagnarokDismount.Validate(runtime);
        }
        else if (foot.Rule is not (NativeFootWorldRuntimeState.FirstLoadRule or NativeFootWorldRuntimeState.SavedReloadRule or NativeFootWorldRuntimeState.BattleReloadRule) || runtime.Allocation.ParkedRagnarokPose2 is null)
        {
            throw new InvalidDataException("Parked Ragnarok foot requires its actual landing or saved62 reload owner.");
        }
        NativeWorldFootCameraState camera = NativeWorldFootCameraState.Create(cameraState);
        runtime = runtime with { GroundCache = foot.GroundCache, GroundYaw = checked((short)foot.GroundYaw!.Value), Forest = foot.Forest, ProjectionCamera = camera };
        NativeRagnarokTransferCamera globals = runtime.TransferCamera ?? throw new NotSupportedException("Ragnarok foot camera requires the retained landing scalar/projection globals.");
        runtime = runtime with { ProjectionCurveThreshold = globals.CurveThreshold };
        _ = NativeWorldPathProjectionCertificate.CreateRagnarok(camera, globals.CurveThreshold);
        uint? previous = runtime.PreviousButtons;
        if (previous is null && (callback.Buttons & 0x42) != 0)
        {
            throw new NotSupportedException("Ragnarok foot action/view edge requires its preceding actual observed button callback.");
        }
        NativeGardenTransferAdmission admission = NativeRagnarokTransferInput.Evaluate(new(callback.Buttons, previous ?? callback.Buttons, foot.EntityType, 0, 0, false, -1, NativeWorldEntryControl.DelayAfterCallback(cameraState.Frame + 1, 0), callback.Memory.ReplacementFlags264, false));
        callback.ConsumeRequest = admission.ConsumeScriptRequest;
        runtime = runtime with { PreviousButtons = callback.Buttons };
        WorldState prepared = cameraState with { NativeRagnarokRuntime = runtime };
        if (admission.Action && runtime.BroadContact == runtime.Allocation.RagnarokIndex)
        {
            NativeRagnarokWorldObject ship = runtime.Objects[runtime.Allocation.RagnarokIndex];
            uint flags = GardenFootCurrentPolygonFlags(foot.GroundCache!);
            bool heightAndSurface = NativeRagnarokTransferInput.PassesBoardingGate(foot.EntityType, flags, runtime.CurrentGroundHeight!.Value, ship.Position.Y, true, true);
            if (heightAndSurface)
            {
                IReadOnlyList<WorldNativeGardenProbe> shape = assets.CreateNativeGardenShapeFootprint(ship.Position, 50, ship.Yaw);
                if (RagnarokFootContact(prepared, shape, 50, 0, true) < 0)
                {
                    callback.BoardingTarget = ship;
                    NativeWorldGroundCacheState cleared = foot.GroundCache! with { Alternatives = foot.GroundCache.Alternatives.Select(alternative => alternative with { Entries = [] }).ToArray() };
                    foot = foot with { GroundCache = cleared };
                    runtime = runtime with { GroundCache = cleared };
                    prepared = prepared with { NativeFootRuntime = foot, NativeRagnarokRuntime = runtime };
                }
            }
        }
        callback.CameraState = prepared;
        return prepared;
    }

    private int RagnarokFootContact(WorldState cameraState, IReadOnlyList<WorldNativeGardenProbe> probes, int selectedEntity, int padding, bool forceCircle = false)
    {
        NativeRagnarokWorldRuntimeState runtime = cameraState.NativeRagnarokRuntime!;
        NativeWorldPathProjectionCertificate projection = NativeWorldPathProjectionCertificate.CreateRagnarok(runtime.ProjectionCamera!, runtime.TransferCamera!.CurveThreshold);
        return WorldRagnarokContactCertificate.Evaluate(assets, runtime, probes, projection, padding, WorldFootContactCertificate.ExcludesOriginalTrainRoutes(projection), selectedEntity, 0, forceCircle);
    }

    private WorldState CompleteRagnarokFootCallback(WorldState after, WorldNativeGroundResult ground)
    {
        NativeFootWorldRuntimeState foot = after.NativeFootRuntime!;
        NativeRagnarokWorldRuntimeState runtime = ragnarokFootCallback!.CameraState!.NativeRagnarokRuntime!;
        NativeRagnarokWorldObject previousFoot = runtime.Objects[0];
        short pitch = checked((short)(previousFoot.Pitch + Math.Clamp(-(int)previousFoot.Pitch, -16, 16)));
        short groundHeight = ground.GroundCommitted ? unchecked((short)-(int)ground.Center!.Ground.Height) : runtime.CurrentGroundHeight!.Value;
        WorldNativeTerrainOcclusionResult occlusion = assets.CertifyNativeTerrainCameraOcclusion(runtime.ProjectionCamera!, groundHeight, replacements, 0, runtime.TransferCamera!.CurveThreshold);
        runtime = runtime with { GroundCache = foot.GroundCache, GroundYaw = checked((short)foot.GroundYaw!.Value), Forest = foot.Forest, CurrentGroundHeight = groundHeight, PrimaryContact = ground.PrimaryContact ?? throw new InvalidDataException("Ragnarok foot callback omitted primary contact."), BroadContact = ground.BroadContact ?? throw new InvalidDataException("Ragnarok foot callback omitted broad contact."), PriorTerrainOcclusion = occlusion.Occluded ?? throw new NotSupportedException(occlusion.Reason), TransferCamera = runtime.TransferCamera with { Pitch = foot.Forest!.CameraPitch, Yaw = checked((short)after.CameraYaw), OffsetZ = foot.CameraOffsetZ } };
        runtime = runtime with { PreviousParticleNativeHeight = ragnarokFootCallback.CameraState.NativeHeight };
        runtime = PublishRagnarokFootBody(runtime, new(after.X, after.NativeHeight, -after.Z), pitch, checked((short)after.BodyYaw), previousFoot.Roll);
        return after with { NativeRagnarokRuntime = runtime };
    }

    private static NativeRagnarokWorldRuntimeState PublishRagnarokFootBody(NativeRagnarokWorldRuntimeState runtime, NativeWorldPathPosition position, short pitch, short yaw, short roll)
    {
        var objects = runtime.Objects.ToBuilder();
        for (int index = 0; index < objects.Count; index++)
        {
            if (objects[index].ShapeId is 0 or 3)
            {
                objects[index] = objects[index] with { Position = position, Pitch = pitch, Yaw = yaw, Roll = roll };
            }
        }
        return runtime with { Objects = objects.ToImmutable() };
    }

    private WorldState CompleteRagnarokFootReboarding(WorldState state)
    {
        NativeRagnarokWorldObject? target = ragnarokFootBoardingTarget;
        ragnarokFootBoardingTarget = null;
        if (target is null)
        {
            return state;
        }
        NativeRagnarokWorldRuntimeState runtime = state.NativeRagnarokRuntime!;
        NativeFootWorldRuntimeState foot = state.NativeFootRuntime!;
        NativeRagnarokTransferCamera initial = runtime.TransferCamera! with { ProjectionDistance = foot.CameraTransition?.ProjectionDistance ?? runtime.TransferCamera!.ProjectionDistance, Pitch = foot.Forest!.CameraPitch, Yaw = checked((short)state.CameraYaw), OffsetZ = foot.CameraOffsetZ };
        NativeRagnarokTransferCamera destination = assets.ReadRagnarokTransferCameraTargets(NativeRagnarokTransferKind.Takeoff, runtime.CurrentGroundHeight!.Value, target.Yaw);
        NativeRagnarokTransferState transfer = new(NativeRagnarokTransferKind.Takeoff, target.Position, runtime.CurrentGroundHeight!.Value, target.Yaw, initial, destination);
        NativeWorldVerticalBiasState bias = (session.State.WorldProcess!.VerticalBias204DADC ?? NativeWorldVerticalBiasState.UnknownWord).Damp(2, 2, state.Frame - session.State.World!.Frame);
        runtime = runtime with { AnimationVariant = 0, MapDisplayMode = foot.MapDisplayMode, Transfer = transfer, LandingPlacement = null, TransferCamera = initial, Motor = runtime.Motor with { Speed = 0, BodyYaw = checked((ushort)WrapYaw(target.Yaw)), BodyPitch = target.Pitch, BodyRoll = target.Roll, CameraYaw = checked((ushort)state.CameraYaw), CameraPitch = initial.Pitch, VerticalBias204DADC = bias.IsExact ? bias.Minimum : null, VerticalBiasAuthority = bias.IsExact ? null : bias }, Forest = foot.Forest };
        runtime = runtime.PublishBody(target.Position, target.Pitch, target.Yaw, target.Roll);
        state = state with { X = target.Position.X, Z = -target.Position.Z, NativeHeight = target.Position.Y, BodyYaw = WrapYaw(target.Yaw), NativeFootRuntime = null, NativeRagnarokRuntime = runtime };
        return AdvanceRagnarokTransfer(state);
    }
}
