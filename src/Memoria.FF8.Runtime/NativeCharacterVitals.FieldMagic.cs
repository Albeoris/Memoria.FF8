using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeCharacterVitals
{
    /// <summary>Native4F02F0 case22 and4F66B0/4F6770: successful field effects consume one saved stock, without battle RNG.</summary>
    public NativeCharacterVitals CastFieldMagic(int sourceRow, int magicId, int targetRow, BattleNativePlayerStatRules sourceRules, out bool used)
    {
        Validate();
        RequireFieldMagicRow(sourceRow);
        RequireFieldMagicRow(targetRow);
        sourceRules.Validate();
        ArgumentOutOfRangeException.ThrowIfLessThan(magicId, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(magicId, 56);
        NativeCharacterVital source = Records[sourceRow];
        NativeCharacterVital target = Records[targetRow];
        if (sourceRules.CharacterId != source.CharacterId || sourceRules.Level != NativeStatDefinitions.Level(source.Experience, Definitions.Characters[source.CharacterId]) || sourceRules.Abilities != source.Junction.Abilities || !sourceRules.MagicJunctions.AsSpan().SequenceEqual(source.Junction.MagicJunctions))
        {
            throw new InvalidDataException("Field magic requires the current caster's native statistic rules.");
        }
        int slot = Array.FindIndex(source.MagicInventory, stock => stock.SpellId == magicId);
        used = false;
        if (slot < 0 || source.MagicInventory[slot].Count == 0)
        {
            return this;
        }
        if (target.HitPoints.Minimum != target.HitPoints.Maximum)
        {
            throw new NotSupportedException("Field magic requires an exact saved target HP WORD.");
        }
        ImmutableArray<byte> spell = sourceRules.MagicRecords[magicId];
        int type = spell[7];
        int power = spell[8];
        int cure = spell[20];
        int before = target.HitPoints.Minimum;
        int maximum = Definitions.CharacterMaximum(target);
        int hp = before;
        int status = target.Status;
        switch (type)
        {
            case 3:
                BattleNativeCharacterGrowth growth = new(source.HitPointBonus, source.StatBonuses.ToImmutableArray());
                int magic = sourceRules.Project(source.MagicInventory, growth).Magic;
                int amount = ((magic + power) / 2) * power;
                if ((status & 1) != 0 || (status & 0x40) != 0 && (cure & 0x40) == 0)
                {
                    amount = 0;
                }
                hp = Math.Clamp(hp + amount, 0, maximum);
                status = FieldMagicStatus(hp, maximum, status) & ~cure;
                break;
            case 5:
            case 6:
                if ((status & 1) == 0)
                {
                    return this;
                }
                hp = type == 5 ? maximum / 8 : maximum;
                status = FieldMagicStatus(hp, maximum, status);
                break;
            default:
                throw new NotSupportedException($"Native attack type {type} is not usable by the field Magic menu.");
        }
        if (hp == before && status == target.Status)
        {
            return this;
        }
        NativeCharacterVital[] records = Records.ToArray();
        records[targetRow] = target with { HitPoints = new((ushort)hp, (ushort)hp), Status = (ushort)status };
        NativeMagicStock[] stocks = records[sourceRow].MagicInventory.ToArray();
        byte remaining = (byte)(stocks[slot].Count - 1);
        stocks[slot] = new(remaining == 0 ? (byte)0 : (byte)magicId, remaining);
        records[sourceRow] = records[sourceRow] with { MagicInventory = stocks };
        used = true;
        NativeCharacterVitals result = this with { Records = records };
        return remaining == 0 ? result.RebuildMagicMenuRow(sourceRow) : result.RebuildMenuRow(sourceRow);
    }

    /// <summary>Field Magic stock movement preserves inventory slots, caps stocks at100 and clears depleted junction assignments through4C2FA0.</summary>
    public NativeCharacterVitals TransferMagic(int sourceRow, int magicId, int targetRow, int requested, out int moved)
    {
        Validate();
        RequireFieldMagicRow(sourceRow);
        RequireFieldMagicRow(targetRow);
        ArgumentOutOfRangeException.ThrowIfLessThan(magicId, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(magicId, 56);
        ArgumentOutOfRangeException.ThrowIfNegative(requested);
        moved = 0;
        if (sourceRow == targetRow || requested == 0)
        {
            return this;
        }
        NativeCharacterVital source = Records[sourceRow];
        NativeCharacterVital target = Records[targetRow];
        int sourceSlot = Array.FindIndex(source.MagicInventory, stock => stock.SpellId == magicId);
        int targetSlot = Array.FindIndex(target.MagicInventory, stock => stock.SpellId == magicId);
        if (targetSlot < 0)
        {
            targetSlot = Array.FindIndex(target.MagicInventory, stock => stock.SpellId == 0);
        }
        if (sourceSlot < 0 || targetSlot < 0)
        {
            return this;
        }
        int targetCount = target.MagicInventory[targetSlot].SpellId == 0 ? 0 : target.MagicInventory[targetSlot].Count;
        moved = Math.Min(requested, Math.Min(source.MagicInventory[sourceSlot].Count, Math.Max(0, 100 - targetCount)));
        if (moved == 0)
        {
            return this;
        }
        NativeMagicStock[] sourceStocks = source.MagicInventory.ToArray();
        NativeMagicStock[] targetStocks = target.MagicInventory.ToArray();
        int remaining = sourceStocks[sourceSlot].Count - moved;
        sourceStocks[sourceSlot] = new(remaining == 0 ? (byte)0 : (byte)magicId, (byte)remaining);
        targetStocks[targetSlot] = new((byte)magicId, (byte)(targetCount + moved));
        byte[] junctions = source.Junction.MagicJunctions.ToArray();
        if (remaining == 0)
        {
            for (int index = 0; index < 19; index++)
            {
                if (junctions[index] == magicId)
                {
                    junctions[index] = 0;
                }
            }
        }
        NativeCharacterVital[] records = Records.ToArray();
        records[sourceRow] = source with { MagicInventory = sourceStocks, Junction = source.Junction with { MagicJunctions = junctions } };
        records[targetRow] = target with { MagicInventory = targetStocks };
        return (this with { Records = records }).RebuildMenuRow(sourceRow).RebuildMenuRow(targetRow);
    }

    private void RequireFieldMagicRow(int row)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)row, (uint)Records.Length);
    }

    private static int FieldMagicStatus(int hp, int maximum, int status)
    {
        status &= 0xFCFE;
        if (hp < maximum >> 1)
        {
            status |= 0x200;
        }
        if (hp < maximum >> 2)
        {
            status |= 0x300;
        }
        return hp <= 0 ? 1 : status;
    }
}
