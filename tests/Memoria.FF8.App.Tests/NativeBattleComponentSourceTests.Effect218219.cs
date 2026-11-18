using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class NativeBattleComponentSourceTests
{
    internal static BattleSession PrepareOriginalEffect218219Component(GameWorld world, EffectPreviewRequest request)
    {
        ushort effect = request.EffectId ?? throw new InvalidDataException("The component requires its selected original effect.");
        (int formation, int stage, int model, byte command, ushort action) = effect switch
        {
            218 => (795, 68, 128, (byte)236, (ushort)330),
            219 => (462, 121, 114, (byte)8, (ushort)311),
            _ => throw new InvalidDataException("The selected original component source is unsupported.")
        };
        if (request.EnemyId != model || request.StageId != stage)
        {
            throw new InvalidDataException("The component requires its original caster and stage.");
        }
        BattleSession source = world.StartDiagnosticBattle(new(formation, request.Seed, 0x80, 0, 0)
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
        if (actor.ModelId != model || actor.NativeAi is null || !world.BattleResources.NativeActions.Any(native => native.Command == command && native.ActionId == action && native.MagicId == effect))
        {
            throw new InvalidDataException("The component requires its installed original caster and action metadata.");
        }
        byte[] sequences = actor.NativeAi.Abilities.Where(ability => ability.Command == command && ability.Id == action).Select(ability => ability.Sequence).Distinct().ToArray();
        if (sequences.Length != 1 || effect == 218 && sequences[0] != 53)
        {
            throw new InvalidDataException("The component requires its unique original action sequence.");
        }
        if (effect == 218 && !source.Combatants.Any(companion => companion.ModelId == 82 && companion.NativeActorIndex == 4))
        {
            throw new InvalidDataException("The component requires its original encounter companion.");
        }
        BattlePresentationProcess process = source.Presentation ?? throw new InvalidDataException("The component requires its initialized presentation process.");
        BattleModelOwner caster = process.FindOriginalActor(NativeBattleModelSlot.Decode(3)) ?? throw new InvalidDataException("The component caster lacks its original model binding.");
        BattleModelOwner target = effect == 218 ? caster : process.FindOriginalActor(NativeBattleModelSlot.Decode(0)) ?? throw new InvalidDataException("The component recipient lacks its original model binding.");
        NativeBattleGraphicsResource resource = world.BattleResources.ReadNativeGraphicsResource(source.GetCombatantDefinition(actor.Id));
        NativeBattlePoseSource pose = resource.PoseSource ?? throw new InvalidDataException("The component caster lacks its original skeleton.");
        if (effect == 218 ? pose.BoneCount != 41 : pose.BoneCount <= 60)
        {
            throw new InvalidDataException("The component caster lacks its required original bone anchors.");
        }
        _ = NativeBattleBodyBoneMatrices.Require(process.Graphics.ActorModels.Get(caster), pose);
        if (effect == 218)
        {
            _ = resource.ReadText91(0);
            _ = resource.ReadText91(1);
            _ = resource.ReadText91(2);
        }
        byte targetIndex = effect == 218 ? (byte)3 : (byte)0;
        NativeBattleStatusMirror cached = source.NativePresentationStatusMemory.Rows[targetIndex];
        ushort saved = cached.Saved ?? throw new InvalidDataException("The component recipient lacks its current physical saved-status owner.");
        uint battle = cached.Battle ?? throw new InvalidDataException("The component recipient lacks its current physical battle-status owner.");
        NativeBattleContactPacket contact = new(new(target, 0, 1, 0, saved, 0, battle), null);
        process.BeginDiagnosticAction(new(caster, command, sequences[0], -1, action, effect, null), [contact]);
        return source;
    }
}
