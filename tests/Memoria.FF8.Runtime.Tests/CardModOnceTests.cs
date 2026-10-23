using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Memoria.FF8.Runtime.Tests;

public sealed class CardModOnceTests
{
    private static readonly TripleTriadRefinement Recipe = new(0, 2, 118, 5);

    [Fact]
    public void OnceModeRetainsCardsAndBlocksTheSameIdAfterToggling()
    {
        GameSession session = Session(4, true);
        GameState before = session.State;
        Assert.True(session.RefineCard(Recipe));
        Assert.Equal(4, session.State.CardInventory!.AvailableCount(0));
        Assert.Equal(new[] { 0 }, session.State.RefinedCardIds);
        Assert.Empty(before.RefinedCardIds);
        Assert.False(session.RefineCard(Recipe with { ItemId = 119 }));
        session.SetGameplayPreferences(new GameplayPreferences());
        Assert.True(session.RefineCard(Recipe));
        Assert.Equal(2, session.State.CardInventory.AvailableCount(0));
        session.SetGameplayPreferences(new GameplayPreferences { CardModOnce = true });
        Assert.False(session.RefineCard(Recipe));
        session.SetCardInventory(session.State.CardInventory.Acquire(1, out _));
        Assert.True(session.RefineCard(Recipe with { CardId = 1, RequiredCards = 1 }));
        Assert.Equal(new[] { 0, 1 }, session.State.RefinedCardIds);
    }

    [Fact]
    public void NormalModeConsumesRepeatedBatchesAndRecordsBeforeEnabling()
    {
        GameSession session = Session(6, false);
        Assert.True(session.RefineCard(Recipe, 2));
        Assert.Equal(2, session.State.CardInventory!.AvailableCount(0));
        Assert.True(session.RefineCard(Recipe));
        Assert.Single(session.State.RefinedCardIds);
        session.SetCardInventory(session.State.CardInventory.Acquire(0, out _).Acquire(0, out _));
        session.SetGameplayPreferences(new GameplayPreferences { CardModOnce = true });
        Assert.False(session.RefineCard(Recipe));
    }

    [Fact]
    public void RejectedBatchesInsufficientCardsAndFullOutputDoNotRecordSuccess()
    {
        GameSession session = Session(1, true);
        GameState before = session.State;
        Assert.False(session.RefineCard(Recipe));
        Assert.False(session.RefineCard(Recipe with { RequiredCards = 1 }, 2));
        Assert.False(session.RefineCard(Recipe with { RequiredCards = 1 }, 0));
        Assert.Same(before, session.State);
        session.SetCardInventory(session.State.CardInventory!.Acquire(0, out _));
        NativeEconomy full = Economy();
        full.Items[0] = new(118, 100);
        GameSession blocked = new(session.State with { Economy = full });
        Assert.False(blocked.RefineCard(Recipe));
        Assert.Empty(blocked.State.RefinedCardIds);
        Assert.Equal(2, blocked.State.CardInventory!.AvailableCount(0));
        Assert.True(session.RefineCard(Recipe));
    }

