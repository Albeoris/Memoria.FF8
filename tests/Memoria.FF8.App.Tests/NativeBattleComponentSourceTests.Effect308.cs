using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class NativeBattleComponentSourceTests
{
    internal static BattleSession PrepareOriginalEffect308Component(GameWorld world)
    {
        BattleSession source = world.StartDiagnosticBattle(new(794, 0, 0x80, 0, 0)
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
            throw new InvalidDataException("The original trio component did not finish its native initialization.");
        }
        BattlePresentationProcess process = source.Presentation ?? throw new InvalidDataException("The original trio component lacks its presentation process.");
        BattleModelOwner[] owners = new BattleModelOwner[3];
        for (int index = 0; index < owners.Length; index++)
        {
            BattleCombatant actor = source.Combatants.Single(candidate => !candidate.IsPlayer && candidate.Slot == BattleRosterSlot.Enemy(index));
            BattleModelOwner owner = process.FindOriginalActor(NativeBattleModelSlot.Decode(checked((byte)(index + 3)))) ?? throw new InvalidDataException("The original trio component lacks a required model binding.");
            if (actor.ModelId != 105 + index || !actor.IsActive || actor.Hidden || actor.Suspended || actor.Untargetable || owner is not BattleEnemyActorModel enemy || enemy.Actor.ActorId != actor.Id)
            {
                throw new InvalidDataException("The original trio component changed its required model identities or entry state.");
            }
            owners[index] = owner;
        }
        BattleModelOwner caster = owners[0];
        if (NativeBattleModelRings.Walk(process.Graphics.ActorModels, caster).Count() != owners.Length)
        {
            throw new InvalidDataException("The original trio component requires its initialized three-model ring.");
        }
        BattleCombatant casterActor = source.Combatants.Single(actor => actor.NativeActorIndex == 3);
        NativeBattleAiDefinition ai = casterActor.NativeAi ?? throw new InvalidDataException("The original trio component lacks its installed ability metadata.");
        if (!ai.Abilities.Any(ability => ability.Command == 8 && ability.Id == 74 && ability.Sequence == 12) || !ai.Abilities.Any(ability => ability.Command == 8 && ability.Id == 286 && ability.Sequence == 14) ||
            !source.ReadDefinition().NativeActions.Any(action => action.Command == 8 && action.ActionId == 74 && action.MagicId == 0))
        {
            throw new InvalidDataException("The original trio component lacks its declared prerequisite and effect action metadata.");
        }
        var unchangedCombat = source.Combatants.Select(actor => (actor.Id, actor.CurrentHp, actor.NativeStatus0, actor.NativeStatus1)).ToArray();
        NativeBattleContactPacket contact = new(new(caster, 0, 1, 0, 0, 0, 0), null);
        process.BeginDiagnosticAction(new(caster, 8, 12, -1, 74, 0, null), [contact]);
        for (int callback = 0; callback < 3600 && (process.ActiveActionToken is not null || source.ObserveDiagnosticDrainBlockers().Count != 0); callback++)
        {
            source.AdvanceNativeGraphicsPresentation(PresentationTiming.SimulationStep);
        }
        if (process.ActiveActionToken is not null || source.ObserveDiagnosticDrainBlockers().Count != 0 || owners.Any(owner => process.Graphics.ActorModels.Get(owner).ReadLinkedModel() is not null))
        {
            throw new InvalidDataException("The original trio prerequisite did not complete and naturally release its model ring.");
        }
        if (!unchangedCombat.SequenceEqual(source.Combatants.Select(actor => (actor.Id, actor.CurrentHp, actor.NativeStatus0, actor.NativeStatus1))))
        {
            throw new InvalidDataException("The presentation prerequisite changed the original trio's combat state.");
        }
        process.BeginDiagnosticAction(new(caster, 8, 14, -1, 286, 308, null), [contact]);
        return source;
    }
}
