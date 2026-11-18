using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class NativeBattleComponentSourceTests
{
    internal static BattleSession PrepareOriginalEffect79Component(GameWorld world, EffectPreviewRequest request)
    {
        if (request.EnemyId != 122 || request.StageId != 142)
        {
            throw new InvalidDataException("The component requires its original caster and native stage owner.");
        }
        BattleSession source = world.StartDiagnosticBattle(new(509, request.Seed, 0x80, 0, 0)
        {
            BattleMusic = 5,
            PrimaryMusicChannel = -1,
            AtbMode = BattleAtbMode.Normal,
            Fixture = new([], [], true) { CombatParty = [0, 255, 255] }
        });
        for (int callback = 0; callback < 3600 && (!source.NativeEntryReady || source.ObserveDiagnosticDrainBlockers().Count != 0); callback++)
        {
            source.AdvanceNativeGraphicsPresentation(PresentationTiming.SimulationStep);
        }
        if (!source.NativeEntryReady || source.ObserveDiagnosticDrainBlockers().Count != 0)
        {
            throw new InvalidDataException("The original component encounter did not finish its native initialization.");
        }
        BattlePresentationProcess process = source.Presentation!;
        BattleCombatant actor = source.Combatants.Single(candidate => candidate.NativeActorIndex == 4);
        BattleCombatant target = source.Combatants.Single(candidate => candidate.NativeActorIndex == 3);
        if (actor.ModelId != 122 || actor.SceneSlot != 1 || !actor.IsActive || actor.Hidden || actor.Suspended || actor.Untargetable ||
            target.ModelId != 123 || target.SceneSlot != 0 || !target.IsActive || !target.Hidden || target.Suspended || !target.Untargetable || process.NativeStage?.FinalStage142 is null)
        {
            throw new InvalidDataException($"The original component changed its caster or hidden companion identities and lifecycle state. Caster: {OriginalSourceActorFacts(actor)}. Target: {OriginalSourceActorFacts(target)}. FinalStage142={process.NativeStage?.FinalStage142 is not null}.");
        }
        NativeBattleAiDefinition ai = actor.NativeAi ?? throw new InvalidDataException("The component lacks its original ability metadata.");
        if (!ai.Abilities.Any(ability => ability.Command == 8 && ability.Id == 351 && ability.Sequence == 15) ||
            !source.ReadDefinition().NativeActions.Any(action => action.Command == 8 && action.ActionId == 351 && action.MagicId == 79))
        {
            throw new InvalidDataException("The component requires its installed original command, action and sequence metadata.");
        }
        BattleModelOwner caster = process.FindOriginalActor(NativeBattleModelSlot.Decode(4)) ?? throw new InvalidDataException("The component caster has no original model binding.");
        BattleModelOwner recipient = process.FindOriginalActor(NativeBattleModelSlot.Decode(3)) ?? throw new InvalidDataException("The component companion has no original model binding.");
        NativeBattlePoseSource pose = world.BattleResources.ReadNativeGraphicsResource(source.GetCombatantDefinition(actor.Id)).PoseSource ?? throw new InvalidDataException("The component caster lacks its installed original skeleton.");
        if (pose.BoneCount != 70 || NativeBattleBodyBoneMatrices.Require(process.Graphics.ActorModels.Get(caster), pose).Length != 70)
        {
            throw new InvalidDataException("The component caster lacks its required original bone anchors.");
        }
        NativeBattleStatusMirror cached = source.NativePresentationStatusMemory.Rows[target.RequireNativeActorIndex()];
        ushort saved = cached.Saved ?? throw new InvalidDataException("The component target lacks its current physical saved-status owner.");
        uint battle = cached.Battle ?? throw new InvalidDataException("The component target lacks its current physical battle-status owner.");
        NativeBattleContactPacket contact = new(new(recipient, 0, 1, 0, saved, 0, battle), null);
        process.BeginDiagnosticAction(new(caster, 8, 15, -1, 351, 79, null), [contact]);
        return source;
    }
}
