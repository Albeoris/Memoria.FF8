using System.Text.Json;

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

    private static GameState Departure()
    {
        return new GameState { Scene = SceneKind.WorldMap, World = new() { EventUi = NativeWorldEventUi.Create() }, SavedWorld = NativeSavedWorldState.CreateNewGame() with { WorldEventFlags116 = new(4, 0xF000000000000094) } };
    }
}
