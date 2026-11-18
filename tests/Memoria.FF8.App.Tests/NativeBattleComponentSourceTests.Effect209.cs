using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class NativeBattleComponentSourceTests
{
    internal static BattleSession PrepareOriginalEffect209Component(GameWorld world, EffectPreviewRequest request)
    {
        if (request.EnemyId != 115 || request.StageId != 137)
        {
            throw new InvalidDataException("The component requires its original encounter caster and stage.");
        }
        BattleSession source = world.StartDiagnosticBattle(new(813, request.Seed, 0x80, 0, 0)
        {
            BattleMusic = 5,
            PrimaryMusicChannel = -1,
            AtbMode = BattleAtbMode.Normal,
            Fixture = new([], [], true) { CombatParty = [0, 255, 255] }
        });
        for (int callback = 0; callback < 3600 && (!source.NativeEntryReady || source.ObserveDiagnosticDrainBlockers().Count != 0); callback++)
        {
            source.AdvanceNativeGraphicsPresentation(BattlePresentationTiming.ElapsedForCallbacks(1));
        }
        if (!source.NativeEntryReady || source.ObserveDiagnosticDrainBlockers().Count != 0)
        {
            throw new InvalidDataException("The original component encounter did not finish its native initialization.");
        }
        BattleCombatant actor = source.Combatants.Single(candidate => candidate.NativeActorIndex == 3);
        if (actor.ModelId != 115 || actor.SceneSlot != 0 || !actor.IsActive || !actor.Hidden || actor.Suspended || actor.NativeAi is null || !actor.NativeAi.Abilities.Any(ability => ability.Command == 8 && ability.Id == 319 && ability.Sequence == 13) || !world.BattleResources.NativeActions.Any(action => action.Command == 8 && action.ActionId == 319 && action.MagicId == 209))
        {
            throw new InvalidDataException("The component requires its original hidden caster and installed action metadata.");
        }
        BattlePresentationProcess process = source.Presentation ?? throw new InvalidDataException("The component requires its initialized presentation process.");
        NativeBattleStageState stage = process.NativeStage ?? throw new InvalidDataException("The component requires its current stage owner.");
        if (stage.Extended?.Stage137.Variants is not { GeometryAddress: > 0 } variants || stage.SourceAddress1D9A820 != variants.GeometryAddress)
        {
            throw new InvalidDataException("The component requires its actual initialized stage variant geometry destination.");
        }
        BattleModelOwner caster = process.FindOriginalActor(NativeBattleModelSlot.Decode(3)) ?? throw new InvalidDataException("The component caster lacks its original model binding.");
        BattleModelOwner target = process.FindOriginalActor(NativeBattleModelSlot.Decode(0)) ?? throw new InvalidDataException("The component recipient lacks its original model binding.");
        NativeBattlePoseSource pose = world.BattleResources.ReadNativeGraphicsResource(source.GetCombatantDefinition(actor.Id)).PoseSource ?? throw new InvalidDataException("The component caster lacks its original skeleton.");
        if (pose.BoneCount != 33 || NativeBattleBodyBoneMatrices.Require(process.Graphics.ActorModels.Get(caster), pose).Length != 33)
        {
            throw new InvalidDataException("The component caster lacks its current original bone owners.");
        }
        NativeBattleStatusMirror cached = source.NativePresentationStatusMemory.Rows[0];
        ushort saved = cached.Saved ?? throw new InvalidDataException("The component recipient lacks its current physical saved-status owner.");
        uint battle = cached.Battle ?? throw new InvalidDataException("The component recipient lacks its current physical battle-status owner.");
        NativeBattleContactPacket contact = new(new(target, 0, 1, 0, saved, 0, battle), null);
        process.BeginDiagnosticAction(new(caster, 8, 13, -1, 319, 209, null), [contact]);
        return source;
    }
}
