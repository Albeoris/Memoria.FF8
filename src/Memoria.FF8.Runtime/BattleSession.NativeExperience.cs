using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleExperienceState([property: JsonRequired] NativeBattleExperienceBanks Banks, [property: JsonRequired] bool Distributed)
{
    public void Validate()
    {
        (Banks ?? throw new InvalidDataException("Native experience state requires its physical WORD owners.")).Validate();
    }
}

public sealed partial class BattleSession
{
    private NativeBattleExperienceState? nativeExperience;
    public NativeBattleExperienceBanks? NativeExperienceBanks => nativeExperience?.Banks;
    public bool NativeExperienceDistributed => nativeExperience?.Distributed == true;

    private void InitializeNativeExperience48D020()
    {
        nativeExperience = new(NativeBattleExperienceBanks.Initialize48D020(), false);
    }

    private void RestoreNativeExperience(NativeBattleExperienceState? state)
    {
        state?.Validate();
        nativeExperience = state;
    }

    private int NativeExperienceAveragePartyLevel()
    {
        BattleCombatant[] party = combatants.Where(actor => actor.IsPlayer && actor.NativeIndex is >= 0 and < 3).ToArray();
        if (party.Length == 0 || party.Any(actor => actor.ModelId < 0 || actor.Level is < 1 or > 100))
        {
            throw new NotSupportedException("Native48B2E0 requires its actual occupied physical party identities and level BYTEs.");
        }
        return party.Sum(actor => actor.Level) / party.Length;
    }

    private void AccrueNativeExperience494AF0(BattleCombatant target)
    {
        if (nativeExperience is null)
        {
            return;
        }
        if (nativeExperience.Distributed)
        {
            throw new InvalidOperationException("Native494AF0 cannot accrue after the result distributor.");
        }
        NativeContactWorking contact = nativeContactWorking ?? throw new NotSupportedException("Native494AF0 requires the actual494410 source actor, rather than reconstructed defeat credit.");
        byte command = nativeActionScratch.Command ?? throw new NotSupportedException("Native494AF0 requires its current effective command BYTE.");
        BattleCombatant source = contact.Actor;
        byte? guardianId = null;
        int recipientLevel = source.Level;
        if (command == 254)
        {
            ushort action = nativeActionScratch.Action ?? throw new NotSupportedException("Native494AF0 requires its current GF action WORD64..79.");
            if (action is < 64 or >= 80)
            {
                throw new InvalidDataException("Native494AF0 GF action is outside64..79.");
            }
            guardianId = checked((byte)(action - 64));
            NativeCharacterVitals vitals = magicVitals ?? throw new NotSupportedException("Native GF experience requires the current saved GF experience and level curve.");
            recipientLevel = NativeStatDefinitions.Level(vitals.GuardianForces[guardianId.Value].Experience, vitals.Definitions.GuardianForces[guardianId.Value]);
        }
        BattleEnemyRewardDefinition rewards = target.SourceDefinition.Rewards ?? throw new NotSupportedException("Native494AF0 requires the current target's original resource EXP WORDs.");
        NativeBattleExperienceInput input = new(checked((byte)source.NativeIndex), command, guardianId, checked((byte)target.Level), checked((byte)recipientLevel), checked((byte)NativeExperienceAveragePartyLevel()), rewards.KillExperience, rewards.SharedExperience, target.MaximumHp, target.CurrentHp, checked((byte)target.SourceDefinition.AbilityPoints));
        nativeExperience = nativeExperience with { Banks = nativeExperience.Banks.Accrue494AF0(input) };
    }

    private void FinalizeNativeExperience494D40()
    {
        if (nativeExperience is null || nativeExperience.Distributed)
        {
            return;
        }
        NativeCharacterVitals vitals = magicVitals ?? throw new NotSupportedException("Native494D40 requires the current saved party junctions and sixteen GF HP owners.");
        NativeBattleExperiencePartySlot[] party = new NativeBattleExperiencePartySlot[3];
        for (int index = 0; index < party.Length; index++)
        {
            BattleCombatant? actor = combatants.SingleOrDefault(candidate => candidate.IsPlayer && candidate.NativeIndex == index);
            if (actor is null)
            {
                party[index] = new(false, null, 0);
                continue;
            }
            int row = actor.MagicRecordId ?? throw new NotSupportedException("Native494D40 requires its physical player's actual saved character-row identity.");
            ushort guardianMask = 0;
            foreach (int id in vitals.Records[row].Junction.GuardianForceAssignments)
            {
                guardianMask |= checked((ushort)(1 << id));
            }
            party[index] = new(actor.IsActive, NativeSavedStatusBits(actor), guardianMask);
        }
        ushort[] guardianHp = vitals.GuardianForces.Select((gf, id) => guardianForces.TryGetValue(id, out BattleGuardianForceVital? current) ? current.HitPoints : gf.HitPoints).ToArray();
        int averageLevel = NativeExperienceAveragePartyLevel();
        List<NativeBattleExperienceSurvivor> survivors = [];
        foreach (BattleCombatant actor in combatants.Where(actor => !actor.IsPlayer && actor.NativeIndex is >= 3 and < 7))
        {
            ushort status = NativeSavedStatusBits(actor);
            if ((status & 1) != 0)
            {
                continue;
            }
            BattleEnemyRewardDefinition rewards = actor.SourceDefinition.Rewards ?? throw new NotSupportedException("Native494D40 requires each surviving physical enemy's original shared EXP WORD.");
            survivors.Add(new(status, rewards.SharedExperience, checked((byte)actor.Level), checked((byte)averageLevel), actor.MaximumHp, actor.CurrentHp));
        }
        NativeBattleExperienceBanks distributed = nativeExperience.Banks.Distribute494D40(party, guardianHp, survivors, (ResolvedNativeFlags & 8) != 0);
        nativeExperience = new(distributed, true);
    }
}
