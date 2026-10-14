using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

/// <summary>The live host and GPU-independent diagnostic preview render the same installed scene and actor presentation.</summary>
internal sealed class BattleScenePresentation
{
    private readonly BattleStage stage;
    private readonly Dictionary<string, BattleModel> models = [];
    private readonly Dictionary<string, BattleModel?> weapons = [];
    private readonly Dictionary<string, float> groundOffsets = [];
    private readonly Dictionary<string, Vector3> positions = [];
    private readonly BattleRenderer portraitRenderer = new(64, 64);
    private BattleRenderer? renderer;

    public BattleScenePresentation(BattleAssets assets, BattleDefinition definition, bool useEncounter)
    {
        stage = assets.LoadStage(definition.StageId ?? throw new NotSupportedException("A battle scene requires its installed stage identity."));
        BattleCombatantDefinition[] party = definition.Combatants.Where(actor => actor.IsPlayer).ToArray();
        BattleCombatantDefinition[] enemies = definition.Combatants.Where(actor => !actor.IsPlayer).ToArray();
        BattleEncounter? encounter = useEncounter && definition.EncounterId is int id ? assets.LoadEncounter(id) : null;
        // TODO(TD-009): Party staging, camera and primitive attack motion use declared presentation tuning until native formation/camera/action contracts are proved
        for (int index = 0; index < party.Length; index++)
        {
            positions[party[index].Id] = new Vector3(-12, 0, (index - (party.Length - 1) / 2f) * 7);
        }
        for (int index = 0; index < enemies.Length; index++)
        {
            positions[enemies[index].Id] = encounter?.Enemies[index].Position ?? new Vector3(12 + index / 4 * 7, 0, (index % 4 - (Math.Min(4, enemies.Length) - 1) / 2f) * 7);
        }
        foreach (BattleCombatantDefinition actor in definition.Combatants)
        {
            BattleModel model = actor.IsPlayer ? assets.LoadPlayer(actor.ModelId, actor.Costume) : assets.LoadEnemy(actor.ModelId);
            models.Add(actor.Id, model);
            groundOffsets.Add(actor.Id, model.Pose(0, 0).SelectMany(mesh => mesh).Min(vertex => vertex.Y));
            if (!actor.IsPlayer && actor.WeaponId is not null)
            {
                throw new NotSupportedException("Enemy weapon replacement requires its own battle model attachment contract.");
            }
            weapons.Add(actor.Id, actor.IsPlayer && actor.WeaponId is int weapon ? assets.ResolveWeaponModel(actor.ModelId, weapon, model) : null);
        }
    }

    public byte[] Render(BattleSession session, int width, int height)
    {
        if (renderer?.Width != width || renderer.Height != height)
        {
            renderer = new BattleRenderer(width, height);
        }
        List<BattleActorDraw> actors = [];
        foreach (BattleCombatant actor in session.Combatants)
        {
            Vector3 offset = Vector3.Zero;
            if (session.ActiveAction is BattleActionPresentation action && action.ActorId == actor.Id)
            {
                float progress = action.Phase == BattleActionPhase.Windup ? 1 - action.RemainingFrames / (float)BattleSession.AttackWindupFrames : action.RemainingFrames / (float)BattleSession.AttackRecoveryFrames;
                offset = (positions[action.TargetId] - positions[actor.Id]) * Math.Clamp(progress, 0, 1) * 0.7f;
            }
            BattleActorDraw drawing = new(models[actor.Id], positions[actor.Id], actor.IsPlayer ? MathF.PI / 2 : -MathF.PI / 2, Frame: session.Frame / 2f, AttackOffset: offset, Opacity: actor.IsAlive ? 1 : 0.25f, GroundOffset: groundOffsets[actor.Id]);
            actors.Add(drawing);
            if (weapons[actor.Id] is BattleModel weapon)
            {
                actors.Add(drawing with { Model = weapon });
            }
        }
        return renderer!.Render(stage, actors);
    }

    public PixelImage Head(BattleCombatant actor, int size = 64)
    {
        return portraitRenderer.RenderPortrait(models[actor.Id], size);
    }
}
