using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8;

/// <summary>The live host and GPU-independent diagnostic preview render the same installed scene and actor presentation.</summary>
internal sealed partial class BattleScenePresentation
{
    private readonly BattleStage stage;
    private readonly BattleAssets assets;
    private readonly Dictionary<string, BattleCombatantDefinition> definitions;
    private readonly Dictionary<string, BattleModel> models = [];
    private readonly Dictionary<string, int?> activeVisualModels = [];
    private readonly Dictionary<string, BattleModel?> weapons = [];
    private BattleCamera? renderedCamera;
    private BattleRenderer? renderer;
    private NativeBattlePreparedPrimitiveFrame? renderedPrimitiveFrame;
    private NativeBattlePrimitiveCapabilities? renderedPrimitiveCapabilities;
    internal NativeBattlePreparedPresentation? PresentedNativeFrame { get; private set; }
    private NativeBattlePcRenderTailReceipt? renderedPcRenderTail;
    private NativeBattlePcUiFrame? renderedPcUiFrame;
    private NativeBattleEffectTextureState? renderedTextures;
    private NativeBattlePcTextureWindowCache? renderedTextureWindows;
    private byte[]? renderedPixels;
    private GpuTriangleRenderer? renderedGpu;
    private long renderedGpuVersion;
    private readonly Dictionary<(BattleModel Model, NativeBattleModelTextureBinding Binding, bool Weapon), (object[] Materials, IReadOnlyList<NativeBattleTextureReplacement> Replacements)> replacementBindings = [];
    private IReadOnlyList<NativeBattleTextureReplacement>? renderedModelReplacements;
    private long renderedEffectTextureGeneration;
    private readonly BattlePilotPresentationTimeline pilotTimeline = new();
    internal bool SmoothCompatibleMotion { get; set; }
    internal string? LastPilotFallbackReason => renderer?.LastPilotFallbackReason;
    internal int LastPilotFallbackCount => renderer?.LastPilotFallbackCount ?? 0;
    internal bool PilotDiagnosticsEnabled { get; set; }
    internal NativeBattlePilotSubmission PilotSubmission => renderer?.PilotSubmission ?? default;
    internal NativeBattleTextureDecodingObservation? TextureDecoding => renderer?.TextureDecoding;
    internal void ClearNativeRawTextureCache() => renderer?.ClearNativeRawTextureCache();
    internal NativeBattleRawTextureContentSummary? InspectNativeRawTextureContent() => renderer?.InspectNativeRawTextureContent();

    public BattleScenePresentation(BattleAssets assets, BattleDefinition definition)
    {
        this.assets = assets;
        definitions = definition.Combatants.ToDictionary(actor => actor.Id);
        stage = assets.LoadStage(definition.StageId ?? throw new NotSupportedException("A battle scene requires its installed stage identity."));
        foreach (BattleCombatantDefinition actor in definition.Combatants.Where(actor => actor.InitialActive))
        {
            EnsureActorResources(actor.Id);
        }
    }

    private static BattleCamera ReadCamera(NativeBattleCameraPose pose, NativeBattleCameraProjection projection)
    {
        int focal = projection.FocalLength ?? throw new InvalidDataException("Battle camera requires its published focal length.");
        short centerX = projection.OffsetX ?? throw new InvalidDataException("Battle camera requires its published horizontal center.");
        short centerY = projection.OffsetY ?? throw new InvalidDataException("Battle camera requires its published vertical center.");
        return BattleCamera.FromNative(new(new(pose.Eye.X, pose.Eye.Y, pose.Eye.Z), new(pose.Target.X, pose.Target.Y, pose.Target.Z), focal), pose.Roll)
            with { VerticalCenter = centerY / 240f, HorizontalCenter = centerX / 320f };
    }

    internal BattleCamera Camera => renderedCamera ?? throw new InvalidOperationException("The original camera has not published its first pose.");

    private void EnsureActorResources(string actorId, int? visualModelId = null)
    {
        if (models.ContainsKey(actorId) && activeVisualModels.GetValueOrDefault(actorId) == visualModelId)
        {
            return;
        }
        models.Remove(actorId);
        weapons.Remove(actorId);
        activeVisualModels[actorId] = visualModelId;
        BattleCombatantDefinition actor = definitions[actorId];
        bool playerResource = actor.IsPlayer && visualModelId is null;
        BattleModel model = visualModelId is int formModel ? assets.LoadEnemy(formModel) : playerResource ? assets.LoadPlayerForWeapon(actor.ModelId, actor.Costume, actor.WeaponId) : assets.LoadEnemy(actor.ModelId);
        if (!actor.IsPlayer && actor.WeaponId is not null)
        {
            throw new NotSupportedException("Enemy weapon replacement requires its own battle model attachment contract.");
        }
        BattleModel? weapon = playerResource && actor.WeaponId is int weaponId ? assets.ResolveWeaponModel(actor.ModelId, weaponId, model) : null;
        models.Add(actor.Id, model);
        weapons.Add(actor.Id, weapon);
    }

