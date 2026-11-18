using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class NativeBattleComponentSourceTests
{
    internal static BattleSession PrepareOriginalEffect243246Component(GameWorld world, EffectPreviewRequest request)
    {
        if (request.EnemyId != 101 || request.StageId != 25)
        {
            throw new InvalidDataException("The component requires its original caster and stage.");
        }
        BattleSession source = world.StartDiagnosticBattle(new(164, request.Seed, 0x80, 0, 0)
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
        ushort effect = request.EffectId ?? throw new InvalidDataException("The component requires its selected effect.");
        ushort action = effect switch
        {
            243 => 147,
            244 => 148,
            245 => 149,
            246 => 150,
            _ => throw new InvalidDataException("The selected original component source is unsupported.")
        };
        if (actor.ModelId != 101 || actor.NativeAi is null || !world.BattleResources.NativeActions.Any(native => native.Command == 8 && native.ActionId == action && native.MagicId == effect))
        {
            throw new InvalidDataException("The component requires its installed original caster and action metadata.");
        }
        byte[] sequences = actor.NativeAi.Abilities.Where(ability => ability.Command == 8 && ability.Id == action).Select(ability => ability.Sequence).Distinct().ToArray();
        if (sequences.Length != 1)
        {
            throw new InvalidDataException("The component requires its unique installed original action sequence.");
        }
        BattlePresentationProcess process = source.Presentation ?? throw new InvalidDataException("The component requires its initialized presentation process.");
        BattleModelOwner caster = process.FindOriginalActor(NativeBattleModelSlot.Decode(3)) ?? throw new InvalidDataException("The component caster lacks its original model binding.");
        BattleModelOwner target = process.FindOriginalActor(NativeBattleModelSlot.Decode(0)) ?? throw new InvalidDataException("The component recipient lacks its original model binding.");
        NativeBattleGraphicsResource resource = world.BattleResources.ReadNativeGraphicsResource(source.GetCombatantDefinition(actor.Id));
        NativeBattlePoseSource pose = resource.PoseSource ?? throw new InvalidDataException("The component lacks its original encounter skeleton.");
        if (pose.BoneCount != 45 || NativeBattleBodyBoneMatrices.Require(process.Graphics.ActorModels.Get(caster), pose).Length != 45)
        {
            throw new InvalidDataException("The component lacks its original current bone anchors.");
        }
        NativeBattleStatusMirror cached = source.NativePresentationStatusMemory.Rows[0];
        ushort saved = cached.Saved ?? throw new InvalidDataException("The component recipient lacks its current physical saved-status owner.");
        uint battle = cached.Battle ?? throw new InvalidDataException("The component recipient lacks its current physical battle-status owner.");
        NativeBattleContactPacket geometry = new(new(target, 0, 1, 0, saved, 0, battle), null);
        process.BeginDiagnosticAction(new(caster, 8, sequences[0], -1, action, effect, null), [geometry]);
        return source;
    }
}
