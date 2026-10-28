using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class OriginalAchievementCatalogTests
{
    [Fact]
    public void CatalogueAccountsForEveryOriginalEntryAndBindsOnlyImplementedRules()
    {
        Assert.Equal(Enumerable.Range(0, 45), OriginalAchievementCatalog.Entries.Select(entry => entry.Index));
        Assert.Equal(45, OriginalAchievementCatalog.Entries.Select(entry => entry.NativeKey).Distinct().Count());
        AchievementRegistry registry = OriginalAchievementCatalog.CreateRegistry();
        AchievementBinding[] bindings = SteamAchievementMappings.ForProduct("39150");
        Assert.Equal(43, registry.Definitions.Count);
        Assert.Equal(OriginalAchievementCatalog.Entries.Where(entry => entry.Supported).Select(entry => entry.Id).OrderBy(id => id.Value), registry.Definitions.Select(definition => definition.Id).OrderBy(id => id.Value));
        Assert.Equal(registry.Definitions.Select(definition => definition.Id).OrderBy(id => id.Value), bindings.Select(binding => binding.Id).OrderBy(id => id.Value));
        Assert.All(OriginalAchievementCatalog.Entries.Where(entry => !entry.Supported), entry => Assert.False(string.IsNullOrWhiteSpace(entry.Blocker)));
        Assert.Equal(new[] { "mag_draw", "mag_stck", "nmy_kill", "won_card" }, bindings.Where(binding => binding.NativeCounter is not null).Select(binding => binding.NativeCounter!).Order());
        Assert.Single(SteamAchievementMappings.ForProduct("1026680"));
        Assert.Single(AchievementRegistry.CreateCombatRegistry(remastered: true).Definitions);
    }

    [Fact]
    public void EverySupportedRuleHasAnExecutableQualifyingHandler()
    {
        using AchievementService service = Create();
        OriginalAchievementProgress progress = new()
        {
            GuardianForceMask = ushort.MaxValue,
            MaximumSeeDRank = true,
            MaximumCharacterLevel = true,
            AllCards = true,
            MaximumGil = true,
            WeaponUpgraded = true,
            AllBlueMagic = true,
            AllAngeloTricks = true,
            MagazineCollection = true,
            CardWins = 100,
            Chocobo = true,
            ObelLake = true,
            TimberManiacs = true,
            OmegaProof = true
        };
        service.Record(OriginalAchievementObservedEvent.Create(progress, null));
        foreach (OriginalAchievementActionKind kind in new[] { OriginalAchievementActionKind.CardPlayed, OriginalAchievementActionKind.MaximumHitPoints, OriginalAchievementActionKind.FinishGame, OriginalAchievementActionKind.Ragnarok, OriginalAchievementActionKind.FirstSalary, OriginalAchievementActionKind.CardsClubMaster, OriginalAchievementActionKind.RareCardLost })
        {
            service.Record(new OriginalAchievementActionEvent(new(kind.ToString()), new("actions"), kind, kind == OriginalAchievementActionKind.FinishGame ? 6500u : null));
        }
        for (int index = 0; index < 100; index++)
        {
            service.Record(new OriginalAchievementActionEvent(new("stock/" + index), new("draws"), OriginalAchievementActionKind.StockDraw, StockDraw: new(1, 9, 0, false)));
            service.Record(new OriginalAchievementActionEvent(new("point/" + index), new("draws"), OriginalAchievementActionKind.DrawPoint));
        }
        service.Record(new BattleKillTotalObservedEvent(new("total"), new("results"), 10000, BattleOutcome.Victory, BattleRewardPolicy.NativeCombat, 16) { DiablosAcquired = true, UfoCompleted = true });
        Assert.All(OriginalAchievementCatalog.Entries.Where(entry => entry.Supported), entry => Assert.True(service.Get(entry.Id).Unlocked, entry.NativeKey));
    }

    [Fact]
    public void DrawUsesPositiveQuantityIncludingFullStockAndDeduplicatesReplay()
    {
        using AchievementService service = Create();
        OriginalAchievementActionEvent failed = new(new("failed"), new("draws"), OriginalAchievementActionKind.StockDraw, StockDraw: new(1, 0, 0, true));
        service.Record(failed);
        Assert.Equal(0, service.Get(OriginalAchievementCatalog.Id("draw.stock.100")).Counter);
        OriginalAchievementActionEvent full = new(new("full"), new("draws"), OriginalAchievementActionKind.StockDraw, StockDraw: new(1, 9, 0, false));
        service.Record(full);
        service.Record(full);
        Assert.Equal(1, service.Get(OriginalAchievementCatalog.Id("draw.stock.100")).Counter);
        service.Record(new OriginalAchievementActionEvent(new("item"), new("import"), OriginalAchievementActionKind.ChocoLoot));
        Assert.Equal(1, service.Get(OriginalAchievementCatalog.Id("draw.stock.100")).Counter);
    }

    [Fact]
    public void SnapshotWinsAndKillsUseAbsoluteHighWaterRatherThanDeltas()
    {
        using AchievementService service = Create();
        service.Record(OriginalAchievementObservedEvent.Create(new() { CardWins = 100 }, null));
        service.Record(OriginalAchievementObservedEvent.Create(new() { CardWins = 5 }, null));
        Assert.Equal(100, service.Get(OriginalAchievementCatalog.Id("cards.wins.100")).Counter);
        service.Record(new BattleKillTotalObservedEvent(new("kills"), new("first"), 9999, BattleOutcome.Defeat, BattleRewardPolicy.NativeCombat, 16));
        Assert.True(service.Get(OriginalAchievementCatalog.Id("kills.1000")).Unlocked);
        Assert.False(service.Get(OriginalAchievementCatalog.Id("kills.10000")).Unlocked);
        service.Record(new BattleKillTotalObservedEvent(new("kills"), new("rollback"), 1, BattleOutcome.Escaped, BattleRewardPolicy.NativeCombat, 0));
        Assert.Equal(9999, service.Get(OriginalAchievementCatalog.Id("kills.10000")).Counter);
        Assert.False(service.Get(OriginalAchievementCatalog.Id("gf.diablos")).ProgressKnown);
    }

    [Theory]
    [InlineData(6999u, true)]
    [InlineData(7000u, false)]
    public void EndingObservesSelectedNativeMovieAndExactExperienceGate(uint experience, bool initialLevel)
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Create();
        vitals = vitals with { Records = vitals.Records.Select((record, row) => row == 0 ? record with { Experience = experience } : record).ToArray() };
        GameSession session = new(new GameState { Scene = SceneKind.Field, MovieSource = new(3, 6), CharacterVitals = vitals });
        session.RequestMovie(6);
        OriginalAchievementActionEvent fact = Assert.Single(session.State.PendingOriginalAchievementActions);
        Assert.Equal(OriginalAchievementActionKind.FinishGame, fact.Kind);
        using AchievementService service = Create();
        service.Record(fact);
        Assert.True(service.Get(OriginalAchievementCatalog.Id("finish")).Unlocked);
        Assert.Equal(initialLevel, service.Get(OriginalAchievementCatalog.Id("finish.initial_level")).Unlocked);
    }

    [Fact]
    public void OtherDiscsAndUnselectedMoviesDoNotInventStoryAchievements()
    {
        GameSession session = new(new GameState { Scene = SceneKind.Field, MovieSource = new(1, 6) });
        session.RequestMovie(6);
        Assert.Empty(session.State.PendingOriginalAchievementActions);
        GameSession ragnarok = new(new GameState { Scene = SceneKind.Field, MovieSource = new(2, 25) });
        ragnarok.RequestMovie(25);
        Assert.Equal(OriginalAchievementActionKind.Ragnarok, Assert.Single(ragnarok.State.PendingOriginalAchievementActions).Kind);
    }

    private static AchievementService Create() => new(OriginalAchievementCatalog.CreateRegistry(), new MemoryRepository());

    [Fact]
    public void CardsClubTransferObservesInstancesEvenWhenIdenticalCommonCopiesHaveNoNetGain()
    {
        NativeCardInventory inventory = NativeCardInventory.CreateNewGame();
        for (int count = 0; count < 5; count++)
        {
            inventory = inventory.Acquire(0, out _);
        }
        TripleTriadTradeResult exchanged = TripleTriadTrade.Settle(inventory, [0, 0, 0, 0, 0], [0, 0, 0, 0, 0], [1, 0, 0, 0, 0, 0, 1, 1, 1, 1], TripleTriadTradeRule.Direct, 0xCA, Enumerable.Repeat(true, 10).ToArray());
        Assert.True(exchanged.TookOpponentInstance);
        Assert.Empty(exchanged.GainedCardIds);
        TripleTriadTradeResult keptOwnHand = TripleTriadTrade.Settle(inventory, [0, 0, 0, 0, 0], [0, 0, 0, 0, 0], [0, 0, 0, 0, 0, 1, 1, 1, 1, 1], TripleTriadTradeRule.Direct, 0xCA, Enumerable.Repeat(true, 10).ToArray());
        Assert.False(keptOwnHand.TookOpponentInstance);
    }

    private sealed class MemoryRepository : IAchievementRepository
    {
        private AchievementProfile profile = new();
        public AchievementProfile Load() => profile;
        public void Save(AchievementProfile value) => profile = value;
        public void Dispose()
        {
        }
    }
}
