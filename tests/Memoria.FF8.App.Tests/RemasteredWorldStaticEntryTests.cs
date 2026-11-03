using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class RemasteredWorldStaticEntryTests
{
    [Fact]
    public void NativeBoatEntryAndIndependentWorldRestoreRetainTheSourceFieldTextureOwner()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(File.Exists(Path.Combine(directory, "main.zzz")), "Select Remastered resources for this native entry check.");
        GameSession session = new(GameSession.Load(TestResources.FixturePath("App.Tests.RemasteredWorldStaticEntryTests.1")));
        Assert.Equal(123, session.State.FieldId);
        using GameWorld world = new(session, directory, validateWorldPresentation: false);
        world.EnsureField();
        Assert.True(world.Apply(new(PlayerActionKind.Wait, Frames: 300)).Accepted);
        Assert.Equal(SceneKind.WorldMap, session.State.Scene);
        Assert.NotNull(session.State.World!.NativeScriptedRuntime);
        Assert.True(session.State.FullBattlePlaythrough);
        GameSession restored = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
        using GameWorld second = new(restored, directory, validateWorldPresentation: false);
        second.EnsureField();
        Assert.Equal(world.Apply(new(PlayerActionKind.Wait)), second.Apply(new(PlayerActionKind.Wait)));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(restored.State)));
        Assert.Equal(world.Render(TimeSpan.Zero), second.Render(TimeSpan.Zero));
    }
}
