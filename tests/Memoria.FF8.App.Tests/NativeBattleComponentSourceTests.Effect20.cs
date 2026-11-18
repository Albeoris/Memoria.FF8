using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class NativeBattleComponentSourceTests
{
    internal static BattleSession PrepareOriginalEffect20Component(GameWorld world)
    {
        BattleSession source = world.StartDiagnosticBattle(new(63, 0, 0x80, 0, 0)
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
        BattlePresentationProcess process = source.Presentation ?? throw new InvalidDataException("The original component encounter lacks its presentation process.");
        int[] models = [68, 81, 80, 77];
        for (int index = 0; index < models.Length; index++)
        {
            BattleCombatant actor = source.Combatants.Single(combatant => !combatant.IsPlayer && combatant.Slot == BattleRosterSlot.Enemy(index));
            BattleModelOwner owner = process.FindOriginalActor(NativeBattleModelSlot.Decode(checked((byte)(index + 3)))) ?? throw new InvalidDataException("The original component encounter lacks a required model binding.");
            if (actor.ModelId != models[index] || owner is not BattleEnemyActorModel enemy || enemy.Actor.ActorId != actor.Id)
            {
                throw new InvalidDataException("The original component encounter changed its required model identities.");
            }
        }
        BattleCombatant deferred = source.Combatants.Single(actor => !actor.IsPlayer && actor.Slot == BattleRosterSlot.Enemy(3));
        if (!deferred.Hidden || !deferred.Suspended || !deferred.Untargetable)
        {
            throw new InvalidDataException("The original component requires its unchanged deferred actor state.");
        }
        BattleModelOwner caster = process.FindOriginalActor(NativeBattleModelSlot.Decode(3))!;
        if (process.Graphics.ActorModels.Get(caster).Alpha7 != 0)
        {
            throw new InvalidDataException("The original component requires its observed primary body rendering state.");
        }
        NativeBattleActionHeaderPreparation header = new(caster, 8, 12, -1, 287, 20, null);
        NativeBattleContactPacket contact = new(new(caster, 0, 1, 0, 0, 0, 0), null);
        process.BeginDiagnosticAction(header, [contact]);
        return source;
    }
}
