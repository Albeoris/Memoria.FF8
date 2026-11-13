using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF8.Rendering;

public sealed record Ff7WorldCameraView(int Heading, int Pitch, int Distance, int GroundHeight, int Roll, int Focal);
public sealed record Ff7WorldVisual(int Model, Vector3 Position, int Direction, int Animation, int Frame, int Flags)
{
    public long PresentationEpoch { get; init; }
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
    public int? SubmarineTint { get; init; }
    public Ff7WorldBubbleVisual? ApproachBubble { get; init; }
    public int ShakeX { get; init; }
    public int ShakeY { get; init; }
}

/// <summary>Composes original world resource adapters into the common triangle backend without advancing simulation.</summary>
public sealed class Ff7WorldPresentation
{
    private readonly Ff7Installation installation;
    private readonly Ff7WorldResources resources;
    private readonly IFf7ActorAppearance? appearance;
    private readonly Ff7WorldCameraProjection camera;
    private readonly Ff7WorldTerrainAdapter terrain;
    private readonly Ff7WorldTextureAnimationResources? textureCycles;
    private readonly Ff7WorldVisibility visibility;
    private readonly Ff7WorldEnvironmentResources? environment;
    private readonly Ff7WorldEnvironmentAdapter? environmentRenderer;
    private readonly Ff7WorldBridgeGeometry bridges;
    private readonly Ff7WorldFootShadowAdapter? shadows;
    private Ff7WorldApproachBubbleAdapter? approachBubbleRenderer;
    private readonly Dictionary<(int Block, int Mesh, int Bridge), PcWorldMesh> meshes = [];
    private readonly Dictionary<(int Model, int? Tint), Ff7WorldModelAdapter> models = [];

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
        if (resources.World != 2)
        {
            textureCycles = new(installation, resources, textures.Descriptors);
            environment = Ff7WorldEnvironmentResources.Load(installation, resources);
            environmentRenderer = new(environment, data);
            shadows = new(environment.Shadow, new Ff7WorldMath(data));
        }
    }

    public void Render(ITriangleRenderer renderer, int width, int height, Ff7WorldRenderFrame frame)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        IWorldTerrainRenderer hardware = renderer as IWorldTerrainRenderer ?? throw new NotSupportedException("FFVII world presentation requires retained terrain rendering.");
        ArgumentNullException.ThrowIfNull(frame);
        renderer.Begin(width, height, environment?.ClearColor ?? new Vector4(0, 0, 0, 1), 0);
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
            Ff7WorldView view = camera.Create(focus, nativeCamera.Heading, nativeCamera.Pitch, nativeCamera.Distance, nativeCamera.GroundHeight, nativeCamera.Roll, nativeCamera.Focal, width, height, frame.ViewMode, frame.ShakeX, frame.ShakeY);
            bool underwater = resources.World == 2;
            if (environment is not null && environmentRenderer is not null)
            {
                Ff7WorldSkyColors colors = environment.SkyColorsAt(focus, frame.SkyZones);
                environmentRenderer.DrawSky(renderer, view, focus, nativeCamera.Heading, nativeCamera.Distance, nativeCamera.GroundHeight, nativeCamera.Roll, frame.MainState >= 1, frame.CloudPhase, colors.Upper, colors.Lower);
            }
            int TextureFrame(int texture) => textureCycles?.Frame(texture, frame.Counter) ?? 0;
            int curvatureThreshold = underwater ? int.MaxValue : frame.CurvatureThreshold;
            foreach (Ff7WorldChunkPlacement placement in visibility.RequiredChunks((int)focus.X, (int)focus.Z, nativeCamera.Heading, frame.WorldMapState, underwater))
            {
                Ff7WorldBridgeDefinition? bridge = underwater ? null : bridges.Definition(placement.ChunkX, placement.ChunkZ);
                var key = (placement.BlockIndex, placement.MeshIndex, bridge?.Index ?? -1);
                if (!meshes.TryGetValue(key, out PcWorldMesh? mesh))
                {
                    mesh = resources.Map.ReadMesh(placement.BlockIndex, placement.MeshIndex);
                    meshes.Add(key, mesh);
                }
                terrain.DrawRetained(hardware, mesh, placement.Origin, view, TextureFrame, curvatureThreshold, bridge, bridges.RotationAxis(bridge, frame.BridgeDeformation));
            }
            List<(Ff7WorldVisual Entity, Vector3 Position, int Curvature)> visible = [];
            foreach (Ff7WorldVisual entity in frame.Models)
            {
                if (visibility.VisiblePosition(focus, entity.Position, entity.Flags, nativeCamera.Heading) is not Vector3 position)
                {
                    continue;
                }
                int curvature = entity.Model == frame.ControlledModel ? 0 : Ff7WorldFootShadowAdapter.Curvature(position, view, nativeCamera.GroundHeight, curvatureThreshold);
                visible.Add((entity, position, curvature));
                shadows?.Draw(renderer, entity, position, curvature, view, nativeCamera.Roll, entity.Model == frame.ControlledModel, frame.HeightSmoothVelocity);
            }
            foreach ((Ff7WorldVisual entity, Vector3 position, int curvature) in visible)
            {
                int? tint = underwater && entity.Model == 13 ? frame.SubmarineTint : null;
                var key = (entity.Model, tint);
                if (!models.TryGetValue(key, out Ff7WorldModelAdapter? model))
                {
                    Ff7WorldCharacterDescriptor descriptor = Ff7PcExecutableData.LoadWorldCharacterDescriptor(installation, entity.Model, resources.World, tint);
                    model = new(descriptor, descriptor.Load(resources));
                    models.Add(key, model);
                }
                int storyCharacter = entity.Model switch { 0 => 0, 1 => 2, 2 => 8, _ => -1 };
                Vector3 bodyPosition = position + new Vector3(0, entity.Elevation - curvature, 0);
                Ff7WorldModelAdapter.Draw(renderer, model.Create(bodyPosition, entity.Direction, entity.Animation, entity.Frame, appearance, storyCharacter), view);
            }
            if (frame.ApproachBubble is { } bubble)
            {
                approachBubbleRenderer ??= new(PcTexture.Read(resources.ReadRequired("dfx.tex")));
                approachBubbleRenderer.Draw(renderer, view, focus, bubble);
            }
            environmentRenderer?.DrawRadar(renderer, width, height, frame.RadarMode, frame.ViewMode, nativeCamera.Heading, frame.Counter, frame.Models.Select(entity => new Ff7WorldRadarMarker(entity.Model, (int)entity.Position.X, (int)entity.Position.Z, entity.Model == frame.ControlledModel)).ToArray());
        }
        finally
        {
            renderer.End();
        }
    }
}
