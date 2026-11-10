using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Rendering;
using Microsoft.Xna.Framework;
using Vector3N = System.Numerics.Vector3;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private Ff7WorldRuntime? worldPresentationOwner;
    private Ff7WorldCameraProjection? worldCamera;
    private Ff7WorldTerrainAdapter? worldTerrain;
    private Ff7WorldTextureAnimationResources? worldTextureCycles;
    private Ff7WorldVisibility? worldVisibility;
    private Ff7WorldEnvironmentResources? worldEnvironment;
    private Ff7WorldEnvironmentAdapter? worldEnvironmentRenderer;
    private readonly Dictionary<(int Block, int Mesh), PcWorldMesh> worldMeshes = [];
    private readonly Dictionary<int, Ff7WorldModelAdapter> worldModels = [];

    private void ResetWorldPresentation()
    {
        worldPresentationOwner = null;
        worldCamera = null;
        worldTerrain = null;
        worldTextureCycles = null;
        worldVisibility = null;
        worldEnvironment = null;
        worldEnvironmentRenderer = null;
        worldMeshes.Clear();
        worldModels.Clear();
    }

    private void PrepareWorldPresentation(Ff7WorldRuntime world)
    {
        if (ReferenceEquals(worldPresentationOwner, world))
        {
            return;
        }
        ResetWorldPresentation();
        Ff7WorldPresentationData data = Ff7PcExecutableData.LoadWorldPresentation(installation, world.Resources);
        Ff7WorldTextureResources textures = new(installation, world.Resources);
        worldCamera = new(data);
        worldVisibility = new(data);
        worldTerrain = new(textures);
        worldTextureCycles = new(installation, world.Resources, textures.Descriptors);
        worldEnvironment = Ff7WorldEnvironmentResources.Load(installation, world.Resources);
        worldEnvironmentRenderer = new(worldEnvironment, data);
        worldPresentationOwner = world;
    }

    private void RenderWorld(Ff7WorldRuntime world, Rectangle bounds)
    {
        PrepareWorldPresentation(world);
        Ff7WorldProcess state = world.Process;
        triangles.Begin(bounds.Width, bounds.Height, worldEnvironment!.ClearColor, 0);
        try
        {
            Ff7WorldEntity? controlled = state.Entities.SingleOrDefault(entity => entity.Model == state.ControlledModel);
            if (controlled is not null)
            {
                Vector3N focus = new(controlled.X, controlled.Y, controlled.Z);
                Ff7WorldCameraState camera = state.Camera ?? new();
                Ff7WorldView view = worldCamera!.Create(focus, camera.Heading, camera.Pitch, camera.Distance, camera.GroundHeight, camera.Roll, camera.Focal, bounds.Width, bounds.Height, state.ViewMode);
                Ff7WorldSkyColors colors = worldEnvironment.SkyColorsAt(focus, state.Lights.Select(light => new Ff7WorldSkyZone(light.Index, light.X, light.Z, light.Enabled, light.Radius, light.Radius2, new(light.Red, light.Green, light.Blue), new(light.Red2, light.Green2, light.Blue2))));
                worldEnvironmentRenderer!.DrawSky(triangles, view, focus, camera.Heading, camera.Distance, camera.GroundHeight, camera.Roll, state.MainState >= 1, state.Sky?.CloudPhase ?? 0, colors.Upper, colors.Lower);
                foreach (Ff7WorldChunkPlacement placement in worldVisibility!.RequiredChunks(controlled.X, controlled.Z, camera.Heading, state.WorldMapState))
                {
                    var key = (placement.BlockIndex, placement.MeshIndex);
                    if (!worldMeshes.TryGetValue(key, out PcWorldMesh? mesh))
                    {
                        mesh = world.Resources.Map.ReadMesh(placement.BlockIndex, placement.MeshIndex);
                        worldMeshes.Add(key, mesh);
                    }
                    worldTerrain!.DrawNative(triangles, mesh, placement.Origin, view, texture => worldTextureCycles!.Frame(texture, state.PresentationCounter));
                }
                foreach (Ff7WorldEntity entity in state.Entities)
                {
                    if (worldVisibility.VisiblePosition(focus, new(entity.X, entity.Y, entity.Z), entity.Flags, camera.Heading) is not Vector3N position)
                    {
                        continue;
                    }
                    if (!worldModels.TryGetValue(entity.Model, out Ff7WorldModelAdapter? model))
                    {
                        Ff7WorldCharacterDescriptor descriptor = Ff7PcExecutableData.LoadWorldCharacterDescriptor(installation, entity.Model);
                        model = new(descriptor, descriptor.Load(world.Resources));
                        worldModels.Add(entity.Model, model);
                    }
                    int storyCharacter = entity.Model switch { 0 => 0, 1 => 2, 2 => 8, _ => -1 };
                    Ff7WorldModelAdapter.Draw(triangles, model.Create(position, (entity.Direction + entity.TurnOffset) & 4095, entity.PoseAnimation, entity.PoseFrame, appearance, storyCharacter), view);
                }
                worldEnvironmentRenderer.DrawRadar(triangles, bounds.Width, bounds.Height, state.RadarMode, state.ViewMode, camera.Heading, state.PresentationCounter, state.Entities.Select(entity => new Ff7WorldRadarMarker(entity.Model, entity.X, entity.Z, entity.Model == state.ControlledModel)).ToArray());
            }
            else if (state.Initialized)
            {
                throw new InvalidDataException("FFVII world presentation has no native controlled model.");
            }
            triangles.End();
        }
        catch
        {
            triangles.Abort();
            throw;
        }
    }
}
