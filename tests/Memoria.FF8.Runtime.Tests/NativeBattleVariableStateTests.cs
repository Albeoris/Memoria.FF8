using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleVariableStateTests
{
    [Fact]
    public void ActualNewGameClearInitializesSavedBankAndWarmNewGameClearsItAgain()
    {
        GameSession session = GameSession.CreateColdProcess();
        Assert.Null(session.State.BattleVariables);
        session.StartNewGame();
        Assert.All(session.State.BattleVariables!.Values, value => Assert.Equal(0u, value));
        session.Restore(session.State with { BattleVariables = new(ImmutableArray.Create<uint>(0, 0, 0, 0, 2, 3, 0, 0)) });
        string checkpoint = JsonSerializer.Serialize(session.State);
        GameState restored = JsonSerializer.Deserialize<GameState>(checkpoint)!;
        Assert.Equal(2u, new GameSession(restored).State.BattleVariables!.Values[4]);
        session.StartNewGame();
        Assert.All(session.State.BattleVariables!.Values, value => Assert.Equal(0u, value));
    }

    [Fact]
    public void IncompleteSavedBankRejectsWithoutInventingMissingWords()
    {
        Assert.Throws<InvalidDataException>(() => new GameSession(new GameState { BattleVariables = new(ImmutableArray.Create<uint>(0, 0)) }));
        Assert.Null(new GameSession(new GameState()).State.BattleVariables);
    }
}
