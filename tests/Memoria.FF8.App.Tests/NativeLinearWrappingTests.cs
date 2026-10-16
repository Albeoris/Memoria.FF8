using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Events;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeLinearWrappingTests
{
    [Fact]
    public void ActualFreshIfritStartsBothOriginalLinearLayersAndRestoresIndependentVmAndPixels()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original field corpus is required.");
        DirectoryInfo? repository = new(TestResources.FixtureDirectory);
        string source = Path.Combine(repository!.FullName, "../fixtures/external-fixture.json");
        byte[] original = File.ReadAllBytes(source);
        var session = new GameSession(GameSession.Load(source));
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        world.SetRenderSize(320, 240);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 60)).Accepted);
        Assert.Equal(3, world.Events.Frame);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        Assert.Equal(4, world.Events.Frame);
        Assert.Equal(new[] { 1, 2 }, world.Events.Scene.BackgroundScrollPositions.Keys.Order());
        Assert.Equal(367, world.Events.Scene.BackgroundScrollPositions[1].TargetX);
        Assert.Equal(-367, world.Events.Scene.BackgroundScrollPositions[2].TargetX);
        Assert.All(world.Events.Scene.BackgroundScrollPositions.Values, phase =>
        {
            Assert.Equal((short?)367, phase.Duration);
            Assert.Equal((short?)1, phase.Counter);
            Assert.Equal((short?)0, phase.X);
            Assert.True(NativeLinearScrollPhase.IsValid(phase));
        });
        JsonElement originalPhase = world.Events.Capture();
        JsonNode genericVariant = JsonNode.Parse(originalPhase.GetRawText())!;
        genericVariant["Scene"]!["BackgroundScrollPositions"]!["1"]!["Duration"] = 350;
        genericVariant["Scene"]!["BackgroundScrollPositions"]!["1"]!["TargetY"] = 1;
        using (JsonDocument variant = JsonDocument.Parse(genericVariant.ToJsonString()))
        {
            world.Events.Restore(variant.RootElement);
            Assert.True(NativeWrappedBackgroundScrollCertificate.SupportsPhase(world.Field!, world.Events.Scene));
            Assert.NotEmpty(world.Render(TimeSpan.Zero));
        }
        world.Events.Restore(originalPhase);
        JsonNode unknownRuntime = JsonNode.Parse(originalPhase.GetRawText())!;
        unknownRuntime["Scene"]!["NativeFieldInitialization"] = null;
        using (JsonDocument unknown = JsonDocument.Parse(unknownRuntime.ToJsonString()))
        {
            Assert.Throws<InvalidDataException>(() => world.Events.Restore(unknown.RootElement));
            Assert.True(JsonElement.DeepEquals(originalPhase, world.Events.Capture()));
        }
        Assert.True(NativeWrappedBackgroundScrollCertificate.SupportsPhase(world.Field!, world.Events.Scene));
        BackgroundScrollFrame scroll = NormalCameraCertificate.Prepare(world.Field!, world.Events.Scene, world.Events.Actors)!;
        Assert.Equal(2, scroll.NativeWrap!.NativeNormalVersion);
        Assert.Equal(new[] { 1, 2 }, scroll.NativeWrap.LayerCurrents.Select(current => (int)current.Layer));
        session.SetEventState(world.Events.Capture());
        string save = Path.Combine(Path.GetTempPath(), "Memoria.FF8-linear-wrap-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            session.Save(save);
            using var resumed = new GameWorld(new GameSession(GameSession.Load(save)), directory);
            resumed.EnsureField();
            resumed.SetRenderSize(320, 240);
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), resumed.Events.Capture()));
            Assert.Equal(world.Render(TimeSpan.Zero), resumed.Render(TimeSpan.Zero));
            for (int tick = 0; tick < 12; tick++)
            {
                var action = new PlayerAction(PlayerActionKind.Wait);
                PlayerActionResult? first = null;
                PlayerActionResult? second = null;
                Exception? failure = Record.Exception(() => first = world.Apply(action));
                Exception? repeatedFailure = Record.Exception(() => second = resumed.Apply(action));
                Assert.Equal(first, second);
                Assert.Equal(failure?.GetType(), repeatedFailure?.GetType());
                Assert.Equal(failure?.Message, repeatedFailure?.Message);
                Assert.True(JsonElement.DeepEquals(world.Events.Capture(), resumed.Events.Capture()));
                Assert.Equal(world.Render(TimeSpan.Zero), resumed.Render(TimeSpan.Zero));
                if (failure is not null)
                {
                    Assert.IsType<EventScriptException>(failure);
                    Assert.Equal("LOFFSET", world.Events.LastFault!.Opcode);
                    break;
                }
                Assert.True(first!.Accepted);
            }
            JsonElement before = world.Events.Capture();
            Assert.False(NativeWrappedBackgroundScrollCertificate.CanSet(world.Field!, new NativeLinearScrollQuery(0, 0, 0, 20, 0, 20), world.Events.Scene.CameraIndex));
            Assert.False(NativeWrappedBackgroundScrollCertificate.CanSet(world.Field!, new NativeLinearScrollQuery(1, 0, 0, 4097, 0, 20), world.Events.Scene.CameraIndex));
            world.Events.Scene.BackgroundScrollLayers[1] = new BackgroundScrollSettings(0, 0, 256, 256);
            Assert.False(world.Events.CanBeginLinearBackgroundScroll!(new NativeLinearScrollQuery(1, 0, 0, 20, 0, 20), world.Events.Scene));
            world.Events.Scene.BackgroundScrollLayers.Clear();
            Assert.True(JsonElement.DeepEquals(before, world.Events.Capture()));
        }
        finally
        {
            File.Delete(save);
        }
        Assert.Equal(original, File.ReadAllBytes(source));
    }
}
