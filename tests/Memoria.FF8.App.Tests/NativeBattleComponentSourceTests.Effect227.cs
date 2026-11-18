using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class NativeBattleComponentSourceTests
{
    internal static BattleSession PrepareOriginalEffect227Component(GameWorld world, EffectPreviewRequest request)
    {
        if (request.EnemyId != 121 || request.StageId != 142)
        {
            throw new InvalidDataException("The component requires its original caster and stage.");
        }
        BattleSession source = world.StartDiagnosticBattle(new(511, request.Seed, 0x80, 0, 0)
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
        BattleCombatant companion = source.Combatants.Single(candidate => candidate.NativeActorIndex == 4);
        if (actor.ModelId != 121 || !actor.IsActive || actor.Hidden || actor.Suspended || companion.ModelId != 123 || !companion.Hidden || !companion.Suspended || !companion.Untargetable)
        {
            throw new InvalidDataException("The component requires its original caster and dormant encounter companion.");
        }
        if (actor.NativeAi is null || !actor.NativeAi.Abilities.Any(ability => ability.Command == 8 && ability.Id == 339 && ability.Sequence == 13) ||
            !world.BattleResources.NativeActions.Any(action => action.Command == 8 && action.ActionId == 339 && action.MagicId == 227))
        {
            throw new InvalidDataException("The component requires its installed original action and sequence metadata.");
        }
        BattlePresentationProcess process = source.Presentation ?? throw new InvalidDataException("The component requires its initialized presentation process.");
        BattleModelOwner caster = process.FindOriginalActor(NativeBattleModelSlot.Decode(3)) ?? throw new InvalidDataException("The component caster lacks its original model binding.");
        BattleModelOwner target = process.FindOriginalActor(NativeBattleModelSlot.Decode(0)) ?? throw new InvalidDataException("The component recipient lacks its original model binding.");
        foreach ((BattleCombatant combatant, int count) in new[] { (actor, 69), (companion, 2) })
        {
            BattleModelOwner owner = process.FindOriginalActor(NativeBattleModelSlot.Decode(checked((byte)combatant.RequireNativeActorIndex()))) ?? throw new InvalidDataException("The component lacks an original encounter model binding.");
            NativeBattleGraphicsResource resource = world.BattleResources.ReadNativeGraphicsResource(source.GetCombatantDefinition(combatant.Id));
            NativeBattlePoseSource pose = resource.PoseSource ?? throw new InvalidDataException("The component lacks its original encounter skeleton.");
            if (pose.BoneCount != count || NativeBattleBodyBoneMatrices.Require(process.Graphics.ActorModels.Get(owner), pose).Length != count)
            {
                throw new InvalidDataException("The component lacks its original current bone anchors.");
            }
        }
        NativeBattleGraphicsResource casterResource = world.BattleResources.ReadNativeGraphicsResource(source.GetCombatantDefinition(actor.Id));
        for (int index = 0; index < 4; index++)
        {
            _ = casterResource.ReadText91(index);
        }
        NativeBattleStatusMirror cached = source.NativePresentationStatusMemory.Rows[0];
        ushort saved = cached.Saved ?? throw new InvalidDataException("The component recipient lacks its current physical saved-status owner.");
        uint battle = cached.Battle ?? throw new InvalidDataException("The component recipient lacks its current physical battle-status owner.");
        NativeBattleContactPacket geometry = new(new(target, 0, 1, 0, saved, 0, battle), null);
        process.BeginDiagnosticAction(new(caster, 8, 13, -1, 339, 227, null), [geometry]);
        return source;
    }
}
