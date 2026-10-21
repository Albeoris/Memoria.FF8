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
        MagicCastQuote quote = MagicCastingService.Quote(this, sourceRow, magicId, MagicCastContext.Field);
        used = false;
        if (!quote.Allowed)
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
        int maximum = GetMagicMaximumHitPoints(targetRow);
        int hp = before;
        int status = target.Status;
        switch (type)
        {
            case 3:
                BattleNativeCharacterGrowth growth = new(source.HitPointBonus, source.StatBonuses.ToImmutableArray());
                int magic = sourceRules.Project(GetMagicInventory(sourceRow), growth).Magic;
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
        string actionId = Guid.NewGuid().ToString("N");
        NativeCharacterVitals reserved = MagicCastingService.Reserve(this, sourceRow, magicId, MagicCastContext.Field, actionId);
        NativeCharacterVitals result = reserved with { Records = records };
        result = MagicCastingService.Commit(result, actionId);
        used = true;
        return result.RebuildMagicMenuRow(sourceRow);
    }

    /// <summary>Transfers use the logical inventory transaction and carry persistent field history.</summary>
    public NativeCharacterVitals TransferMagic(int sourceRow, int magicId, int targetRow, int requested, out int moved)
    {
        Validate();
        RequireFieldMagicRow(sourceRow);
        RequireFieldMagicRow(targetRow);
        ArgumentOutOfRangeException.ThrowIfLessThan(magicId, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(magicId, 56);
        ArgumentOutOfRangeException.ThrowIfNegative(requested);
        if (!MagicSystem.SharedMagicStock && (sourceRow == targetRow || requested == 0))
        {
            moved = 0;
            return this;
        }
        NativeCharacterVitals result = TransferMagicStock(sourceRow, targetRow, magicId, requested, out MagicStockMutation mutation);
        moved = mutation.Accepted;
        return moved == 0 ? this : result.RebuildMenuRow(sourceRow).RebuildMenuRow(targetRow);
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
