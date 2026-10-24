using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    private sealed class GardenFootCallback(uint buttons, NativeWorldEntryMemory memory)
    {
        public uint Buttons { get; } = buttons;
        public NativeWorldEntryMemory Memory { get; } = memory;
        public bool ConsumeScriptRequest { get; set; }
        public WorldState? CameraState { get; set; }
        public NativeGardenWorldObject? BoardingTarget { get; set; }
    }

    // This context exists only across one synchronous physical callback. All retained
    // input, contacts, camera, cache and object owners live in the resulting WorldState.
    private GardenFootCallback? gardenFootCallback;

    private WorldState StepGardenFoot(WorldState before, float horizontal, float vertical, float cameraHorizontal, int priorGraphicsSpeed, uint buttons, NativeWorldEntryMemory memory, ref NativeWorldSalaryState salary, out bool consumeGardenRequest, out NativeGardenWorldObject? boardingTarget, out WorldFieldSelection? fieldEntry, out uint salaryMovementUnits, out int graphicsSpeed)
    {
        if (gardenFootCallback is not null)
        {
            throw new InvalidOperationException("Garden foot callbacks cannot be nested.");
        }
        gardenFootCallback = new GardenFootCallback(buttons, memory);
        try
        {
            WorldState after = StepFoot(before, horizontal, vertical, cameraHorizontal, priorGraphicsSpeed, ref salary, out fieldEntry, out salaryMovementUnits, out graphicsSpeed);
            consumeGardenRequest = gardenFootCallback.ConsumeScriptRequest;
            boardingTarget = gardenFootCallback.BoardingTarget;
            return after;
        }
        finally
        {
            gardenFootCallback = null;
        }
    }

    private static WorldState CompleteGardenReboarding(WorldState after, NativeGardenWorldObject target)
    {
        NativeGardenWorldRuntimeState garden = after.NativeGardenRuntime!;
        NativeFootWorldRuntimeState foot = after.NativeFootRuntime!;
        NativeWorldForestState forest = foot.Forest!;
        //558D70 must finish its actual183 transition here; a longer transition
        //requires its retained camera phase rather than an invented completion.
        NativeGardenReboardCompletion completion = garden.TransferControls.CompleteReboarding(1024, 5216, forest.CameraPitch, foot.CameraOffsetY, foot.CameraOffsetZ, garden.CameraDefaults);
        forest = forest with { CameraPitch = completion.Pitch };
        garden = garden with { Speed = completion.Speed, AnimationVariant = 0, CameraPitch = forest.CameraPitch, Forest = forest, Transfer = null, TransferControls = completion.Controls };
        garden = garden.PublishBody(target.Position, target.RotationX, target.RotationY, target.RotationZ);
        //54A7F0 captured this shape64 pose before the foot query. Only late
        //54B460, after encounters/auxiliary AI, restores the selected entity.
        return after with
        {
            X = target.Position.X,
            Z = -target.Position.Z,
            NativeHeight = target.Position.Y,
            BodyYaw = WrapYaw(target.RotationY),
            AnimationId = 0,
            AnimationCounter = 0,
            NativeFootRuntime = null,
            NativeGardenRuntime = garden
        };
    }

    private WorldState PrepareGardenFootCallback(WorldState cameraState)
    {
        GardenFootCallback callback = gardenFootCallback ?? throw new InvalidOperationException("Parked Garden foot movement requires its current ordinary callback input.");
        NativeFootWorldRuntimeState foot = cameraState.NativeFootRuntime!;
        NativeGardenWorldRuntimeState garden = cameraState.NativeGardenRuntime!;
        foot.RequireNativeGroundState();
        garden.Validate();
        NativeWorldFootCameraState camera = NativeWorldFootCameraState.Create(cameraState);
        _ = NativeWorldPathProjectionCertificate.CreateFoot(camera);
        garden = garden with { Forest = foot.Forest!, CameraPitch = foot.Forest!.CameraPitch, ProjectionCamera = camera, GroundCache = foot.GroundCache!, GroundYaw = checked((short)foot.GroundYaw!.Value) };
        NativeGardenTransferInput input = garden.TransferControls.PrepareInput(callback.Buttons, foot.EntityType, cameraState.Frame + 1, null, callback.Memory);
        NativeGardenTransferAdmission admission = garden.Buttons.Evaluate(input);
        callback.ConsumeScriptRequest = admission.ConsumeScriptRequest;
        if (admission.ToggleView)
        {
            throw new NotSupportedException("Native foot view toggle requires the distinct variant1 camera/model transition.");
        }
        garden = garden with { Buttons = garden.Buttons.Observe(callback.Buttons) };
        if (admission.Action && garden.BroadContact is int hit && (hit == garden.Allocation.Garden64Index || hit == garden.Allocation.Garden65Index))
        {
            NativeGardenWorldObject target = garden.Objects[garden.Allocation.Garden64Index];
            uint flags = GardenFootCurrentPolygonFlags(foot.GroundCache!);
            if (NativeGardenTransferState.PassesBoardingGate(foot.EntityType, 48, flags, garden.CurrentGroundHeight!.Value, target.Position.Y, true))
            {
                IReadOnlyList<WorldNativeGardenProbe> shape = assets.CreateNativeGardenShapeFootprint(target.Position, 48, target.RotationY);
                if (GardenFootContact(cameraState with { NativeGardenRuntime = garden }, shape, 48, 0, true) < 0)
                {
                    callback.BoardingTarget = target;
                    NativeWorldGroundCacheState cleared = foot.GroundCache! with { Alternatives = foot.GroundCache.Alternatives.Select(alternative => alternative with { Entries = [] }).ToArray() };
                    foot = foot with { GroundCache = cleared };
                    garden = garden with { GroundCache = cleared, Transfer = null, TransferControls = garden.TransferControls with { TransitionPhase = int.MinValue } };
                }
            }
        }
        WorldState prepared = cameraState with { NativeFootRuntime = foot, NativeGardenRuntime = garden };
        callback.CameraState = prepared;
        return prepared;
    }

    private uint GardenFootCurrentPolygonFlags(NativeWorldGroundCacheState cache)
    {
        uint? flags = null;
        foreach (NativeWorldGroundAlternative alternative in cache.Alternatives)
        {
            WorldTriangle polygon = assets.ResolveNativeGroundIdentity(alternative.CurrentGround, replacements);
            uint candidate = polygon.GroundType | (uint)polygon.TextureFlags << 8 | (uint)polygon.VertexFlags << 16;
            if (flags is uint retained && retained != candidate)
            {
                throw new NotSupportedException("Retained foot ground alternatives disagree on Garden reboarding surface permissions.");
            }
            flags = candidate;
        }
        return flags!.Value;
    }

    private int GardenFootContact(WorldState cameraState, IReadOnlyList<WorldNativeGardenProbe> probes, int selectedEntity, int padding, bool forceCircle = false)
    {
        NativeGardenWorldRuntimeState garden = cameraState.NativeGardenRuntime!;
        NativeWorldFootCameraState camera = garden.ProjectionCamera ?? throw new NotSupportedException("Garden foot contact lost its actual pre-terrain camera.");
        NativeWorldPathProjectionCertificate projection = NativeWorldPathProjectionCertificate.CreateFoot(camera);
        bool trainContactsExcluded = WorldFootContactCertificate.ExcludesOriginalTrainRoutes(projection);
        return WorldGardenContactCertificate.Evaluate(assets, garden.Allocation, garden.Objects, cameraState.NativeFootRuntime!.EntityType, selectedEntity, probes, projection, padding, forceCircle, trainContactsExcluded);
    }

    private WorldState CompleteGardenFootCallback(WorldState after, WorldNativeGroundResult ground, int graphicsSpeed)
    {
        GardenFootCallback callback = gardenFootCallback ?? throw new InvalidOperationException("Garden foot completion has no physical callback context.");
        NativeFootWorldRuntimeState foot = after.NativeFootRuntime!;
        NativeGardenWorldRuntimeState garden = callback.CameraState!.NativeGardenRuntime!;
        WorldNativeTerrainOcclusionResult occlusion = assets.CertifyNativeTerrainCameraOcclusion(garden.ProjectionCamera!, garden.CurrentGroundHeight!.Value, replacements, foot.EntityType);
        garden = garden with
        {
            GroundCache = foot.GroundCache!,
            GroundYaw = checked((short)foot.GroundYaw!.Value),
            Forest = foot.Forest!,
            CameraPitch = foot.Forest!.CameraPitch,
            PriorTerrainOcclusion = occlusion.Occluded ?? throw new NotSupportedException(occlusion.Reason),
            CurrentGroundHeight = ground.GroundCommitted ? unchecked((short)-(int)ground.Center!.Ground.Height) : garden.CurrentGroundHeight,
            MovementMagnitude20409F4 = ground.PositionCommitted ? graphicsSpeed : 0,
            PrimaryContact = ground.PrimaryContact ?? throw new InvalidDataException("Garden foot callback omitted its native primary contact publication."),
            BroadContact = ground.BroadContact ?? throw new InvalidDataException("Garden foot callback omitted its native broad contact publication.")
        };
        garden = garden.PublishBody(new(after.X, after.NativeHeight, -after.Z), 0, unchecked((short)after.BodyYaw), 0, gardenControlled: false);
        return after with { NativeGardenRuntime = garden };
    }
}
