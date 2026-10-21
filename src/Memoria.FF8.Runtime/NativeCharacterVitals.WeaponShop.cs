namespace Memoria.FF8.Runtime;

public sealed partial record NativeCharacterVitals
{
    /// <summary>4EA534/4BFC90 temporarily equips a weapon and rebuilds one physical row, writes status, then restores combat references.</summary>
    public NativeCharacterVitals PreviewWeaponShopRow(int physicalRow, byte weapon)
    {
        Validate();
        if ((uint)physicalRow >= Records.Length)
        {
            throw new NotSupportedException("Native weapon preview requires a retained physical character row.");
        }
        byte priorWeapon = Records[physicalRow].Weapon;
        NativeCharacterVital[] records = Records.ToArray();
        records[physicalRow] = records[physicalRow] with { Weapon = weapon };
        NativeCharacterVitals restored = (this with { Records = records }).RebuildMenuRow(physicalRow);
        records = restored.Records.ToArray();
        records[physicalRow] = records[physicalRow] with { Weapon = priorWeapon };
        return (restored with { Records = records }).DeepCopy();
    }

    /// <summary>4BFCF0 and4BFC90 share4BFB40: rebuild the selected physical row, write status, restore party and rebuild it.</summary>
    public NativeCharacterVitals RebuildMenuRow(int physicalRow)
    {
        Validate();
        if ((uint)physicalRow >= Records.Length)
        {
            throw new NotSupportedException("Native menu rebuild requires a retained physical character row.");
        }
        byte[] priorParty = CombatParty.ToArray();
        NativeCharacterVitals temporary = (this with { CombatParty = [(byte)physicalRow, 255, 255] }).RebuildParty();
        NativeCharacterVital row = temporary.Records[physicalRow];
        ushort maximum = temporary.GetMagicMaximumHitPoints(physicalRow);
        NativeCharacterVital[] records = temporary.Records.ToArray();
        records[physicalRow] = row with { Status = MenuRowStatus(row.Status, row.HitPoints, maximum) };
        return (temporary with { Records = records, CombatParty = priorParty }).RebuildParty();
    }

    /// <summary>4BE790 normalizes all32 stocks and clears all20 junction bytes whose spell is absent, before4BFCF0.</summary>
    public NativeCharacterVitals RebuildMagicMenuRow(int physicalRow)
    {
        Validate();
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)physicalRow, (uint)Records.Length);
        NativeCharacterVital[] records = Records.ToArray();
        NativeCharacterVital row = records[physicalRow];
        records[physicalRow] = NormalizeLogicalMagicInventory(physicalRow, row);
        return (this with { Records = records }).RebuildMenuRow(physicalRow);
    }

    /// <summary>4BE790 canonicalizes zero stocks and missing junctions without a stat rebuild.</summary>
    public static NativeCharacterVital NormalizeMagicInventory(NativeCharacterVital row)
    {
        NativeMagicStock[] magic = row.MagicInventory.Select(stock => stock.SpellId == 0 || stock.Count == 0 ? new NativeMagicStock(0, 0) : stock).ToArray();
        HashSet<byte> present = magic.Where(stock => stock.SpellId != 0).Select(stock => stock.SpellId).ToHashSet();
        byte[] junctions = row.Junction.MagicJunctions.Select(spell => present.Contains(spell) ? spell : (byte)0).ToArray();
        return row with { MagicInventory = magic, Junction = row.Junction with { MagicJunctions = junctions } };
    }

    /// <summary>Refinement constructor4D7180 invokes4BE870, which calls4BE790 on all eight physical rows.</summary>
    public NativeCharacterVitals NormalizeMagicInventories()
    {
        Validate();
        return this with { Records = Records.Select((row, index) => NormalizeLogicalMagicInventory(index, row)).ToArray() };
    }

    private NativeCharacterVital NormalizeLogicalMagicInventory(int physicalRow, NativeCharacterVital row)
    {
        if (!MagicSystem.RequiresMemoriaFormat)
        {
            return NormalizeMagicInventory(row);
        }
        NativeMagicStock[] magic = row.MagicInventory.Select(stock => stock.SpellId == 0 || stock.Count == 0 ? new NativeMagicStock(0, 0) : stock).ToArray();
        byte[] junctions = row.Junction.MagicJunctions.ToArray();
        for (int slot = 0; slot < 19; slot++)
        {
            if (junctions[slot] != 0 && MagicJunctionPolicy.GetEffectiveCount(this, physicalRow, junctions[slot]) == 0)
            {
                junctions[slot] = 0;
            }
        }
        return row with { MagicInventory = magic, Junction = row.Junction with { MagicJunctions = junctions } };
    }

    private static ushort MenuRowStatus(ushort prior, NativeHpWord hitPoints, ushort maximum)
    {
        int signedMaximum = unchecked((short)maximum);
        ushort Evaluate(ushort word)
        {
            int current = unchecked((short)word);
            if (current <= 0)
            {
                return 1;
            }
            ushort result = (ushort)(prior & 0xFCFE);
            if (current < (signedMaximum >> 1))
            {
                result |= 0x200;
            }
            if (current < (signedMaximum >> 2))
            {
                result |= 0x300;
            }
            return result;
        }
        ushort result = Evaluate(hitPoints.Minimum);
        int[] boundaries = [hitPoints.Maximum, 0, 1, 32767, 32768, signedMaximum >> 1, (signedMaximum >> 1) - 1, signedMaximum >> 2, (signedMaximum >> 2) - 1];
        foreach (int boundary in boundaries)
        {
            ushort word = unchecked((ushort)boundary);
            if (word >= hitPoints.Minimum && word <= hitPoints.Maximum && Evaluate(word) != result)
            {
                throw new NotSupportedException("Native menu row has an unresolved HP interval spanning distinct saved status writes.");
            }
        }
        return result;
    }
}
