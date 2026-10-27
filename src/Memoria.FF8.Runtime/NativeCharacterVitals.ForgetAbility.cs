using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeCharacterVitals
{
    /// <summary>4FB046 clears the GF bit, records original-rule suppression, then refreshes every character's remaining junctions.</summary>
    public NativeCharacterVitals ForgetGuardianForceAbility(int id, int ability)
    {
        Validate();
        if ((uint)id >= 16 || (uint)ability >= 128 || (GuardianForces[id].Flags & 1) == 0 || !GuardianForceAbilities(id).Any(entry => entry.Ability == ability))
        {
            throw new InvalidOperationException("Amnesia Greens require an acquired GF's current native ability entry.");
        }
        NativeCharacterVitals result = ApplyGuardianForceTemplates(ResolveGuardianForceTemplates());
        NativeGuardianForceVital gf = result.GuardianForces[id];
        GuardianForceAbilityTemplate? template = result.ResolveGuardianForceTemplates()[id].FirstOrDefault(entry => entry.Ability == ability);
        if (template is { NativeLearningSlots: not 0 })
        {
            int owner = Array.FindIndex(MenuDefinitions.Learning[id].ToArray(), rule => rule.Ability == ability);
            gf = gf with { ForgottenAbilities = gf.ForgottenAbilities | (1u << owner) };
            result.GuardianForces[id] = gf;
        }
        bool forgotten = template is { NativeLearningSlots: not 0 } ? result.NativeGuardianForceForgotten(id, ability) : true;
        result.GuardianForces[id] = gf with { Progression = gf.Progression! with { Abilities = gf.Progression.Abilities.Select(entry => entry.Ability == ability ? entry with { Learned = false, Forgotten = forgotten } : entry).ToImmutableArray() } };
        result.GuardianForces[id] = result.SynchronizeGuardianForce(id);
        for (int row = 0; row < 8; row++)
        {
            bool retained = result.JunctionLearnedAbilities(row).Contains(ability);
            NativeCharacterVital character = result.Records[row];
            NativeCharacterJunction junction = character.Junction;
            byte[] magic = junction.MagicJunctions.ToArray();
            for (int slot = 0; slot < 19; slot++)
            {
                if (!result.JunctionMagicSlotAvailable(row, slot))
                {
                    magic[slot] = 0;
                }
            }
            uint commands = junction.Commands;
            uint passives = 0;
            int passiveSlot = 0;
            for (int slot = 0; slot < 4; slot++)
            {
                byte command = (byte)(commands >> (8 * slot));
                if (command == ability && !retained)
                {
                    commands &= ~(255u << (8 * slot));
                }
                byte passive = (byte)(junction.Abilities >> (8 * slot));
                if (passive != 0 && (passive != ability || retained) && passiveSlot < result.JunctionAbilitySlots(row))
                {
                    passives |= (uint)passive << (8 * passiveSlot++);
                }
            }
            character = character with { Junction = junction with { Commands = commands, Abilities = passives, MagicJunctions = magic } };
            result.Records[row] = character;
            result = result.RebuildMagicMenuRow(row);
        }
        return result.RebuildParty();
    }
}
