using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class Ff8CompletionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TerminalCreditsPublishesOneReceiptAndPreservesProgression(bool skipped)
    {
        GameSession session = new(TerminalState(skipped));
        GameState before = session.State;
        Assert.True(Complete(session));
        Ff8CompletionState completion = Assert.IsType<Ff8CompletionState>(session.State.Ff8Completion);
        completion.Validate();
        Assert.Equal(skipped, completion.CreditsSkipped);
        Assert.Equal(3064, completion.CreditsTerminalFrame);
        Assert.Same(before.CharacterVitals, session.State.CharacterVitals);
        Assert.Same(before.Economy, session.State.Economy);
        Assert.Same(before.CardInventory, session.State.CardInventory);
        Assert.Equal((byte)0x87, session.State.CardInventory!.Cards[0]);
        Assert.Same(before.EventMemory, session.State.EventMemory);
        Assert.False(Complete(session));
        Assert.Same(completion, session.State.Ff8Completion);
        GameSession restored = new(session.State);
        Assert.False(Complete(restored));
        Assert.Same(completion, restored.State.Ff8Completion);
    }

    [Fact]
    public void RequestsAndUnrelatedMovieCompletionNeverCompleteTheGame()
    {
        GameState terminal = TerminalState(false);
        GameState[] rejected =
        [
            terminal with { Scene = SceneKind.Movie, MoviePlayback = new(0, 3064, true, false, false) },
            terminal with { MoviePlayback = null },
            terminal with { MoviePlayback = new(3064, 3064, false, false, false) },
            terminal with { MoviePlayback = new(0, 0, false, true, false) },
            terminal with { MovieSource = new(3, 4) },
            terminal with { MovieSource = new(2, 5) },
            terminal with { FieldId = 75 }
        ];
        foreach (GameState state in rejected)
        {
            Assert.False(Ff8CompletionState.IsTerminal(state, Ff8CompletionState.OriginalEndingProgramHash, 0, 1, 42, true));
        }
    }

    [Theory]
    [InlineData(1, 1, 42, true)]
    [InlineData(0, 2, 42, true)]
    [InlineData(0, 1, 41, true)]
    [InlineData(0, 1, 42, false)]
    public void OnlyTheOriginalCreditsTerminalInstructionCompletes(int objectId, int scriptId, int operation, bool nativeHalt)
    {
        GameSession session = new(TerminalState(false));
        Assert.False(session.TryCompleteFf8Ending(Ff8CompletionState.OriginalEndingProgramHash, 20054, objectId, scriptId, operation, nativeHalt));
        Assert.Null(session.State.Ff8Completion);
    }

    [Fact]
    public void ChangedEndingProgramDoesNotManufactureCompletion()
    {
        GameSession session = new(TerminalState(false));
        Assert.False(session.TryCompleteFf8Ending(new string('0', 64), 20054, 0, 1, 42, true));
        Assert.Null(session.State.Ff8Completion);
    }

    [Fact]
    public void InvalidReceiptCannotBePublished()
    {
        GameSession session = new(TerminalState(false));
        GameState before = session.State;
        Assert.Throws<InvalidDataException>(() => session.TryCompleteFf8Ending(Ff8CompletionState.OriginalEndingProgramHash, -1, 0, 1, 42, true));
        Assert.Same(before, session.State);
    }

    private static GameState TerminalState(bool skipped)
    {
        NativeCardInventory cards = NativeCardInventory.CreateNewGame();
        cards.Cards[0] = 0x87;
        return new GameState { Scene = SceneKind.Field, FieldId = 76, MovieSource = new(3, 5), MoviePlayback = new(3064, 3064, false, true, skipped), CardInventory = cards };
    }

    private static bool Complete(GameSession session)
    {
        return session.TryCompleteFf8Ending(Ff8CompletionState.OriginalEndingProgramHash, 20054, 0, 1, 42, true);
    }
}
