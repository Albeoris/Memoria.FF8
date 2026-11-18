using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class NativeBattleComponentSourceTests
{
    internal static BattleSession PrepareOriginalEffect44Component(GameWorld world, EffectPreviewRequest request)
    {
        BattleSession source = world.StartEffectPreview(request);
        BattleCombatant actor = source.Combatants.Single(candidate => !candidate.IsPlayer && candidate.NativeActorIndex == 3);
        if (actor.ModelId != 83 || actor.SceneSlot != 0)
        {
            throw new InvalidDataException("The component requires its original caster model and initialized native cell.");
        }
        NativeBattleAiDefinition ai = world.BattleResources.ReadEnemyAi(actor.ModelId, actor.Level);
        if (!ai.Abilities.Any(ability => ability.Command == 8 && ability.Id == 226 && ability.Sequence == 15) ||
            !world.BattleResources.NativeActions.Any(action => action.Command == 8 && action.ActionId == 226 && action.MagicId == 44))
        {
            throw new InvalidDataException("The component requires its installed original command, action and sequence metadata.");
        }
        BattlePresentationProcess process = source.Presentation ?? throw new InvalidDataException("The component requires its initialized presentation process.");
        BattleModelOwner caster = process.FindOriginalActor(NativeBattleModelSlot.Decode(3)) ?? throw new InvalidDataException("The component caster has no original model binding.");
        BattleModelOwner recipient = process.FindOriginalActor(NativeBattleModelSlot.Decode(0)) ?? throw new InvalidDataException("The component recipient has no original model binding.");
        NativeBattlePoseSource pose = world.BattleResources.ReadNativeGraphicsResource(source.GetCombatantDefinition(actor.Id)).PoseSource ?? throw new InvalidDataException("The component caster lacks its installed original skeleton.");
        if (pose.BoneCount <= 23 || NativeBattleBodyBoneMatrices.Require(process.Graphics.ActorModels.Get(caster), pose).Length != pose.BoneCount)
        {
            throw new InvalidDataException("The component caster lacks its required original bone anchors.");
        }
        NativeBattleActionHeaderPreparation header = new(caster, 8, 15, -1, 226, 44, null);
        NativeBattleContactPacket contact = new(new(recipient, 0, 1, 0, 0, 0, 0), null);
        process.BeginDiagnosticAction(header, [contact]);
        return source;
    }
}
