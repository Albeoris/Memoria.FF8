namespace Memoria.FF8.Runtime.Tests;

public sealed class OriginalAchievementBattleStateTests
{
    [Theory]
    [InlineData(3)]
    [InlineData(6)]
    [InlineData(15)]
    [InlineData(5)]
    public void NativeLiveGuardianAcquisitionIsObservedBeforeResultsAndSurvivesRollback(int id)
    {
        NativeCharacterVitals vitals = Vitals();
        vitals.GuardianForces[2] = vitals.GuardianForces[2] with { Flags = 2 };
        GameSession session = Session(vitals);
        GameState earlier = session.State;
        BattleSession battle = Battle(guardianForces: [new(id, 10, 10, true)]);
        session.SetLiveBattle(battle);
        OriginalAchievementObservedEvent fact = Assert.Single(session.State.PendingOriginalAchievementObservations);
        Assert.Equal((ushort)((1 << id) | (1 << 2)), fact.Progress.GuardianForceMask);
        Assert.Equal(0, session.State.CharacterVitals!.GuardianForces[id].Flags);
        Assert.Equal(2, session.State.CharacterVitals.GuardianForces[2].Flags);
        using AchievementService service = new(OriginalAchievementCatalog.CreateRegistry(), new MemoryRepository());
        service.Record(fact);
        Assert.Equal(id != 5, service.Get(OriginalAchievementCatalog.Entries[id].Id).Unlocked);
        session.Restore(earlier);
        Assert.Equal(fact, Assert.Single(session.State.PendingOriginalAchievementObservations));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NativeCurrentCardsAndItemsOverrideTheStaleRootWithoutMutatingIt(bool complete)
    {
        NativeCardInventory staleCards = Cards(!complete);
        NativeEconomy staleEconomy = Economy(!complete);
        GameSession session = Session(Vitals(), staleCards, staleEconomy);
        BattleSession battle = Battle(cards: Cards(complete), items: Economy(complete).Items);
        session.SetLiveBattle(battle);
        OriginalAchievementProgress progress = Assert.Single(session.State.PendingOriginalAchievementObservations).Progress;
        Assert.Equal(complete, progress.AllCards);
        Assert.Equal(complete, progress.MagazineCollection);
        Assert.Equal(staleCards, session.State.CardInventory);
        Assert.Equal(staleEconomy, session.State.Economy);
        Assert.Equal(!complete, OriginalAchievementState.Observe(session.State).AllCards);
        Assert.Equal(!complete, OriginalAchievementState.Observe(session.State).MagazineCollection);
    }

    [Fact]
    public void CurrentMagicOwnerOverridesSavedCharacterState()
    {
        NativeCharacterVitals current = Vitals();
        current.Records[0] = current.Records[0] with { Experience = 99000 };
        GameSession session = Session(Vitals());
        session.SetLiveBattle(Battle(magicVitals: current));
        Assert.True(Assert.Single(session.State.PendingOriginalAchievementObservations).Progress.MaximumCharacterLevel);
        Assert.Equal(0U, session.State.CharacterVitals!.Records[0].Experience);
    }

    [Fact]
    public void SyntheticAndDebugBattlesDoNotProduceNativeSnapshotObservations()
    {
        GameSession synthetic = Session(Vitals());
        synthetic.SetLiveBattle(Battle(native: false, guardianForces: [new(3, 10, 10, true)]));
        Assert.Empty(synthetic.State.PendingOriginalAchievementObservations);
        GameSession debug = Session(Vitals());
        BattleSession shortcut = Battle(guardianForces: [new(3, 10, 10, true)]);
        shortcut.InstantVictory();
        debug.SetLiveBattle(shortcut);
        Assert.Empty(debug.State.PendingOriginalAchievementObservations);
    }

    [Fact]
    public void MissingCanonicalOwnersStayUnknownEvenWithAnAcquiredBattleGuardian()
    {
        GameSession session = Session(null);
        session.SetLiveBattle(Battle(guardianForces: [new(3, 10, 10, true)]));
        Assert.Empty(session.State.PendingOriginalAchievementObservations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AcceptedNativeProofIsDurableAndDebugVictoryCannotPublishIt(bool debug)
    {
        GameSession session = Session(null);
        GameState earlier = session.State;
        BattleSnapshot snapshot = Battle().Capture();
        BattleSession battle = BattleSession.Restore(snapshot with { NativeScriptState = snapshot.NativeScriptState with { UnlockedTutorials = [127] } });
        if (debug)
        {
            battle.InstantVictory();
        }
        session.SetLiveBattle(battle);
        if (debug)
        {
            Assert.Empty(session.State.PendingOriginalAchievementObservations);
            return;
        }
        OriginalAchievementObservedEvent fact = Assert.Single(session.State.PendingOriginalAchievementObservations);
        Assert.True(fact.Progress.OmegaProof);
        session.SetLiveBattle(battle);
        Assert.Single(session.State.PendingOriginalAchievementObservations);
        session.Restore(earlier);
        GameState saved = System.Text.Json.JsonSerializer.Deserialize<GameState>(System.Text.Json.JsonSerializer.Serialize(session.State))!;
        GameSession restarted = new(saved);
        Assert.Equal(fact, Assert.Single(restarted.State.PendingOriginalAchievementObservations));
        using AchievementService service = new(OriginalAchievementCatalog.CreateRegistry(), new MemoryRepository());
        service.Record(fact);
        Assert.True(service.Get(OriginalAchievementCatalog.Id("omega")).Unlocked);
    }

    private static GameSession Session(NativeCharacterVitals? vitals, NativeCardInventory? cards = null, NativeEconomy? economy = null) => new(new GameState
    {
        Scene = SceneKind.Field,
        FieldId = 74,
        CharacterVitals = vitals,
        CardInventory = cards,
        Economy = economy,
        PendingBattleEncounter = new("live-native-observation", 1, new string('a', 64), SceneKind.Field, 74, 0, new string('b', 64), null)
    });

    private static NativeCharacterVitals Vitals()
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Create();
        return vitals with { GuardianForces = vitals.GuardianForces.Select(gf => gf with { Flags = 0 }).ToArray() };
    }

    private static NativeCardInventory Cards(bool complete) => complete ? NativeCardInventory.CreateNewGame() with
    {
        Cards = Enumerable.Range(0, 110).Select(id => id < 77 ? (byte)0x81 : (byte)0xF0).ToArray(),
        ObtainedRareCards = [255, 255, 255, 255, 1]
    } : NativeCardInventory.CreateNewGame();

    private static NativeEconomy Economy(bool complete)
    {
        NativeItemStock[] items = Enumerable.Repeat(new NativeItemStock(0, 0), NativeEconomy.ItemSlotCount).ToArray();
        if (complete)
        {
            for (int index = 0; index < 22; index++)
            {
                items[index] = new((byte)(0xB1 + index), 1);
            }
        }
        return new() { Version = 1, Rule = NativeEconomy.InitializationRule, InitOutSha256 = new string('0', 64), Items = items, Gil = 0, PastGil = 0, EventBankGil72 = 0, EventBankPastGil76 = 0 };
    }

    private static BattleSession Battle(bool native = true, BattleGuardianForceVital[]? guardianForces = null, NativeCardInventory? cards = null, NativeItemStock[]? items = null, NativeCharacterVitals? magicVitals = null)
    {
        NativeBattleEnemyLevel level = new(100, 20, 5, 20, 5, 100, Enumerable.Repeat(800, 8).ToArray(), [], [], 0);
        return new(new BattleDefinition
        {
            EncounterId = 1,
            NativeEntry = native ? new(0x80, 0) : null,
            MagicVitals = magicVitals,
            GuardianForces = guardianForces ?? [],
            NativeCards = cards,
            NativeItemInventory = items,
            Combatants =
            [
                new() { Id = "party", Name = "Party", IsPlayer = true },
                new() { Id = "enemy", Name = "Enemy", SceneSlot = 0, NativeTemplate = new(1, Enumerable.Repeat(level, 101).ToArray()) { Information254 = 0 } }
            ]
        }, BattleAtbMode.TurnBased);
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
