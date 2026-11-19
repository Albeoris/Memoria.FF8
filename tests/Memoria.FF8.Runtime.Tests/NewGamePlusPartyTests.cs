namespace Memoria.FF8.Runtime.Tests;

public sealed class NewGamePlusPartyTests
{
    [Fact]
    public void SelectedGuestRemainsIndependentAcrossStoryRemovalWhileUncheckedGuestsStayLocked()
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Create().WithExtendedRoster(true) with { NewGamePlusPermanentGuests = 1 << 7 };
        for (int row = 6; row < vitals.Records.Length; row++)
        {
            vitals.Records[row] = vitals.Records[row] with { MemberFlags = 0 };
        }
        vitals = MagicSystemFixture.Stock(vitals, 7, 1, 50);
        vitals.Records[7] = vitals.Records[7] with { Experience = 75 };
        NativeCharacterVitals after = vitals.WithMemberFlags(7, false);
        Assert.True(after.IsRosterMember(7));
        Assert.False(after.IsRosterMember(6));
        Assert.False(after.IsRosterMember(8));
        Assert.Equal((uint)75, after.Records[7].Experience);
        Assert.Equal(50, after.GetMagicCount(7, 1));
        Assert.Equal(vitals.Records[7].Junction, after.Records[7].Junction);
        Assert.True(after.WithMemberFlags(6, true).IsRosterMember(6));
        NativeCharacterVitals restored = GameplaySaveCharacters.Capture(after).Restore(MagicSystemFixture.Create());
        Assert.Equal(after.NewGamePlusPermanentGuests, restored.NewGamePlusPermanentGuests);
        Assert.True(restored.IsRosterMember(7));
        Assert.False(restored.IsRosterMember(6));
    }

    [Fact]
    public void WorldMapRuleOverridesOnlyThePartyEntryBit()
    {
        GameState state = new() { Scene = SceneKind.WorldMap, WorldControls = NativeWorldControlState.CreateNewGame(), NewGamePlus = new() { AllowWorldMapPartyChanges = true } };
        byte native = state.WorldControls.Menu209;
        Assert.Equal((byte)(native | 2), NewGamePlusPartyPolicy.EffectiveMenuSelection(state));
        Assert.Equal(native, NewGamePlusPartyPolicy.EffectiveMenuSelection(state with { Scene = SceneKind.Field }));
        Assert.Equal(native, NewGamePlusPartyPolicy.EffectiveMenuSelection(state with { NewGamePlus = null }));
        Assert.Equal(native, state.WorldControls.Menu209);
    }

    [Fact]
    public void DreamIdentityUsesItsConfiguredExperienceAndKeepsEarnedProgressAcrossRestore()
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Create().WithGuestBattleRecords() with { NewGamePlusPermanentGuests = 0 };
        vitals.Records[0] = vitals.Records[0] with { Experience = 10 };
        vitals.Records[8] = vitals.Records[8] with { Experience = 80 };
        NativeCharacterVitals dream = vitals.PrepareJunction(1, [0, 1, 2], 7).Vitals;
        Assert.Equal((byte)8, dream.Records[0].CharacterId);
        Assert.Equal((uint)80, dream.Records[0].Experience);
        dream.Records[0] = dream.Records[0] with { Experience = 85 };
        dream = GameplaySaveCharacters.Capture(dream).Restore(MagicSystemFixture.Create());
        NativeCharacterVitals restored = dream.PrepareJunction(0, [8, 9, 10], 7).Vitals;
        Assert.Equal((uint)10, restored.Records[0].Experience);
        Assert.Equal((uint)85, restored.Records[8].Experience);
        Assert.Null(restored.NewGamePlusDreamExperienceBackup);
        Assert.Equal((uint)85, restored.PrepareJunction(1, [0, 1, 2], 7).Vitals.Records[0].Experience);
    }
}