    public byte[] Render(BattleSession session, int width, int height, ITriangleRenderer? triangleRenderer = null)
    {
        return RenderCore(session, width, height, triangleRenderer, null);
    }

    public byte[] Render(BattleSession session, int width, int height, ITriangleRenderer? triangleRenderer, float presentationPhase)
    {
        return RenderCore(session, width, height, triangleRenderer, presentationPhase);
    }

    private byte[] RenderCore(BattleSession session, int width, int height, ITriangleRenderer? triangleRenderer, float? presentationPhase)
    {
        if (renderer?.Width != width || renderer.Height != height)
        {
            renderer?.ClearNativeRawTextureCache();
            renderer?.ClearNativePilotInterval();
            renderer = new BattleRenderer(width, height, ViewportLayout.Fit(320, 240, width, height));
            renderedPixels = null;
            renderedGpu = null;
        }
        renderer.TriangleRenderer = triangleRenderer;
        using IDisposable visibleArea = BattleSceneVisibleArea.Begin(BattleSceneVisibleArea.FromViewport(width, height, renderer.ProjectionBounds.X, renderer.ProjectionBounds.Y, renderer.ProjectionBounds.Width, renderer.ProjectionBounds.Height));
        renderer.PilotDiagnosticsEnabled = PilotDiagnosticsEnabled;
        BattlePresentationProcess process = session.Presentation ?? throw new InvalidOperationException("Battle rendering requires its presentation process.");
        NativeBattlePreparedPresentation? prepared = session.PreparedPresentation;
        NativeBattlePreparedPresentation? pilotNext = null;
        float pilotAlpha = 0;
        bool pilotInterval = false;
        if (prepared is not null)
        {
            (prepared, pilotNext, pilotAlpha) = pilotTimeline.Select(prepared, SmoothCompatibleMotion && presentationPhase.HasValue, presentationPhase ?? 0, session);
            pilotInterval = pilotNext is not null;
            if (pilotNext is null)
            {
                renderer.ClearNativePilotInterval();
            }
            else if (!renderer.CanSampleNativePilotInterval(prepared, pilotNext))
            {
                pilotNext = null;
            }
        }
        else
        {
            renderer.ClearNativePilotInterval();
        }
        if (prepared is null && process.Graphics.Callback == 0)
        {
            PresentedNativeFrame = null;
            byte[] blank = renderer.RenderNativePrimitiveFrame(new NativeBattlePreparedPrimitiveFrame([], []), null);
            renderedGpu = triangleRenderer as GpuTriangleRenderer;
            renderedGpuVersion = renderedGpu?.PresentationVersion ?? 0;
            renderedPixels = triangleRenderer is null ? blank : null;
            return blank;
        }
        if (prepared is null)
        {
            throw new InvalidOperationException("Battle rendering requires a completed prepared presentation.");
        }
        PresentedNativeFrame = prepared;
        renderer.ObservePilotSource(prepared, pilotNext, pilotAlpha, pilotInterval);
        if (prepared.GuardianAnimationSkipPending)
        {
            if (triangleRenderer is null && renderedPixels is not null)
            {
                return renderedPixels;
            }
            if (triangleRenderer is GpuTriangleRenderer heldGpu && ReferenceEquals(renderedGpu, heldGpu) && renderedGpuVersion == heldGpu.PresentationVersion && !heldGpu.Target.IsDisposed)
            {
                return [];
            }
            byte[] blank = renderer.RenderNativePrimitiveFrame(new NativeBattlePreparedPrimitiveFrame([], []), null);
            renderedGpu = triangleRenderer as GpuTriangleRenderer;
            renderedGpuVersion = renderedGpu?.PresentationVersion ?? 0;
            renderedPixels = triangleRenderer is null ? blank : null;
            return blank;
        }
        BattleCamera? selectedCamera = null;
        if ((pilotInterval ? prepared.PilotCamera : prepared.Camera) is { FocalLength: > 0 } nativeCamera)
        {
            if (pilotNext?.PilotCamera is { } nextCamera)
            {
                nativeCamera = NativeBattlePilotVisualGeometry.SampleCamera(nativeCamera, nextCamera, pilotAlpha);
            }
            selectedCamera = ReadCamera(nativeCamera, process.Camera.Projection ?? throw new InvalidOperationException("Battle rendering requires its published camera projection."));
        }
        NativeBattlePreparedPrimitiveFrame frame = prepared.Primitives;
        NativeBattlePrimitiveCapabilities? primitiveCapabilities = prepared.PrimitiveCapabilities;
        NativeBattleEffectTextureState? textures = prepared.Textures;
        NativeBattlePcUiFrame? uiFrame = prepared.UiFrame;
        NativeBattlePcTextureWindowCache? windows = prepared.TextureWindows;
        IReadOnlyList<NativeBattleTextureReplacement> replacements = NativeModelReplacements(prepared);
        bool unchanged = pilotNext is null && ReferenceEquals(renderedPrimitiveFrame, frame) && renderedPrimitiveCapabilities == primitiveCapabilities && ReferenceEquals(renderedPcRenderTail, frame.LastPcRenderTail) && ReferenceEquals(renderedPcUiFrame, uiFrame) && ReferenceEquals(renderedTextures, textures) && ReferenceEquals(renderedTextureWindows, windows) && renderedCamera == selectedCamera && renderedModelReplacements is not null && renderedModelReplacements.SequenceEqual(replacements) && renderedEffectTextureGeneration == assets.EffectTextureSource.Generation;
        if (triangleRenderer is null && renderedPixels is not null && unchanged)
        {
            return renderedPixels;
        }
        if (triangleRenderer is GpuTriangleRenderer gpu && ReferenceEquals(renderedGpu, gpu) && renderedGpuVersion == gpu.PresentationVersion && !gpu.Target.IsDisposed && unchanged)
        {
            return [];
        }
        if (triangleRenderer is not null)
        {
            renderedPixels = null;
            byte[] result = RenderNative();
            renderedPrimitiveFrame = frame;
            renderedPrimitiveCapabilities = primitiveCapabilities;
            renderedPcRenderTail = frame.LastPcRenderTail;
            renderedPcUiFrame = uiFrame;
            renderedTextures = textures;
            renderedTextureWindows = windows;
            renderedModelReplacements = replacements;
            renderedEffectTextureGeneration = assets.EffectTextureSource.Generation;
            renderedCamera = selectedCamera;
            renderedGpu = triangleRenderer as GpuTriangleRenderer;
            renderedGpuVersion = renderedGpu?.PresentationVersion ?? 0;
            return result;
        }
        renderedGpu = null;
        renderedPixels = RenderNative();
        renderedPrimitiveFrame = frame;
        renderedPrimitiveCapabilities = primitiveCapabilities;
        renderedPcRenderTail = frame.LastPcRenderTail;
        renderedPcUiFrame = uiFrame;
        renderedTextures = textures;
        renderedTextureWindows = windows;
        renderedModelReplacements = replacements;
        renderedEffectTextureGeneration = assets.EffectTextureSource.Generation;
        renderedCamera = selectedCamera;
        return renderedPixels;

        byte[] RenderNative()
        {
            return pilotNext is null
                ? renderer.RenderNativePrimitiveFrame(frame, textures, selectedCamera, primitiveCapabilities, windows, replacements.Count == 0 ? null : replacements, uiFrame, uiFrame is null ? null : assets.ReadNativePcUiIcons().Resolve, assets.EffectTextureSource)
                : renderer.RenderNativePilotPrimitiveFrame(prepared, pilotNext, pilotAlpha, selectedCamera, primitiveCapabilities, replacements.Count == 0 ? null : replacements, uiFrame is null ? null : assets.ReadNativePcUiIcons().Resolve, assets.EffectTextureSource);
        }
    }

