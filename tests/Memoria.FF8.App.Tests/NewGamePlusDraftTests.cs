using System.Collections.Immutable;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NewGamePlusDraftTests
{
    [Fact]
    public void EditingAndStartingKeepTheSourceAndSettingsDetached()
    {
        GameState source = CompletedSaveFixture.State();
        GameState native = Native();
        UiPreferences settings = new();
        NewGamePlusDraft draft = new(source, native, settings);
        draft.SetItem(17, 3);
        draft.SetCard(0, 100);
        draft.SetLevel(0, 17);
        draft.ExperienceMultiplierTenths = 0;
        draft.ApMultiplierTenths = 27;
        GameState started = draft.ApplyTo(native);
        Assert.Equal(43, source.Economy!.Items[0].Count);
        Assert.Equal(7, source.CardInventory!.AvailableCount(0));
        Assert.Equal(12345u, source.CharacterVitals!.Records[0].Experience);
        Assert.Equal(3, started.Economy!.Items.Single(item => item.ItemId == 17).Count);
        Assert.Equal(100, started.CardInventory!.AvailableCount(0));
        Assert.Equal(0, started.NewGamePlus!.ExperienceMultiplierTenths);
        Assert.Equal(27, started.NewGamePlus.ApMultiplierTenths);
        Assert.Equal(new UiPreferences(), settings);
        Assert.True(started.NewGamePlus.StartingChoicesApplied);
    }

    [Fact]
    public void StoredLevelsUseSourceExperienceButAvailabilityUsesPristineInitialization()
    {
        GameState source = CompletedSaveFixture.State();
        GameState native = Native();
        NewGamePlusDraft draft = new(source, native, new());
        Assert.Equal(100, draft.Characters.Single(character => character.CharacterId == 0).IndividualLevel);
        Assert.True(draft.Characters.Single(character => character.CharacterId == 0).AvailableFromStart);
        Assert.False(draft.Characters.Single(character => character.CharacterId == 1).AvailableFromStart);
        Assert.True(draft.Characters.Single(character => character.CharacterId == 8).NativeFallback);
        draft.SeedCharacters(NewGamePlusPreset.Native);
        Assert.Equal(2, draft.Characters.Single(character => character.CharacterId == 0).IndividualLevel);
        Assert.False(draft.Characters.Single(character => character.CharacterId == 1).AvailableFromStart);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(17)]
    [InlineData(100)]
    public void GlobalLevelsResolveToTheMinimumExperienceThresholdWithoutErasingIndividualChoices(int level)
    {
        GameState native = Native();
        NewGamePlusDraft draft = new(CompletedSaveFixture.State(), native, new());
        draft.SetLevel(1, 23);
        draft.SetGlobalLevel(level);
        draft.SetLevel(1, 71);
        GameState started = draft.ApplyTo(native);
        foreach (NativeCharacterVital record in started.CharacterVitals!.Records)
        {
            NativeStatCurve curve = started.CharacterVitals.Definitions.Characters[record.CharacterId];
            Assert.Equal(level, NativeStatDefinitions.Level(record.Experience, curve));
            if (record.Experience > 0)
            {
                Assert.True(NativeStatDefinitions.Level(record.Experience - 1, curve) < level);
            }
        }
        draft.SetGlobalLevel(0);
        Assert.Equal(23, draft.ResolveDefinition().ResolvedLevel(1));
    }

    [Fact]
    public void EmptyCollectionsClearAllStockAndRareCardsStayUniqueWithoutImportingStory()
    {
        GameState source = CompletedSaveFixture.State() with { EventMemory = NativeEventMemory.FromDWords(new Dictionary<int, int> { [300] = 987654 }) };
        GameState native = Native();
        NewGamePlusDraft draft = new(source, native, new());
        draft.Reset("items", NewGamePlusPreset.Empty);
        draft.Reset("cards", NewGamePlusPreset.Empty);
        Assert.All(draft.Items.Items, item => Assert.Equal(0, item.Count));
        Assert.All(Enumerable.Range(0, 110), card => Assert.Equal(0, Math.Max(0, draft.Cards.AvailableCount(card))));
        draft.SetCard(77, 100);
        draft.SetCard(77, 100);
        draft.SetCard(0, 100);
        GameState started = draft.ApplyTo(native);
        Assert.Equal(1, started.CardInventory!.AvailableCount(77));
        Assert.Equal(100, started.CardInventory.AvailableCount(0));
        Assert.Same(native.EventMemory, started.EventMemory);
        Assert.Same(native.SavedWorld, started.SavedWorld);
        Assert.Equal(43, source.Economy!.Items[0].Count);
        Assert.Equal(0, Math.Max(0, source.CardInventory!.AvailableCount(77)));
    }

    [Fact]
    public void EmptyMagicCannotResurrectRetainedSharedOverflowDuringRepeatedConversions()
    {
        GameState source = CompletedSaveFixture.State();
        NativeCharacterVitals vitals = source.CharacterVitals!;
        foreach (int row in new[] { 0, 1, 2 })
        {
            vitals = vitals.ChangeMagicStock(row, 1, 70, MagicOperationReason.ScriptReward, out _);
        }
        vitals = MagicModeCoordinator.Preview(vitals, new GameplayPreferences { SharedMagicStock = true }).Vitals;
        NewGamePlusDraft draft = new(source with { CharacterVitals = vitals }, Native(), new());
        draft.Reset("magic", NewGamePlusPreset.Empty);
        for (int cycle = 0; cycle < 3; cycle++)
        {
            draft.ConvertMagic(new GameplayPreferences { SharedMagicStock = false });
            draft.ConvertMagic(new GameplayPreferences { SharedMagicStock = true });
        }
        Assert.All(draft.Magic.Records, record => Assert.All(record.MagicInventory, stock => Assert.Equal(0, stock.Count)));
        Assert.Empty(draft.Magic.MagicSystem.RecordZeroExtension);
        Assert.Equal(100, vitals.GetMagicCount(0, 1));
    }

    [Fact]
    public void SharedMagicIsTransferredOnceRatherThanCopiedToEveryCharacter()
    {
        GameState native = Native();
        NativeCharacterVitals vitals = native.CharacterVitals!.ChangeMagicStock(0, 1, 70, MagicOperationReason.ScriptReward, out _);
        vitals = MagicModeCoordinator.Preview(vitals, new GameplayPreferences { SharedMagicStock = true }).Vitals;
        NewGamePlusDraft draft = new(CompletedSaveFixture.State() with { CharacterVitals = vitals }, native, new());
        for (int cycle = 0; cycle < 3; cycle++)
        {
            draft.ConvertMagic(new GameplayPreferences { SharedMagicStock = false });
            draft.ConvertMagic(new GameplayPreferences { SharedMagicStock = true });
        }
        draft.ConvertMagic(new GameplayPreferences { SharedMagicStock = false });
        Assert.Equal(70, draft.Magic.Records.Sum(record => record.MagicInventory.Where(stock => stock.SpellId == 1).Sum(stock => stock.Count)));
    }

    [Fact]
    public void GuardianForceDefinitionsPreservePartialApAndEditingDoesNotMutateSource()
    {
        GameState source = CompletedSaveFixture.State();
        NativeCharacterVitals vitals = source.CharacterVitals!;
        ImmutableArray<ImmutableArray<GuardianForceAbilityTemplate>> templates = vitals.ResolveGuardianForceTemplates().SetItem(0, [new(20, true, 100), new(21, false, 100)]);
        vitals = vitals.ApplyGuardianForceTemplates(templates);
        vitals.GuardianForces[0] = vitals.GuardianForces[0] with { Learning = 21, Progression = new(true, [new(20, true, 0), new(21, false, 37)]) };
        source = source with { CharacterVitals = vitals };
        NewGamePlusDraft draft = new(source, Native(), new());
        GuardianForceAbilityProgress partial = draft.GuardianForces.GuardianForces[0].Progression!.Abilities.Single(entry => entry.Ability == 21);
        Assert.Equal(37, partial.EarnedPoints);
        Assert.False(partial.Learned);
        draft.ToggleAbility(0, 21);
        GuardianForceAbilityProgress learned = draft.GuardianForces.GuardianForces[0].Progression!.Abilities.Single(entry => entry.Ability == 21);
        Assert.True(learned.Learned);
        Assert.Equal(0, learned.EarnedPoints);
        Assert.Equal(37, source.CharacterVitals.GuardianForces[0].Progression!.Abilities.Single(entry => entry.Ability == 21).EarnedPoints);
        draft.RemoveAbility(0, 21);
        Assert.DoesNotContain(draft.GuardianForces.ResolveGuardianForceTemplates()[0], entry => entry.Ability == 21);
        Assert.NotEqual(21, draft.GuardianForces.GuardianForces[0].Learning);
        draft.AddAbility(0, 21);
        Assert.Throws<InvalidOperationException>(() => draft.AddAbility(0, 21));
    }

    private static GameState Native()
    {
        GameState state = CompletedSaveFixture.State() with { Ff8Completion = null };
        for (int row = 0; row < state.CharacterVitals!.Records.Length; row++)
        {
            NativeCharacterVital record = state.CharacterVitals.Records[row];
            state.CharacterVitals.Records[row] = record with { Experience = 10, MemberFlags = (ushort)(row == 0 ? 1 : 0) };
        }
        return state;
    }
}
