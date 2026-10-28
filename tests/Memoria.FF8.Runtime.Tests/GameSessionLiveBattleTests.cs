using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class GameSessionLiveBattleTests
{
    [Fact]
    public void LivePublicationDefersPersistenceAndExportsRetainedMemoryAtReturn()
    {
        BattleSession battle = CreateBattle();
        battle.Tick();
        GameSession validated = GameSession.CreateColdProcess();
        GameSession live = GameSession.CreateColdProcess();

        validated.SetBattle(battle.Capture());
        live.SetLiveBattle(battle);

        Assert.Null(live.State.Battle);
        Assert.True(live.HasActiveBattle);
        Assert.NotNull(live.LiveBattle);
        Assert.Equal(JsonSerializer.Serialize(validated.State), JsonSerializer.Serialize(live.PrepareSnapshot()));
        NativeBattleRetainedMemory beforeReturn = live.State.NativeBattleMemory!;
        live.SetLiveBattle(null);
        Assert.Null(live.State.Battle);
        Assert.False(live.HasActiveBattle);
        Assert.Null(live.LiveBattle);
        Assert.NotNull(live.State.NativeBattleMemory);
        Assert.NotSame(beforeReturn, live.State.NativeBattleMemory);
    }

    [Fact]
    public void PublishedSnapshotRemainsIndependentOfLaterLiveCommands()
    {
        BattleSession battle = CreateBattle();
        GameSession live = GameSession.CreateColdProcess();
        live.SetLiveBattle(battle);
        BattleSnapshot published = live.PrepareSnapshot().Battle!;
        string before = JsonSerializer.Serialize(published);

        battle.InstantVictory();

        Assert.Equal(before, JsonSerializer.Serialize(published));
        live.SetLiveBattle(battle);
        Assert.Equal(BattleOutcome.Active, published.Outcome);
        Assert.Null(live.State.Battle);
        Assert.Equal((byte)4, live.LiveBattle!.NativeResult);
        BattleSnapshot current = live.PrepareSnapshot().Battle!;
        Assert.Equal(BattleOutcome.Victory, current.Outcome);
        Assert.NotSame(published.Definition.Combatants, current.Definition.Combatants);
    }

    [Fact]
    public void SnapshotPublicationStillRejectsInvalidExternalState()
    {
        GameSession session = GameSession.CreateColdProcess();
        BattleSnapshot invalid = CreateBattle().Capture() with { NativePartyStatusMirrors = null };

        Assert.Throws<InvalidDataException>(() => session.SetBattle(invalid));
        Assert.Null(session.State.Battle);
    }

    [Fact]
    public void DirectSnapshotPreparationCapturesAttachedBattleAndKeepsPresenceGuardsLive()
    {
        GameSession session = GameSession.CreateColdProcess();
        BattleSession battle = CreateBattle();
        session.SetLiveBattle(battle);
        using IDisposable binding = session.BindSnapshotPreparation(state => state with { FieldName = "prepared" });

        battle.Tick();
        GameState captured = session.PrepareSnapshot();

        Assert.Equal("prepared", captured.FieldName);
        Assert.Equal(battle.Frame, captured.Battle!.Frame);
        Assert.Null(session.State.Battle);
        Assert.True(session.HasActiveBattle);
        session.CompleteSnapshotPreparation(binding);
        Assert.Equal(battle.Frame, session.State.Battle!.Frame);
        Assert.True(session.HasActiveBattle);
    }

    [Fact]
    public void FaultAbandonmentDetachesAnUncapturableLiveBattleWithoutChangingPublishedState()
    {
        GameSession session = GameSession.CreateColdProcess();
        BattleSession battle = CreateBattle();
        session.SetLiveBattle(battle);
        GameState published = session.State;
        using IDisposable binding = session.BindSnapshotPreparation(state => state);
        using NativeBattleFileStagingTransaction transaction = new(NativeBattleFileStaging.Unknown());
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeFileStaging))!.SetValue(battle, transaction.View);
        Assert.Throws<InvalidOperationException>(() => session.PrepareSnapshot());

        session.AbandonSnapshotPreparation(binding);
        session.CompleteSnapshotPreparation(binding);

        Assert.Null(session.LiveBattle);
        Assert.False(session.HasActiveBattle);
        Assert.Same(published, session.State);
        Assert.Same(published, session.PrepareSnapshot());
    }

    [Fact]
    public void Reading_definition_copies_catalog_metadata_without_capturing_live_effect_storage()
    {
        BattleSession battle = CreateBattle();
        NativeBattleEffectMemory memory = battle.NativeEffectMemory!.BeginLoad(0);
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeEffectMemory))!.SetValue(battle, memory);
        long captures = memory.Arena!.Working.CaptureBytes;

        BattleDefinition first = battle.ReadDefinition();
        BattleDefinition second = battle.ReadDefinition();

        Assert.NotSame(first.Combatants, second.Combatants);
        Assert.NotSame(first.Combatants[0], second.Combatants[0]);
        Assert.Equal(first.Combatants[0].Id, second.Combatants[0].Id);
        Assert.Equal(captures, memory.Arena.Working.CaptureBytes);
        Assert.Null(first.NativeRetainedMemory?.EffectMemory?.Arena);
    }

    private static BattleSession CreateBattle() => new(new BattleDefinition
    {
        NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
        Combatants =
        [
            new() { Id = "party", IsPlayer = true, Name = "Party", MaximumHp = 1000, CurrentHp = 1000 },
            new() { Id = "enemy", Name = "Enemy", MaximumHp = 1000, CurrentHp = 1000 }
        ]
    }, BattleAtbMode.Normal);
}
