using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativePlayerDrawTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(12, false)]
    [InlineData(98, false)]
    [InlineData(99, false)]
    [InlineData(100, false)]
    [InlineData(0, true)]
    [InlineData(12, true)]
    [InlineData(98, true)]
    [InlineData(99, true)]
    [InlineData(100, true)]
    public void StockMessageIncludesFinalPoolAndPreservesResourceTextAfterRestore(int initialStock, bool localized)
    {
        BattleSession battle = Create(initialStock, localized);
        battle.QueueAction("party", "source", BattleAbility.Draw, 1);
        battle.Tick();
        BattleSnapshot prepared = battle.Capture();
        BattleNativeDrawContext draw = prepared.ActiveAction!.NativeDraw!;
        int total = Math.Min(100, initialStock + draw.Quantity);
        Assert.Equal(total - initialStock, draw.Added);
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(prepared))!);

        Complete(battle);
        Complete(restored);

        string expected = localized ? $"Выкачали {draw.Added} ({total}/100) Fire{(draw.Added == 1 ? "." : "!")}" : $"Caster stocks {draw.Added} ({total}/100) Fire.";
        Assert.Equal(total, Assert.Single(battle.Combatants[0].MagicStocks).Count);
        Assert.Equal(expected, Assert.Single(battle.Events, entry => entry.Kind == "Draw").Message);
        Assert.Equal(expected, Assert.Single(restored.Events, entry => entry.Kind == "Draw").Message);
        if (localized)
        {
            Assert.Equal(expected, Assert.Single(battle.Events, entry => entry.Kind == "NativeText").Message);
            Assert.Equal(expected, Assert.Single(restored.Events, entry => entry.Kind == "NativeText").Message);
        }
    }

    [Fact]
    public void StockIsPreparedOnceBeforeWindupAndPreservesPhysicalStockSlots()
    {
        BattleSession battle = Create();
        battle.QueueAction("party", "source", BattleAbility.Draw, 1);
        Assert.Equal(2, battle.Combatants[0].NativeMagicSlots[7].Count);
        battle.Tick();
        BattleSnapshot prepared = battle.Capture();
        BattleNativeDrawContext draw = Assert.IsType<BattleNativeDrawContext>(prepared.ActiveAction!.NativeDraw);
        Assert.InRange(draw.Quantity, (byte)1, (byte)9);
        Assert.Equal(2 + draw.Added, battle.Combatants[0].NativeMagicSlots[7].Count);
        Assert.Equal(0, battle.Combatants[0].NativeMagicSlots[0].SpellId);
        BattleSession restored = BattleSession.Restore(prepared);
        Complete(battle);
        Complete(restored);
        Assert.Equal(2 + draw.Added, battle.Combatants[0].NativeMagicSlots[7].Count);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    [Fact]
    public void DrawCastHealingUsesSeparateAllyAndDoesNotChangeCasterStock()
    {
        BattleSession battle = Create();
        battle.QueueAction("party", "source", BattleAbility.Draw, 2, true, drawCastTargetId: "ally");
        battle.Tick();
        BattleSession restored = BattleSession.Restore(battle.Capture());
        Complete(restored);
        Assert.True(restored.Combatants.Single(actor => actor.Id == "ally").CurrentHp > 10);
        Assert.Equal(500, restored.Combatants.Single(actor => actor.Id == "source").CurrentHp);
        Assert.Equal(2, Assert.Single(restored.Combatants[0].MagicStocks).Count);
    }

    [Fact]
    public void DrawCastCanReviveADifferentAlly()
    {
        BattleSession battle = Create();
        battle.QueueAction("party", "source", BattleAbility.Draw, 3, true, drawCastTargetId: "fallen");
        Complete(battle);
        Assert.Equal(12, battle.Combatants.Single(actor => actor.Id == "fallen").CurrentHp);
    }

    [Fact]
    public void GuardianForceDrawPreservesSavedHpAndRemovesAllSourceSlotsOnlyOnCompletion()
    {
        BattleSession battle = Create();
        NativeBattleRandomState random = battle.Capture().Random!;
        battle.QueueAction("party", "source", BattleAbility.Draw, 65);
        battle.Tick();
        Assert.True(Assert.Single(battle.GuardianForces).Acquired);
        Assert.Equal((ushort)1000, Assert.Single(battle.GuardianForces).HitPoints);
        Assert.Contains(1, battle.Combatants.Single(actor => actor.Id == "source").DrawGuardianForces);
        Assert.Equal(random.Indexes.ToArray(), battle.Capture().Random!.Indexes.ToArray());
        BattleSession restored = BattleSession.Restore(battle.Capture());
        Complete(restored);
        Assert.Empty(restored.Combatants.Single(actor => actor.Id == "source").DrawGuardianForces);
        Assert.Empty(restored.Combatants.Single(actor => actor.Id == "source2").DrawGuardianForces);
        Assert.Equal(new byte[] { 1 }, restored.NativeScriptState.AcquiredGuardianForces);
        Assert.Equal((ushort)1000, Assert.Single(restored.GuardianForces).HitPoints);
        Assert.Equal((byte)6, restored.Capture().Combatants.Single(actor => actor.Id == "source").NativeParticipant!.LastAction!.Command);
        Assert.Equal((byte)65, restored.Capture().Combatants.Single(actor => actor.Id == "source").NativeParticipant!.LastAction!.ActionId);
    }

    [Fact]
    public void DrawCastValidationIsAtomicAndRequiresExplicitRecipient()
    {
        BattleSession battle = Create();
        string before = JsonSerializer.Serialize(battle.Capture());
        Assert.Throws<InvalidOperationException>(() => battle.QueueAction("party", "source", BattleAbility.Draw, 2, true));
        Assert.Throws<InvalidOperationException>(() => battle.QueueAction("party", "source", BattleAbility.Draw, 2, true, drawCastTargetId: "source"));
        Assert.Throws<InvalidOperationException>(() => battle.QueueAction("party", "source", BattleAbility.Draw, 65, true, drawCastTargetId: "party"));
        Assert.Equal(before, JsonSerializer.Serialize(battle.Capture()));
    }

    [Fact]
    public void RestoreRejectsMissingPhysicalSourceSlotsAndMissingPreparedResult()
    {
        BattleSession battle = Create();
        BattleSnapshot original = battle.Capture();
        BattleCombatantSnapshot[] actors = original.Combatants.ToArray();
        actors[0] = actors[0] with { NativeDrawSlots = null };
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(original with { Combatants = actors }));
        foreach (byte[]? invalid in new byte[]?[] { [0], [64], [65], [1, 1], null })
        {
            actors = original.Combatants.ToArray();
            actors[3] = actors[3] with { DrawnMagicIds = invalid! };
            Assert.Throws<InvalidDataException>(() => BattleSession.Restore(original with { Combatants = actors }));
        }
        battle.QueueAction("party", "source", BattleAbility.Draw, 65);
        battle.Tick();
        BattleSnapshot prepared = battle.Capture();
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(prepared with { ActiveAction = prepared.ActiveAction! with { NativeDraw = null } }));
    }

    [Fact]
    public void ImmediateCounterSeesTheGuardianForceSourceAndQueuedCounterObservesItsCompletionRefresh()
    {
        BattleDefinition definition = Create().Capture().Definition;
        BattleCombatantDefinition[] actors = definition.Combatants.ToArray();
        byte[] immediate = [2, 17, 0, 0, 0, 0, 3, 0, 15, 97, 1, 0];
        byte[] pending = [15, 99, 1, 2, 17, 0, 0, 0, 0, 3, 0, 15, 98, 1, 0];
        actors[3] = actors[3] with { NativeCombatantFlags = 0x10, NativeAi = new([[0], [0], pending, [0], immediate], Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, []) };
        BattleSession battle = new(definition with { Combatants = actors }, BattleAtbMode.TurnBased);
        battle.QueueAction("party", "source", BattleAbility.Draw, 65);
        Complete(battle);
        for (int callback = 0; callback < 100 && (battle.ActiveAction is not null || battle.QueuedActions.Count != 0); callback++)
        {
            battle.Tick();
        }
        Assert.Equal(1u, battle.Capture().NativeAiVariables![1]);
        //47DE70 queues callback2 through484720, then48B8B0/48C7A0 clears acquired GF entries before that callback executes.
        Assert.Equal(0u, battle.Capture().NativeAiVariables![2]);
        Assert.Equal(1u, battle.Capture().NativeAiVariables![3]);
        Assert.Empty(battle.Combatants[3].DrawGuardianForces);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SuccessfulStockAndCastConsumeTheSameSpellForEveryCaster(bool castFirst)
    {
        BattleSession battle = CreateWithSecondCaster();
        battle.QueueAction("party", "source", BattleAbility.Draw, 2, castFirst, drawCastTargetId: castFirst ? "ally" : null);
        battle.Tick();
        BattleCombatant source = battle.Combatants.Single(actor => actor.Id == "source");
        Assert.False(battle.ActiveAction!.NativeDraw!.Failed);
        Assert.Equal(new byte[] { 2 }, source.DrawnMagicIds);
        Assert.DoesNotContain(source.NativeDrawSlots, slot => slot.Id == 2);
        Complete(battle);

        string before = JsonSerializer.Serialize(battle.Capture());
        Assert.Throws<InvalidOperationException>(() => battle.QueueAction("ally", "source", BattleAbility.Draw, 2, !castFirst, drawCastTargetId: castFirst ? null : "party"));
        Assert.Equal(before, JsonSerializer.Serialize(battle.Capture()));
    }

    [Fact]
    public void ConsumedSpellLeavesOtherSpellsAndOtherMonstersIndependent()
    {
        BattleSession original = CreateWithSecondCaster();
        BattleDefinition definition = original.Capture().Definition;
        BattleCombatantDefinition[] actors = definition.Combatants.ToArray();
        actors[4] = actors[4] with { NativeDrawSlots = [new(1, 0), new(2, 0), new(0, 0), new(0, 0)] };
        BattleSession battle = new(definition with { Combatants = actors }, BattleAtbMode.TurnBased);
        battle.QueueAction("party", "source", BattleAbility.Draw, 1);
        battle.Tick();
        BattleCombatant source = battle.Combatants.Single(actor => actor.Id == "source");
        BattleCombatant other = battle.Combatants.Single(actor => actor.Id == "source2");
        Assert.Contains(source.NativeDrawSlots, slot => slot.Id == 2);
        Assert.Contains(other.NativeDrawSlots, slot => slot.Id == 1);
        Assert.Empty(other.DrawnMagicIds);
        Complete(battle);
        battle.Combatants[0].Atb = BattleSession.AtbMaximum;
        battle.QueueAction("party", "source", BattleAbility.Draw, 2);
        battle.Tick();
        Assert.False(battle.ActiveAction!.NativeDraw!.Failed);
        Assert.Equal(new byte[] { 1, 2 }, source.DrawnMagicIds);
        Complete(battle);
        battle.QueueAction("ally", "source2", BattleAbility.Draw, 1);
        battle.Tick();
        Assert.False(battle.ActiveAction!.NativeDraw!.Failed);
        Assert.Equal(new byte[] { 1 }, other.DrawnMagicIds);
    }

    [Fact]
    public void SuccessfulDrawConsumesEveryPhysicalEntryForThatSpell()
    {
        BattleSession battle = Create();
        BattleCombatant source = battle.Combatants.Single(actor => actor.Id == "source");
        source.RestoreDrawSlots([new(1, 0), new(1, 9), new(2, 3), new(65, 4)]);
        battle.QueueAction("party", "source", BattleAbility.Draw, 1);
        battle.Tick();
        Assert.Equal(new byte[] { 1 }, source.DrawnMagicIds);
        Assert.Equal(new byte[] { 0, 0, 2, 65 }, source.NativeDrawSlots.Select(slot => slot.Id));
        Assert.Equal(new byte[] { 0, 9, 3, 4 }, source.NativeDrawSlots.Select(slot => slot.Resistance));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ZeroQuantityLeavesSpellAvailableForAnotherAttempt(bool cast)
    {
        BattleSession battle = CreateWithSecondCaster();
        BattleCombatant source = battle.Combatants.Single(actor => actor.Id == "source");
        source.RestoreDrawSlots([new(2, 255), new(0, 0), new(0, 0), new(0, 0)]);
        battle.QueueAction("party", "source", BattleAbility.Draw, 2, cast, drawCastTargetId: cast ? "ally" : null);
        battle.Tick();
        Assert.True(battle.ActiveAction!.NativeDraw!.Failed);
        Assert.Equal(0, battle.ActiveAction.NativeDraw.Quantity);
        Assert.Empty(source.DrawnMagicIds);
        Assert.Equal(2, source.NativeDrawSlots[0].Id);
        Complete(battle);
        source.RestoreDrawSlots([new(2, 0), new(0, 0), new(0, 0), new(0, 0)]);
        battle.QueueAction("ally", "source", BattleAbility.Draw, 2, cast, drawCastTargetId: cast ? "party" : null);
        battle.Tick();
        Assert.False(battle.ActiveAction!.NativeDraw!.Failed);
        Assert.Equal(new byte[] { 2 }, source.DrawnMagicIds);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void QueuedContendersCannotStockOrCastTheSameSpellTwice(bool cast)
    {
        BattleSession battle = CreateWithSecondCaster();
        battle.QueueAction("party", "source", BattleAbility.Draw, 2, cast, drawCastTargetId: cast ? "ally" : null);
        battle.QueueAction("ally", "source", BattleAbility.Draw, 2, cast, drawCastTargetId: cast ? "party" : null);
        battle.Tick();
        Assert.False(battle.ActiveAction!.NativeDraw!.Failed);
        for (int frame = 0; frame < 60 && battle.ActiveAction?.ActorId != "ally"; frame++)
        {
            battle.Tick();
        }
        Assert.Equal("ally", battle.ActiveAction!.ActorId);
        Assert.True(battle.ActiveAction.NativeDraw!.Failed);
        Assert.Equal(0, battle.ActiveAction.NativeDraw.Quantity);
        Complete(battle);
        if (cast)
        {
            Assert.Equal(100, battle.Combatants[0].CurrentHp);
        }
        else
        {
            Assert.DoesNotContain(battle.Combatants[1].MagicStocks, stock => stock.Id == 2);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SerializedDepletionSurvivesActiveAndCompletedNativeTableRefresh(bool active)
    {
        BattleDefinition definition = CreateWithSecondCaster().Capture().Definition;
        BattleCombatantDefinition[] actors = definition.Combatants.ToArray();
        NativeBattleEnemyLevel level = new(500, 10, 10, 10, 10, 1, new int[8], [], [], 0);
        NativeBattleDrawSlot[] table = [new(1, 0), new(2, 0), new(3, 0), new(65, 0)];
        actors[3] = actors[3] with
        {
            NativeTemplate = new(5, Enumerable.Repeat(level, 101).ToArray()) { NativeDrawTables = [table, table, table] },
            NativeAi = new([[0], [0], [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, [])
        };
        BattleSession battle = new(definition with { Combatants = actors }, BattleAtbMode.TurnBased);
        battle.QueueAction("party", "source", BattleAbility.Draw, 1);
        battle.Tick();
        if (!active)
        {
            Complete(battle);
        }
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
        Complete(restored);
        BattleCombatant source = restored.Combatants.Single(actor => actor.Id == "source");
        Assert.Equal(new byte[] { 1 }, source.DrawnMagicIds);
        Assert.DoesNotContain(source.NativeDrawSlots, slot => slot.Id == 1);
        Assert.Contains(source.NativeDrawSlots, slot => slot.Id == 2);
        string before = JsonSerializer.Serialize(restored.Capture());
        Assert.Throws<InvalidOperationException>(() => restored.QueueAction("ally", "source", BattleAbility.Draw, 1));
        Assert.Equal(before, JsonSerializer.Serialize(restored.Capture()));
        BattleSession freshBattle = new(definition with { Combatants = actors }, BattleAtbMode.TurnBased);
        Assert.Empty(freshBattle.Combatants.Single(actor => actor.Id == "source").DrawnMagicIds);
        freshBattle.QueueAction("party", "source", BattleAbility.Draw, 1);
    }

    [Fact]
    public void OlderSnapshotWithoutDepletionTrackingStartsTrackingAtLoadWithoutInferringPriorDraws()
    {
        BattleSession battle = Create();
        battle.QueueAction("party", "source", BattleAbility.Draw, 1);
        Complete(battle);
        System.Text.Json.Nodes.JsonNode snapshot = JsonSerializer.SerializeToNode(battle.Capture())!;
        foreach (System.Text.Json.Nodes.JsonNode? actor in snapshot["Combatants"]!.AsArray())
        {
            actor!.AsObject().Remove("DrawnMagicIds");
        }
        BattleSession restored = BattleSession.Restore(snapshot.Deserialize<BattleSnapshot>()!);
        BattleCombatant source = restored.Combatants.Single(actor => actor.Id == "source");
        Assert.Empty(source.DrawnMagicIds);
        Assert.DoesNotContain(source.NativeDrawSlots, slot => slot.Id == 1);
        source.RestoreDrawSlots([new(1, 0), new(2, 0), new(3, 0), new(65, 0)]);
        restored.Combatants[0].Atb = BattleSession.AtbMaximum;
        restored.QueueAction("party", "source", BattleAbility.Draw, 1);
        restored.Tick();
        Assert.False(restored.ActiveAction!.NativeDraw!.Failed);
        Assert.Equal(new byte[] { 1 }, source.DrawnMagicIds);
    }

    private static BattleSession CreateWithSecondCaster()
    {
        BattleDefinition definition = Create().Capture().Definition;
        BattleCombatantDefinition[] actors = definition.Combatants.ToArray();
        actors[1] = actors[1] with { Magic = 100, InitialAtb = BattleSession.AtbMaximum, Abilities = [BattleAbility.Draw] };
        return new(definition with { Combatants = actors }, BattleAtbMode.TurnBased);
    }

    private static BattleSession Create(int initialStock = 2, bool localized = false)
    {
        NativeMagicStock[] stocks = Enumerable.Repeat(new NativeMagicStock(0, 0), 32).ToArray();
        stocks[7] = initialStock == 0 ? new(0, 0) : new(1, checked((byte)initialStock));
        string?[] messages = new string?[120];
        messages[8] = "!";
        messages[9] = "Выкачали";
        messages[11] = " 0123456789";
        messages[118] = ".";
        return new(new BattleDefinition
        {
            Name = "Native player Draw",
            NativeMessageSpeed = localized ? (byte)0 : null,
            NativeGlobalMessages = localized ? messages : [],
            Combatants =
            [
                new() { Id = "party", Name = "Caster", IsPlayer = true, MaximumHp = 500, CurrentHp = 100, Magic = 100, Speed = 1, Level = 10, InitialAtb = BattleSession.AtbMaximum, Abilities = [BattleAbility.Draw], NativeMagicInventory = stocks },
                new() { Id = "ally", Name = "Ally", IsPlayer = true, MaximumHp = 500, CurrentHp = 10, Speed = 1 },
                new() { Id = "fallen", Name = "Fallen", IsPlayer = true, MaximumHp = 100, CurrentHp = 0, NativeStatus0 = 1, Speed = 1 },
                new() { Id = "source", Name = "Source", MaximumHp = 500, CurrentHp = 500, Level = 5, Speed = 1, NativeDrawSlots = [new(1, 0), new(2, 0), new(3, 0), new(65, 0)] },
                new() { Id = "source2", Name = "Source2", MaximumHp = 500, CurrentHp = 500, Speed = 1, DrawGuardianForces = [1] }
            ],
            GuardianForces = [new(1, 1000, 100, false)],
            ActionDefinitions = [new(BattleAbility.Magic, 1, "Fire", BattleActionEffect.Damage, 20, false), new(BattleAbility.Magic, 2, "Cure", BattleActionEffect.Heal, 20, true), new(BattleAbility.Magic, 3, "Life", BattleActionEffect.Revive, 0, true)]
        }, BattleAtbMode.TurnBased);
    }

    private static void Complete(BattleSession battle)
    {
        for (int frame = 0; frame < 25; frame++)
        {
            battle.Tick();
        }
    }
}
