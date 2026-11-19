using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class NativeBattleComponentSourceTests
{
    internal static BattleSession PrepareOriginalEffect290298303319Component(GameWorld world, EffectPreviewRequest request)
    {
        ushort effect = request.EffectId ?? throw new InvalidDataException("The component requires its selected original effect.");
        (int formation, int stage, int model, int bones, ushort action, byte sequence, byte targetIndex) = effect switch
        {
            290 => (431, 126, 109, 28, (ushort)291, (byte)14, (byte)0),
            298 => (194, 52, 102, 45, (ushort)119, (byte)14, (byte)0),
            303 => (363, 96, 11, 46, (ushort)258, (byte)13, (byte)4),
            319 => (372, 140, 38, 36, (ushort)279, (byte)14, (byte)0),
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
        if (actor.ModelId != model || actor.NativeAi is null ||
            !actor.NativeAi.Abilities.Any(ability => ability.Command == 8 && ability.Id == action && ability.Sequence == sequence) ||
            !world.BattleResources.NativeActions.Any(native => native.Command == 8 && native.ActionId == action && native.MagicId == effect))
        {
            throw new InvalidDataException("The component requires its installed original caster and action metadata.");
        }
        if (effect == 290 && source.Combatants.Count(candidate => !candidate.IsPlayer && candidate.ModelId == 110) != 2)
        {
            throw new InvalidDataException("The component requires both original dormant encounter companions.");
        }
        if (effect == 303)
        {
            BattleCombatant companion = source.Combatants.Single(candidate => candidate.NativeActorIndex == 4);
            if (companion.ModelId != 12 || !companion.Hidden || !companion.Suspended)
            {
                throw new InvalidDataException("The component requires its actual loaded dormant recipient.");
            }
        }
        BattlePresentationProcess process = source.Presentation ?? throw new InvalidDataException("The component requires its initialized presentation process.");
        BattleModelOwner caster = process.FindOriginalActor(NativeBattleModelSlot.Decode(3)) ?? throw new InvalidDataException("The component caster lacks its original model binding.");
        BattleModelOwner target = process.FindOriginalActor(NativeBattleModelSlot.Decode(targetIndex)) ?? throw new InvalidDataException("The component recipient lacks its original model binding.");
        NativeBattleGraphicsResource resource = world.BattleResources.ReadNativeGraphicsResource(source.GetCombatantDefinition(actor.Id));
        NativeBattlePoseSource pose = resource.PoseSource ?? throw new InvalidDataException("The component lacks its original encounter skeleton.");
        if (pose.BoneCount != bones || NativeBattleBodyBoneMatrices.Require(process.Graphics.ActorModels.Get(caster), pose).Length != bones)
        {
            throw new InvalidDataException("The component lacks its original current bone anchors.");
        }
        NativeBattleStatusMirror cached = source.NativePresentationStatusMemory.Rows[targetIndex];
        ushort saved = cached.Saved ?? throw new InvalidDataException("The component recipient lacks its current physical saved-status owner.");
        uint battle = cached.Battle ?? throw new InvalidDataException("The component recipient lacks its current physical battle-status owner.");
        NativeBattleContactPacket geometry = new(new(target, 0, 1, 0, saved, 0, battle), null);
        process.BeginDiagnosticAction(new(caster, 8, sequence, -1, action, effect, null), [geometry]);
        return source;
    }
}
