using System.Text.Json;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class OriginalAchievementActionTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void StockReceiptSurvivesRestoreWithoutBeingPreparedAgain(int initialStock)
    {
        BattleSession battle = Create(initialStock);
        battle.QueueAction("party", "source", BattleAbility.Draw, 1);
        Assert.Empty(battle.Capture().OriginalStockDrawOperations!);
        battle.Tick();
        BattleSnapshot prepared = battle.Capture();
        BattleOriginalStockDrawOperation receipt = Assert.Single(prepared.OriginalStockDrawOperations!);
        Assert.Equal(1, receipt.Ordinal);
        Assert.InRange(receipt.Result.Quantity, (byte)1, (byte)9);
        Assert.Equal(initialStock == 100 ? 0 : receipt.Result.Quantity, receipt.Result.Added);
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(prepared))!);
        for (int frame = 0; frame < 25; frame++)
        {
            restored.Tick();
        }
        Assert.Equal(receipt, Assert.Single(restored.Capture().OriginalStockDrawOperations!));
    }

    [Fact]
    public void DrawCastDoesNotProduceAStockOperation()
    {
        BattleSession battle = Create(0);
        battle.QueueAction("party", "source", BattleAbility.Draw, 1, true, drawCastTargetId: "source");
        battle.Tick();
        Assert.Empty(battle.Capture().OriginalStockDrawOperations!);
    }

    [Fact]
    public void MissingLegacyLedgerDoesNotReconstructInventoryHistory()
    {
        BattleSession battle = Create(100);
        BattleSnapshot legacy = battle.Capture() with { OriginalStockDrawOperations = null };
        BattleSession restored = BattleSession.Restore(legacy);
        Assert.Empty(restored.Capture().OriginalStockDrawOperations!);
        restored.QueueAction("party", "source", BattleAbility.Draw, 1);
        restored.Tick();
        Assert.Equal(1, Assert.Single(restored.Capture().OriginalStockDrawOperations!).Ordinal);
    }

    [Fact]
    public void RestoreRejectsNoncontiguousOperationsAndInvalidCombatantOwners()
    {
        BattleSnapshot snapshot = Create(0).Capture();
        BattleOriginalStockDrawOperation operation = new(1, "party", "source", new(1, 4, 4, false));
        foreach (BattleOriginalStockDrawOperation invalid in new[] { operation with { Ordinal = 2 }, operation with { ActorId = "source" }, operation with { SourceId = "party" }, operation with { Result = new(1, 4, 5, false) } })
        {
            Assert.Throws<InvalidDataException>(() => BattleSession.Restore(snapshot with { OriginalStockDrawOperations = [invalid] }));
        }
    }

    [Fact]
    public void AcceptedActionKeepsCapturedOwnerAndRequiresMatchingReceipt()
    {
        AchievementOwner owner = new("test", "original", "first", "profile");
        GameSession session = new(new GameState());
        session.CaptureAchievementOwner = () => owner;
        session.AcceptOriginalAchievementAction(OriginalAchievementActionKind.FinishGame, "finish", "movie", 6999);
        OriginalAchievementActionEvent fact = Assert.Single(session.State.PendingOriginalAchievementActions);
        owner = owner with { User = "second" };
        session.AcceptOriginalAchievementAction(OriginalAchievementActionKind.FinishGame, "finish", "movie", 6999);
        Assert.Equal("first", Assert.Single(session.State.PendingOriginalAchievementActions).Owner!.User);
        Assert.Throws<InvalidDataException>(() => session.AcceptOriginalAchievementAction(OriginalAchievementActionKind.FinishGame, "finish", "movie", 7000));
        Assert.Single(session.State.PendingOriginalAchievementActions);
        session.AcknowledgeOriginalAchievementActions([fact with { Owner = owner }]);
        Assert.Single(session.State.PendingOriginalAchievementActions);
        session.AcknowledgeOriginalAchievementActions([fact]);
        Assert.Empty(session.State.PendingOriginalAchievementActions);
    }

    [Fact]
    public void LivePublicationStagesEachStockOperationOnceAndKeepsCursorAfterAcknowledgment()
    {
        BattleSession battle = Create(100);
        GameSession session = new(new GameState { Scene = SceneKind.Field, FieldId = 74, PendingBattleEncounter = Encounter(), Battle = battle.Capture() });
        battle.QueueAction("party", "source", BattleAbility.Draw, 1);
        battle.Tick();
        session.SetLiveBattle(battle);
        OriginalAchievementActionEvent fact = Assert.Single(session.State.PendingOriginalAchievementActions);
        Assert.Equal(OriginalAchievementActionKind.StockDraw, fact.Kind);
        Assert.True(fact.StockDraw!.Quantity > 0);
        Assert.Equal(0, fact.StockDraw.Added);
        session.SetLiveBattle(battle);
        Assert.Single(session.State.PendingOriginalAchievementActions);
        session.AcknowledgeOriginalAchievementActions([fact]);
        GameState checkpoint = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!;
        GameSession restored = new(checkpoint);
        restored.SetBattle(checkpoint.Battle);
        Assert.Empty(restored.State.PendingOriginalAchievementActions);
        Assert.Equal(1, restored.State.OriginalAchievementDrawCursor!.Operations);
        Assert.Throws<InvalidDataException>(() => restored.SetBattle(Create(100).Capture()));
        Assert.Equal(1, restored.State.OriginalAchievementDrawCursor.Operations);
    }

    [Fact]
    public void LegacyAcceptedBattleSeedsCursorWithoutHistoricalActionBackfill()
    {
        BattleSession battle = Create(0);
        battle.QueueAction("party", "source", BattleAbility.Draw, 1);
        battle.Tick();
        GameSession session = new(new GameState { Scene = SceneKind.Field, FieldId = 74, PendingBattleEncounter = Encounter(), Battle = battle.Capture() });
        session.SetLiveBattle(battle);
        Assert.Empty(session.State.PendingOriginalAchievementActions);
        Assert.Equal(1, session.State.OriginalAchievementDrawCursor!.Operations);
    }

    [Fact]
    public void ReexecutedStockDrawUsesNewLiveBranchWhilePreservingAcceptedPendingFacts()
    {
        BattleSession battle = Create(0);
        GameSession session = new(new GameState { Scene = SceneKind.Field, FieldId = 74, PendingBattleEncounter = Encounter(), Battle = battle.Capture() });
        session.SetLiveBattle(battle);
        GameState beforeDraw = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!;

        battle.QueueAction("party", "source", BattleAbility.Draw, 1);
        battle.Tick();
        session.SetLiveBattle(battle);
        OriginalAchievementActionEvent first = Assert.Single(session.State.PendingOriginalAchievementActions);

        session.Restore(beforeDraw);
        Assert.Equal(first, Assert.Single(session.State.PendingOriginalAchievementActions));
        BattleSession rewound = BattleSession.Restore(beforeDraw.Battle!);
        rewound.QueueAction("party", "source", BattleAbility.Draw, 1);
        rewound.Tick();
        session.SetLiveBattle(rewound);
        Assert.Equal(2, session.State.PendingOriginalAchievementActions.Length);
        Assert.Contains(first, session.State.PendingOriginalAchievementActions);
        OriginalAchievementActionEvent second = Assert.Single(session.State.PendingOriginalAchievementActions, fact => fact != first);
        Assert.Equal(first.EventId, second.EventId);
        Assert.NotEqual(first.ContextId, second.ContextId);
        Assert.Equal(first.StockDraw, second.StockDraw);
        session.SetLiveBattle(rewound);
        Assert.Equal(2, session.State.PendingOriginalAchievementActions.Length);

        GameSession cold = new(beforeDraw);
        BattleSession coldBattle = BattleSession.Restore(beforeDraw.Battle!);
        coldBattle.QueueAction("party", "source", BattleAbility.Draw, 1);
        coldBattle.Tick();
        cold.SetLiveBattle(coldBattle);
        OriginalAchievementActionEvent third = Assert.Single(cold.State.PendingOriginalAchievementActions);
        Assert.Equal(first.EventId, third.EventId);
        Assert.NotEqual(first.ContextId, third.ContextId);
        Assert.NotEqual(second.ContextId, third.ContextId);
        Assert.Equal(first.StockDraw, third.StockDraw);
    }

    private static BattleEncounterOccurrence Encounter() => new("stock-encounter", 1, new string('a', 64), SceneKind.Field, 74, 0, new string('b', 64), null);

    [Theory]
    [InlineData(9999, true, true, true)]
    [InlineData(9977, true, true, false)]
    [InlineData(9999, false, true, false)]
    [InlineData(9999, true, false, false)]
    public void MaximumHpRequiresAnActiveNativePlayableStatisticOwner(int maximumHp, bool active, bool native, bool expected)
    {
        BattleSession battle = NativeMaximumHpBattle(maximumHp, active, native);
        GameSession session = new(new GameState { Scene = SceneKind.Field, FieldId = 74, PendingBattleEncounter = Encounter() });
        session.SetLiveBattle(battle);
        Assert.Equal(expected ? 1 : 0, session.State.PendingOriginalAchievementActions.Count(fact => fact.Kind == OriginalAchievementActionKind.MaximumHitPoints));
        session.SetLiveBattle(battle);
        Assert.Equal(expected ? 1 : 0, session.State.PendingOriginalAchievementActions.Count(fact => fact.Kind == OriginalAchievementActionKind.MaximumHitPoints));
    }

    [Fact]
    public void MaximumHpExplicitSnapshotOperandIsValidatedWhileMissingLegacyOperandRemainsUnknown()
    {
        BattleSnapshot snapshot = NativeMaximumHpBattle(9999, true, true).Capture();
        BattleCombatantSnapshot actor = snapshot.Combatants[0];
        Assert.Equal(9999, actor.NativeParticipant!.MaximumHitPoints);
        BattleCombatantSnapshot[] altered = snapshot.Combatants.ToArray();
        altered[0] = actor with { NativeParticipant = actor.NativeParticipant with { MaximumHitPoints = 9977 } };
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(snapshot with { Combatants = altered }));
        altered[0] = actor with { NativeParticipant = actor.NativeParticipant with { MaximumHitPoints = null } };
        Assert.Equal(9999, BattleSession.Restore(snapshot with { Combatants = altered }).Combatants[0].MaximumHp);
    }

    private static BattleSession NativeMaximumHpBattle(int maximumHp, bool active, bool native)
    {
        byte[] character = new byte[36];
        for (int offset = 12; offset < 36; offset += 4)
        {
            character[offset + 1] = 1;
            character[offset + 2] = 40;
            character[offset + 3] = 1;
        }
        ImmutableArray<ImmutableArray<byte>> spells = Enumerable.Range(0, 57).Select(_ => new byte[60].ToImmutableArray()).ToImmutableArray();
        ImmutableArray<ImmutableArray<byte>> passives = Enumerable.Range(0, 19).Select(_ => new byte[8].ToImmutableArray()).ToImmutableArray();
        NativeStatCurve curve = new(100, 0, 0, 255, 0);
        NativeStatModifier modifier = new(0, 9, 0);
        NativeStatDefinitions definitions = new(Enumerable.Repeat(curve, 11).ToImmutableArray(), new byte[57].ToImmutableArray(), Enumerable.Repeat(modifier, 19).ToImmutableArray(), Enumerable.Repeat(curve, 16).ToImmutableArray(), Enumerable.Repeat(modifier, 9).ToImmutableArray());
        BattleNativePlayerStatRules rules = new(0, 1, character.ToImmutableArray(), new byte[12].ToImmutableArray(), spells, passives, new byte[20].ToImmutableArray(), 0, definitions) { NativeStatusPassiveFlags = 0 };
        BattleSession battle = new(new BattleDefinition
        {
            EncounterId = 1,
            Combatants =
            [
                new() { Id = "party", Name = "Caster", IsPlayer = true, MagicRecordId = 0, MaximumHp = maximumHp, CurrentHp = 100, NativePlayerStatRules = native ? rules : null, NativeGrowth = new(checked((ushort)maximumHp), new byte[6].ToImmutableArray()) },
                new() { Id = "source", Name = "Source", MaximumHp = 100, CurrentHp = 100 }
            ]
        }, BattleAtbMode.TurnBased);
        battle.Combatants[0].ReprojectNativePlayerStatistics();
        battle.Combatants[0].IsActive = active;
        return battle;
    }

    private static BattleSession Create(int initialStock)
    {
        NativeMagicStock[] stocks = Enumerable.Repeat(new NativeMagicStock(0, 0), 32).ToArray();
        stocks[7] = initialStock == 0 ? new(0, 0) : new(1, checked((byte)initialStock));
        return new(new BattleDefinition
        {
            Combatants =
            [
                new() { Id = "party", Name = "Caster", IsPlayer = true, MaximumHp = 500, CurrentHp = 100, Magic = 100, Speed = 1, Level = 10, InitialAtb = BattleSession.AtbMaximum, Abilities = [BattleAbility.Draw], NativeMagicInventory = stocks },
                new() { Id = "source", Name = "Source", MaximumHp = 500, CurrentHp = 500, Level = 5, Speed = 1, NativeDrawSlots = [new(1, 0), new(0, 0), new(0, 0), new(0, 0)] }
            ],
            ActionDefinitions = [new(BattleAbility.Magic, 1, "Fire", BattleActionEffect.Damage, 20, false)]
        }, BattleAtbMode.TurnBased);
    }
}
