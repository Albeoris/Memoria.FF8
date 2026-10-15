using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8;

/// <summary>The live host and GPU-independent diagnostic preview render the same installed scene and actor presentation.</summary>
internal sealed class BattleScenePresentation
{
    private readonly BattleStage stage;
    private readonly Dictionary<string, BattleModel> models = [];
    private readonly Dictionary<string, BattleModel?> weapons = [];
    private readonly Dictionary<string, float> groundOffsets = [];
    private readonly Dictionary<string, Vector3> positions = [];
    private readonly Dictionary<string, float> heights = [];
    private readonly Dictionary<string, BattleAnimationPresentation> animations = [];
    private readonly BattleRenderer portraitRenderer = new(64, 64);
    private readonly BattleImpactTimeline impactTimeline = new();
    private readonly BattleCamera camera;
    private BattleRenderer? renderer;

    public BattleScenePresentation(BattleAssets assets, BattleDefinition definition, bool useEncounter)
    {
        stage = assets.LoadStage(definition.StageId ?? throw new NotSupportedException("A battle scene requires its installed stage identity."));
        BattleCombatantDefinition[] party = definition.Combatants.Where(actor => actor.IsPlayer).ToArray();
        BattleCombatantDefinition[] enemies = definition.Combatants.Where(actor => !actor.IsPlayer).ToArray();
        BattleEncounter? encounter = useEncounter && definition.EncounterId is int id ? assets.LoadEncounter(id) : null;
        // TODO(TD-009): Show the primary native introduction's settled pose immediately; random alternative
        // selection, introduction motion and action camera scripts remain unsupported
        int cameraId = (encounter?.PrimaryCamera ?? 0) & 0xF7;
        if (!stage.IntroductionCameras.TryGetValue(cameraId, out BattleStageCamera? introduction))
        {
            throw new NotSupportedException($"Stage {stage.Id} camera {cameraId} requires an unsupported native endpoint resolver.");
        }
        camera = BattleCamera.FromNative(introduction);
        for (int index = 0; index < party.Length; index++)
        {
            positions[party[index].Id] = BattleFormationPresentation.PartyPosition(index, party.Length);
        }
        for (int index = 0; index < enemies.Length; index++)
        {
            BattleCombatantDefinition actor = enemies[index];
            positions[actor.Id] = encounter is null ? BattleFormationPresentation.DebugEnemyPosition(index, enemies.Length) : encounter.Slots.Single(slot => slot.Slot == (actor.SceneSlot ?? throw new NotSupportedException("An original battle participant requires its scene slot identity."))).Position;
        }
        foreach (BattleCombatantDefinition actor in definition.Combatants)
        {
            BattleModel model = actor.IsPlayer ? assets.LoadPlayer(actor.ModelId, actor.Costume) : actor.ModelId == -1 ? BattleDebugModels.CreateDummy() : assets.LoadEnemy(actor.ModelId);
            models.Add(actor.Id, model);
            BattleSequenceSet? sequences = actor.IsPlayer ? assets.LoadPlayerSequences(actor.ModelId, actor.WeaponId, model) : model.Sequences;
            BattleWeaponDefinition? equipped = actor.IsPlayer ? actor.WeaponId is int equippedId ? assets.WeaponDefinitions.Single(weapon => weapon.Id == equippedId) : assets.WeaponDefinitions.FirstOrDefault(weapon => weapon.CharacterId == actor.ModelId) : null;
            animations.Add(actor.Id, new BattleAnimationPresentation(model, sequences, actor) { Melee = equipped?.Melee == true, AttackSequence = actor.IsPlayer ? 13 : actor.ModelId == -1 ? 0 : assets.ReadEnemyAttackSequence(actor.ModelId) });
            groundOffsets.Add(actor.Id, model.Pose(0, 0).SelectMany(mesh => mesh).Min(vertex => vertex.Y));
            heights.Add(actor.Id, model.Pose(0, 0).SelectMany(mesh => mesh).Max(vertex => vertex.Y) - groundOffsets[actor.Id]);
            if (!actor.IsPlayer && actor.WeaponId is not null)
            {
                throw new NotSupportedException("Enemy weapon replacement requires its own battle model attachment contract.");
            }
            weapons.Add(actor.Id, actor.IsPlayer && actor.WeaponId is int weapon ? assets.ResolveWeaponModel(actor.ModelId, weapon, model) : null);
        }
    }

    internal BattleCamera Camera => camera;

    public byte[] Render(BattleSession session, int width, int height)
    {
        if (renderer?.Width != width || renderer.Height != height)
        {
            renderer = new BattleRenderer(width, height);
        }
        List<BattleActorDraw> actors = [];
        impactTimeline.Collect(session);
        foreach (BattleCombatant actor in session.Combatants)
        {
            if (!actor.IsActive || actor.Hidden)
            {
                continue;
            }
            BattleEvent? action = session.Events.LastOrDefault(item => item.Kind == "Windup" && item.ActorId == actor.Id);
            Vector3 target = action?.TargetId is string targetId ? positions[targetId] : positions[actor.Id];
            BattleAnimationSample pose = animations[actor.Id].Sample(session, actor, positions[actor.Id], target);
            float ground = animations[actor.Id].GroundOffset(pose, actor.IsAlive, groundOffsets[actor.Id]);
            // Original502070 party0/enemy2048 plus world(x,-y,-z) and DAT-local(-x,-y,z)
            // bases yields renderer partyPI/enemy0
            BattleActorDraw drawing = new(models[actor.Id], positions[actor.Id], actor.IsPlayer ? MathF.PI : 0, Animation: pose.Animation, Frame: pose.Frame, AttackOffset: pose.Offset, Opacity: pose.Opacity, GroundOffset: ground, LoopAnimation: pose.Loop);
            actors.Add(drawing);
            if (weapons[actor.Id] is BattleModel weapon)
            {
                actors.Add(drawing with { Model = weapon });
            }
        }
        return renderer!.Render(stage, actors, camera);
    }

    public void DrawEffects(BattleSession session, IUiCanvas canvas, UiRect viewport)
    {
        if (renderer is null)
        {
            throw new InvalidOperationException("Render the battle scene before projecting its effects.");
        }
        float scale = viewport.Height / 672f;
        canvas.PushClip(viewport);
        try
        {
            foreach (BattleImpactCue cue in impactTimeline.Collect(session))
            {
                string target = cue.Event.TargetId!;
                Vector3 anchor = positions[target] + Vector3.UnitY * heights[target] * 0.65f;
                if (!renderer.TryProject(anchor, out Vector2 projected))
                {
                    continue;
                }
                float x = viewport.X + projected.X * viewport.Width / renderer.Width;
                float y = viewport.Y + projected.Y * viewport.Height / renderer.Height;
                byte alpha = (byte)(255 * Math.Min(1, (BattleImpactPresentation.Duration - cue.Age) / 8f));
                if (cue.IsPhysical && cue.Age < 7)
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
                canvas.Text(cue.Event.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture), 24 * scale, new UiRect(x - 30 * scale, y - 24 * scale - rise, 120 * scale, 38 * scale), color);
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

    public PixelImage Head(BattleCombatant actor, int size = 64)
    {
        return portraitRenderer.RenderPortrait(models[actor.Id], size);
    }
}
