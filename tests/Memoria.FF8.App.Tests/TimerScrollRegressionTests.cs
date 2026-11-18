using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class TimerScrollRegressionTests
{
    [Fact]
    public void ReportedFieldInitializationAdmitsNonselectedDirectTarget()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_TIMER_SOURCE");
        Assert.SkipUnless(source is not null && File.Exists(source), "Select a standalone field timer checkpoint.");
        byte[] sourceHash = SHA256.HashData(File.ReadAllBytes(source!));
        GameSession session = new(GameplaySaveSessionLoader.Load(source!, TestResources.GameDirectory));
        // Isolate the reported entry without changing the source checkpoint or its timer.
        using GameWorld world = new(session, TestResources.GameDirectory);
        world.EnsureField();
        session.RequestField(new FieldTransition(417, short.MaxValue, short.MaxValue, short.MaxValue, 0));
        world.EnsureField();
        int issued = 0;
        Func<int, short, short, bool> certificate = world.Events.CanSetBackgroundScrollPosition!;
        world.Events.CanSetBackgroundScrollPosition = (layer, x, y) =>
        {
            if (layer != 1)
            {
                bool accepted = certificate(layer, x, y);
                Assert.True(accepted);
                return accepted;
            }
            SceneEventState scene = world.Events.Scene;
            Assert.Equal(1, layer);
            Assert.Equal((short)72, x);
            Assert.Equal((short)0, y);
            Assert.Equal(0, scene.CameraIndex);
            Assert.Equal(NativeFieldInitializationStage.Initializing, scene.NativeFieldInitialization!.Stage);
            Assert.False(DirectBackgroundScrollCertificate.HasSelectedBaseline(world.Field!, scene));
            Assert.True(certificate(layer, x, y));
            Assert.False(NativeNonselectedBackgroundScrollCertificate.CanSetCosine(world.Field!, scene, layer, x, y, 4));
            Assert.False(NativeNonselectedBackgroundScrollCertificate.CanBeginLinear(world.Field!, new(layer, 0, 0, x, y, 4), scene));
            JsonNode unknown = JsonSerializer.SerializeToNode(scene)!;
            unknown["BackgroundScrollPositions"] = JsonSerializer.SerializeToNode(new Dictionary<int, BackgroundScrollPosition> { [layer] = new(null, null, x, y, null, null, null, 3, 0) });
            SceneEventState pending = unknown.Deserialize<SceneEventState>()!;
            string before = JsonSerializer.Serialize(pending);
            Assert.True(NativeNonselectedBackgroundScrollCertificate.CanSetDirect(world.Field!, pending, layer, x, y));
            Assert.False(NativeNonselectedBackgroundScrollCertificate.Supports(world.Field!, pending));
            Assert.Equal(before, JsonSerializer.Serialize(pending));
            foreach (Action<JsonNode> corrupt in new Action<JsonNode>[]
            {
                node => node["NativeFieldInitialization"]!["ProgramHash"] = new string('0', 64),
                node => node["NativeFieldInitialization"]!["FieldId"] = 418,
                node => node["NativeFieldLoadVersion"] = 1,
                node => node["NativeActorLayoutVersion"] = 1,
                node => node["NativeFieldInitialization"] = null
            })
            {
                JsonNode changed = JsonSerializer.SerializeToNode(scene)!;
                corrupt(changed);
                Assert.False(NativeNonselectedBackgroundScrollCertificate.CanSetDirect(world.Field!, changed.Deserialize<SceneEventState>()!, layer, x, y));
            }
            issued++;
            return true;
        };
        world.Apply(new(PlayerActionKind.Wait));
        Assert.Equal(1, issued);
        Assert.Null(world.Events.LastFault);
        Assert.Equal((byte)0, world.Events.Scene.BackgroundScrollPositions[1].Status);
        for (int i = 0; i < 30 && world.Events.Scene.BackgroundScrollPositions[1].Status == 0; i++)
        {
            world.Apply(new(PlayerActionKind.Wait));
        }
        BackgroundScrollPosition completed = world.Events.Scene.BackgroundScrollPositions[1];
        // The first ordinary script replaces the pending direct row with LSCROLL2
        // before the shared post-VM driver, retaining the actual old current.
        Assert.Equal((byte)4, completed.Mode);
        Assert.Equal((byte)1, completed.Status);
        Assert.NotNull(completed.X);
        Assert.NotNull(completed.Y);
        session.SetEventState(world.Events.Capture());
        using GameWorld restored = new(new GameSession(session.PrepareSnapshot()), TestResources.GameDirectory);
        restored.EnsureField();
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.Equal(world.Apply(new(PlayerActionKind.Wait)), restored.Apply(new(PlayerActionKind.Wait)));
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
        Assert.Equal(sourceHash, SHA256.HashData(File.ReadAllBytes(source!)));
    }
}
