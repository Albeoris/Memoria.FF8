using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class GameSessionLiveBattleTests
{
    [Fact]
    public void LivePublicationMatchesValidatedSnapshotAndPreservesRetainedMemoryAfterReturn()
    {
        BattleSession battle = CreateBattle();
        battle.Tick();
        GameSession validated = GameSession.CreateColdProcess();
        GameSession live = GameSession.CreateColdProcess();

        validated.SetBattle(battle.Capture());
        live.SetLiveBattle(battle);

        Assert.Equal(JsonSerializer.Serialize(validated.State), JsonSerializer.Serialize(live.State));
        NativeBattleRetainedMemory retained = live.State.NativeBattleMemory!;
        live.SetLiveBattle(null);
        Assert.Null(live.State.Battle);
        Assert.Same(retained, live.State.NativeBattleMemory);
    }

    [Fact]
    public void PublishedSnapshotRemainsIndependentOfLaterLiveCommands()
    {
        BattleSession battle = CreateBattle();
        GameSession live = GameSession.CreateColdProcess();
        live.SetLiveBattle(battle);
        BattleSnapshot published = live.State.Battle!;
        string before = JsonSerializer.Serialize(published);

        battle.InstantVictory();

        Assert.Equal(before, JsonSerializer.Serialize(published));
        live.SetLiveBattle(battle);
        Assert.Equal(BattleOutcome.Active, published.Outcome);
        Assert.Equal(BattleOutcome.Victory, live.State.Battle!.Outcome);
        Assert.NotSame(published.Definition.Combatants, live.State.Battle.Definition.Combatants);
    }

    [Fact]
    public void SnapshotPublicationStillRejectsInvalidExternalState()
    {
        GameSession session = GameSession.CreateColdProcess();
        BattleSnapshot invalid = CreateBattle().Capture() with { NativePartyStatusMirrors = null };

        Assert.Throws<InvalidDataException>(() => session.SetBattle(invalid));
        Assert.Null(session.State.Battle);
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
