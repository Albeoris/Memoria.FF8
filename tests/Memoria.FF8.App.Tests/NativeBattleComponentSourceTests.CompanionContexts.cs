using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class NativeBattleComponentSourceTests
{
    internal static BattleSession PrepareOriginalCompanionContext(GameWorld world, ushort effect)
    {
        (int formation, int stage, int model, int companionModel, ushort action, byte sequence, bool hidden) = effect switch
        {
            314 => (216, 56, 103, 104, (ushort)191, (byte)16, false),
            315 => (104, 19, 75, 34, (ushort)151, (byte)13, true),
            _ => throw new InvalidDataException("The selected component has no declared companion encounter.")
        };
        BattleSession source = world.StartDiagnosticBattle(new(formation, 0, 0x80, 0, 0)
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
        if (!source.NativeEntryReady || source.ObserveDiagnosticDrainBlockers().Count != 0 || source.ReadDefinition().StageId != stage)
        {
            throw new InvalidDataException("The original companion encounter did not finish its native initialization.");
        }
        BattlePresentationProcess process = source.Presentation ?? throw new InvalidDataException("The original companion encounter lacks its presentation process.");
        BattleCombatant casterActor = source.Combatants.Single(actor => !actor.IsPlayer && actor.NativeActorIndex == 3);
        BattleCombatant companionActor = source.Combatants.Single(actor => !actor.IsPlayer && actor.NativeActorIndex == 4);
        BattleModelOwner caster = process.FindOriginalActor(NativeBattleModelSlot.Decode(3)) ?? throw new InvalidDataException("The original companion encounter lacks its caster binding.");
        BattleModelOwner companion = process.FindOriginalActor(NativeBattleModelSlot.Decode(4)) ?? throw new InvalidDataException("The original companion encounter lacks its second model binding.");
        if (casterActor.ModelId != model || casterActor.Slot != BattleRosterSlot.Enemy(0) || caster is not BattleEnemyActorModel casterOwner || casterOwner.Actor.ActorId != casterActor.Id ||
            companionActor.ModelId != companionModel || companionActor.Slot != BattleRosterSlot.Enemy(1) || companion is not BattleEnemyActorModel companionOwner || companionOwner.Actor.ActorId != companionActor.Id ||
            companionActor.Hidden != hidden || !companionActor.Suspended || !companionActor.Untargetable)
        {
            throw new InvalidDataException("The original companion encounter changed its actual model identities or dormant state.");
        }
        BattleNativeAction native = source.ReadDefinition().NativeActions.Single(candidate => candidate.Command == 8 && candidate.ActionId == action && candidate.MagicId == effect);
        if (native.AttackType != 0 || native.Power != 0 || native.AttackFlags != 3 || native.Statuses0 != 0 || native.Statuses1 != 0)
        {
            throw new InvalidDataException("The original companion action is not the declared neutral presentation context.");
        }
        BattleModelOwner target = effect == 314
            ? process.FindOriginalActor(NativeBattleModelSlot.Decode(0)) ?? throw new InvalidDataException("The original companion encounter lacks its declared party recipient.")
            : caster;
        NativeBattleActionHeaderPreparation header = new(caster, 8, sequence, -1, action, effect, null);
        NativeBattleContactPacket contact = new(new(target, 0, 1, 0, 0, 0, 0), null);
        process.BeginDiagnosticAction(header, [contact]);
        return source;
    }
}
