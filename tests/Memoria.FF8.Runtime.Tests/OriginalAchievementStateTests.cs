using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class OriginalAchievementStateTests
{
    [Fact]
    public void MissingOwnersRemainUnknown()
    {
        Assert.Equal(default, OriginalAchievementState.Observe(new GameState()));
    }

    [Fact]
    public void GuardianForcesUseNonzeroSavedFlagAndLevelsUseOriginalExperienceThreshold()
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Create();
        vitals = vitals with { GuardianForces = vitals.GuardianForces.Select((gf, id) => gf with { Flags = id == 5 ? (byte)2 : (byte)0 }).ToArray() };
        OriginalAchievementProgress before = OriginalAchievementState.Observe(new GameState { CharacterVitals = vitals });
        Assert.Equal((ushort)(1 << 5), before.GuardianForceMask);
        Assert.False(before.MaximumCharacterLevel);
        NativeCharacterVital[] records = vitals.Records.ToArray();
        records[7] = records[7] with { Experience = 98999 };
        Assert.False(OriginalAchievementState.Observe(new GameState { CharacterVitals = vitals with { Records = records } }).MaximumCharacterLevel);
        records[7] = records[7] with { Experience = 99000 };
        Assert.True(OriginalAchievementState.Observe(new GameState { CharacterVitals = vitals with { Records = records } }).MaximumCharacterLevel);
    }

    [Fact]
    public void AllCardsRequiresCurrentCopiesAndCompleteRareHistory()
    {
        NativeCardInventory cards = NativeCardInventory.CreateNewGame() with
        {
            Cards = Enumerable.Range(0, 110).Select(id => id < 77 ? (byte)0x81 : (byte)0xF0).ToArray(),
            ObtainedRareCards = [255, 255, 255, 255, 1]
        };
        Assert.True(OriginalAchievementState.Observe(new GameState { CardInventory = cards }).AllCards);
        cards.Cards[0] = 0x80;
        Assert.False(OriginalAchievementState.Observe(new GameState { CardInventory = cards }).AllCards);
        cards.Cards[0] = 0x81;
        cards.Cards[109] = 200;
        Assert.False(OriginalAchievementState.Observe(new GameState { CardInventory = cards }).AllCards);
        cards.Cards[109] = 0xF0;
        cards.ObtainedRareCards[4] = 0;
        Assert.False(OriginalAchievementState.Observe(new GameState { CardInventory = cards }).AllCards);
    }

    [Fact]
    public void MagazineCollectionRequiresAllUniqueOwnedItems()
    {
        NativeItemStock[] items = Enumerable.Repeat(new NativeItemStock(0, 0), NativeEconomy.ItemSlotCount).ToArray();
        NativeEconomy economy = new() { Version = 1, Rule = NativeEconomy.InitializationRule, InitOutSha256 = new string('0', 64), Items = items, Gil = 99999998, PastGil = 0, EventBankGil72 = 0, EventBankPastGil76 = 0 };
        for (int index = 0; index < 22; index++)
        {
            items[index] = new NativeItemStock((byte)(0xB1 + index), 1);
        }
        Assert.True(OriginalAchievementState.Observe(new GameState { Economy = economy }).MagazineCollection);
        items[21] = items[20];
        Assert.False(OriginalAchievementState.Observe(new GameState { Economy = economy }).MagazineCollection);
        Assert.False(OriginalAchievementState.Observe(new GameState { Economy = economy }).MaximumGil);
        Assert.True(OriginalAchievementState.Observe(new GameState { Economy = economy with { Gil = 99999999 } }).MaximumGil);
    }

    [Fact]
    public void PartialWorldKnowledgeAndLanguageDependentMagazineMasksRemainUnknown()
    {
        NativeSavedWorldState world = NativeSavedWorldState.CreateNewGame() with { WorldEventFlags116 = new NativeWorldEventFlags(1UL << 3, 1UL << 3) };
        OriginalAchievementProgress progress = OriginalAchievementState.Observe(new GameState { SavedWorld = world });
        Assert.True(progress.Chocobo);
        Assert.Null(progress.ObelLake);
        NativeEventMemory memory = NativeEventMemory.Empty.Write(304, 2, NativeScalarValue.Exact(0x37FE));
        Assert.Null(OriginalAchievementState.Observe(new GameState { EventMemory = memory }).TimberManiacs);
        Assert.False(OriginalAchievementState.Observe(new GameState { EventMemory = memory }, nativeLanguage12: false).TimberManiacs);
        Assert.True(OriginalAchievementState.Observe(new GameState { EventMemory = memory }, nativeLanguage12: true).TimberManiacs);
        memory = memory.Write(304, 2, NativeScalarValue.Exact(0x3BFD));
        Assert.True(OriginalAchievementState.Observe(new GameState { EventMemory = memory }).TimberManiacs);
        memory = memory.Write(304, 2, NativeScalarValue.Exact(0));
        Assert.False(OriginalAchievementState.Observe(new GameState { EventMemory = memory }).TimberManiacs);
    }

    [Fact]
    public void WinHistoryAndSeedThresholdDoNotUseUiVisibility()
    {
        NativeCardInventory cards = NativeCardInventory.CreateNewGame() with { TripleTriadStatistics = null };
        Assert.Null(OriginalAchievementState.Observe(new GameState { CardInventory = cards }).CardWins);
        cards = cards with { TripleTriadStatistics = new TripleTriadStatistics(99, 0, 0) };
        Assert.Equal(99, OriginalAchievementState.Observe(new GameState { CardInventory = cards }).CardWins);
        cards = cards with { TripleTriadStatistics = new TripleTriadStatistics(100, 0, 0) };
        OriginalAchievementProgress progress = OriginalAchievementState.Observe(new GameState { CardInventory = cards, SeeD = NativeSeeDState.CreateNewGame() with { RankPoints = 3100, SalaryUiEnabled = false } });
        Assert.Equal(100, progress.CardWins);
        Assert.True(progress.MaximumSeeDRank);
    }
}
