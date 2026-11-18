using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class NativeBattleComponentSourceTests
{
    internal static BattleSession PrepareOriginalEffect279Component(GameWorld world, EffectPreviewRequest request)
    {
        if (request.EnemyId != 30 || request.StageId != 127)
        {
            throw new InvalidDataException("The component requires its original caster and stage.");
        }
        BattleSession source = world.StartDiagnosticBattle(new(377, request.Seed, 0x80, 0, 0)
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
        if (request.EffectId != 279 || actor.ModelId != 30 || actor.NativeAi is null ||
            !actor.NativeAi.Abilities.Any(ability => ability.Command == 8 && ability.Id == 106 && ability.Sequence == 14) ||
            !world.BattleResources.NativeActions.Any(native => native.Command == 8 && native.ActionId == 106 && native.MagicId == 279))
        {
            throw new InvalidDataException("The component requires its installed original caster and action metadata.");
        }
        BattlePresentationProcess process = source.Presentation ?? throw new InvalidDataException("The component requires its initialized presentation process.");
        BattleModelOwner caster = process.FindOriginalActor(NativeBattleModelSlot.Decode(3)) ?? throw new InvalidDataException("The component caster lacks its original model binding.");
        BattleModelOwner target = process.FindOriginalActor(NativeBattleModelSlot.Decode(0)) ?? throw new InvalidDataException("The component recipient lacks its original model binding.");
        NativeBattleGraphicsResource resource = world.BattleResources.ReadNativeGraphicsResource(source.GetCombatantDefinition(actor.Id));
        NativeBattlePoseSource pose = resource.PoseSource ?? throw new InvalidDataException("The component lacks its original encounter skeleton.");
        if (pose.BoneCount != 46 || NativeBattleBodyBoneMatrices.Require(process.Graphics.ActorModels.Get(caster), pose).Length != 46)
        {
            throw new InvalidDataException("The component lacks its original current bone anchors.");
        }
        NativeBattleStatusMirror cached = source.NativePresentationStatusMemory.Rows[0];
        ushort saved = cached.Saved ?? throw new InvalidDataException("The component recipient lacks its current physical saved-status owner.");
        uint battle = cached.Battle ?? throw new InvalidDataException("The component recipient lacks its current physical battle-status owner.");
        NativeBattleContactPacket geometry = new(new(target, 0, 1, 0, saved, 0, battle), null);
        process.BeginDiagnosticAction(new(caster, 8, 14, -1, 106, 279, null), [geometry]);
        return source;
    }
}
