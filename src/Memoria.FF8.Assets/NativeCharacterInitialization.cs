using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeCharacterInitialization
{
    public static NativeCharacterVitals Read(byte[] initial, byte[] kernel)
    {
        if (initial.Length != 2812 || kernel.Length < 228 || BinaryPrimitives.ReadUInt32LittleEndian(kernel) != 56)
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
        ReadOnlySpan<byte> guardianForceSection = Section(2, 132, guardianForces.Length);
        for (int id = 0; id < guardianForces.Length; id++)
        {
            ReadOnlySpan<byte> row = guardianForceSection.Slice(id * 132, 132);
            guardianForces[id] = new NativeStatCurve(row[24], row[25], row[20], row[21], row[22]);
        }
        NativeStatModifier[] guardianForceModifiers = new NativeStatModifier[9];
        ReadOnlySpan<byte> guardianForceModifierSection = Section(16, 8, guardianForceModifiers.Length);
        for (int id = 0; id < guardianForceModifiers.Length; id++)
        {
            ReadOnlySpan<byte> row = guardianForceModifierSection.Slice(id * 8, 8);
            guardianForceModifiers[id] = new NativeStatModifier(row[5], row[6], row[7]);
        }
        NativeStatDefinitions definitions = new(characters.ToImmutableArray(), magicHitPoints.ToImmutableArray(), passives.ToImmutableArray(), guardianForces.ToImmutableArray(), guardianForceModifiers.ToImmutableArray());
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
            records[index] = new NativeCharacterVital(character, new NativeHpWord(hp, hp), BinaryPrimitives.ReadUInt16LittleEndian(row[150..]), junction, BinaryPrimitives.ReadUInt32LittleEndian(row[4..]), BinaryPrimitives.ReadUInt16LittleEndian(row[2..]), row[9], row.Slice(10, 6).ToArray(), inventory, BinaryPrimitives.ReadUInt16LittleEndian(row[148..]));
        }
        NativeGuardianForceVital[] savedGuardianForces = new NativeGuardianForceVital[16];
        for (int id = 0; id < savedGuardianForces.Length; id++)
        {
            ReadOnlySpan<byte> row = initial.AsSpan(id * 68, 68);
            savedGuardianForces[id] = new NativeGuardianForceVital(BinaryPrimitives.ReadUInt32LittleEndian(row[12..]), row[17], BinaryPrimitives.ReadUInt16LittleEndian(row[18..]), BinaryPrimitives.ReadUInt64LittleEndian(row[28..]), null);
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
            DreamWeapons = initial.AsSpan(2744, 3).ToArray(),
            // Native56D9F0 clears the global junction flag; the template has no such global.
            DreamEnabled = false
        };
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
