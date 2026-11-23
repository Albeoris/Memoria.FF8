using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Mods;
using Memoria.FF8.Rendering;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    internal FieldScene3DMods ActiveSceneMods3D { get; set; } = FieldScene3DMods.Empty;
    internal PreparedFieldScene? Scene3DPreview { get; set; }
    internal bool Scene3DCompareNative { get; set; }
    internal float? Scene3DPreviewTime { get; set; }
    internal string? Scene3DStatus { get; private set; }
    internal Vector2 Scene3DNativeScroll => lastLightingPresentationFrame?.Scroll.ActorOffset ?? Vector2.Zero;

    internal (float Rms, float Maximum) MeasureScene3DReprojection(IReadOnlyList<FieldSceneLandmark> points, Matrix4x4 placement)
    {
        FieldNativeRenderView view = renderer?.NativeView ?? throw new InvalidOperationException("Wait for the native field view before measuring calibration.");
        float ProjectError(FieldSceneLandmark point)
        {
            FieldCamera source = view.Camera;
            ProjectedPoint expected = source.Project(point.Field);
            ProjectedPoint actual = source.Project(Vector3.Transform(point.Asset, placement));
            if (expected.Depth <= 1 || actual.Depth <= 1)
            {
                throw new InvalidDataException("A calibration landmark lies behind the native camera.");
            }
            return (new Vector2(actual.X - expected.X, actual.Y - expected.Y) * view.FocalPixels / source.FocalLength).Length();
        }
        float[] errors = points.Select(ProjectError).ToArray();
        return (MathF.Sqrt(errors.Sum(error => error * error) / errors.Length), errors.Max());
    }

    private FieldAssets? customSceneClockField;
    private TimeSpan customSceneEntry;
    private FieldSceneView? customSceneReplacement;
    private IReadOnlySet<int>? customSceneHiddenModules;

    private FieldSceneView? PrepareScene3D(FieldPresentationFrame frame, TimeSpan elapsed, ITriangleRenderer? backend, FieldCamera? inspectionCamera)
    {
        renderer!.CustomSceneInstances = [];
        renderer.CustomSceneCamera = null;
        renderer.CustomSceneOwnsNativeShadows = false;
        Scene3DStatus = null;
        if (Scene3DCompareNative || Field is not { } current || session.State.Scene != Runtime.SceneKind.Field)
        {
            return null;
        }
        if (!ReferenceEquals(current, customSceneClockField))
        {
            customSceneClockField = current;
            customSceneEntry = elapsed;
        }
        IReadOnlyDictionary<int, int> states = Events.BackgroundStates;
        IReadOnlyList<PreparedFieldSceneView> views = Scene3DPreview is { } preview && preview.Document.Field == current.Name
            ? preview.Document.Views.Where(view => view.Matches(frame.CameraIndex, states)).Select(view => new PreparedFieldSceneView(preview, view)).ToArray()
            : ActiveSceneMods3D.Resolve(current.Name, frame.CameraIndex, states);
        if (views.Count == 0)
        {
            return null;
        }
        if (backend is null)
        {
            Scene3DStatus = "3D field scenes require the GPU triangle renderer. Native field presentation is active.";
            return null;
        }

        List<FieldSceneRenderInstance> instances = [];
        FieldSceneView? replacement = null;
        foreach (PreparedFieldSceneView resolved in views)
        {
            FieldSceneView view = resolved.View;
            if (view.Background == FieldSceneBackgroundMode.Replace && current.Background.Any(tile => tile.AnimationId != 255 && !view.CoversNativeModule(tile.AnimationId)) ||
                view.Background == FieldSceneBackgroundMode.SelectedModules && view.ReplacedModules!.Any(module => !current.Background.Any(tile => tile.AnimationId == module)))
            {
                Scene3DStatus = $"Package '{resolved.Scene.PackageId}', view '{view.Id}' has incomplete native background coverage.";
                continue;
            }
            if (view.Projection == FieldSceneProjection.Authored && inspectionCamera is null)
            {
                if (frame.FieldParticleSprites is { Count: > 0 })
                {
                    Scene3DStatus = $"Package '{resolved.Scene.PackageId}', view '{view.Id}' has a native screen-space effect incompatible with its authored camera.";
                    continue;
                }
                FieldSceneInstance owner = view.Instances.Single(instance => instance.Id == view.CameraInstance);
                FieldSceneAsset asset = resolved.Scene.Assets[owner.AssetId];
                FieldSceneCamera? camera = asset.Cameras.FirstOrDefault(candidate => candidate.Id == view.CameraId);
                if (camera is null)
                {
                    Scene3DStatus = $"View '{view.Id}' needs its imported camera remapped.";
                    continue;
                }
                (int cameraAnimation, float cameraTime) = SampleScene3DPlayback(owner, asset, elapsed);
                Matrix4x4 cameraToField = asset.SampleTransforms(cameraAnimation, cameraTime)[camera.Node] * FieldSceneDocuments.Matrix(owner.AssetToField);
                if (!Matrix4x4.Decompose(cameraToField, out Vector3 scale, out Quaternion rotation, out Vector3 origin) || scale.X <= 0 || MathF.Abs(scale.X - scale.Y) > scale.X * 0.001f || MathF.Abs(scale.X - scale.Z) > scale.X * 0.001f || !Matrix4x4.Invert(Matrix4x4.CreateFromQuaternion(rotation) * Matrix4x4.CreateTranslation(origin), out Matrix4x4 fieldToCamera))
                {
                    Scene3DStatus = $"Package '{resolved.Scene.PackageId}', camera '{camera.Id}' needs a positive uniform scale without shear.";
                    continue;
                }
                renderer.CustomSceneCamera = new(fieldToCamera * Matrix4x4.CreateScale(1, -1, -1), camera.Orthographic, camera.Orthographic ? scale.X * camera.YMagnitude : MathF.Tan(camera.VerticalFieldOfView / 2), camera.AspectRatio, scale.X * camera.NearPlane, camera.FarPlane * scale.X)
                {
                    FrameFit = (Rendering.FieldSceneFrameFit)view.FrameFit,
                    PrincipalOffset = (camera.PrincipalPoint ?? new Vector2(0.5f)) - new Vector2(0.5f),
                    NativeScrollBaseline = view.ScrollBaseline is { } baseline ? new Vector2(baseline.X, baseline.Y) : null
                };
            }
            if (view.Background != FieldSceneBackgroundMode.Overlay)
            {
                replacement = view;
            }
            foreach (FieldSceneInstance binding in view.Instances.Where(instance => instance.Enabled))
            {
                FieldSceneAsset asset = resolved.Scene.Assets[binding.AssetId];
                (int clipIndex, float time) = SampleScene3DPlayback(binding, asset, elapsed);
                Dictionary<string, FieldSceneRenderMaterial>? materials = binding.Materials?.ToDictionary(material => material.MaterialId, material => new FieldSceneRenderMaterial(material.Shading switch
                {
                    FieldSceneShading.Lit => FieldSceneMaterialMode.FieldLit,
                    FieldSceneShading.UnlitShadowReceiver => FieldSceneMaterialMode.UnlitShadowReceiver,
                    FieldSceneShading.Unlit => FieldSceneMaterialMode.Unlit,
                    _ => asset.Materials.FirstOrDefault(source => source.Id == material.MaterialId)?.Unlit == true ? FieldSceneMaterialMode.Unlit : FieldSceneMaterialMode.FieldLit
                }, material.CastShadows, material.ReceiveShadows)
                {
                    AlphaMode = (FieldSceneRenderAlphaMode)material.Alpha,
                    DoubleSided = material.DoubleSided
                });
                Dictionary<string, FieldSceneGeometryRole>? roles = binding.Materials is null ? null : asset.Primitives.Where(primitive => primitive.MaterialIndex >= 0).ToDictionary(primitive => primitive.Id, primitive => binding.Materials.FirstOrDefault(material => material.MaterialId == asset.Materials[primitive.MaterialIndex].Id)?.Role switch
                {
                    FieldSceneRole.Occluder => FieldSceneGeometryRole.OccluderProxy,
                    FieldSceneRole.ShadowReceiver => FieldSceneGeometryRole.ReceiverProxy,
                    _ => FieldSceneGeometryRole.PhysicalSurface
                });
                instances.Add(new(asset, FieldSceneDocuments.Matrix(binding.AssetToField), clipIndex, time, materials, roles));
            }
        }
        renderer.CustomSceneInstances = instances;
        renderer.CustomSceneOwnsNativeShadows = replacement?.Background == FieldSceneBackgroundMode.Replace;
        if (Scene3DStatus is null && instances.Count > 0)
        {
            Scene3DStatus = $"{instances.Count} 3D scene instance(s), camera slot {frame.CameraIndex}";
        }
        if (!ReferenceEquals(customSceneReplacement, replacement))
        {
            customSceneReplacement = replacement;
            customSceneHiddenModules = replacement?.ReplacedModules?.ToHashSet();
        }
        return replacement;
    }

    private (int Clip, float Time) SampleScene3DPlayback(FieldSceneInstance instance, FieldSceneAsset asset, TimeSpan elapsed)
    {
        if (instance.Playback is not { } playback || asset.Clips.FirstOrDefault(clip => clip.Id == playback.ClipId) is not { } clip)
        {
            return (-1, 0);
        }
        float seconds = Scene3DPreviewTime ?? (playback.Autoplay ? (float)Math.Max(0, (elapsed - customSceneEntry).TotalSeconds) : 0);
        float position = seconds * playback.Speed + playback.PhaseSeconds;
        float offset = playback.Loop && clip.Duration > 0 ? (position % clip.Duration + clip.Duration) % clip.Duration : Math.Clamp(position, 0, clip.Duration);
        return (clip.AnimationIndex, clip.StartTime + offset);
    }

    private IReadOnlySet<int>? Scene3DHiddenModules(FieldSceneView? replacement, IReadOnlySet<int>? debugModules)
    {
        if (replacement?.Background != FieldSceneBackgroundMode.SelectedModules || customSceneHiddenModules is null)
        {
            return debugModules;
        }
        return debugModules is null || debugModules.Count == 0 ? customSceneHiddenModules : customSceneHiddenModules.Concat(debugModules).ToHashSet();
    }

    internal string ExportScene3DReference(string path, string provider, string? language, TimeSpan elapsed)
    {
        if (Field is not { } current || !ReferenceEquals(lastLightingPresentationField, current) || lastLightingPresentationFrame is not { } frame || !ReferenceEquals(frame.Owner, Events.Scene) || frame.Frame != Events.Frame || frame.CameraIndex != Events.Scene.CameraIndex || lastLightingPresentationSample is not { } sample || Events.FieldShake is not { } shake)
        {
            throw new InvalidOperationException("Wait for the current field and camera to finish presenting before exporting.");
        }
        FieldAssets nativeField = current with { Background = current.Background.Select(tile => tile with { Texture = null }).ToArray() };
        Dictionary<int, int> states = new(Events.BackgroundStates);
        FieldRenderer referenceRenderer = new(nativeField, renderWidth, renderHeight) { NativeFieldShadows = false };
        BackgroundScrollFrame scroll = frame.Scroll;
        byte[] background = referenceRenderer.Render(states, [], elapsed, cameraIndex: frame.CameraIndex, backgroundModuleShades: sample.BackgroundShades, backgroundScrollOffsets: scroll.LayerOffsets, actorScrollOffset: scroll.ActorOffset, nativeWrapFrame: scroll.NativeWrap, drawingJitterX: shake.X.Output, drawingJitterY: shake.Y.Output, backgroundBlend: sample.Background, backgroundLayerScales: scroll.LayerScales);
        FieldNativeRenderView view = referenceRenderer.NativeView;
        Vector2 jitter = new(shake.X.Output, shake.Y.Output);
        return FieldSceneReferenceExchange.Export(path, nativeField, new FieldSceneReferenceCapture(frame.CameraIndex, view.Camera, renderWidth, renderHeight, background)
        {
            ResourceProvider = provider,
            Language = language,
            Elapsed = elapsed,
            ActorScroll = view.ActorOffset - jitter,
            Jitter = jitter,
            DrawingOrigin = view.DrawingOrigin,
            OutputViewport = view.Viewport,
            ProjectionScale = view.FocalPixels,
            PrincipalPoint = view.PrincipalPixels,
            BackgroundStates = states,
            SimulationFrame = frame.Frame,
            ResourceGeneration = current.Background.Select(tile => tile.Texture).FirstOrDefault(texture => texture is not null)?.Generation,
            Actors = sample.Actors.Where(actor => actor.Visible && (uint)actor.ModelId < nativeField.Models.Count).Select(actor => new FieldSceneReferenceActor(actor.ObjectId, actor.ModelId, new(actor.X, actor.Y, actor.Z), actor.Direction, actor.AnimationId, actor.AnimationFrame)).ToArray()
        });
    }
}
