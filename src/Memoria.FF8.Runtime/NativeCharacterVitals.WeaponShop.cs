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
        byte[] priorParty = CombatParty.ToArray();
        NativeCharacterVital[] records = Records.ToArray();
        records[physicalRow] = records[physicalRow] with { Weapon = weapon };
        NativeCharacterVitals temporary = (this with { Records = records, CombatParty = [(byte)physicalRow, 255, 255] }).RebuildParty();
        NativeCharacterVital row = temporary.Records[physicalRow];
        ushort maximum = temporary.Definitions.CharacterMaximum(row);
        ushort status = WeaponPreviewStatus(row.Status, row.HitPoints, maximum);
        records = temporary.Records.ToArray();
        records[physicalRow] = row with { Status = status };
        //4BFC90 restores the combat references and rebuilds while the temporary
        //weapon is still equipped. Its caller restores the weapon afterward.
        NativeCharacterVitals restored = (temporary with { Records = records, CombatParty = priorParty }).RebuildParty();
        records = restored.Records.ToArray();
        records[physicalRow] = records[physicalRow] with { Weapon = priorWeapon };
        return (restored with { Records = records }).DeepCopy();
    }

    private static ushort WeaponPreviewStatus(ushort prior, NativeHpWord hitPoints, ushort maximum)
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
                throw new NotSupportedException("Native weapon preview has an unresolved HP interval spanning distinct saved status writes.");
            }
        }
        return result;
    }
}
