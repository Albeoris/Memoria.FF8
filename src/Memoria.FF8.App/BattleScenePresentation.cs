using System.Numerics;
using Albeoris.Games.FF8.Imaging;
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
    private readonly NativeBattlePrimitiveCapabilities? primitiveCapabilities;
    private readonly Dictionary<string, BattleCombatantDefinition> definitions;
    private readonly Dictionary<string, BattleModel> models = [];
    private readonly Dictionary<string, int?> activeVisualModels = [];
    private readonly Dictionary<string, BattleModel?> weapons = [];
    private readonly Dictionary<string, float> groundOffsets = [];
    private readonly Dictionary<string, Vector3> positions = [];
    private readonly Dictionary<string, float> heights = [];
    private readonly Dictionary<string, BattleAnimationPresentation> animations = [];
    private readonly Dictionary<ushort, BattleNativeRenzokukenResource> renzokukenResources;
    private readonly Dictionary<(string ActorId, ushort MagicId), (BattleRenzokukenModels Models, BattleAnimationPresentation Animation)> renzokukenModels = [];
    private readonly BattleImpactTimeline impactTimeline = new();
    private readonly BattleCamera? camera;
    private BattleCamera? renderedCamera;
    private BattleRenderer? renderer;
    private NativeBattlePrimitiveFrame? renderedPrimitiveFrame;
    private NativeBattlePcRenderTailReceipt? renderedPcRenderTail;
    private NativeBattleEffectTextureState? renderedTextures;
    private NativeBattlePcTextureWindowCache? renderedTextureWindows;
    private byte[]? renderedPixels;
    private GpuTriangleRenderer? renderedGpu;
    private long renderedGpuVersion;
    private readonly Dictionary<(BattleModel Model, NativeBattleModelTextureBinding Binding, bool Weapon), (PixelImage[] Images, IReadOnlyList<NativeBattleTextureReplacement> Replacements)> replacementBindings = [];
    private IReadOnlyList<NativeBattleTextureReplacement>? renderedModelReplacements;

    public BattleScenePresentation(BattleAssets assets, BattleDefinition definition, bool useEncounter)
    {
        this.assets = assets;
        primitiveCapabilities = definition.NativeGraphics?.PrimitiveCapabilities;
        renzokukenResources = definition.NativeRenzokukenResources.ToDictionary(resource => resource.MagicId);
        definitions = definition.Combatants.ToDictionary(actor => actor.Id);
        stage = assets.LoadStage(definition.StageId ?? throw new NotSupportedException("A battle scene requires its installed stage identity."));
        BattleCombatantDefinition[] party = definition.Combatants.Where(actor => actor.IsPlayer).ToArray();
        BattleCombatantDefinition[] enemies = definition.Combatants.Where(actor => !actor.IsPlayer).ToArray();
        BattleEncounter? encounter = useEncounter && definition.EncounterId is int id ? assets.LoadEncounter(id) : null;
        if (definition.NativeGraphics is null)
        {
            //TODO(TD-009): Component battles without the raw graphics owner retain their declared settled introduction view.
            int cameraId = (encounter?.PrimaryCamera ?? 0) & 0xF7;
            if (!stage.IntroductionCameras.TryGetValue(cameraId, out BattleStageCamera? introduction))
            {
                throw new NotSupportedException($"Stage {stage.Id} camera {cameraId} requires an unsupported native endpoint resolver.");
            }
            camera = BattleCamera.FromNative(introduction);
        }
        for (int index = 0; index < party.Length; index++)
        {
            positions[party[index].Id] = BattleFormationPresentation.PartyPosition(index, party.Length);
        }
        for (int index = 0; index < enemies.Length; index++)
        {
            BattleCombatantDefinition actor = enemies[index];
            positions[actor.Id] = encounter is null ? BattleFormationPresentation.DebugEnemyPosition(index, enemies.Length) : encounter.Slots.Single(slot => slot.Slot == (actor.SceneSlot ?? throw new NotSupportedException("An original battle participant requires its scene slot identity."))).Position;
        }
        foreach (BattleCombatantDefinition actor in definition.Combatants.Where(actor => actor.InitialActive))
        {
            EnsureActorResources(actor.Id);
        }
    }

    internal BattleCamera Camera => renderedCamera ?? camera ?? throw new InvalidOperationException("The original camera has not published its first pose.");

    private void EnsureActorResources(string actorId, int? visualModelId = null)
    {
        if (models.ContainsKey(actorId) && activeVisualModels.GetValueOrDefault(actorId) == visualModelId)
        {
            return;
        }
        models.Remove(actorId);
        animations.Remove(actorId);
        groundOffsets.Remove(actorId);
        heights.Remove(actorId);
        weapons.Remove(actorId);
        activeVisualModels[actorId] = visualModelId;
        BattleCombatantDefinition actor = definitions[actorId];
        bool playerResource = actor.IsPlayer && visualModelId is null;
        int modelId = visualModelId ?? actor.ModelId;
        BattleModel model = visualModelId is int formModel ? assets.LoadEnemy(formModel) : actor.IsPlayer ? assets.LoadPlayer(actor.ModelId, actor.Costume) : actor.ModelId == -1 ? BattleDebugModels.CreateDummy() : assets.LoadEnemy(actor.ModelId);
        BattleSequenceSet? sequences = playerResource ? assets.LoadPlayerSequences(actor.ModelId, actor.WeaponId, model) : model.Sequences;
        BattleWeaponDefinition? equipped = playerResource ? actor.WeaponId is int equippedId ? assets.WeaponDefinitions.Single(weapon => weapon.Id == equippedId) : assets.WeaponDefinitions.FirstOrDefault(weapon => weapon.CharacterId == actor.ModelId) : null;
        if (!actor.IsPlayer && actor.WeaponId is not null)
        {
            throw new NotSupportedException("Enemy weapon replacement requires its own battle model attachment contract.");
        }
        BattleModel? weapon = playerResource && actor.WeaponId is int weaponId ? assets.ResolveWeaponModel(actor.ModelId, weaponId, model) : null;
        float ground = model.Pose(0, 0).SelectMany(mesh => mesh).Min(vertex => vertex.Y);
        float height = model.Pose(0, 0).SelectMany(mesh => mesh).Max(vertex => vertex.Y) - ground;
        BattleAnimationPresentation animation = new(model, sequences, actor) { Melee = equipped?.Melee == true, AttackSequence = playerResource ? 13 : modelId == -1 ? 0 : assets.ReadEnemyAttackSequence(modelId) };
        models.Add(actor.Id, model);
        animations.Add(actor.Id, animation);
        groundOffsets.Add(actor.Id, ground);
        heights.Add(actor.Id, height);
        weapons.Add(actor.Id, weapon);
    }

    public byte[] Render(BattleSession session, int width, int height, ITriangleRenderer? triangleRenderer = null)
    {
        if (renderer?.Width != width || renderer.Height != height)
        {
            renderer = new BattleRenderer(width, height, ViewportLayout.Fit(320, 240, width, height));
            renderedPixels = null;
            renderedGpu = null;
        }
        renderer.TriangleRenderer = triangleRenderer;
        if (session.GuardianAnimationSkipPending)
        {
            if (triangleRenderer is null && renderedPixels is not null)
            {
                return renderedPixels;
            }
            if (triangleRenderer is GpuTriangleRenderer heldGpu && ReferenceEquals(renderedGpu, heldGpu) && renderedGpuVersion == heldGpu.PresentationVersion && !heldGpu.Target.IsDisposed)
            {
                return [];
            }
            byte[] blank = renderer.RenderNativePrimitiveFrame(NativeBattlePrimitiveFrame.Clear(), null);
            renderedGpu = triangleRenderer as GpuTriangleRenderer;
            renderedGpuVersion = renderedGpu?.PresentationVersion ?? 0;
            renderedPixels = triangleRenderer is null ? blank : null;
            return blank;
        }
        BattleCamera? selectedCamera = camera;
        if (session.NativeGraphics is { } rawGraphics)
        {
            if (session.NativeCamera?.CurrentPose is { FocalLength: > 0 } nativeCamera)
            {
                selectedCamera = BattleCamera.FromNative(new(new(nativeCamera.Eye.X, nativeCamera.Eye.Y, nativeCamera.Eye.Z), new(nativeCamera.Target.X, nativeCamera.Target.Y, nativeCamera.Target.Z), nativeCamera.FocalLength!.Value), nativeCamera.Roll);
            }
            NativeBattlePrimitiveFrame frame = rawGraphics.PrimitiveFrame ?? throw new NotSupportedException("Native battle presentation requires its executed original primitive frame; migrate the named standalone snapshot explicitly.");
            NativeBattleEffectTextureState? textures = session.NativeEffectMemory?.Textures;
            NativeBattlePcTextureWindowCache? windows = session.NativeEffectMemory?.PrimitiveProcess?.TextureWindowCache;
            IReadOnlyList<NativeBattleTextureReplacement> replacements = NativeModelReplacements(session, rawGraphics);
            bool unchanged = ReferenceEquals(renderedPrimitiveFrame, frame) && ReferenceEquals(renderedPcRenderTail, frame.LastPcRenderTail) && ReferenceEquals(renderedTextures, textures) && ReferenceEquals(renderedTextureWindows, windows) && renderedCamera == selectedCamera && renderedModelReplacements is not null && renderedModelReplacements.SequenceEqual(replacements);
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
                byte[] result = renderer.RenderNativePrimitiveFrame(frame, textures, selectedCamera, primitiveCapabilities, windows, replacements.Count == 0 ? null : replacements);
                renderedPrimitiveFrame = frame;
                renderedPcRenderTail = frame.LastPcRenderTail;
                renderedTextures = textures;
                renderedTextureWindows = windows;
                renderedModelReplacements = replacements;
                renderedCamera = selectedCamera;
                renderedGpu = triangleRenderer as GpuTriangleRenderer;
                renderedGpuVersion = renderedGpu?.PresentationVersion ?? 0;
                return result;
            }
            renderedGpu = null;
            renderedPixels = renderer.RenderNativePrimitiveFrame(frame, textures, selectedCamera, primitiveCapabilities, windows, replacements.Count == 0 ? null : replacements);
            renderedPrimitiveFrame = frame;
            renderedPcRenderTail = frame.LastPcRenderTail;
            renderedTextures = textures;
            renderedTextureWindows = windows;
            renderedModelReplacements = replacements;
            renderedCamera = selectedCamera;
            return renderedPixels;
        }
        renderedCamera = selectedCamera;
        List<BattleActorDraw> actors = [];
        impactTimeline.Collect(session);
        foreach (BattleCombatant actor in session.Combatants.Where(actor => !definitions.ContainsKey(actor.Id)))
        {
            BattleCombatantDefinition allocated = session.GetCombatantDefinition(actor.Id);
            BattleCombatantDefinition prototype = definitions.Values.First(candidate => !candidate.IsPlayer && candidate.SceneSlot == allocated.SceneSlot);
            definitions.Add(actor.Id, allocated);
            positions.Add(actor.Id, positions[prototype.Id]);
        }
        foreach (BattleCombatant actor in session.Combatants)
        {
            if (!actor.IsActive || actor.Hidden)
            {
                continue;
            }
            EnsureActorResources(actor.Id, session.GetGuardianFormVisual(actor.Id));
            BattleEvent? action = session.Events.LastOrDefault(item => item.Kind == "Windup" && item.ActorId == actor.Id);
            Vector3 target = action?.TargetId is string targetId ? positions[targetId] : positions[actor.Id];
            BattleModel shownModel = models[actor.Id];
            BattleModel? shownWeapon = weapons[actor.Id];
            BattleAnimationPresentation animation = animations[actor.Id];
            BattleAnimationSample pose;
            if (session.GetGuardianFormVisual(actor.Id) is null && session.GetNativeRenzokuken(actor.Id) is { } renzokuken)
            {
                var key = (actor.Id, renzokuken.MagicId);
                if (!renzokukenModels.TryGetValue(key, out var replacement))
                {
                    BattleRenzokukenModels changed = assets.ReadNativeRenzokukenModels(renzokukenResources[renzokuken.MagicId], shownModel, shownWeapon);
                    replacement = (changed, new(changed.Body, changed.Body.Sequences, definitions[actor.Id]));
                    renzokukenModels.Add(key, replacement);
                }
                shownModel = replacement.Models.Body;
                shownWeapon = replacement.Models.Weapon;
                animation = replacement.Animation;
                pose = animation.SampleNativeSequence(1, renzokuken.PoseFrames);
            }
            else
            {
                pose = animation.Sample(session, actor, positions[actor.Id], target);
            }
            if (pose.Opacity == 0)
            {
                continue;
            }
            float ground = animation.GroundOffset(pose, actor.IsAlive, groundOffsets[actor.Id]);
            // Original502070 party0/enemy2048 plus world(x,-y,-z) and DAT-local(-x,-y,z)
            // bases yields renderer partyPI/enemy0.
            BattleActorDraw drawing = new(shownModel, positions[actor.Id], actor.IsPlayer ? MathF.PI : 0, Animation: pose.Animation, Frame: pose.Frame, AttackOffset: pose.Offset, Opacity: pose.Opacity, GroundOffset: ground, LoopAnimation: pose.Loop) { NativePose = pose.NativePose };
            actors.Add(drawing);
            if (shownWeapon is BattleModel weapon)
            {
                actors.Add(drawing with { Model = weapon });
            }
        }
        byte[] pixels = renderer!.Render(stage, actors, selectedCamera ?? throw new InvalidOperationException("The original camera has not published its first pose."));
        return pixels;
    }

    public void DrawEffects(BattleSession session, IUiCanvas canvas, UiRect viewport)
    {
        if (session.GuardianAnimationSkipPending)
        {
            return;
        }
        if (renderer is null)
        {
            throw new InvalidOperationException("Render the battle scene before projecting its effects.");
        }
        float scale = renderer.ProjectionBounds.Height * viewport.Height / renderer.Height / 672f;
        canvas.PushClip(viewport);
        try
        {
            if (session.NativeGraphics is { } graphics)
            {
                PixelBounds native = renderer.ProjectionBounds;
                UiRect nativeViewport = new(viewport.X + native.X * viewport.Width / renderer.Width, viewport.Y + native.Y * viewport.Height / renderer.Height, native.Width * viewport.Width / renderer.Width, native.Height * viewport.Height / renderer.Height);
                BattleDamageTextPresentation.Draw(graphics.Effects.TextDraws, canvas, nativeViewport);
                return;
            }
            foreach (BattleImpactCue cue in impactTimeline.Collect(session))
            {
                string target = cue.Event.TargetId!;
                BattleCombatant actor = session.Combatants.Single(actor => actor.Id == target);
                if (!actor.IsActive || actor.Hidden)
                {
                    continue;
                }
                Vector3 anchor = positions[target] + Vector3.UnitY * heights[target] * 0.65f;
                if (!renderer.TryProject(anchor, out Vector2 projected))
                {
                    continue;
                }
                float x = viewport.X + projected.X * viewport.Width / renderer.Width;
                float y = viewport.Y + projected.Y * viewport.Height / renderer.Height;
                byte alpha = (byte)(255 * Math.Min(1, (BattleImpactPresentation.Duration - cue.Age) / 8f));
                //TODO(TD-009): Component battles retain their existing impact projection until the raw effect dispatcher owns normal encounters.
                if (session.NativeGraphics is null && cue.IsPhysical && cue.Age < 7)
                {
                    float radius = (8 + cue.Age * 3) * scale;
                    for (int ray = 0; ray < 8; ray++)
                    {
                        float angle = ray * MathF.PI / 4 + 0.2f;
                        float dx = MathF.Cos(angle);
                        float dy = MathF.Sin(angle);
                        canvas.Line(x + dx * radius * 0.2f, y + dy * radius * 0.2f, x + dx * radius, y + dy * radius, new UiColor(255, 220, 135, alpha), 3 * scale);
                        canvas.Line(x, y, x + dx * radius * 0.65f, y + dy * radius * 0.65f, new UiColor(255, 255, 255, alpha), scale);
                    }
                }
                UiColor color = cue.IsHealing ? new UiColor(105, 255, 130, alpha) : cue.IsPhysical ? new UiColor(255, 255, 255, alpha) : new UiColor(220, 130, 255, alpha);
                float rise = (cue.Age * 0.8f + MathF.Sin(Math.Min(1, cue.Age / 12f) * MathF.PI) * 10) * scale;
                canvas.OutlinedText(cue.Event.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture), 24 * scale, new UiRect(x - 30 * scale, y - 24 * scale - rise, 120 * scale, 38 * scale), color, new UiColor(0, 0, 0, alpha), scale);
            }
        }
        finally
        {
            canvas.PopClip();
        }
    }

    public void AdvancePresentation(TimeSpan elapsed)
    {
        impactTimeline.Advance(elapsed);
        foreach (BattleAnimationPresentation animation in animations.Values)
        {
            animation.Advance(elapsed);
        }
    }

    public IReadOnlyList<BattleSequenceSoundCue> CollectSounds(BattleSession session) => animations.Values.SelectMany(animation => animation.CollectSounds(session)).ToArray();


}
