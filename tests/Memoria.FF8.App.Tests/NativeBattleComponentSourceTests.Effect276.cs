using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class NativeBattleComponentSourceTests
{
    internal static BattleSession PrepareOriginalEffect276Component(GameWorld world, EffectPreviewRequest request)
    {
        if (request.EffectId != 276 || request.EnemyId != 92)
        {
            throw new InvalidDataException("The component requires its original native death owner.");
        }
        BattleSession source = world.StartDiagnosticBattle(new(9, request.Seed, 0x80, 0, 0)
        {
            BattleMusic = 5,
            PrimaryMusicChannel = -1,
            AtbMode = BattleAtbMode.Normal,
            Fixture = new([], [], true) { CombatParty = [0, 1, 2] }
        });
        BattleCombatant original = source.Combatants.Single(actor => !actor.IsPlayer && actor.ModelId == 92);
        if (original.NativeAi is null || !original.NativeAi.Abilities.Any(ability => ability.Command == 8 && ability.Id == 145 && ability.Sequence == 19))
        {
            throw new InvalidDataException("The component lacks its installed original death action metadata.");
        }
        RequireOriginalEffect276Counter(original.NativeAi);
        int attacks = 0;
        int revivals = 0;
        int previousHp = original.CurrentHp;
        for (int callback = 0; callback < 90000 && source.Outcome == BattleOutcome.Active; callback++)
        {
            if (previousHp == 0 && original.CurrentHp > 0)
            {
                revivals++;
            }
            previousHp = original.CurrentHp;
            BattlePresentationProcess process = source.Presentation!;
            if (process.ActiveActionToken is not null && process.ActionPackets.PublishedHeaders().Any(entry => entry.Slot.Effect == 276))
            {
                BattleDiagnosticComponentAdmission admission = GameWorld.ValidateEffectComponentSource(source, 276);
                NativeBattleActionHeaderSlot header = process.ActionPackets.ReadHeader(new(admission.HeaderIndex));
                BattleCombatantSnapshot actorState = source.CaptureForContinuation().Combatants.Single(candidate => candidate.Id == original.Id);
                if (actorState.NativeParticipant?.Variables is not { Length: 8 } variablesOwner || variablesOwner[3] != 5 || original.CurrentHp != 0 || admission.Command != 8 || admission.Action != 145 || header.Sequence != 19 || process.Graphics.ActorModels.Get(admission.Actor).ModelId4 != 108)
                {
                    string variables = string.Join(",", actorState.NativeParticipant?.Variables ?? []);
                    throw new InvalidDataException($"The native death component admission differs: attacks={attacks}, revivals={revivals}, hp={original.CurrentHp}/{original.MaximumHp}, deathCount={original.NativeDeathCount}, logicalModel={original.ModelId}, model={process.Graphics.ActorModels.Get(admission.Actor).ModelId4}, command={admission.Command}, action={admission.Action}, sequence={header.Sequence}, callback={source.NativeHostCallbacks}, variables=[{variables}].");
                }
                return source;
            }
            if (source.NativeEntryReady && source.ActiveAction is null && source.QueuedActions.Count == 0 && original.IsActive && original.IsAlive && !original.Untargetable && !original.Suspended)
            {
                BattleCombatant? ready = source.Combatants.FirstOrDefault(actor => actor.IsPlayer && source.CanChoosePlayerCommand(actor.Id));
                if (ready is not null)
                {
                    if (++attacks > 1024)
                    {
                        throw new InvalidDataException("The original death source exceeded its bounded ordinary attack preparation.");
                    }
                    source.QueueAttack(ready.Id, original.Id);
                }
            }
            source.Tick();
        }
        throw new InvalidDataException("The original encounter did not publish its unstarted sixth death component within the preparation budget.");
    }

    internal static void RequireOriginalEffect276Counter(NativeBattleAiDefinition ai)
    {
        if (ai.Scripts.Length <= 3 || ai.Abilities.Length <= 8)
        {
            throw new InvalidDataException("The original death component lacks its native counter program.");
        }
        NativeBattleAiInstruction initial = Read(ai.Scripts[0], 9);
        NativeBattleAiInstruction condition = Read(ai.Scripts[3], 0);
        NativeBattleAiInstruction increment = Read(ai.Scripts[3], 29);
        NativeBattleAiInstruction dispatch = Read(ai.Scripts[3], 118);
        if (initial.Opcode != 14 || initial[0] != 223 || initial[1] != 0 ||
            condition.Opcode != 2 || condition.Condition != new NativeBattleAiCondition(223, 200, 4, 4) || condition.BranchOffset != 75 ||
            increment.Opcode != 18 || increment[0] != 223 || increment[1] != 1 ||
            dispatch.Opcode != 12 || dispatch[0] != 8 || ai.Abilities[8] is not { Command: 8, Id: 145, Sequence: 19 })
        {
            throw new InvalidDataException("The original death component requires its actual five-revival local counter and final native dispatch.");
        }

        static NativeBattleAiInstruction Read(byte[] script, int offset)
        {
            if (!NativeBattleAiInstruction.TryDecode(script, offset, out NativeBattleAiInstruction instruction, out string? error))
            {
                throw new InvalidDataException("The original death counter instruction is invalid: " + error);
            }
            return instruction;
        }
    }
}
