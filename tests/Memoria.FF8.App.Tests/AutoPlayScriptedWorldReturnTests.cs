using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayScriptedWorldReturnTests
{
    [Fact]
    public void ActualScriptedReturnPreservesDormantByteAndCopiesCurrentEntityOnlyIntoFreshAllocation()
    {
        string source = TestResources.FixturePath("App.Tests.AutoPlayScriptedWorldReturnTests.1");
        byte[] original = File.ReadAllBytes(source);
        GameSession session = new(GameSession.Load(source));
        GameState before = session.State;
        JsonElement departing = before.DepartingFieldState!.Value;
        Assert.Equal(123, departing.GetProperty("FieldId").GetInt32());
        Assert.Equal(1, departing.GetProperty("Scene").GetProperty("PreviousWorldEntityByte86").GetInt32());
        Assert.Null(before.EventState);
        WorldState worldReturn = before.DepartingWorldState!;
        Assert.True(before.WorldConfiguration!.SourceWorldFrame < worldReturn.Frame);
        Assert.Equal(worldReturn.NativeScriptedRuntime!.Return!.SourceWorldFrame, before.WorldConfiguration.SourceWorldFrame);
        Assert.Equal(NativeWorldConfiguration.FromWorldReturn(worldReturn, 123), before.WorldConfiguration);
        using GameWorld live = new(session, TestResources.GameDirectory, validateWorldPresentation: false);
        live.EnsureField();
        Assert.Equal(260, live.Events.FieldId);
        Assert.Equal(0, live.Events.Frame);
        Assert.Equal((byte)66, live.Events.Scene.PreviousWorldEntityByte86);
        Assert.Equal(before.WorldConfiguration, session.State.WorldConfiguration);
        Assert.Null(session.State.DepartingFieldState);
        Assert.Equal(1, departing.GetProperty("Scene").GetProperty("PreviousWorldEntityByte86").GetInt32());
        Assert.Equal(before.ExamTimer, session.State.ExamTimer);
        Assert.Equal(before.Playtime, session.State.Playtime);
        session.SetEventState(live.Events.Capture());
        GameSession restored = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
        using GameWorld cold = new(restored, TestResources.GameDirectory, validateWorldPresentation: false);
        cold.EnsureField();
        Assert.Equal((byte)66, cold.Events.Scene.PreviousWorldEntityByte86);
        Assert.True(JsonElement.DeepEquals(live.Events.Capture(), cold.Events.Capture()));
        Assert.Equal(original, File.ReadAllBytes(source));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DormantReturnRequiresTheExactPacketWriteFrameAndSourceField(bool alterFrame)
    {
        GameState original = GameSession.Load(TestResources.FixturePath("App.Tests.AutoPlayScriptedWorldReturnTests.1"));
        NativeWorldConfiguration configuration = original.WorldConfiguration!;
        NativeWorldConfiguration changed = alterFrame ? configuration with { SourceWorldFrame = configuration.SourceWorldFrame + 1 } : configuration with { SourceField = (ushort)(configuration.SourceField + 1) };
        GameSession session = new(original with { WorldConfiguration = changed });
        using GameWorld world = new(session, TestResources.GameDirectory, validateWorldPresentation: false);
        GameState before = session.State;
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => world.EnsureField());
        Assert.Contains("BYTE86", error.Message);
        Assert.Same(before, session.State);
        Assert.True(JsonElement.DeepEquals(original.DepartingFieldState!.Value, session.State.DepartingFieldState!.Value));
    }
}
