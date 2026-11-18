using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class NativeBattleComponentSourceTests
{
    internal static BattleSession PrepareOriginalEffect253Component(GameWorld world, EffectPreviewRequest request)
    {
        ushort effect = request.EffectId ?? throw new InvalidDataException("The component requires its selected effect.");
        (int model, byte physical, ushort action, byte sequence) = effect switch
        {
            253 => (105, (byte)3, (ushort)283, (byte)16),
            254 => (107, (byte)5, (ushort)281, (byte)17),
            255 => (106, (byte)4, (ushort)282, (byte)17),
            _ => throw new InvalidDataException("The selected original counter component is unsupported.")
        };
        if (request.EnemyId != model || request.StageId != 65)
        {
            throw new InvalidDataException("The component requires its original caster and stage.");
        }
        BattleSession source = world.StartDiagnosticBattle(new(794, request.Seed, 0x80, 0, 0)
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
        BattleCombatant actor = source.Combatants.Single(candidate => candidate.NativeActorIndex == physical);
        if (actor.ModelId != model || actor.NativeAi is null ||
            !actor.NativeAi.Abilities.Any(ability => ability.Command == 8 && ability.Id == action && ability.Sequence == sequence) ||
            !world.BattleResources.NativeActions.Any(native => native.Command == 8 && native.ActionId == action && native.MagicId == effect))
        {
            throw new InvalidDataException("The component requires its installed original counter action and sequence metadata.");
        }
        BattlePresentationProcess process = source.Presentation ?? throw new InvalidDataException("The component requires its initialized presentation process.");
        BattleModelOwner caster = process.FindOriginalActor(NativeBattleModelSlot.Decode(physical)) ?? throw new InvalidDataException("The component caster lacks its original model binding.");
        BattleModelOwner target = process.FindOriginalActor(NativeBattleModelSlot.Decode(0)) ?? throw new InvalidDataException("The component recipient lacks its original model binding.");
        for (byte index = 3; index <= 5; index++)
        {
            BattleCombatant member = source.Combatants.Single(candidate => candidate.NativeActorIndex == index);
            BattleModelOwner owner = process.FindOriginalActor(NativeBattleModelSlot.Decode(index)) ?? throw new InvalidDataException("The component lacks its original trio model binding.");
            if (member.ModelId != 105 + index - 3 || !member.IsActive || member.Hidden || member.Suspended || member.Untargetable)
            {
                throw new InvalidDataException("The component changed its original trio identities or lifecycle.");
            }
            NativeBattleGraphicsResource resource = world.BattleResources.ReadNativeGraphicsResource(source.GetCombatantDefinition(member.Id));
            NativeBattlePoseSource pose = resource.PoseSource ?? throw new InvalidDataException("The component lacks its original trio skeleton.");
            _ = NativeBattleBodyBoneMatrices.Require(process.Graphics.ActorModels.Get(owner), pose);
        }
        if (NativeBattleModelRings.Walk(process.Graphics.ActorModels, caster).Count() != 3)
        {
            throw new InvalidDataException("The component requires its original initialized three-model ring.");
        }
        NativeBattleStatusMirror cached = source.NativePresentationStatusMemory.Rows[0];
        ushort saved = cached.Saved ?? throw new InvalidDataException("The component recipient lacks its current physical saved-status owner.");
        uint battle = cached.Battle ?? throw new InvalidDataException("The component recipient lacks its current physical battle-status owner.");
        NativeBattleContactPacket geometry = new(new(target, 0, 1, 0, saved, 0, battle), null);
        process.BeginDiagnosticAction(new(caster, 8, sequence, -1, action, effect, null), [geometry]);
        return source;
    }
}