    internal UiRect? GetActorBounds(string actorId, UiRect viewport)
    {
        if (renderer is null || !renderer.TryGetActorBounds(actorId, out Vector4 bounds))
        {
            return null;
        }
        return new UiRect(viewport.X + bounds.X * viewport.Width / renderer.Width, viewport.Y + bounds.Y * viewport.Height / renderer.Height, (bounds.Z - bounds.X) * viewport.Width / renderer.Width, (bounds.W - bounds.Y) * viewport.Height / renderer.Height);
    }

    public void DrawEffects(BattleSession session, IUiCanvas canvas, UiRect viewport)
    {
        if (session.GuardianAnimationSkipPending || PresentedNativeFrame is not { } prepared)
        {
            return;
        }
        if (renderer is null)
        {
            throw new InvalidOperationException("Render the battle scene before projecting its effects.");
        }
        PixelBounds native = renderer.ProjectionBounds;
        UiRect nativeViewport = new(viewport.X + native.X * viewport.Width / renderer.Width, viewport.Y + native.Y * viewport.Height / renderer.Height, native.Width * viewport.Width / renderer.Width, native.Height * viewport.Height / renderer.Height);
        canvas.PushClip(viewport);
        try
        {
            BattleDamageTextPresentation.Draw(prepared.DamageText, canvas, nativeViewport);
        }
        finally
        {
            canvas.PopClip();
        }
    }
}
