using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayWorldConfirmTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData(0, false)]
    [InlineData(0x40, true)]
    [InlineData(0x41, true)]
    [InlineData(0x80, false)]
    public void UnknownOrHeldConfirmRequiresAnOrdinaryReleaseCallback(int? buttons, bool expected)
    {
        NativeWorldInputHistory? history = buttons is int current ? NativeWorldInputHistory.Sample(null, (uint)current) : null;
        Assert.Equal(expected, AutoPlayController.NeedsWorldConfirmRelease(history));
        if (expected)
        {
            NativeWorldInputHistory released = NativeWorldInputHistory.Sample(history, 0);
            Assert.False(AutoPlayController.NeedsWorldConfirmRelease(released));
            NativeWorldInputHistory confirmed = NativeWorldInputHistory.Sample(released, 0x40);
            Assert.Equal((ushort)0, confirmed.PreviousButtons);
            Assert.True(AutoPlayController.NeedsWorldConfirmRelease(confirmed));
        }
    }

    [Fact]
    public void ColdResumedLegacyCarGuardReleasesThenConfirmsThroughTheNativeFieldEntry()
    {
        string gameDirectory = TestResources.GameDirectory;
        string fixture = TestResources.FixturePath("App.Tests.AutoPlayCarWorldTests.1");
        byte[] fixtureBytes = File.ReadAllBytes(fixture);
        string temporary = Path.Combine(Path.GetTempPath(), "ff8-autoplay-world-confirm-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        GameWorld? world = null;
        try
        {
            GameSession source = new(GameSession.Load(fixture));
            using (WorldMapScene placement = new(source, gameDirectory))
            {
                placement.DebugPlaceCurrentCar(12390, -26709);
            }
            using (GameWorld live = new(source, gameDirectory))
            {
                live.EnsureField();
                PlayerActionResult guard = live.Apply(new(PlayerActionKind.Wait, Frames: 24));
                Assert.Equal(49, guard.Trigger.MessageId);
                Assert.Equal(24, source.State.World!.Frame);
                Assert.Equal((byte)14, source.State.World.EventUi!.ScriptMode);
            }
            JsonObject old = JsonNode.Parse(JsonSerializer.Serialize(source.State))!.AsObject();
            old["WorldProcess"]!.AsObject().Remove("InputHistory");
            string snapshot = Path.Combine(temporary, "legacy.json");
            File.WriteAllText(snapshot, old.ToJsonString());
            string manifest = Path.Combine(temporary, "legacy.run.json");
            File.WriteAllText(manifest, JsonSerializer.Serialize(new AutoPlayRunState { Snapshot = "legacy.json" }, AutoPlayController.JsonOptions));
            byte[] snapshotBytes = File.ReadAllBytes(snapshot);
            byte[] manifestBytes = File.ReadAllBytes(manifest);
            GameSession resumed = GameSession.CreateColdProcess();
            GameWorld GetWorld()
            {
                return world ??= new GameWorld(resumed, gameDirectory);
            }
            void Restore(GameState state)
            {
                world?.Dispose();
                world = null;
                resumed.Restore(state);
            }
            AutoPlayController controller = new(resumed, GetWorld, Restore, Path.Combine(temporary, "output"));
            controller.ResumeFrom(manifest);
            Assert.Null(resumed.WorldProcess!.InputHistory);
            WorldState before = resumed.State.World!;
            controller.Advance(1);
            Assert.Null(controller.Error);
            Assert.Equal(SceneKind.WorldMap, resumed.State.Scene);
            WorldState released = resumed.State.World!;
            Assert.Equal(before.Frame + 1, released.Frame);
            Assert.Equal((before.X, before.Z, before.NativeHeight), (released.X, released.Z, released.NativeHeight));
            Assert.Equal(49, GetWorld().CurrentDialogue!.MessageId);
            Assert.Equal((byte)14, released.EventUi!.ScriptMode);
            Assert.Equal((ushort)0, resumed.WorldProcess.InputHistory!.CurrentButtons);
            Assert.Null(resumed.WorldProcess.InputHistory.PreviousButtons);
            controller.Advance(1);
            Assert.Null(controller.Error);
            Assert.Equal(SceneKind.Field, resumed.State.Scene);
            Assert.Equal(106, resumed.State.FieldId);
            Assert.Equal((byte)33, resumed.State.SavedWorld!.CarReturn!.EntityType105);
            Assert.Equal((ushort)0x40, resumed.WorldProcess.InputHistory!.CurrentButtons);
            Assert.Equal((ushort)0, resumed.WorldProcess.InputHistory.PreviousButtons);
            Assert.Equal(snapshotBytes, File.ReadAllBytes(snapshot));
            Assert.Equal(manifestBytes, File.ReadAllBytes(manifest));
            Assert.Equal(fixtureBytes, File.ReadAllBytes(fixture));
        }
        finally
        {
            world?.Dispose();
            Directory.Delete(temporary, true);
        }
    }
}
