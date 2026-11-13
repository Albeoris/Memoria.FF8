using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    private void InitializeGarden(WorldTransition arrival, WorldEntryVariables inputs)
    {
        NativeSavedWorldState saved = session.State.SavedWorld!;
        NativeWorldEntryMemory entry = session.State.WorldEntryMemory ?? throw new NotSupportedException("Garden allocation requires the current executed world-entry bytes.");
        NativeCharacterVitals vitals = session.State.CharacterVitals ?? throw new NotSupportedException("Garden allocation requires its actual saved GF13 flags.");
        if (unchecked((byte)arrival.Mode) == ChocoboEntity && session.State.World?.NativeGardenRuntime is { } formerChocoboAllocation)
        {
            ValidateRetainedGardenAllocation(formerChocoboAllocation, arrival, inputs);
            ValidateChocoboEscapeGround(session.State.World.ChocoboDismount);
            return;
        }
        if (session.State.World?.NativeGardenRuntime is { CarBodyPose: not null } retainedGarden)
        {
            ValidateRetainedGardenAllocation(retainedGarden, arrival, inputs);
            return;
        }
        //The allocation packet remains the original foot entry after54B460 reboards
        //Garden. Authenticate that retained allocation without rerunning a48 loader.
        NativeGardenWorldAllocation allocation = unchecked((byte)arrival.Mode) == 0
            ? assets.ReadParkedGardenAllocation(arrival, entry, session.State.EventMemory, saved, vitals, session.State.World?.NativeGardenRuntime?.AuxiliaryEntry)
            : assets.ReadGardenAllocation(arrival, entry, session.State.EventMemory, saved, vitals);
        NativeWorldAuxiliaryRelocationReceipt? auxiliaryEntry = assets.ReadGardenAuxiliaryRelocations(arrival, session.State.EventMemory, allocation.LoadPose);
        if (session.State.World is null)
        {
            NativeSavedWorldPose pose = allocation.LoadPose;
            WorldGroundSelection ground = assets.LookupNativeGround(pose.X, pose.RawZ, replacements, []) ?? throw new NotSupportedException("Garden entry has no original initial ground polygon.");
            NativeWorldGroundCacheState cache = CreateFreshGroundCache(ground.Identity);
            NativeWorldProcess process = session.State.WorldProcess ?? throw new NotSupportedException("Garden entry requires its retained native process owner.");
            NativeGardenWorldRuntimeState garden = NativeGardenWorldRuntimeState.CreateFresh(saved, allocation, cache, process, assets.ReadGardenCameraDefaults(), currentGroundHeight: unchecked((short)-(int)ground.Ground.Height), priorTerrainOcclusion: ReadInitialTerrainOcclusion()) with { AuxiliaryEntry = auxiliaryEntry };
            garden = garden with { BackgroundRails = CreateBackgroundRails(allocation.Flags265, garden.ProjectionCamera!) };
            WorldState initial = new()
            {
                X = pose.X,
                Z = pose.RawZ,
                NativeHeight = pose.Height,
                BodyYaw = pose.Yaw,
                CameraYaw = allocation.CameraYaw,
                CameraTargetX = pose.X,
                CameraTargetZ = pose.RawZ,
                CameraTargetNativeHeight = pose.Height - 256,
                GroundType = ground.Ground.Triangle.GroundType,
                ReplacementMask = WorldTerrainSelection.ReplacementMask(inputs.ReplacementFlags, inputs.StoryByte),
                NativeGardenRuntime = garden,
                EventUi = NativeWorldEventUi.Create()
            };
            session.SetWorldState(initial);
            if (auxiliaryEntry is not null)
            {
                session.SetEventMemory(auxiliaryEntry.ApplyTo(session.State.EventMemory));
            }
        }
        else if (auxiliaryEntry is not null)
        {
            throw new InvalidDataException("A restored active Garden cannot re-execute pending loader relocation selectors.");
        }
        NativeGardenWorldRuntimeState current = State.NativeGardenRuntime ?? throw new InvalidDataException("Garden restoration has no executed Garden runtime owner.");
        current.Validate();
        if (current.AuxiliaryEntry is not null)
        {
            assets.ValidateGardenAuxiliaryRelocations(current.AuxiliaryEntry);
        }
        assets.ValidateNativeGroundCache(current.GroundCache, replacements);
        //544860 appends shape79/80 pairs from264 bits0..2 only. The retained
        //allocation records the original full byte;543750 can consume its bit6
        //during ordinary play without re-running or changing that allocation.
        JsonElement expectedAllocation = JsonSerializer.SerializeToElement(allocation with { ReplacementFlags264 = (byte)(allocation.ReplacementFlags264 & 7) });
        JsonElement restoredAllocation = JsonSerializer.SerializeToElement(current.Allocation with { ReplacementFlags264 = (byte)(current.Allocation.ReplacementFlags264 & 7) });
        if (!JsonElement.DeepEquals(expectedAllocation, restoredAllocation))
        {
            string differences = string.Join(", ", expectedAllocation.EnumerateObject().Where(property => !restoredAllocation.TryGetProperty(property.Name, out JsonElement restored) || !JsonElement.DeepEquals(property.Value, restored)).Select(property => property.Name));
            throw new InvalidDataException($"Restored Garden allocation differs from its authenticated original loader inputs: {differences}.");
        }
    }

    private void ValidateRetainedGardenAllocation(NativeGardenWorldRuntimeState garden, WorldTransition arrival, WorldEntryVariables inputs)
    {
        garden.Validate();
        assets.ValidateGardenAllocationResources(garden.Allocation);
        assets.ValidateNativeGroundCache(garden.GroundCache, replacements);
        NativeSavedWorldState? saved = session.State.SavedWorld;
        byte? savedCarShape = HasSavedParkedCar(saved) ? saved!.Slot4.ShapeId : null;
        if (garden.Allocation.Packet != arrival || garden.Allocation.Story != inputs.StoryWord || (garden.Allocation.ReplacementFlags264 & 7) != (inputs.ReplacementFlags & 7) || garden.Allocation.SavedCarSlot?.ShapeId != savedCarShape)
        {
            throw new InvalidDataException("Retained Garden allocation differs from its original allocation, current story or actual saved vehicle.");
        }
        if (garden.AuxiliaryEntry is not null)
        {
            assets.ValidateGardenAuxiliaryRelocations(garden.AuxiliaryEntry);
        }
    }

    private static NativeWorldPathProjectionCertificate GardenProjection(WorldState state)
    {
        NativeWorldFootCameraState camera = state.NativeGardenRuntime?.ProjectionCamera ?? throw new NotSupportedException("Garden contact requires its current executed pre-terrain projection camera.");
        return NativeWorldPathProjectionCertificate.CreateFoot(camera);
    }

    private WorldRenderFrame PrepareGardenFrame(WorldState state)
    {
        NativeGardenWorldRuntimeState garden = state.NativeGardenRuntime!;
        garden.Validate();
        NativeWorldPathProjectionCertificate projection = GardenProjection(state);
        // Native 5472C0 animates the allocated models with the current world draw counter.
        uint mainLoopCounter = session.State.WorldProcess!.MainLoopCounter;
        List<NativeWorldStaticModelDraw> models = [];
        if (garden.BackgroundRails is { } background)
        {
            ValidateBackgroundRails(state, background, true);
            models.AddRange(ReadBackgroundRailModels(background));
        }
        for (int index = 0; index < garden.Objects.Length; index++)
        {
            NativeGardenWorldObject item = garden.Objects[index];
            // The Garden allocation retains the chocobo slots from the loader.
            // Native 5472C0 hides them before projection while the chocobo process is idle.
            if (item.ShapeId is 2 or 3)
            {
                continue;
            }
            if (item.ShapeId == 94)
            {
                if (projection.IsNear(item.Position))
                {
                    throw new NotSupportedException("Nearby Cactuar presentation requires its executed pre-AI draw phase and pose snapshot.");
                }
                continue;
            }
            if (index == garden.Allocation.FootObjectIndex && state.NativeFootRuntime is null || !projection.IsNear(item.Position))
            {
                continue;
            }
            NativeWorldPathNodeProjection draw = projection.ProjectNode(item.Position) ?? throw new NotSupportedException(projection.UnsupportedReason);
            if (!draw.Visible)
            {
                continue;
            }
            if (item.ShapeId < 64)
            {
                if (index == garden.Allocation.FootObjectIndex)
                {
                    continue;
                }
                throw new NotSupportedException("A nearby Garden allocation actor requires its actual animated world-model presentation.");
            }
            NativeWorldPathAngles angles = WorldStaticModelTransform.AnimatedAllocationAngles(item, mainLoopCounter);
            models.Add(new NativeWorldStaticModelDraw(assets.ReadScriptedStaticModel(item.ShapeId), new NativeWorldStaticModelPose(projection.CanonicalModelAnchor(draw), angles)) { ObjectId = new(WorldRenderObjectKind.Allocation, index) });
        }
        // 558D70 updates live words after the callback has drawn the terrain.
        NativeWorldFootCameraState drawn = garden.ProjectionCamera!;
        WorldCamera camera = state.NativeFootRuntime is not null ? FootCamera(state) : WorldCamera.Foot(drawn.Yaw, 2048, projectionDistance: drawn.ProjectionDistance) with
        {
            TargetPosition = new Vector3(drawn.TargetX, -drawn.TargetNativeHeight, drawn.TargetRawZ),
            NativePitch = drawn.Pitch,
            NativeOffsetY = drawn.OffsetY,
            NativeOffsetZ = drawn.OffsetZ
        };
        camera = camera with { ParticleProjection = projection };
        Vector3 position = new(state.X, -state.NativeHeight, state.Z);
        WorldSky sky = WorldTerrainSelection.SelectSky(assets.Skies, state.X, state.Z, storyByte);
        bool footVisible = state.NativeFootRuntime is not null && state.GroundType is null or > 5;
        return new WorldRenderFrame(position, state.BodyYaw * MathF.Tau / 4096, state.AnimationId, state.AnimationCounter >> 4, camera, sky, footVisible, state.NativeFootRuntime is null ? null : assets.GetFootModel(state.NativeFootRuntime.EntityType), models, state.NativeFootRuntime?.Forest ?? garden.Forest);
    }
}
