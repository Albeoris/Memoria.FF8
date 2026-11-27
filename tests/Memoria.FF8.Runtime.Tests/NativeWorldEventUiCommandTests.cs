using System.Text.Json;
using System.Text.Json.Nodes;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeWorldEventUiCommandTests
{
    [Theory]
    [InlineData(0x0000, 0)]
    [InlineData(0x010E, 14)]
    [InlineData(0xFFFF, 255)]
    public void ScriptModeWritesLowByteAndCloseSuppressionResetsOnlyAtNextCallback(int operand, int mode)
    {
        NativeWorldSalaryState before = NativeWorldSalaryState.Begin(Departure());
        NativeWorldSalaryState written = before.WithWorldEventCommand(0xFF26, (ushort)operand).WithWorldEventCommand(0xFF36, 0xFFFF);
        Assert.Equal((byte)mode, written.EventUi!.ScriptMode);
        Assert.True(written.EventUi.CloseSuppressedThisCallback);
        Assert.Equal(NativeWorldEventUi.Create(), before.EventUi);
        NativeWorldSalaryState next = written.BeginWorldEventCallback();
        Assert.Equal((byte)mode, next.EventUi!.ScriptMode);
        Assert.False(next.EventUi.CloseSuppressedThisCallback);
        Assert.True(written.EventUi.CloseSuppressedThisCallback);
    }

    [Fact]
    public void AuthoredOpenFlagAndCloseCommandsStageTogetherAndRestoreWithoutInventingTextReadiness()
    {
        GameState source = Departure();
        NativeWorldMessage message = new(9, 52, 0, 0, 0, 32, 16, new string('A', 64), false);
        NativeWorldSalaryState opened = NativeWorldSalaryState.Begin(source).WithWorldEventCommand(0xFF26, 14).WithWorldEventCommand(0xFF28, 0x013F).WithWorldEventCommand(0xFF28, 0x0104).WithWorldEventCommand(0xFF36, 0).WithWorldEventCommand(0xFF1F, 0x3409, message);
        Assert.True(opened.SavedWorldEvents!.WorldEventFlags116!.Matches(0x0104));
        Assert.True(opened.SavedWorldEvents.WorldEventFlags116.Matches(0x013F));
        Assert.False(opened.SavedWorldEvents.WorldEventFlags116.IsKnown(3));
        Assert.False(opened.EventUi!.MatchesTextComplete(9, 1));
        Assert.True(opened.EventUi.MatchesTextComplete(9, 0));
        NativeWorldSalaryState next = opened.BeginWorldEventCallback();
        Assert.Null(next.EventUi!.MatchesTextComplete(9, 1));
        Assert.False(next.EventUi.MatchesTextComplete(9, 2));
        Assert.Null(next.WithWorldEventCommand(0xFF1F, 0x3409, message).EventUi!.MatchesTextComplete(9, 1));
        NativeWorldEventUi restored = JsonSerializer.Deserialize<NativeWorldEventUi>(JsonSerializer.Serialize(next.EventUi))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(next.EventUi.BeginCallback()), JsonSerializer.Serialize(restored.BeginCallback()));
        NativeWorldSalaryState closed = next.WithWorldEventCommand(0xFF36, 0).WithWorldEventCommand(0xFF24, 9).WithWorldEventCommand(0xFF24, 11).WithWorldEventCommand(0xFF28, 63).WithWorldEventCommand(0xFF26, 0);
        Assert.Empty(closed.EventUi!.Windows);
        Assert.Equal((byte)0, closed.EventUi.ScriptMode);
        Assert.True(closed.EventUi.CloseSuppressedThisCallback);
        Assert.True(closed.SavedWorldEvents!.WorldEventFlags116!.Matches(0x0104));
        Assert.True(closed.SavedWorldEvents.WorldEventFlags116.Matches(63));
        Assert.Equal(NativeWorldEventUi.Create(), source.World!.EventUi);
        Assert.True(source.SavedWorld!.WorldEventFlags116!.Matches(4));
        WorldState final = source.World with { Frame = 1 };
        GameState published = opened.Advance(null, null, null, 0, 0).ApplyTo(source with { World = final }, final);
        Assert.Equal(opened.EventUi, published.World!.EventUi);
        Assert.Equal(opened.SavedWorldEvents.WorldEventFlags116, published.SavedWorld!.WorldEventFlags116);
        Assert.Throws<NotSupportedException>(() => next.WithWorldEventCommand(0xFF24, 265));
        Assert.Throws<NotSupportedException>(() => next.WithWorldEventCommand(0xFF1F, 0x3409, message with { MessageId = 51 }));
        Assert.Equal((byte)52, Assert.Single(next.EventUi.Windows).MessageId);
        Assert.Throws<NotSupportedException>(() => NativeWorldSalaryState.Begin(source with { World = source.World! with { EventUi = null } }).WithWorldEventCommand(0xFF26, 14));
    }

    [Fact]
    public void ModeFourteenRetainsNativeNonzeroMotorDampingWithoutOrdinaryCameraFollowing()
    {
        NativeRagnarokMotorState before = new(80, 128, 64, -96, -700, 2048, -300, 24, 112);
        NativeRagnarokMotorResult after = NativeRagnarokMotor.StepScripted(before, 14);
        Assert.Equal(64, after.State.Speed);
        Assert.Equal((short)48, after.State.BodyPitch);
        Assert.Equal((short)-64, after.State.BodyRoll);
        Assert.Equal(before.CameraYaw, after.State.CameraYaw);
        Assert.Equal(before.CameraPitch, after.State.CameraPitch);
        Assert.Equal(before.CameraFollowSpeed, after.State.CameraFollowSpeed);
        Assert.Equal(NativeRagnarokMotor.StepScripted(before, 6), after);
    }

    [Fact]
    public void ChoiceRequiresCompletedTextAndFreshConfirmAndRetainsItsCursorAcrossRestore()
    {
        NativeWorldMessage message = ChoiceMessage(11, 58);
        NativeWorldEventUi opened = NativeWorldEventUi.Create().ShowChoice(message);
        Assert.True(opened.HasPendingChoice);
        Assert.False(opened.CanAcceptChoice);
        Assert.Same(opened, opened.AdvanceChoice(0x40, 0));
        NativeWorldEventUi ready = opened.AdvanceText(8);
        Assert.True(ready.CanAcceptChoice);
        Assert.Same(ready, ready.AdvanceChoice(0x40, 0x40));
        NativeWorldEventUi second = ready.AdvanceChoice(0x1000, 0);
        Assert.Equal((byte)2, second.FocusedChoice!.Selection);
        Assert.Equal((byte)1, second.AdvanceChoice(0x4000, 0).FocusedChoice!.Selection);
        NativeWorldEventUi cancelled = ready.AdvanceChoice(0x10, 0);
        Assert.Equal((byte)2, cancelled.FocusedChoice!.Selection);
        Assert.Equal(-1, cancelled.ReadChoiceResult(11));

        NativeWorldEventUi restored = JsonSerializer.Deserialize<NativeWorldEventUi>(JsonSerializer.Serialize(cancelled))!;
        restored.Validate();
        Assert.Equal(cancelled.FocusedChoice, restored.FocusedChoice);
        NativeWorldEventUi accepted = restored.AdvanceChoice(0x40, 0);
        Assert.Equal(1, accepted.ReadChoiceResult(11));
        Assert.False(accepted.HasPendingChoice);
        Assert.Equal(0, restored.AdvanceChoice(0x40, 0, selection: 1).ReadChoiceResult(11));
    }

    [Fact]
    public void ReopenedChoiceResetsThePhysicalResultAndClosingKeepsOtherCacheAliases()
    {
        NativeWorldMessage ordinary = ChoiceMessage(9, 75);
        NativeWorldMessage choice = ChoiceMessage(11, 58);
        NativeWorldEventUi accepted = NativeWorldEventUi.Create().Show(ordinary).ShowChoice(choice).AdvanceText(8).AdvanceChoice(0x40, 0);
        Assert.Equal(0, accepted.ReadChoiceResult(9));
        Assert.Equal(0, accepted.ReadChoiceResult(11));
        NativeWorldEventUi closed = accepted.Close(11);
        closed.Validate();
        Assert.Equal(0, closed.ReadChoiceResult(9));
        Assert.Equal(-1, closed.ReadChoiceResult(11));

        NativeWorldEventUi reopened = accepted.ShowChoice(choice);
        Assert.False(reopened.CanAcceptChoice);
        Assert.Equal((byte)1, reopened.FocusedChoice!.Selection);
        Assert.Equal(-1, reopened.ReadChoiceResult(9));
        Assert.Equal(-1, reopened.ReadChoiceResult(11));
        Assert.Single(reopened.AliasedWindows);
    }

    [Fact]
    public void ChoiceDataIsOptionalInOlderResearchAndGameplaySaves()
    {
        NativeWorldEventUi ui = NativeWorldEventUi.Create().WithScriptMode(14).Show(ChoiceMessage(9, 75));
        JsonObject snapshot = JsonSerializer.SerializeToNode(ui)!.AsObject();
        snapshot.Remove(nameof(NativeWorldEventUi.ChoiceChannels));
        NativeWorldEventUi olderUi = snapshot.Deserialize<NativeWorldEventUi>()!;
        olderUi.Validate();
        Assert.False(olderUi.HasPendingChoice);
        Assert.Equal(-1, olderUi.ReadChoiceResult(9));
        Assert.Single(olderUi.Windows);

        var save = new GameplaySaveWorldUi(false, [GameplaySaveWorldMessage.Capture(ui.Windows[0])], [], [], [], null, 0) { ScriptMode = 14 };
        JsonObject gameplay = JsonSerializer.SerializeToNode(save)!.AsObject();
        gameplay.Remove(nameof(GameplaySaveWorldUi.ChoiceChannels));
        GameplaySaveWorldUi olderSave = gameplay.Deserialize<GameplaySaveWorldUi>()!;
        olderSave.Validate();
        Assert.Empty(olderSave.ChoiceChannels);
        Assert.Single(olderSave.Windows);
    }

    private static NativeWorldMessage ChoiceMessage(int window, byte message)
    {
        return new NativeWorldMessage(window, message, 0, 0, 0, 32, 16, new string('A', 64), false)
        {
            Text = NativeWorldTextState.Create([0x21, 2, 0x22, 2, 0x23, 0], 0)
        };
    }

    private static GameState Departure()
    {
        return new GameState { Scene = SceneKind.WorldMap, World = new() { EventUi = NativeWorldEventUi.Create() }, SavedWorld = NativeSavedWorldState.CreateNewGame() with { WorldEventFlags116 = new(4, 0xF000000000000094) } };
    }
}
