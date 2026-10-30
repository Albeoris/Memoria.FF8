using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeMovementProgressResult(NativeEventMemory Memory, NativeCharacterVitals? Vitals, NativeLimitProgress? Limits, bool LearnedAngelo);

public static class NativeMovementProgress
{
    public static NativeMovementProgressResult Sample(NativeEventMemory memory, NativeCharacterVitals? vitals, NativeLimitProgress? limits, IReadOnlyList<byte>? fieldParty, NativeSavedBattleFlags? savedFlags, uint units)
    {
        uint Read(int address) => unchecked((uint)memory.Read(address, 4).RequireExact());
        NativeEventMemory Write(NativeEventMemory current, int address, uint before, uint after)
        {
            return before == after ? current : current.Write(address, 4, NativeScalarValue.Exact(unchecked((int)after)));
        }
        uint beforeTotal = Read(4);
        uint beforeRecovery = Read(12);
        uint beforeAngelo = Read(244);
        uint recovery = unchecked(beforeRecovery + units);
        uint angelo = unchecked(beforeAngelo + units);
        NativeEventMemory nextMemory = Write(memory, 4, beforeTotal, unchecked(beforeTotal + units));
        if (recovery >= 8)
        {
            recovery = 0;
            vitals = Recover(vitals ?? throw new NotSupportedException("Native movement HP recovery requires current saved characters and GFs."));
        }
        nextMemory = Write(nextMemory, 12, beforeRecovery, recovery);
        bool learned = false;
        if (angelo >= 0x250)
        {
            bool dream = vitals?.DreamEnabled ?? throw new NotSupportedException("Angelo movement training requires the current dream flag.");
            if (!dream)
            {
                savedFlags?.Validate();
                if (savedFlags is null || (savedFlags.EffectiveKnownMask & 0x10) == 0)
                {
                    throw new NotSupportedException("Angelo movement training requires the saved disabled flag.");
                }
                if ((savedFlags.Value & 0x10) == 0)
                {
                    angelo = 0;
                    if (fieldParty is null)
                    {
                        throw new NotSupportedException("Angelo movement training requires the saved field party identities.");
                    }
                    if (fieldParty.Contains((byte)4))
                    {
                        NativeLimitProgress current = limits ?? throw new NotSupportedException("Angelo movement training requires its saved learning progress.");
                        current.Validate();
                        int selected = current.TrainingSelection;
                        if (selected >= 8)
                        {
                            throw new NotSupportedException("Angelo movement training addresses outside its eight native counters.");
                        }
                        byte remaining = current.TrainingCounters[selected];
                        if (remaining != 0)
                        {
                            limits = current with { TrainingCounters = current.TrainingCounters.SetItem(selected, (byte)(remaining - 1)) };
                        }
                        else if ((current.AngeloFlags & (1 << selected)) == 0)
                        {
                            limits = current with { AngeloFlags = (byte)(current.AngeloFlags | (1 << selected)) };
                            learned = true;
                        }
                    }
                }
            }
        }
        nextMemory = Write(nextMemory, 244, beforeAngelo, angelo);
        return new(nextMemory, vitals, limits, learned);
    }

    private static NativeCharacterVitals Recover(NativeCharacterVitals vitals)
    {
        vitals.Validate();
        NativeGuardianForceVital[] guardianForces = vitals.GuardianForces.ToArray();
        bool changed = false;
        for (int id = 0; id < guardianForces.Length; id++)
        {
            NativeGuardianForceVital gf = guardianForces[id];
            if ((gf.Flags & 1) == 0 || gf.HitPoints == 0)
            {
                continue;
            }
            int maximum = unchecked((short)(gf.MaximumHitPoints ?? vitals.Definitions.GuardianForceMaximum(id, gf)));
            if (gf.HitPoints < maximum)
            {
                guardianForces[id] = gf with { HitPoints = (ushort)(gf.HitPoints + 1) };
                changed = true;
            }
        }
        NativeCharacterVital[] records = vitals.Records.ToArray();
        foreach (byte row in vitals.CombatParty)
        {
            if (row == 255)
            {
                continue;
            }
            NativeCharacterVital character = records[row];
            int maximum = unchecked((short)vitals.Definitions.CharacterMaximum(character));
            if (character.HitPoints.Maximum == 0 || character.HitPoints.Minimum >= maximum || (vitals.Definitions.EquippedStatusPassiveFlags(character.Junction.Abilities) & 0x20000) == 0)
            {
                continue;
            }
            ushort Restore(ushort hp) => hp != 0 && hp < maximum ? (ushort)(hp + 1) : hp;
            NativeHpWord recovered = new(Restore(character.HitPoints.Minimum), Restore(character.HitPoints.Maximum));
            records[row] = character with { HitPoints = recovered };
            changed |= recovered != character.HitPoints;
        }
        return changed ? vitals with { Records = records, GuardianForces = guardianForces } : vitals;
    }
}
