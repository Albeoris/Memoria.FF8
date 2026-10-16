using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeField135LinearScrollTests
{
    [Fact]
    public void ActualLavaLoopAndIndependentMidphaseSavePreserveCompleteVmPixelsAndNativeRestart()
    {
        (string directory, string source) = Corpus();
        byte[] original = File.ReadAllBytes(source);
        var session = new GameSession(RecreateLava(directory, source));
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        world.SetRenderSize(320, 240);
        GameWorld? restored = null;
        string save = Path.Combine(Path.GetTempPath(), "Memoria.FF8-lava-" + Guid.NewGuid().ToString("N") + ".json");
        byte[]? firstLavaPixels = null;
        bool completed = false;
        bool restarted = false;
        try
        {
            while (world.Events.Frame < 360)
            {
                var action = new PlayerAction(PlayerActionKind.Wait);
                PlayerActionResult result = world.Apply(action);
                Assert.True(result.Accepted);
                Assert.Null(world.Events.LastFault);
                BackgroundScrollPosition phase = world.Events.Scene.BackgroundScrollPositions[1];
                Assert.Equal(1, phase.NativeLinearVersion);
                Assert.True(NativeWrappedBackgroundScrollCertificate.SupportsPhase(world.Field!, world.Events.Scene));
                bool inspectPixels = world.Events.Frame is 3 or 100 or 360 || phase.Status == 2 || completed && phase.Counter == 1;
                byte[]? pixels = inspectPixels ? world.Render(TimeSpan.Zero) : null;
                if (world.Events.Frame == 3)
                {
                    Assert.Equal((0, 1, 1), ((int)Assert.IsType<short>(phase.X), (int)Assert.IsType<short>(phase.Counter), (int)phase.Status));
                    firstLavaPixels = pixels!.ToArray();
                }
                if (restored is not null)
                {
                    Assert.Equal(result, restored.Apply(action));
                    Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
                    if (pixels is not null)
                    {
                        Assert.Equal(pixels, restored.Render(TimeSpan.Zero));
                    }
                }
                if (world.Events.Frame == 100)
                {
                    Assert.NotEqual(firstLavaPixels, pixels);
                    session.SetEventState(world.Events.Capture());
                    session.Save(save);
                    restored = new GameWorld(new GameSession(GameSession.Load(save)), directory);
                    restored.EnsureField();
                    restored.SetRenderSize(320, 240);
                    Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
                    Assert.Equal(pixels, restored.Render(TimeSpan.Zero));
                }
                if (phase.Status == 2)
                {
                    Assert.Equal((351, 351, 2), ((int)Assert.IsType<short>(phase.X), (int)Assert.IsType<short>(phase.Counter), (int)phase.Status));
                    completed = true;
                }
                if (completed && phase.Counter == 1)
                {
                    Assert.Equal((0, 1, 1), ((int)Assert.IsType<short>(phase.X), (int)Assert.IsType<short>(phase.Counter), (int)phase.Status));
                    restarted = true;
                }
            }
            Assert.True(completed);
            Assert.True(restarted);
            Assert.Equal(original, File.ReadAllBytes(source));
        }
        finally
        {
            restored?.Dispose();
            if (File.Exists(save))
            {
                File.Delete(save);
            }
        }
    }

    [Fact]
    public void SavedNativeLavaPhaseRejectsMalformedAuthorityBeforeChangingTheVm()
    {
        (string directory, string source) = Corpus();
        using var world = new GameWorld(new GameSession(RecreateLava(directory, source)), directory);
        world.EnsureField();
        for (int tick = 0; tick < 10; tick++)
        {
            Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        }
        JsonElement current = world.Events.Capture();
        foreach ((string property, int value) in new[] { ("NativeLinearVersion", 0), ("X", 351), ("Counter", 0), ("Duration", -1), ("TargetY", 4097) })
        {
            JsonNode changed = JsonNode.Parse(current.GetRawText())!;
            changed["Scene"]!["BackgroundScrollPositions"]!["1"]![property] = value;
            using JsonDocument saved = JsonDocument.Parse(changed.ToJsonString());
            Assert.Throws<InvalidDataException>(() => world.Events.Restore(saved.RootElement));
            Assert.True(JsonElement.DeepEquals(current, world.Events.Capture()));
        }
        Assert.True(NativeWrappedBackgroundScrollCertificate.CanSet(world.Field!, new NativeLinearScrollQuery(1, 0, 0, 351, 0, 351), world.Events.Scene.CameraIndex));
        Assert.True(NativeWrappedBackgroundScrollCertificate.CanSet(world.Field!, new NativeLinearScrollQuery(1, 0, 0, 350, 0, 351), world.Events.Scene.CameraIndex));
        Assert.False(NativeWrappedBackgroundScrollCertificate.CanSet(world.Field!, new NativeLinearScrollQuery(0, 0, 0, 351, 0, 351), world.Events.Scene.CameraIndex));
    }

    internal static GameState RecreateLava(string directory, string source)
    {
        return RecreateBoundary(directory, source, 135, 2);
    }

    internal static GameState RecreateBoundary(string directory, string source, int fieldId, int frame)
    {
        return SnapshotFixtures.Require($"MEMORIA_FF8_FIELD_{fieldId}_FRAME_{frame}_SNAPSHOT", fieldId, frame);
    }

    private static (string Directory, string Source) Corpus()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(System.IO.Directory.Exists(directory), "Installed field corpus is required.");
        DirectoryInfo? repository = new(TestResources.FixtureDirectory);
        Assert.NotNull(repository);
        return (directory, Path.Combine(repository.FullName, "../fixtures/external-fixture.json"));
    }
}
