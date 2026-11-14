using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF8.Rendering;

public sealed record Ff7WorldCameraView(int Heading, int Pitch, int Distance, int GroundHeight, int Roll, int Focal);
public sealed record Ff7WorldVisual(int Model, Vector3 Position, int Direction, int Animation, int Frame, int Flags)
{
    public long PresentationEpoch { get; init; }
    public long PresentationAnimationEpoch { get; init; }
    public int? GroundHeight { get; init; }
    public int Terrain { get; init; }
    public int NativeAnimation { get; init; }
    public int Elevation { get; init; }
    public Ff7FieldPoseBlend? AnimationBlend { get; init; }
    public float TiltX { get; init; }
    public float TiltY { get; init; }
    public float TiltZ { get; init; }
}
public sealed record Ff7WorldRenderFrame(int ControlledModel, bool Initialized, int MainState, int ViewMode, int RadarMode, int WorldMapState, uint Counter, int CloudPhase, Ff7WorldCameraView Camera, IReadOnlyList<Ff7WorldVisual> Models, IReadOnlyList<Ff7WorldSkyZone> SkyZones)
{
    public int? ResourceArea { get; init; }
    public int ChocoboTint { get; init; }
    public bool MeteorVisible { get; init; }
    public IReadOnlyList<Ff7WorldEffectVisual> Effects { get; init; } = [];
    public int HighwindPropellerFrame { get; init; }
    public Ff7FieldPoseBlend? HighwindPropellerBlend { get; init; }
    public IReadOnlyList<Ff7WorldMarshSegment> MarshSegments { get; init; } = [];
    public IReadOnlyList<Ff7WorldSnowLayer> SnowLayers { get; init; } = [];
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
    private Ff7WorldEffectsAdapter? effectsRenderer;
    private Ff7WorldMarshAdapter? marshRenderer;
    private Ff7WorldSnowAdapter? snowRenderer;
    private readonly Dictionary<(int Block, int Mesh, int Bridge), PcWorldMesh> meshes = [];
    private readonly Dictionary<(int Model, int? Tint, int? Area), Ff7WorldModelAdapter?> models = [];

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
        terrain = new(textures, resources.World == 2);
        environment = Ff7WorldEnvironmentResources.Load(installation, resources);
        environmentRenderer = new(environment, data, resources.World == 0 ? PcTexture.Read(resources.ReadRequired("midlmap2.tex")) : null);
        if (resources.World == 0)
        {
            textureCycles = new(installation, resources, textures.Descriptors);
        }
        if (resources.World != 2)
        {
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
            if (resources.World == 0 && environment is not null && environmentRenderer is not null)
            {
                Ff7WorldSkyColors colors = environment.SkyColorsAt(focus, frame.SkyZones);
                environmentRenderer.DrawSky(renderer, view, focus, nativeCamera.Heading, nativeCamera.Distance, nativeCamera.GroundHeight, nativeCamera.Roll, frame.MainState >= 1, frame.CloudPhase, colors.Upper, colors.Lower, frame.MeteorVisible, frame.CurvatureThreshold);
            }
            int TextureFrame(int texture) => textureCycles?.Frame(texture, frame.Counter) ?? 0;
            int curvatureThreshold = underwater ? int.MaxValue : frame.CurvatureThreshold;
            IReadOnlyList<Ff7WorldChunkPlacement> nativeChunks = visibility.RequiredChunks((int)focus.X, (int)focus.Z, nativeCamera.Heading, frame.WorldMapState, underwater, resources.World == 3);
            PcWorldMesh ReadMesh(Ff7WorldChunkPlacement placement, Ff7WorldBridgeDefinition? bridge)
            {
                var key = (placement.BlockIndex, placement.MeshIndex, bridge?.Index ?? -1);
                if (!meshes.TryGetValue(key, out PcWorldMesh? mesh))
                {
                    mesh = resources.Map.ReadMesh(placement.BlockIndex, placement.MeshIndex);
                    meshes.Add(key, mesh);
                }
                return mesh;
            }
            float farDepth = 1;
            if (width * 3L > height * 4L)
            {
                foreach (Ff7WorldChunkPlacement placement in nativeChunks)
                {
                    foreach (Vector3 vertex in ReadMesh(placement, underwater ? null : bridges.Definition(placement.ChunkX, placement.ChunkZ)).Vertices)
                    {
                        farDepth = MathF.Max(farDepth, Vector3.Transform(vertex + placement.Origin, view.NativeView).Z);
                    }
                }
            }
            foreach (Ff7WorldChunkPlacement placement in Ff7WorldViewportChunks.Extend(nativeChunks, focus, view, farDepth, frame.WorldMapState, underwater, resources.World == 3))
            {
                Ff7WorldBridgeDefinition? bridge = underwater ? null : bridges.Definition(placement.ChunkX, placement.ChunkZ);
                PcWorldMesh mesh = ReadMesh(placement, bridge);
                terrain.DrawRetained(hardware, mesh, placement.Origin, view, TextureFrame, curvatureThreshold, bridge, bridges.RotationAxis(bridge, frame.BridgeDeformation));
            }
            if (frame.MarshSegments.Count != 0)
            {
                marshRenderer ??= new(PcTexture.Read(resources.ReadRequired("midg.tex")), Ff7PcExecutableData.LoadWorldPresentation(installation, resources));
                marshRenderer.Draw(renderer, view, focus, nativeCamera.GroundHeight, curvatureThreshold, nativeChunks, frame.MarshSegments);
            }
            List<(Ff7WorldVisual Entity, Vector3 Position, int Curvature, Ff7WorldModelAdapter Model)> visible = [];
            foreach (Ff7WorldVisual entity in frame.Models)
            {
                if (visibility.VisiblePosition(focus, entity.Position, entity.Flags, nativeCamera.Heading) is not Vector3 position)
                {
                    continue;
                }
                int? tint = underwater && entity.Model == 13 ? frame.SubmarineTint : entity.Model == 19 ? frame.ChocoboTint : null;
                var key = (entity.Model, tint, frame.ResourceArea);
                if (!models.TryGetValue(key, out Ff7WorldModelAdapter? model))
                {
                    model = Ff7PcExecutableData.TryLoadWorldCharacterDescriptor(installation, entity.Model, out Ff7WorldCharacterDescriptor? descriptor, resources.World, tint, frame.ResourceArea) ? new(descriptor!, descriptor!.Load(resources)) : null;
                    models.Add(key, model);
                }
                if (model is null)
                {
                    continue;
                }
                int curvature = entity.Model == frame.ControlledModel ? 0 : Ff7WorldFootShadowAdapter.Curvature(position, view, nativeCamera.GroundHeight, curvatureThreshold);
                visible.Add((entity, position, curvature, model));
                shadows?.Draw(renderer, entity, position, curvature, view, nativeCamera.Roll, entity.Model == frame.ControlledModel, frame.HeightSmoothVelocity);
            }
            foreach ((Ff7WorldVisual entity, Vector3 position, int curvature, Ff7WorldModelAdapter model) in visible)
            {
                int storyCharacter = entity.Model switch { 0 => 0, 1 => 2, 2 => 8, _ => -1 };
                Vector3 bodyPosition = position + new Vector3(0, entity.Elevation - curvature, 0);
                int colorWeight = underwater && entity.Model is 17 or 18 or 26 or 27 or 28 or 30 ? Ff7WorldLighting.UnderwaterModelWeight((int)MathF.Round(Vector3.Transform(bodyPosition, view.NativeView).Z, MidpointRounding.ToEven)) : 255;
                Ff7WorldModelAdapter.Draw(renderer, model.Create(bodyPosition, entity.Direction, entity.Animation, entity.Frame, appearance, storyCharacter, entity.AnimationBlend, new(entity.TiltX, entity.TiltY, entity.TiltZ), entity.Model == 6 ? 4096 : 256), view, underwater, colorWeight);
                if (entity.Model == 3 && frame.ResourceArea is 1 or 2 or 5 or 7)
                {
                    var propellerKey = (9, (int?)null, frame.ResourceArea);
                    if (!models.TryGetValue(propellerKey, out Ff7WorldModelAdapter? propeller))
                    {
                        Ff7WorldCharacterDescriptor descriptor = Ff7PcExecutableData.LoadWorldCharacterDescriptor(installation, 9, resources.World, resourceArea: frame.ResourceArea);
                        propeller = new(descriptor, descriptor.Load(resources));
                        models.Add(propellerKey, propeller);
                    }
                    Ff7WorldModelAdapter.Draw(renderer, propeller!.Create(bodyPosition, entity.Direction, 0, frame.HighwindPropellerFrame, animationBlend: frame.HighwindPropellerBlend, nativeTilt: new(entity.TiltX, entity.TiltY, entity.TiltZ)), view);
                }
            }
            if (frame.ApproachBubble is { } bubble)
            {
                approachBubbleRenderer ??= new(PcTexture.Read(resources.ReadRequired("dfx.tex")));
                approachBubbleRenderer.Draw(renderer, view, focus, bubble);
            }
            if (frame.Effects.Count != 0)
            {
                effectsRenderer ??= new(PcTexture.Read(resources.ReadRequired("dfx.tex")), Ff7PcExecutableData.LoadWorldEffects(installation), Ff7PcExecutableData.LoadWorldPresentation(installation, resources));
                effectsRenderer.Draw(renderer, view, focus, nativeCamera.Roll, curvatureThreshold, frame.Effects);
            }
            if (frame.SnowLayers.Count != 0)
            {
                snowRenderer ??= new(PcTexture.Read(resources.ReadRequired("snow4.tex")), PcTexture.Read(resources.ReadRequired("snow5.tex")));
                snowRenderer.Draw(renderer, width, height, frame.SnowLayers);
            }
            if (resources.World != 3)
            {
                environmentRenderer?.DrawRadar(renderer, width, height, frame.RadarMode, frame.ViewMode, nativeCamera.Heading, frame.Counter, frame.Models.Select(entity => new Ff7WorldRadarMarker(entity.Model, (int)entity.Position.X, (int)entity.Position.Z, entity.Model == frame.ControlledModel)).ToArray(), alternateMap: resources.World == 0 && frame.ResourceArea is >= 1 and <= 8 or 10);
            }
        }
        finally
        {
            renderer.End();
        }
    }
}
