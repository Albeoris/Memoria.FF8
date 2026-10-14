namespace Memoria.FF8.Runtime;

public sealed partial record NativeCharacterVitals
{
    /// <summary>Native4ACAE0(0) resets the eight identities and clears dream mode;495EF0 then rebuilds the current combat party.</summary>
    public NativeCharacterVitals EnterNormalPartyMenu()
    {
        Validate();
        NativeCharacterVital[] records = Records.Select((record, index) => record with { CharacterId = (byte)index }).ToArray();
        return (this with { Records = records, DreamEnabled = false }).RebuildParty();
    }

    public byte[] PreparePartyMenuFieldSlots(IReadOnlyList<int> currentFieldParty, IReadOnlyList<byte> selectedCombatParty)
    {
        Validate();
        if (DreamEnabled || currentFieldParty.Count != 3 || currentFieldParty.Any(value => value != 255 && (uint)value >= 8) || selectedCombatParty.Count != 3 || selectedCombatParty.Any(value => value != 255 && value >= 8) || selectedCombatParty.Where(value => value != 255).Distinct().Count() != selectedCombatParty.Count(value => value != 255))
        {
            throw new NotSupportedException("Party menu confirmation needs three distinct native normal-party slots.");
        }
        byte[] result = currentFieldParty.Select(value => value != 255 && (Records[value].MemberFlags & 5) == 5 ? (byte)value : (byte)255).ToArray();
        Queue<byte> nonHeld = new(selectedCombatParty.Where(value => value != 255 && (Records[value].MemberFlags & 5) != 5));
        for (int index = 0; index < result.Length && nonHeld.Count > 0; index++)
        {
            if (result[index] == 255)
            {
                result[index] = nonHeld.Dequeue();
            }
        }
        if (nonHeld.Count != 0 || result.Where(value => value != 255).Distinct().Count() != result.Count(value => value != 255))
        {
            throw new NotSupportedException("Native forced-field members leave no distinct slot for the selected combat party.");
        }
        return result;
    }
}
