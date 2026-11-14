using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class BattlePartySelectionPolicyTests
{
    [Theory]
    [InlineData(449, BattlePartySelectionRestriction.None)]
    [InlineData(450, BattlePartySelectionRestriction.StorySquad)]
    [InlineData(749, BattlePartySelectionRestriction.StorySquad)]
    [InlineData(750, BattlePartySelectionRestriction.None)]
    [InlineData(849, BattlePartySelectionRestriction.None)]
    [InlineData(850, BattlePartySelectionRestriction.StorySquad)]
    [InlineData(893, BattlePartySelectionRestriction.StorySquad)]
    [InlineData(894, BattlePartySelectionRestriction.None)]
    public void Story_squad_boundaries_use_the_saved_story_word(int storyProgress, BattlePartySelectionRestriction expected)
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Create();
        GameState state = new()
        {
            CharacterVitals = vitals,
            EventMemory = NativeEventMemory.Empty.Write(256, 2, NativeScalarValue.Exact(storyProgress))
        };

        Assert.Equal(expected, BattlePartySelectionPolicy.GetRestriction(vitals, null, storyProgress));
        Assert.Equal(expected == BattlePartySelectionRestriction.None ? 11 : 3, BattlePartySelectionPolicy.ResolveCapacity(state, 11));
    }

    [Fact]
    public void Fixed_count_then_dream_then_castle_take_priority_over_story()
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Create();
        NativeCharacterVitals dream = vitals with { DreamEnabled = true };
        NativeWorldControlState controls = NativeWorldControlState.CreateNewGame() with { EventFlags104 = 0x800 };

        Assert.Equal(BattlePartySelectionRestriction.FixedCountSelection, BattlePartySelectionPolicy.GetRestriction(dream, controls, 450, fixedCountSelection: true));
        Assert.Equal(BattlePartySelectionRestriction.Dream, BattlePartySelectionPolicy.GetRestriction(dream, controls, 450));
        Assert.Equal(BattlePartySelectionRestriction.CastleSplit, BattlePartySelectionPolicy.GetRestriction(vitals, controls, 450));
        Assert.Equal(3, BattlePartySelectionPolicy.ResolveCapacity(new GameState { CharacterVitals = vitals }, 11, fixedCountSelection: true));
        Assert.Equal(3, BattlePartySelectionPolicy.ResolveCapacity(dream, 11, null, null));
    }

    [Fact]
    public void Castle_uses_the_partition_bit_independently_of_menu_permissions()
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Create();
        NativeWorldControlState controls = NativeWorldControlState.CreateNewGame().WithPartyPower(0);
        NativeWorldControlState castle = controls with { EventFlags104 = controls.EventFlags104 | 0x800 };
        NativeWorldControlState afterLastOut = castle with { EventFlags104 = castle.EventFlags104 & ~0x800u };

        Assert.Equal(BattlePartySelectionRestriction.CastleSplit, BattlePartySelectionPolicy.GetRestriction(vitals, castle, null));
        Assert.Equal(3, BattlePartySelectionPolicy.ResolveCapacity(new GameState { CharacterVitals = vitals, WorldControls = castle }, 11));
        Assert.Equal(BattlePartySelectionRestriction.None, BattlePartySelectionPolicy.GetRestriction(vitals, afterLastOut, null));
        Assert.Equal(11, BattlePartySelectionPolicy.ResolveCapacity(new GameState { CharacterVitals = vitals, WorldControls = afterLastOut }, 11));
    }

    [Fact]
    public void Missing_optional_facts_leave_configured_capacity_available()
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Create();
        GameState noStory = new() { CharacterVitals = vitals };
        GameState noParty = new()
        {
            WorldControls = NativeWorldControlState.CreateNewGame() with { EventFlags104 = 0x800 },
            EventMemory = NativeEventMemory.Empty.Write(256, 2, NativeScalarValue.Exact(450))
        };

        Assert.Equal(BattlePartySelectionRestriction.None, BattlePartySelectionPolicy.GetRestriction(vitals, null, null));
        Assert.Equal(11, BattlePartySelectionPolicy.ResolveCapacity(noStory, 11));
        Assert.Equal(11, BattlePartySelectionPolicy.ResolveCapacity(noParty, 11, fixedCountSelection: true));
    }

    [Fact]
    public void Every_explicit_capacity_is_preserved_or_limited_without_changing_saved_extras()
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Create() with { AdditionalCombatParty = [4, 3, 5] };
        byte[] additional = vitals.AdditionalCombatParty!;
        for (int capacity = 3; capacity <= NativeCharacterVitals.ExtendedRecordCount; capacity++)
        {
            Assert.Equal(capacity, BattlePartySelectionPolicy.ResolveCapacity(vitals, capacity, null, 750));
            Assert.Equal(3, BattlePartySelectionPolicy.ResolveCapacity(vitals, capacity, null, 749));
        }

        Assert.Same(additional, vitals.AdditionalCombatParty);
        Assert.Equal(new byte[] { 4, 3, 5 }, additional);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(12)]
    public void Invalid_explicit_capacities_are_rejected_even_without_a_party_or_during_restriction(int capacity)
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Create();

        Assert.Throws<ArgumentOutOfRangeException>(() => BattlePartySelectionPolicy.ResolveCapacity(vitals, capacity, null, 450));
        Assert.Throws<ArgumentOutOfRangeException>(() => BattlePartySelectionPolicy.ResolveCapacity(new GameState(), capacity));
    }
}
