using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeMessageVariableTests
{
    [Fact]
    public void CurrentEightDwordsRoundTripWithoutChangingSignedBitsAndUseNativeUnsignedFormats()
    {
        NativeMessageVariables bank = NativeMessageVariables.FromColdProcess().WithWrite(1, -1);
        Assert.Equal("4294967295", NativeMessageVariables.Format(49, bank));
        Assert.Equal("4,294,967,295", NativeMessageVariables.Format(33, bank));
        Assert.Equal("FFFFFFFF", NativeMessageVariables.Format(65, bank));
        Assert.Equal("0", NativeMessageVariables.Format(48, bank));
        Assert.Equal("00000000", NativeMessageVariables.Format(64, bank));
        Assert.Equal("", NativeMessageVariables.Format(47, null));
        Assert.Equal(bank, JsonSerializer.Deserialize<NativeMessageVariables>(JsonSerializer.Serialize(bank)));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeMessageVariables>("{\"Slot0\":0}"));
        Assert.Equal("4294967295/4294967295", NativeMessageVariables.Expand("{Var 49}/{Var49}", bank));
        Assert.Throws<NotSupportedException>(() => NativeMessageVariables.Format(49, NativeMessageVariables.Unknown()));
    }

    [Fact]
    public void OnlyExplicitColdProcessRootsKnownZeroBankAndWarmNewGamePreservesPriorValues()
    {
        GameSession cold = GameSession.CreateColdProcess();
        cold.StartNewGame();
        Assert.True(cold.ReplayJournal!.IsComplete);
        Assert.Equal(NativeMessageVariables.FromColdProcess(), cold.MessageVariables);
        using (cold.BeginReplayCapture())
        {
            cold.SetMessageVariables(cold.MessageVariables!.WithWrite(7, int.MinValue));
        }
        cold.StartNewGame();
        Assert.Equal(int.MinValue, cold.MessageVariables!.At(7));
        Assert.False(cold.ReplayJournal!.IsComplete);
        GameSession unknown = new(new GameState { ExamTimer = NativeExamTimerState.FromColdProcess() });
        unknown.StartNewGame();
        Assert.Null(unknown.MessageVariables);
        Assert.False(unknown.ReplayJournal!.IsComplete);
    }
}
