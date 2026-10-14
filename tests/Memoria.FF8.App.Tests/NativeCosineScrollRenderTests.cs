using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeCosineScrollRenderTests
{
    [Fact]
    public void SharedModeFivePreservesBothAxesAndRejectsUnknownOrCorruptPhases()
    {
        byte[] identity = [3, 4, 5];
        FieldCamera camera = new(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, Vector3.Zero, Vector2.Zero, 100);
        FieldAssets field = new(600, "cosine", identity, [], [], camera, new FieldWalkmesh([]), []) { Information = new FieldInformation(0, 200, Enumerable.Repeat(new FieldBounds(-512, 512, 512, -512), 8).ToArray(), [new FieldBounds(0, 224, 320, 0)], [], []) };
        NativeFieldInitializationProof proof = new(1, NativeFieldRuntimeProfile.FF8EnExecutableHash, field.Id, Convert.ToHexString(SHA256.HashData(identity)), 20, NativeFieldInitializationStage.Completed);
        SceneEventState scene = JsonSerializer.Deserialize<SceneEventState>(JsonSerializer.Serialize(new { NativeFieldInitialization = proof, NativeFieldLoadVersion = 1, FieldCallbackClock = new NativeFieldCallbackClock(1, 5, 0, 0, 0), NormalBackgroundScrollCenter = new NativeNormalBackgroundScrollCenter(1, field.Id, 0, 5, 7, -28) }))!;
        Assert.True(BackgroundScrollCertificate.CanSetCosinePosition(field, scene, 0, -50, 120, 30));
        Assert.False(BackgroundScrollCertificate.CanSetCosinePosition(field, scene, 1, -50, 120, 30));
        Assert.False(BackgroundScrollCertificate.CanSetCosinePosition(field, scene, 0, -50, 120, -1));
        Assert.False(BackgroundScrollCertificate.CanSetCosinePosition(field with { Id = 601 }, scene, 0, -50, 120, 30));
        Assert.False(BackgroundScrollCertificate.CanSetCosinePosition(field with { Information = field.Information with { BackgroundWrapMask = 1 } }, scene, 0, -50, 120, 30));
        SceneEventState unknown = JsonSerializer.Deserialize<SceneEventState>(JsonSerializer.Serialize(new { NativeFieldInitialization = proof, NativeFieldLoadVersion = 1 }))!;
        Assert.False(BackgroundScrollCertificate.CanSetCosinePosition(field, unknown, 0, -50, 120, 30));
        for (short elapsed = 0; elapsed <= 30; elapsed++)
        {
            short x = (short)NativeCosineInterpolation.Interpolate(7, -50, 30, elapsed);
            short y = (short)NativeCosineInterpolation.Interpolate(-28, 120, 30, elapsed);
            BackgroundScrollPosition position = new(x, y, -50, 120, 7, -28, elapsed == 30 ? elapsed : (short)(elapsed + 1), 5, elapsed == 30 ? (byte)2 : (byte)1, 30);
            scene.BackgroundScrollPositions[0] = position;
            Assert.Equal(new Vector2(-x, -y), DirectBackgroundScrollCertificate.Prepare(field, scene)!.ActorOffset);
            Assert.True(BackgroundScrollCertificate.CanSetCosinePosition(field, scene, 0, 0, 0, 0));
            Assert.Equal(elapsed == 30, DirectBackgroundScrollCertificate.IsCompleted(position));
            scene.BackgroundScrollPositions[0] = position with { X = (short)(x + 1) };
            Assert.Null(DirectBackgroundScrollCertificate.Prepare(field, scene));
            Assert.False(BackgroundScrollCertificate.CanSetCosinePosition(field, scene, 0, 0, 0, 30));
        }
    }

    [Fact]
    public void AuthenticReachedDolletPrefixRetainsNormalCenterAndRestoresAnOrdinaryCosineCallback()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        DirectoryInfo? repository = new(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "docs", "recreate-checkpoint.md")))
        {
            repository = repository.Parent;
        }
        string source = Path.Combine(repository!.FullName, "../fixtures/external-fixture.json");
        Assert.SkipUnless(Directory.Exists(directory) && File.Exists(source), "Authentic reached Dollet journal and installed resources are required.");
        GameState current = GameWorld.RecreateCommittedPrefix(GameSession.LoadReplaySource(source), directory);
        using GameWorld world = new(new GameSession(current), directory, persistReplayJournal: false);
        world.EnsureField();
        Assert.Equal(342, world.Field!.Id);
        NativeNormalBackgroundScrollCenter previous = Assert.IsType<NativeNormalBackgroundScrollCenter>(world.Events.Scene.NormalBackgroundScrollCenter);
        JsonElement before = world.Events.Capture();
        foreach ((string name, long value) in new[] { ("Version", 2L), ("FieldId", 343L), ("CameraIndex", 1L), ("Frame", world.Events.Frame + 1), ("Frame", 0L), ("X", 4097L), ("Y", -4096L) })
        {
            JsonNode corrupt = JsonNode.Parse(before.GetRawText())!;
            corrupt["Scene"]!["NormalBackgroundScrollCenter"]![name] = value;
            Assert.Throws<InvalidDataException>(() => world.Events.Restore(JsonSerializer.SerializeToElement(corrupt)));
            Assert.True(JsonElement.DeepEquals(before, world.Events.Capture()));
        }
        JsonNode competingMode = JsonNode.Parse(before.GetRawText())!;
        competingMode["Scene"]!["BackgroundScrollPositions"]!["0"] = JsonSerializer.SerializeToNode(new BackgroundScrollPosition(0, 0, 0, 0, 0, 0, 0, 3, 2));
        Assert.Throws<InvalidDataException>(() => world.Events.Restore(JsonSerializer.SerializeToElement(competingMode)));
        Assert.True(JsonElement.DeepEquals(before, world.Events.Capture()));
        JsonNode missingClock = JsonNode.Parse(before.GetRawText())!;
        missingClock["Scene"]!["FieldCallbackClock"] = null;
        Assert.Throws<InvalidDataException>(() => world.Events.Restore(JsonSerializer.SerializeToElement(missingClock)));
        Assert.True(JsonElement.DeepEquals(before, world.Events.Capture()));
        Assert.Equal((previous.X, previous.Y), DirectBackgroundScrollCertificate.ResolveNormalCenter(world.Field, world.Events.Scene, world.Events.Actors));
        Assert.True(BackgroundScrollCertificate.CanSetCosinePosition(world.Field, world.Events.Scene, 0, 0, -120, 30));
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        Assert.Null(world.Events.LastFault);
        BackgroundScrollPosition entry = world.Events.Scene.BackgroundScrollPositions[0];
        Assert.Equal((byte)5, entry.Mode);
        Assert.Equal(previous.X, entry.PreviousX);
        Assert.Equal(previous.Y, entry.PreviousY);
        Assert.Equal((short)0, entry.TargetX);
        Assert.Equal((short)-120, entry.TargetY);
        Assert.Equal((short)30, entry.Duration);
        Assert.Null(world.Events.Scene.NormalBackgroundScrollCenter);
        GameState saved = current with { EventState = world.Events.Capture() };
        using GameWorld restored = new(new GameSession(saved), directory, persistReplayJournal: false);
        restored.EnsureField();
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        Assert.True(restored.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
    }
}
