using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeField92InitializationScrollTests
{
    [Fact]
    public void ActualEntryDirectTargetUsesAuthenticatedInitializationWithoutConsumingCurrentWords()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        DirectoryInfo? repository = new(TestResources.FixtureDirectory);
        Assert.NotNull(repository);
        string source = Path.Combine(repository.FullName, "../fixtures/external-fixture.json");
        Assert.SkipUnless(Directory.Exists(directory) && File.Exists(source), "The standalone movie8 field92 entry and installed resources are required.");
        byte[] originalHash = SHA256.HashData(File.ReadAllBytes(source));
        using GameWorld world = new(new GameSession(GameSession.Load(source)), directory);
        world.EnsureField();
        Assert.Equal(92, world.Field!.Id);
        Assert.Equal(NativeFieldInitializationStage.Allocated, world.Events.Scene.NativeFieldInitialization!.Stage);
        Func<int, short, short, bool> originalCertificate = world.Events.CanSetBackgroundScrollPosition!;
        int checkedTargets = 0;
        int pendingTargets = 0;
        world.Events.CanSetBackgroundScrollPosition = (layer, x, y) =>
        {
            SceneEventState scene = world.Events.Scene;
            Assert.Equal(NativeFieldInitializationStage.Initializing, scene.NativeFieldInitialization!.Stage);
            Assert.Equal(0, scene.NativeFieldLoadVersion);
            Assert.Equal(0, scene.NativeActorLayoutVersion);
            string before = JsonSerializer.Serialize(scene);
            bool direct = DirectBackgroundScrollCertificate.CanSet(world.Field, scene, layer, x, y);
            Assert.True(direct, $"Direct init target {layer}/{x}/{y}: proof={scene.NativeFieldInitialization.IsFrom(NativeFieldRuntimeProfile.FF8En, world.Field.Id, Convert.ToHexString(SHA256.HashData(world.Field.Jsm)))}, load={scene.NativeFieldLoadVersion}, camera={scene.CameraIndex}, baseline={NormalCameraCertificate.HasBaseline(world.Field, scene.CameraIndex)}, settings={BackgroundScrollCertificate.HasCurrentSettings(scene)}/{BackgroundScrollCertificate.HasSupportedSettings(world.Field, scene)}, center={DirectBackgroundScrollCertificate.SupportsCenter(world.Field, scene, x, y)}, screen={world.Field.Information!.ScreenBounds[0]}, wrap={world.Field.Information.BackgroundWrapMask}, pan={world.Field.Camera.Pan}.");
            Assert.False(DirectBackgroundScrollCertificate.HasBaseline(world.Field, scene));
            Assert.False(DirectBackgroundScrollCertificate.CanSetCosine(world.Field, scene, layer, x, y, 4));
            Assert.False(DirectBackgroundScrollCertificate.CanBeginLinear(world.Field, new NativeLinearScrollQuery(layer, 0, 0, x, y, 4), scene));
            Assert.Null(DirectBackgroundScrollCertificate.ResolveNormalCenter(world.Field, scene, world.Events.Actors));
            JsonNode unknownCurrent = JsonNode.Parse(before)!;
            unknownCurrent["NativeLayerZeroScrollCurrent"] = null;
            unknownCurrent["BackgroundScrollPositions"] = JsonSerializer.SerializeToNode(new Dictionary<int, BackgroundScrollPosition> { [layer] = new(null, null, x, y, null, null, null, 3, 0) });
            SceneEventState pendingUnknown = unknownCurrent.Deserialize<SceneEventState>()!;
            string pendingBefore = JsonSerializer.Serialize(pendingUnknown);
            Assert.True(DirectBackgroundScrollCertificate.CanSet(world.Field, pendingUnknown, layer, x, y));
            Assert.Null(DirectBackgroundScrollCertificate.Prepare(world.Field, pendingUnknown));
            Assert.Equal(pendingBefore, JsonSerializer.Serialize(pendingUnknown));
            foreach (Action<JsonNode> corrupt in new Action<JsonNode>[]
            {
                node => node["NativeFieldInitialization"]!["ProgramHash"] = new string('0', 64),
                node => node["NativeFieldInitialization"]!["ExecutableHash"] = new string('0', 64),
                node => node["NativeFieldInitialization"]!["FieldId"] = 93,
                node => node["NativeFieldInitialization"]!["FieldClock"] = 19,
                node => node["NativeFieldInitialization"]!["Stage"] = (int)NativeFieldInitializationStage.Allocated,
                node => node["NativeFieldLoadVersion"] = 1,
                node => node["NativeActorLayoutVersion"] = 1,
                node => node["NativeFieldInitialization"] = null
            })
            {
                JsonNode changed = JsonNode.Parse(before)!;
                corrupt(changed);
                Assert.False(DirectBackgroundScrollCertificate.CanSet(world.Field, changed.Deserialize<SceneEventState>()!, layer, x, y));
            }
            Assert.Equal(before, JsonSerializer.Serialize(scene));
            checkedTargets++;
            return originalCertificate(layer, x, y);
        };
        world.Events.CommandExecuted += command =>
        {
            if (command.Name == "DSCROLL")
            {
                BackgroundScrollPosition pending = world.Events.Scene.BackgroundScrollPositions[0];
                Assert.Equal((byte)3, pending.Mode);
                Assert.Equal((byte)0, pending.Status);
                Assert.Equal((short)0, pending.X);
                Assert.Equal((short)0, pending.Y);
                Assert.Equal((short)0, pending.PreviousX);
                Assert.Equal((short)0, pending.PreviousY);
                Assert.Equal(pending, JsonSerializer.Deserialize<BackgroundScrollPosition>(JsonSerializer.Serialize(pending)));
                pendingTargets++;
            }
        };
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        Assert.Null(world.Events.LastFault);
        Assert.True(checkedTargets > 0 && pendingTargets > 0);
        Assert.Equal(originalHash, SHA256.HashData(File.ReadAllBytes(source)));
    }
}
