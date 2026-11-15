using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class SelectedBattlePartyTests
{
    [Fact]
    public void All_explicit_capacities_are_accepted_and_do_not_recruit_unselected_members()
    {
        NativeCharacterVitals source = MagicSystemFixture.Create().WithExtendedRoster(true);
        new GameplayPreferences().Validate();
        for (int capacity = 3; capacity <= NativeCharacterVitals.ExtendedRecordCount; capacity++)
        {
            new GameplayPreferences { BattleAllyCount = capacity }.Validate();
            Assert.Equal(source.CombatParty, source.GetBattlePartyRows(capacity));
            byte[] selection = Enumerable.Range(0, capacity).Select(row => (byte)row).Reverse().ToArray();
            Assert.Equal(selection, source.WithBattleParty(selection, capacity).GetBattlePartyRows(capacity));
        }
        foreach (int capacity in new[] { -1, 1, 2, NativeCharacterVitals.ExtendedRecordCount + 1 })
        {
            Assert.Throws<InvalidDataException>(() => new GameplayPreferences { BattleAllyCount = capacity }.Validate());
            Assert.Throws<ArgumentOutOfRangeException>(() => source.GetBattlePartyRows(capacity));
            Assert.Throws<ArgumentOutOfRangeException>(() => source.WithBattleParty([0], capacity));
        }
    }

    [Fact]
    public void Selection_round_trips_in_order_with_KO_and_keeps_three_native_slots()
    {
        NativeCharacterVitals installed = MagicSystemFixture.Create();
        NativeCharacterVitals source = installed.DeepCopy();
        source.Records[4] = source.Records[4] with { HitPoints = new(0, 0), Status = 1 };

        NativeCharacterVitals selected = source.WithBattleParty([2, 0, 1, 4, 3, 5], 6);
        GameplaySaveCharacters captured = GameplaySaveCharacters.Capture(selected);
        GameplaySaveCharacters decoded = JsonSerializer.Deserialize<GameplaySaveCharacters>(JsonSerializer.Serialize(captured))!;
        NativeCharacterVitals restored = decoded.Restore(installed);
        NativeCharacterVitals copy = restored.DeepCopy();

        Assert.Equal(new byte[] { 2, 0, 1 }, selected.CombatParty);
        Assert.Equal(new byte[] { 4, 3, 5 }, restored.AdditionalCombatParty);
        Assert.Equal(new byte[] { 2, 0, 1, 4, 3, 5 }, restored.GetBattlePartyRows(11));
        Assert.Equal(new byte[] { 2, 0, 1, 4 }, restored.GetBattlePartyRows(4));
        Assert.Equal(new byte[] { 2, 0, 1 }, restored.GetBattlePartyRows(11, allowAdditionalMembers: false));
        Assert.True(restored.Records[4].HitPoints.IsZero);
        Assert.NotSame(restored.AdditionalCombatParty, copy.AdditionalCombatParty);
        Assert.Equal(restored.AdditionalCombatParty, copy.AdditionalCombatParty);
        Assert.Equal(new byte[] { 3, 255, 255 }, selected.WithBattleParty([3], 6).CombatParty);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Missing_null_and_empty_selection_keep_the_native_party(string? encoded)
    {
        NativeCharacterVitals source = MagicSystemFixture.Create();
        GameplaySaveCharacters captured = GameplaySaveCharacters.Capture(source);
        JsonObject document = JsonSerializer.SerializeToNode(captured)!.AsObject();
        Assert.False(document.ContainsKey(nameof(GameplaySaveCharacters.AdditionalCombatParty)));
        Assert.Equal(source.CombatParty, captured.Restore(source).GetBattlePartyRows(11));

        document[nameof(GameplaySaveCharacters.AdditionalCombatParty)] = encoded;
        GameplaySaveCharacters decoded = document.Deserialize<GameplaySaveCharacters>()!;
        Assert.Equal(source.CombatParty, decoded.Restore(source).GetBattlePartyRows(11));
    }

    [Fact]
    public void Native_capacity_keeps_empty_native_slots_even_with_saved_additional_members()
    {
        NativeCharacterVitals selected = MagicSystemFixture.Create() with { CombatParty = [0, 255, 255], AdditionalCombatParty = [3] };

        Assert.Equal(new byte[] { 0 }, selected.GetBattlePartyRows(3));
        Assert.Equal(new byte[] { 0, 3 }, selected.GetBattlePartyRows(4));
        Assert.Equal(new byte[] { 3 }, selected.AdditionalCombatParty);
    }

    [Fact]
    public void Capped_edits_keep_dormant_choices_and_remove_only_visible_deselections()
    {
        NativeCharacterVitals source = MagicSystemFixture.Create().WithExtendedRoster(true);
        byte[] full = Enumerable.Range(0, NativeCharacterVitals.ExtendedRecordCount).Select(row => (byte)row).ToArray();
        NativeCharacterVitals selected = source.WithBattleParty(full, 11);
        NativeCharacterVitals edited = selected.WithBattleParty([0, 1, 2, 8], 4);
        Assert.Equal(new byte[] { 8, 4, 5, 6, 7, 9, 10 }, edited.AdditionalCombatParty);
        Assert.Equal(new byte[] { 0, 1, 2, 8, 4, 5, 6, 7, 9, 10 }, edited.GetBattlePartyRows(11));

        NativeCharacterVitals nativeEdit = edited.WithBattleParty([2, 0, 1], 3);
        Assert.Equal(new byte[] { 2, 0, 1 }, nativeEdit.GetBattlePartyRows(3));
        Assert.Equal(new byte[] { 2, 0, 1, 8, 4, 5, 6, 7, 9, 10 }, nativeEdit.GetBattlePartyRows(11));
        nativeEdit.Validate();
    }

    [Fact]
    public void Removing_a_visible_ally_does_not_promote_a_dormant_choice_until_capacity_increases()
    {
        NativeCharacterVitals source = MagicSystemFixture.Create();
        NativeCharacterVitals selected = source.WithBattleParty([0, 1, 2, 3, 4], 5);
        Assert.Equal(new byte[] { 0, 1, 2, 3 }, selected.GetBattlePartyRows(4));

        NativeCharacterVitals edited = selected.WithBattleParty([0, 1, 2], 4);
        Assert.Equal(new byte[] { 4 }, edited.AdditionalCombatParty);
        Assert.Equal(new byte[] { 0, 1, 2 }, edited.GetBattlePartyRows(4));
        Assert.Equal(new byte[] { 0, 1, 2, 4 }, edited.GetBattlePartyRows(5));

        GameplaySaveCharacters saved = GameplaySaveCharacters.Capture(edited);
        NativeCharacterVitals restored = JsonSerializer.Deserialize<GameplaySaveCharacters>(JsonSerializer.Serialize(saved))!.Restore(source);
        Assert.Equal(edited.GetBattlePartyRows(4), restored.GetBattlePartyRows(4));
        Assert.Equal(edited.GetBattlePartyRows(5), restored.GetBattlePartyRows(5));
        Assert.Equal(new byte[] { 0, 1, 2, 4 }, restored.WithBattleParty([0, 1, 2, 4], 4).GetBattlePartyRows(4));
    }

    [Fact]
    public void Native_holes_and_dormant_choices_can_keep_more_than_eight_distinct_saved_rows()
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Create().WithExtendedRoster(true) with
        {
            ExtendedRosterEnabled = false,
            CombatParty = [0, 255, 255],
            AdditionalCombatParty = [4, 5, 6, 7, 8, 9, 10]
        };
        vitals.Records[4] = vitals.Records[4] with { MemberFlags = 0 };
        vitals.Records[5] = vitals.Records[5] with { MemberFlags = 0 };
        vitals.Records[6] = vitals.Records[6] with { MemberFlags = 0 };
        vitals.Records[7] = vitals.Records[7] with { MemberFlags = 0 };
        Assert.Equal(new byte[] { 0 }, vitals.GetBattlePartyRows(11));

        NativeCharacterVitals selected = vitals.WithBattleParty([0, 1, 2, 3], 4);
        Assert.Equal(new byte[] { 3, 4, 5, 6, 7, 8, 9, 10 }, selected.AdditionalCombatParty);
        NativeCharacterVitals restored = selected with { CombatParty = [0, 255, 255], AdditionalCombatParty = [1, 2, .. selected.AdditionalCombatParty!] };
        restored = restored.WithExtendedRoster(true);
        restored.Records[4] = vitals.Records[4] with { MemberFlags = 1 };
        restored.Records[5] = vitals.Records[5] with { MemberFlags = 1 };
        byte[] rows = restored.GetBattlePartyRows(11);

        Assert.Equal(10, restored.AdditionalCombatParty!.Length);
        Assert.Equal(Enumerable.Range(0, 11).Select(row => (byte)row), rows);
    }

    [Fact]
    public void Confirmation_rebuilds_selected_rows_including_the_additional_members()
    {
        NativeCharacterVitals source = MagicSystemFixture.Create();
        source.Records[4] = source.Records[4] with { HitPoints = new(ushort.MaxValue, ushort.MaxValue) };
        source.Records[5] = source.Records[5] with { HitPoints = new(ushort.MaxValue, ushort.MaxValue) };

        NativeCharacterVitals selected = source.WithBattleParty([0, 1, 2, 4], 4);

        Assert.Equal(selected.GetMagicMaximumHitPoints(4), selected.Records[4].HitPoints.Minimum);
        Assert.Equal(ushort.MaxValue, selected.Records[5].HitPoints.Minimum);
        Assert.Equal(ushort.MaxValue, source.Records[4].HitPoints.Minimum);
    }

    [Fact]
    public void Native_changes_suspend_duplicate_and_ineligible_rows_without_erasing_choices()
    {
        NativeCharacterVitals selected = MagicSystemFixture.Create().WithBattleParty([0, 1, 2, 5, 3, 4], 6);
        NativeCharacterVitals changed = selected.DeepCopy() with { CombatParty = [5, 2, 255] };
        changed.Records[3] = changed.Records[3] with { MemberFlags = 0 };

        changed.Validate();
        Assert.Equal(new byte[] { 5, 2, 4 }, changed.GetBattlePartyRows(11));
        Assert.Equal(new byte[] { 5, 3, 4 }, changed.AdditionalCombatParty);
        changed.Records[3] = selected.Records[3];
        Assert.Equal(new byte[] { 0, 1, 2, 5, 3, 4 }, (changed with { CombatParty = selected.CombatParty }).GetBattlePartyRows(11));
    }

    [Fact]
    public void Dream_reorders_native_rows_without_changing_dormant_selection()
    {
        NativeCharacterVitals selected = MagicSystemFixture.Create().WithBattleParty([0, 1, 2, 5, 3], 5);
        NativeCharacterVitals dream = selected with { DreamEnabled = true };
        Assert.Equal(new byte[] { 0, 1, 2 }, dream.GetBattlePartyRows(11));
        Assert.Throws<ArgumentException>(() => dream.WithBattleParty([0, 1, 2, 5], 5));
        Assert.Throws<ArgumentException>(() => dream.WithBattleParty([0, 1, 5], 5));

        NativeCharacterVitals reordered = dream.WithBattleParty([2, 0, 1], 5);
        Assert.Equal(new byte[] { 2, 0, 1 }, reordered.GetBattlePartyRows(11));
        Assert.Equal(new byte[] { 5, 3 }, reordered.AdditionalCombatParty);
        Assert.Equal(new byte[] { 2, 0, 1, 5, 3 }, (reordered with { DreamEnabled = false }).GetBattlePartyRows(11));
    }

    [Fact]
    public void Invalid_stored_shapes_and_ineligible_selection_fail_validation()
    {
        NativeCharacterVitals source = MagicSystemFixture.Create();
        GameplaySaveCharacters saved = GameplaySaveCharacters.Capture(source);
        byte[][] invalidSelections = [[3, 3], [255], [8], [0, 1, 2, 3, 4, 5, 6, 7, 0]];
        foreach (byte[] invalid in invalidSelections)
        {
            Assert.Throws<InvalidDataException>(() => (source with { AdditionalCombatParty = invalid }).Validate());
            Assert.Throws<InvalidDataException>(() => (source with { AdditionalCombatParty = invalid }).GetBattlePartyRows(11));
            Assert.Throws<InvalidDataException>(() => (saved with { AdditionalCombatParty = invalid }).Validate());
        }
        NativeCharacterVitals extended = source.WithExtendedRoster(true);
        Assert.Throws<InvalidDataException>(() => (extended with { AdditionalCombatParty = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 0] }).Validate());

        source.Records[3] = source.Records[3] with { MemberFlags = 0 };
        Assert.Throws<ArgumentException>(() => source.WithBattleParty([], 5));
        Assert.Throws<ArgumentException>(() => source.WithBattleParty([0, 1, 2, 3], 5));
        Assert.Throws<ArgumentException>(() => source.WithBattleParty([0, 1, 2, 2], 5));
        Assert.Throws<ArgumentException>(() => source.WithBattleParty([0, 1, 2, 255], 5));
        Assert.Throws<ArgumentException>(() => source.WithBattleParty([0, 1, 2, 4], 3));
    }

    [Fact]
    public void Original_export_rejects_active_and_dormant_selection_before_conversion()
    {
        NativeCharacterVitals source = MagicSystemFixture.Create();
        NativeCharacterVitals selected = source.WithBattleParty([0, 1, 2, 5], 4);
        NativeCharacterVitals duplicate = selected with { CombatParty = [0, 1, 5] };
        NativeCharacterVitals dream = selected with { DreamEnabled = true };
        NativeCharacterVitals extended = selected.WithExtendedRoster(true);

        foreach (NativeCharacterVitals vitals in new[] { selected, duplicate, dream, extended })
        {
            Assert.Throws<NotSupportedException>(() => vitals.ForOriginalSaveExport());
            Assert.Equal(new byte[] { 5 }, vitals.AdditionalCombatParty);
        }

        Assert.Same(source, source.ForOriginalSaveExport());
        Assert.Null((source with { AdditionalCombatParty = [] }).ForOriginalSaveExport().AdditionalCombatParty);
        Assert.Null(selected.WithBattleParty([0, 1, 2], 4).ForOriginalSaveExport().AdditionalCombatParty);
    }
}
