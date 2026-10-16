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
        NativeGardenWorldAllocation allocation = assets.ReadGardenAllocation(arrival, entry, session.State.EventMemory, saved, vitals);
        NativeWorldAuxiliaryRelocationReceipt? auxiliaryEntry = assets.ReadGardenAuxiliaryRelocations(arrival, session.State.EventMemory, allocation.LoadPose);
        if (session.State.World is null)
        {
            NativeSavedWorldPose pose = allocation.LoadPose;
            WorldGroundSelection ground = assets.LookupNativeGround(pose.X, pose.RawZ, replacements, []) ?? throw new NotSupportedException("Garden entry has no original initial ground polygon.");
            NativeWorldGroundCacheState? retained = session.State.DepartingWorldState?.NativeGardenRuntime?.GroundCache ?? session.State.DepartingWorldState?.NativeFootRuntime?.GroundCache ?? session.State.DepartingWorldState?.NativeCarRuntime?.GroundCache;
            NativeWorldGroundCacheState cache = NativeWorldGroundCacheState.CreateFresh(ground.Identity, retained);
            NativeWorldProcess process = session.State.WorldProcess ?? throw new NotSupportedException("Garden entry requires its retained native process owner.");
            NativeGardenWorldRuntimeState garden = NativeGardenWorldRuntimeState.CreateFresh(saved, allocation, cache, process, assets.ReadGardenCameraDefaults(), currentGroundHeight: unchecked((short)-(int)ground.Ground.Height), priorTerrainOcclusion: ReadInitialGardenTerrainOcclusion()) with { AuxiliaryEntry = auxiliaryEntry };
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
                NativeGardenRuntime = garden
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
        if (!JsonElement.DeepEquals(JsonSerializer.SerializeToElement(allocation), JsonSerializer.SerializeToElement(current.Allocation)))
        {
            throw new InvalidDataException("Restored Garden allocation differs from its authenticated original loader inputs.");
        }
    }

    private static NativeWorldPathProjectionCertificate GardenProjection(WorldState state)
    {
        NativeWorldFootCameraState camera = state.NativeGardenRuntime?.ProjectionCamera ?? throw new NotSupportedException("Garden contact requires its current executed pre-terrain projection camera.");
        return NativeWorldPathProjectionCertificate.CreateFoot(camera);
    }

    private byte[] RenderGarden(WorldState state)
    {
        NativeGardenWorldRuntimeState garden = state.NativeGardenRuntime!;
        garden.Validate();
        NativeWorldPathProjectionCertificate projection = GardenProjection(state);
        List<NativeWorldStaticModelDraw> models = [];
        for (int index = 0; index < garden.Objects.Length; index++)
        {
            NativeGardenWorldObject item = garden.Objects[index];
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
            NativeWorldPathAngles angles = WorldStaticModelTransform.AllocationAngles(item.ShapeId, item.RotationX, item.RotationY, item.RotationZ);
            models.Add(new NativeWorldStaticModelDraw(assets.ReadScriptedStaticModel(item.ShapeId), new NativeWorldStaticModelPose(projection.CanonicalModelAnchor(draw), angles)));
        }
        WorldCamera camera = WorldCamera.Foot(state.CameraYaw, 2048) with
        {
            TargetPosition = new Vector3(state.CameraTargetX, -state.CameraTargetNativeHeight, state.CameraTargetZ),
            NativePitch = garden.CameraPitch,
            NativeOffsetY = garden.CameraDefaults.OffsetY,
            NativeOffsetZ = garden.CameraDefaults.OffsetZ
        };
        Vector3 position = new(state.X, -state.NativeHeight, state.Z);
        WorldSky sky = WorldTerrainSelection.SelectSky(assets.Skies, state.X, state.Z, storyByte);
        bool footVisible = state.NativeFootRuntime is not null && state.GroundType is null or > 5;
        return renderer!.Render(position, state.BodyYaw * MathF.Tau / 4096, state.AnimationId, state.AnimationCounter >> 4, camera, replacements, sky, footVisible, state.NativeFootRuntime is null ? null : assets.GetFootModel(state.NativeFootRuntime.EntityType), models, garden.Forest);
    }
}
