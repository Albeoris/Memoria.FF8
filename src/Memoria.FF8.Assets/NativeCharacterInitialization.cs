using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeCharacterInitialization
{
    public static NativeCharacterVitals Read(byte[] initial, byte[] kernel)
    {
        NativeInitialData.Validate(initial);
        if (kernel.Length < 228 || BinaryPrimitives.ReadUInt32LittleEndian(kernel) != 56)
        {
            throw new InvalidDataException("Original New Game template/kernel structure is unsupported.");
        }
        ReadOnlySpan<byte> Section(int index, int recordSize, int count)
        {
            int start = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(kernel.AsSpan(4 + 4 * index)));
            int end = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(kernel.AsSpan(8 + 4 * index)));
            if (start < 228 || end > kernel.Length || end < start || end - start != recordSize * count)
            {
                throw new InvalidDataException($"Kernel saved-stat section{index} is invalid.");
            }
            return kernel.AsSpan(start, end - start);
        }
        NativeStatCurve[] characters = new NativeStatCurve[11];
        ReadOnlySpan<byte> characterSection = Section(6, 36, characters.Length);
        for (int id = 0; id < characters.Length; id++)
        {
            ReadOnlySpan<byte> row = characterSection.Slice(id * 36, 36);
            characters[id] = new NativeStatCurve(row[6], row[7], row[8], row[9], row[10]);
        }
        byte[] magicHitPoints = new byte[57];
        ReadOnlySpan<byte> magicSection = Section(1, 60, magicHitPoints.Length);
        for (int id = 0; id < magicHitPoints.Length; id++)
        {
            magicHitPoints[id] = magicSection[id * 60 + 23];
        }
        NativeStatModifier[] passives = new NativeStatModifier[19];
        ReadOnlySpan<byte> passiveSection = Section(13, 8, passives.Length);
        for (int id = 0; id < passives.Length; id++)
        {
            ReadOnlySpan<byte> row = passiveSection.Slice(id * 8, 8);
            passives[id] = new NativeStatModifier(row[4], row[5], row[6]);
        }
        NativeStatCurve[] guardianForces = new NativeStatCurve[16];
        ImmutableArray<NativeGuardianForceLearningRule>[] learning = new ImmutableArray<NativeGuardianForceLearningRule>[16];
        ReadOnlySpan<byte> guardianForceSection = Section(2, 132, guardianForces.Length);
        for (int id = 0; id < guardianForces.Length; id++)
        {
            ReadOnlySpan<byte> row = guardianForceSection.Slice(id * 132, 132);
            guardianForces[id] = new NativeStatCurve(row[24], row[25], row[20], row[21], row[22]);
            NativeGuardianForceLearningRule[] rules = new NativeGuardianForceLearningRule[21];
            for (int index = 0; index < rules.Length; index++)
            {
                rules[index] = new NativeGuardianForceLearningRule(row[28 + index * 4], row[29 + index * 4], row[30 + index * 4]);
            }
            learning[id] = rules.ToImmutableArray();
        }
        NativeStatModifier[] guardianForceModifiers = new NativeStatModifier[9];
        ReadOnlySpan<byte> guardianForceModifierSection = Section(16, 8, guardianForceModifiers.Length);
        for (int id = 0; id < guardianForceModifiers.Length; id++)
        {
            ReadOnlySpan<byte> row = guardianForceModifierSection.Slice(id * 8, 8);
            guardianForceModifiers[id] = new NativeStatModifier(row[5], row[6], row[7]);
        }
        NativeStatDefinitions definitions = new(characters.ToImmutableArray(), magicHitPoints.ToImmutableArray(), passives.ToImmutableArray(), guardianForces.ToImmutableArray(), guardianForceModifiers.ToImmutableArray());
        List<byte> abilityPoints = [];
        int[] abilityCounts = [20, 19, 19, 20, 5, 9, 24];
        byte[] commandMappings = new byte[19];
        byte[] partyAbilityFlags = new byte[5];
        for (int section = 11; section <= 17; section++)
        {
            ReadOnlySpan<byte> table = Section(section, 8, abilityCounts[section - 11]);
            for (int id = 0; id < abilityCounts[section - 11]; id++)
            {
                abilityPoints.Add(table[8 * id + 4]);
                if (section == 12)
                {
                    commandMappings[id] = table[8 * id + 5];
                }
                if (section == 15)
                {
                    partyAbilityFlags[id] = table[8 * id + 5];
                }
            }
        }
        NativeGuardianForceMenuDefinitions menuDefinitions = new(learning.ToImmutableArray(), abilityPoints.ToImmutableArray(), commandMappings.ToImmutableArray(), partyAbilityFlags.ToImmutableArray());
        NativeCharacterVital[] records = new NativeCharacterVital[8];
        for (int index = 0; index < records.Length; index++)
        {
            ReadOnlySpan<byte> row = initial.AsSpan(1088 + index * 152, 152);
            byte character = row[8];
            if (character != index)
            {
                throw new NotSupportedException("New Game character identities differ from their native saved row initialization.");
            }
            NativeMagicStock[] inventory = new NativeMagicStock[32];
            for (int slot = 0; slot < inventory.Length; slot++)
            {
                inventory[slot] = new NativeMagicStock(row[16 + slot * 2], row[17 + slot * 2]);
            }
            ushort hp = BinaryPrimitives.ReadUInt16LittleEndian(row);
            NativeCharacterJunction junction = new(BinaryPrimitives.ReadUInt16LittleEndian(row[88..]), BinaryPrimitives.ReadUInt32LittleEndian(row[80..]), BinaryPrimitives.ReadUInt32LittleEndian(row[84..]), row.Slice(92, 20).ToArray());
            records[index] = new NativeCharacterVital(character, new NativeHpWord(hp, hp), BinaryPrimitives.ReadUInt16LittleEndian(row[150..]), junction, BinaryPrimitives.ReadUInt32LittleEndian(row[4..]), BinaryPrimitives.ReadUInt16LittleEndian(row[2..]), row[9], row.Slice(10, 6).ToArray(), inventory, BinaryPrimitives.ReadUInt16LittleEndian(row[148..]), BinaryPrimitives.ReadUInt16LittleEndian(row[144..])) { NativeByte90 = row[90], DeathCount = BinaryPrimitives.ReadUInt16LittleEndian(row[146..]), GuardianForceCompatibility = Enumerable.Range(0, 16).Select(gf => BinaryPrimitives.ReadUInt16LittleEndian(initial.AsSpan(1088 + index * 152 + 112 + gf * 2, 2))).ToArray() };
        }
        NativeGuardianForceVital[] savedGuardianForces = new NativeGuardianForceVital[16];
        for (int id = 0; id < savedGuardianForces.Length; id++)
        {
            ReadOnlySpan<byte> row = initial.AsSpan(id * 68, 68);
            savedGuardianForces[id] = new NativeGuardianForceVital(BinaryPrimitives.ReadUInt32LittleEndian(row[12..]), row[17], BinaryPrimitives.ReadUInt16LittleEndian(row[18..]), new NativeGuardianForceLearnedBank(BinaryPrimitives.ReadUInt64LittleEndian(row[20..]), BinaryPrimitives.ReadUInt64LittleEndian(row[28..])), null, row.Slice(36, 21).ToArray().ToImmutableArray(), row[64], (uint)(row[65] | row[66] << 8 | row[67] << 16)) { NativeOpaqueBytes = new(row[16], row[57], row[58], row[59]), KillCount = BinaryPrimitives.ReadUInt16LittleEndian(row[60..]), DeathCount = BinaryPrimitives.ReadUInt16LittleEndian(row[62..]) };
        }
        NativeCharacterVitals state = new()
        {
            Version = 1,
            Rule = NativeCharacterVitals.InitializationRule,
            InitOutSha256 = Convert.ToHexString(SHA256.HashData(initial)),
            KernelSha256 = Convert.ToHexString(SHA256.HashData(kernel)),
            Records = records,
            CombatParty = initial.AsSpan(2724, 3).ToArray(),
            GuardianForces = savedGuardianForces,
            Definitions = definitions,
            MenuDefinitions = menuDefinitions,
            DreamWeapons = initial.AsSpan(2744, 3).ToArray(),
            // Executed52CA90(1) clears248 event-bank bytes, including188..193.
            JunctionPartyBackup = new byte[6],
            // Native56D9F0 clears the global junction flag; the template has no such global.
            DreamEnabled = false
        };
        // Capture template defaults while the only progression owner is the original init.out.
        ImmutableArray<ImmutableArray<GuardianForceAbilityTemplate>> templates = state.ResolveGuardianForceTemplates();
        state = state with { MenuDefinitions = menuDefinitions with { Templates = templates, SourceTemplates = templates, InitialStates = savedGuardianForces.Select(gf => new NativeGuardianForceInitialState(gf.LearnedBank, gf.AbilityProgress, gf.Learning, gf.ForgottenAbilities)).ToImmutableArray() } };
        state = state.RebuildParty();
        byte[] party = state.CombatParty.ToArray();
        for (int slot = 0; slot < party.Length; slot++)
        {
            if (party[slot] != 255 && (state.Records[party[slot]].MemberFlags & 1) == 0)
            {
                party[slot] = 255;
            }
        }
        if ((state.Records[0].MemberFlags & 1) == 0)
        {
            throw new NotSupportedException("Native New Game field-party Squall lacks its original membership flag.");
        }
        return state with { CombatParty = party };
    }
}