    [Fact]
    public void RareCardLocationAndAcquisitionHistoryArePreserved()
    {
        GameSession session = Session(0, true);
        session.SetCardInventory(session.State.CardInventory!.Acquire(109, out _));
        NativeCardInventory before = session.State.CardInventory!;
        Assert.True(session.RefineCard(new(109, 1, 118, 5)));
        Assert.Equal(before, session.State.CardInventory);
        Assert.False(session.RefineCard(new(109, 1, 118, 5)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HistoryRoundTripsWhenModeIsEnabledOrDisabled(bool enabled)
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            GameSession session = Session(4, enabled);
            Assert.True(session.RefineCard(Recipe));
            session.Save(path, compact: enabled);
            GameSession restored = new(GameSession.Load(path));
            Assert.Equal(enabled, restored.State.GameplayPreferences.CardModOnce);
            Assert.Equal(new[] { 0 }, restored.State.RefinedCardIds);
            restored.SetGameplayPreferences(new GameplayPreferences { CardModOnce = true });
            Assert.False(restored.RefineCard(Recipe));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void OldSaveDefaultsAndNewGameResetHistory()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            GameSession session = GameSession.CreateColdProcess();
            session.StartNewGame();
            session.RecordCardRefinement(0);
            session.SetGameplayPreferences(new GameplayPreferences { CardModOnce = true });
            session.Save(path);
            JsonObject saved = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            saved.Remove(nameof(GameState.RefinedCardIds));
            saved[nameof(GameState.GameplayPreferences)]!.AsObject().Remove(nameof(GameplayPreferences.CardModOnce));
            File.WriteAllText(path, saved.ToJsonString());
            GameState loaded = GameSession.Load(path);
            Assert.Empty(loaded.RefinedCardIds);
            Assert.False(loaded.GameplayPreferences.CardModOnce);
            session.StartNewGame();
            Assert.Empty(session.State.RefinedCardIds);
            Assert.False(session.State.GameplayPreferences.CardModOnce);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void InvalidHistoryIsRejectedAndValidHistoryIsImmutable()
    {
        GameState state = new();
        Assert.Throws<InvalidDataException>(() => new GameSession(state with { RefinedCardIds = default }));
        Assert.Throws<InvalidDataException>(() => new GameSession(state with { RefinedCardIds = [-1] }));
        Assert.Throws<InvalidDataException>(() => new GameSession(state with { RefinedCardIds = [110] }));
        Assert.Throws<InvalidDataException>(() => new GameSession(state with { RefinedCardIds = [1, 1] }));
        int[] ids = [1];
        GameSession session = new(state with { RefinedCardIds = ids.ToImmutableArray() });
        ids[0] = 2;
        Assert.Equal(1, session.State.RefinedCardIds[0]);
        session.RecordCardRefinement(1);
        Assert.Single(session.State.RefinedCardIds);
    }

    [Fact]
    public void NativeRefinementUsesOnceModeWithoutChangingNormalRecipeRules()
    {
        NativeCharacterVitals vitals = Vitals();
        NativeEconomy economy = Economy();
        NativeCardInventory cards = Session(4, false).State.CardInventory!;
        NativeRefinementRecipe recipe = new(5, 0, 0, 2, 118);
        NativeRefinementDefinitions definitions = new(NativeFieldRuntimeProfile.FF8EnExecutableHash, vitals.KernelSha256, Enumerable.Repeat(new NativeRefinementAbility("Synthetic", 4, 0, 0), 24).ToImmutableArray(), new[] { 102, 143, 10, 12, 110 }.Select(count => Enumerable.Repeat(recipe, count).ToImmutableArray()).ToImmutableArray());
        Assert.Equal(2, definitions.MaximumBatches(vitals, economy, cards, 115, 0, 0));
        Assert.Equal(1, definitions.MaximumBatches(vitals, economy, cards, 115, 0, 0, true, []));
        Assert.Equal(0, definitions.MaximumBatches(vitals, economy, cards, 115, 0, 0, true, [0]));
        Assert.Equal(0, definitions.MaximumBatches(vitals, economy, NativeCardInventory.CreateNewGame().Acquire(0, out _), 115, 0, 0, true, []));
        Assert.Throws<InvalidOperationException>(() => definitions.Refine(vitals, economy, cards, 115, 0, 0, 2, true, []));
        Assert.Throws<InvalidOperationException>(() => definitions.Refine(vitals, economy, cards, 115, 0, 0, 1, true, [0]));
        NativeRefinementResult once = definitions.Refine(vitals, economy, cards, 115, 0, 0, 1, true, []);
        Assert.Equal(cards.Cards, once.Cards!.Cards);
        Assert.Equal(5, once.Economy.Items[0].Count);
        NativeRefinementResult normal = definitions.Refine(vitals, economy, cards, 115, 0, 0, 2, false, [0]);
        Assert.Equal(0, normal.Cards!.AvailableCount(0));
        Assert.Equal(10, normal.Economy.Items[0].Count);
    }

    private static GameSession Session(int count, bool enabled)
    {
        NativeCardInventory cards = NativeCardInventory.CreateNewGame();
        for (int index = 0; index < count; index++)
        {
            cards = cards.Acquire(0, out _);
        }
        return new(new GameState { CharacterVitals = Vitals(), CardInventory = cards, Economy = Economy(), GameplayPreferences = new() { CardModOnce = enabled } });
    }

    private static NativeCharacterVitals Vitals()
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Create();
        vitals = vitals with { KernelSha256 = "E378FB8F198EDE3DAE858F0DED6670F9BA423AA79ABFFF7237E701DFC7F9E7F6", MenuDefinitions = vitals.MenuDefinitions with { Learning = vitals.MenuDefinitions.Learning.SetItem(0, vitals.MenuDefinitions.Learning[0].SetItem(0, new(1, 255, 115))) } };
        vitals.GuardianForces[0] = vitals.GuardianForces[0] with { LearnedBank = new(0, 1UL << (115 - 64)) };
        return vitals;
    }

    private static NativeEconomy Economy()
    {
        return new() { Version = 1, Rule = NativeEconomy.InitializationRule, InitOutSha256 = new string('0', 64), Items = Enumerable.Repeat(new NativeItemStock(0, 0), NativeEconomy.ItemSlotCount).ToArray(), Gil = 0, PastGil = 0, EventBankGil72 = 0, EventBankPastGil76 = 0 };
    }
}
