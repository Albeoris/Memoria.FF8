using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class BattleAssets
{
    public BattleNativePlayerStatRules ReadPlayerNativeStatRules(NativeCharacterVitals vitals, int row)
    {
        return playerStats.ReadRules(vitals, row);
    }

    public BattleNativeAction ReadPlayerNativeAttackData(NativeCharacterVitals vitals, int row)
    {
        vitals.Validate();
        if ((uint)row >= vitals.Records.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(row));
        }
        NativeCharacterVital character = vitals.Records[row];
        int weaponId = character.CharacterId >= 8 ? vitals.DreamWeapons[character.CharacterId - 8] : character.Weapon;
        ImmutableArray<byte> weapon = NativeKernelRecords.Single(record => record.Section == 4 && record.Index == weaponId).Bytes;
        int spellId = character.Junction.MagicJunctions[10];
        ImmutableArray<byte> spell = NativeKernelRecords.Single(record => record.Section == 1 && record.Index == spellId).Bytes;
        int quantity = spellId == 0 ? 0 : character.MagicInventory.FirstOrDefault(stock => stock.SpellId == spellId)?.Count ?? 0;
        ushort junctionStatuses = BinaryPrimitives.ReadUInt16LittleEndian(spell.AsSpan().Slice(38));
        //496AC0/496AF0 split the junction mask into saved status bits0..6 and Sleep/Slow/Stop battle bits.
        uint battleStatuses = (junctionStatuses & 0x80) != 0 ? 1u : 0;
        if ((junctionStatuses & 0x100) != 0)
        {
            battleStatuses |= 4;
        }
        if ((junctionStatuses & 0x200) != 0)
        {
            battleStatuses |= 8;
        }
        BattlePlayerStats stats = ReadPlayerStats(vitals, row);
        return new BattleNativeAction(1, 0, 4, weaponId)
        {
            Name = ReadNativeCommand(1).Name,
            DispatchCommand = 1,
            CharacterId = character.CharacterId,
            SupplementalKernelIndex = spellId,
            MagicId = 0,
            AttackType = weapon[5],
            Power = weapon[6],
            HitCount = 1,
            TargetFlags = ReadNativeCommand(1).TargetFlags,
            AttackFlags = 0,
            DamagePresentation = 4,
            Element = checked((byte)stats.ElementAttack),
            ElementPercent = checked((byte)stats.ElementAttackPercent),
            StatusAttack = checked((byte)(100 + spell[36] * quantity / 100)),
            Statuses0 = (ushort)(junctionStatuses & 0x7F),
            Statuses1 = battleStatuses,
            Hit = checked((byte)stats.Hit),
            Critical = weapon[10],
            PowerModifier = null,
            LevelModifier = null,
            RawRecord = weapon,
            SupplementalRecord = spell
        };
    }
}
