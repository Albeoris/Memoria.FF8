namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePartyReplacementSelectionTests
{
    [Fact]
    public void Native_replacement_uses_unsigned_experience_and_the_first_saved_row_on_ties()
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Create();
        vitals.Records[3] = vitals.Records[3] with { Experience = 0x80000000u };
        vitals.Records[4] = vitals.Records[4] with { Experience = 20 };
        vitals.Records[5] = vitals.Records[5] with { Experience = 21 };
        vitals.Records[6] = vitals.Records[6] with { Experience = 20 };
        vitals.Records[7] = vitals.Records[7] with { Experience = uint.MaxValue };

        Assert.Equal((byte?)4, NativeBattlePartyReplacementSelection.Select(vitals, [2, 0, 1]));
        Assert.Equal((byte?)7, NativeBattlePartyReplacementSelection.Select(vitals, [0, 1, 2, 3, 4, 5, 6]));
    }

    [Fact]
    public void Native_replacement_reads_saved_KO_and_membership_bits_independently_of_HP()
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Create();
        vitals.Records[3] = vitals.Records[3] with { Status = 1, HitPoints = new(10, 10), Experience = 0 };
        vitals.Records[4] = vitals.Records[4] with { MemberFlags = 2, Experience = 0 };
        vitals.Records[5] = vitals.Records[5] with { MemberFlags = 3, Status = 2, HitPoints = new(0, 0), Experience = 10 };
        vitals.Records[6] = vitals.Records[6] with { Experience = 20 };
        vitals.Records[7] = vitals.Records[7] with { Experience = 30 };

        Assert.Equal((byte?)5, NativeBattlePartyReplacementSelection.Select(vitals, [0, 1, 2]));
    }

    [Fact]
    public void Every_occupied_row_including_the_retained_eject_owner_is_excluded()
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Create();
        vitals.Records[6] = vitals.Records[6] with { Experience = 10 };
        vitals.Records[7] = vitals.Records[7] with { Experience = 20 };

        Assert.Equal((byte?)6, NativeBattlePartyReplacementSelection.Select(vitals, [4, 0, 1, 5, 3, 2]));
        Assert.Equal((byte?)3, NativeBattlePartyReplacementSelection.Select(vitals, [4, 0, 1, 5, 2]));
    }

    [Fact]
    public void No_eligible_reserve_returns_no_row()
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Create();
        vitals.Records[6] = vitals.Records[6] with { Status = 1 };
        vitals.Records[7] = vitals.Records[7] with { MemberFlags = 0 };

        Assert.Null(NativeBattlePartyReplacementSelection.Select(vitals, [0, 1, 2, 3, 4, 5]));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(8)]
    public void Duplicate_or_out_of_range_occupied_rows_are_corrupt_ownership(int row)
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Create();

        Assert.Throws<InvalidDataException>(() => NativeBattlePartyReplacementSelection.Select(vitals, [0, row]));
    }

    [Theory]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    public void Guest_admission_requires_the_explicit_extended_roster_outside_dream(int guest)
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Create().WithGuestBattleRecords();
        for (int row = 0; row < vitals.Records.Length; row++)
        {
            vitals.Records[row] = vitals.Records[row] with { Experience = 100 };
        }
        vitals.Records[guest] = vitals.Records[guest] with { MemberFlags = 0, Experience = 0 };
        NativeCharacterVitals optedIn = vitals with { ExtendedRosterEnabled = true };

        Assert.Equal((byte?)3, NativeBattlePartyReplacementSelection.Select(vitals, [0, 1, 2]));
        Assert.Equal((byte?)guest, NativeBattlePartyReplacementSelection.Select(optedIn, [0, 1, 2]));
        Assert.Equal((byte?)3, NativeBattlePartyReplacementSelection.Select(optedIn with { DreamEnabled = true }, [0, 1, 2]));

        vitals.Records[guest] = vitals.Records[guest] with { MemberFlags = 1 };
        byte? original = guest < NativeCharacterVitals.NativeRecordCount ? (byte)guest : (byte)3;
        Assert.Equal(original, NativeBattlePartyReplacementSelection.Select(vitals, [0, 1, 2]));
        Assert.Equal(original, NativeBattlePartyReplacementSelection.Select(optedIn with { DreamEnabled = true }, [0, 1, 2]));
    }
}
