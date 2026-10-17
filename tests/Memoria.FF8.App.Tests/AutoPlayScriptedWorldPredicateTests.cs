using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayScriptedWorldPredicateTests
{
    [Theory]
    [InlineData(66)]
    [InlineData(77)]
    [InlineData(92)]
    [InlineData(95)]
    [InlineData(127)]
    [InlineData(128)]
    [InlineData(65535)]
    public void AuthenticatedScriptedAllocationRejectsAbsentDenseShapesWithoutInventingContacts(int shape)
    {
        GameSession session = Create();
        using WorldMapScene scene = new(session, TestResources.GameDirectory);
        Assert.Equal(66, scene.State.NativeScriptedRuntime!.Path.EntityType);
        Assert.Equal((byte)4, scene.State.NativeScriptedRuntime.Load.FootRecord0.ShapeId);
        GameState before = session.State;
        foreach (ushort opcode in new ushort[] { 0xFF1A, 0xFF1B, 0xFF1C, 0xFF1D })
        {
            Assert.False(scene.EvaluateWorldEventPredicate(scene.State, 1, before.SavedWorld!, opcode, (ushort)shape));
        }
        Assert.Same(before, session.State);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(78)]
    [InlineData(94)]
    public void DeclaredCurrentAndSeparatelyAppendedShapesStillRequireTheirActualPublication(int shape)
    {
        GameSession session = Create();
        using WorldMapScene scene = new(session, TestResources.GameDirectory);
        GameState before = session.State;
        foreach (ushort opcode in new ushort[] { 0xFF1A, 0xFF1B, 0xFF1C, 0xFF1D })
        {
            Assert.Throws<NotSupportedException>(() => scene.EvaluateWorldEventPredicate(scene.State, 1, before.SavedWorld!, opcode, (ushort)shape));
        }
        Assert.Same(before, session.State);
    }

    [Fact]
    public void ShapeAbsenceRefusesAnUnauthenticatedSelectedAllocationReceipt()
    {
        GameSession session = Create();
        using WorldMapScene scene = new(session, TestResources.GameDirectory);
        NativeScriptedWorldRuntimeState scripted = scene.State.NativeScriptedRuntime!;
        NativeScriptedWorldFootRecord changed = scripted.Load.FootRecord0 with { ProgramHash = new string('0', 64) };
        WorldState candidate = scene.State with { NativeScriptedRuntime = scripted with { Load = scripted.Load with { FootRecord0 = changed } } };
        GameState before = session.State;
        Assert.Throws<InvalidDataException>(() => scene.EvaluateWorldEventPredicate(candidate, 1, before.SavedWorld!, 0xFF1B, 92));
        Assert.Same(before, session.State);
    }

    [Fact]
    public void OriginalShipCallbacksContinueThroughTheAbsentShapePredicateAndRepeatAfterSnapshot()
    {
        GameSession session = Create();
        using WorldMapScene scene = new(session, TestResources.GameDirectory);
        Assert.Equal(0, scene.State.Frame);
        PlayerAction wait = new(PlayerActionKind.Wait, Frames: 24);
        Assert.Equal(24, scene.Apply(wait).FramesAdvanced);
        Assert.Equal(24, scene.State.NativeScriptedRuntime!.Path.Callback);
        GameSession restored = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
        using WorldMapScene replay = new(restored, TestResources.GameDirectory);
        PlayerAction next = new(PlayerActionKind.Wait);
        Assert.Equal(scene.Apply(next), replay.Apply(next));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(restored.State)));
    }

    private static GameSession Create()
    {
        return new GameSession(GameSession.Load(TestResources.FixturePath("App.Tests.AutoPlayScriptedWorldPredicateTests.1")));
    }
}
