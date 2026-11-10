using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF8.Rendering;

public sealed record Ff7WorldCameraView(int Heading, int Pitch, int Distance, int GroundHeight, int Roll, int Focal);
public sealed record Ff7WorldVisual(int Model, Vector3 Position, int Direction, int Animation, int Frame, int Flags)
{
    public int? GroundHeight { get; init; }
    public int Terrain { get; init; }
    public int NativeAnimation { get; init; }
    public int Elevation { get; init; }
}
public sealed record Ff7WorldRenderFrame(int ControlledModel, bool Initialized, int MainState, int ViewMode, int RadarMode, int WorldMapState, uint Counter, int CloudPhase, Ff7WorldCameraView Camera, IReadOnlyList<Ff7WorldVisual> Models, IReadOnlyList<Ff7WorldSkyZone> SkyZones)
{
    public Ff7WorldBridgeDeformation? BridgeDeformation { get; init; }
    public int HeightSmoothVelocity { get; init; }
    public int CurvatureThreshold { get; init; } = 5000;
}

/// <summary>Composes original world resource adapters into the common triangle backend without advancing simulation.</summary>
public sealed class Ff7WorldPresentation
{
    private readonly Ff7Installation installation;
    private readonly Ff7WorldResources resources;
    private readonly IFf7ActorAppearance? appearance;
    private readonly Ff7WorldCameraProjection camera;
    private readonly Ff7WorldTerrainAdapter terrain;
    private readonly Ff7WorldTextureAnimationResources textureCycles;
    private readonly Ff7WorldVisibility visibility;
    private readonly Ff7WorldEnvironmentResources environment;
    private readonly Ff7WorldEnvironmentAdapter environmentRenderer;
    private readonly Ff7WorldBridgeGeometry bridges;
    private readonly Ff7WorldFootShadowAdapter shadows;
    private readonly Dictionary<(int Block, int Mesh), PcWorldMesh> meshes = [];
    private readonly Dictionary<int, Ff7WorldModelAdapter> models = [];

    public Ff7WorldPresentation(Ff7Installation installation, Ff7WorldResources resources, IFf7ActorAppearance? appearance = null)
    {
        this.installation = installation;
        this.resources = resources;
        this.appearance = appearance;
        Ff7WorldPresentationData data = Ff7PcExecutableData.LoadWorldPresentation(installation, resources);
        Ff7WorldTextureResources textures = new(installation, resources);
        camera = new(data);
        visibility = new(data);
        bridges = new(Ff7PcExecutableData.LoadWorldBridges(installation), new Ff7WorldMath(data));
        terrain = new(textures);
        textureCycles = new(installation, resources, textures.Descriptors);
        environment = Ff7WorldEnvironmentResources.Load(installation, resources);
        environmentRenderer = new(environment, data);
        shadows = new(environment.Shadow, new Ff7WorldMath(data));
    }

    public void Render(ITriangleRenderer renderer, int width, int height, Ff7WorldRenderFrame frame)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(frame);
        renderer.Begin(width, height, environment.ClearColor, 0);
        try
        {
            Ff7WorldVisual? controlled = frame.Models.SingleOrDefault(model => model.Model == frame.ControlledModel);
            if (controlled is null)
            {
                if (frame.Initialized)
                {
                    throw new InvalidDataException("FFVII world presentation has no native controlled model.");
                }
                return;
            }
            Vector3 focus = controlled.Position;
            Ff7WorldCameraView nativeCamera = frame.Camera;
            Ff7WorldView view = camera.Create(focus, nativeCamera.Heading, nativeCamera.Pitch, nativeCamera.Distance, nativeCamera.GroundHeight, nativeCamera.Roll, nativeCamera.Focal, width, height, frame.ViewMode);
            Ff7WorldSkyColors colors = environment.SkyColorsAt(focus, frame.SkyZones);
            environmentRenderer.DrawSky(renderer, view, focus, nativeCamera.Heading, nativeCamera.Distance, nativeCamera.GroundHeight, nativeCamera.Roll, frame.MainState >= 1, frame.CloudPhase, colors.Upper, colors.Lower);
            foreach (Ff7WorldChunkPlacement placement in visibility.RequiredChunks((int)focus.X, (int)focus.Z, nativeCamera.Heading, frame.WorldMapState))
            {
                var key = (placement.BlockIndex, placement.MeshIndex);
                if (!meshes.TryGetValue(key, out PcWorldMesh? mesh))
                {
                    mesh = resources.Map.ReadMesh(placement.BlockIndex, placement.MeshIndex);
                    meshes.Add(key, mesh);
                }
                terrain.DrawNative(renderer, mesh, placement.Origin, view, texture => textureCycles.Frame(texture, frame.Counter), deformVertex: frame.BridgeDeformation is null ? null : (vertex, position) => bridges.Vertex(placement.ChunkX, placement.ChunkZ, vertex, position, frame.BridgeDeformation));
            }
            List<(Ff7WorldVisual Entity, Vector3 Position, int Curvature)> visible = [];
            foreach (Ff7WorldVisual entity in frame.Models)
            {
                if (visibility.VisiblePosition(focus, entity.Position, entity.Flags, nativeCamera.Heading) is not Vector3 position)
                {
                    continue;
                }
                int curvature = entity.Model == frame.ControlledModel ? 0 : Ff7WorldFootShadowAdapter.Curvature(position, view, nativeCamera.GroundHeight, frame.CurvatureThreshold);
                visible.Add((entity, position, curvature));
                shadows.Draw(renderer, entity, position, curvature, view, nativeCamera.Roll, entity.Model == frame.ControlledModel, frame.HeightSmoothVelocity);
            }
            foreach ((Ff7WorldVisual entity, Vector3 position, int curvature) in visible)
            {
                if (!models.TryGetValue(entity.Model, out Ff7WorldModelAdapter? model))
                {
                    Ff7WorldCharacterDescriptor descriptor = Ff7PcExecutableData.LoadWorldCharacterDescriptor(installation, entity.Model);
                    model = new(descriptor, descriptor.Load(resources));
                    models.Add(entity.Model, model);
                }
                int storyCharacter = entity.Model switch { 0 => 0, 1 => 2, 2 => 8, _ => -1 };
                Vector3 bodyPosition = position + new Vector3(0, entity.Elevation - curvature, 0);
                Ff7WorldModelAdapter.Draw(renderer, model.Create(bodyPosition, entity.Direction, entity.Animation, entity.Frame, appearance, storyCharacter), view);
            }
            environmentRenderer.DrawRadar(renderer, width, height, frame.RadarMode, frame.ViewMode, nativeCamera.Heading, frame.Counter, frame.Models.Select(entity => new Ff7WorldRadarMarker(entity.Model, (int)entity.Position.X, (int)entity.Position.Z, entity.Model == frame.ControlledModel)).ToArray());
        }
        finally
        {
            renderer.End();
        }
    }
}
