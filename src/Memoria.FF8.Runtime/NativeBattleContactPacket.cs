using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleContactHalf(BattleModelOwner Recipient, byte Sequence, byte Flags2, byte HitFlags, ushort SavedStatuses4, ushort Amount6, uint BattleStatuses8)
{
    public BattleModelOwner? CoverSource { get; init; }
    public int? ExtendedDamageAmount { get; init; }
    public int PresentedAmount => ExtendedDamageAmount ?? Amount6;
    public ushort ContactFlags => (ushort)(Flags2 | HitFlags << 8);

    public static NativeBattleContactHalf FromScratch(BattleModelOwner recipient, byte sequence, byte flags2, byte hitFlags, ushort savedStatuses, int amountDword, uint battleStatuses, BattleModelOwner? coverSource = null)
    {
        return new(recipient, sequence, flags2, hitFlags, savedStatuses, unchecked((ushort)amountDword), battleStatuses) { CoverSource = coverSource };
    }

    internal void Validate()
    {
        if (Recipient is null || (HitFlags & 0xC0) != 0 || ((HitFlags & 8) != 0) != (CoverSource is not null))
        {
            throw new InvalidDataException("A contact requires a recipient, low-six hit flags, and a cover source matching its cover flag.");
        }
        ValidateExtendedDamage(ExtendedDamageAmount, HitFlags, Amount6);
    }

    internal static void ValidateExtendedDamage(int? extendedDamage, byte hitFlags, ushort amount6)
    {
        if (extendedDamage is int amount && (amount is <= short.MaxValue or > 100000 || (hitFlags & 1) != 0 || amount6 != unchecked((ushort)amount)))
        {
            throw new InvalidDataException("Extended contact damage must preserve its native low WORD and positive damage identity.");
        }
    }
}

public sealed record NativeBattleContactPacket(NativeBattleContactHalf Primary, NativeBattleContactHalf? Secondary)
{
    [JsonIgnore]
    public BattleContactOccurrence? Occurrence { get; init; }

    public bool Equals(NativeBattleContactPacket? other)
    {
        return other is not null && Primary == other.Primary && Secondary == other.Secondary;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Primary, Secondary);
    }

    public void Validate()
    {
        if (Primary is null)
        {
            throw new InvalidDataException("A contact packet requires a primary recipient.");
        }
        Primary.Validate();
        Secondary?.Validate();
    }
}
