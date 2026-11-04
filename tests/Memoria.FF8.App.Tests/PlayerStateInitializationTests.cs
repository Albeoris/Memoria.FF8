using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class PlayerStateInitializationTests
{
    [Fact]
    public void CompleteCheckpointsDoNotOpenAnInstallationForCompatibility()
    {
        string path = Path.Combine(Path.GetTempPath(), "Memoria.FF8-complete-load-" + Guid.NewGuid().ToString("N"));
        try
        {
            GameState state = GameSession.CreateColdProcess().State with { Scene = SceneKind.Field };
            new GameSession(state).Save(path, compact: true);

            GameState loaded = PlayerStateLoader.Load(path, Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

            Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(loaded));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void InstalledTemplateCompletesLegacyReadsThroughLaunchAndPlayerBanks()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeLimitProgress initial = assets.ReadNewGameLimitProgress();
        NativeLimitProgress progress = initial with { BlueMagicFlags = 0x8181, DuelFlags = 0x4242, SpecialMagicFlags = 0x35, Options978 = 0x1234 };
        JsonSerializerOptions options = new() { Converters = { new JsonStringEnumConverter() } };
        JsonObject saved = JsonSerializer.SerializeToNode(GameSession.CreateColdProcess().State with { SnapshotSchemaVersion = 8, Scene = SceneKind.Field, LimitProgress = progress }, options)!.AsObject();
        foreach (string property in new[] { "AngeloFlags", "TrainingSelection", "TrainingCounters", "PetPalsFlags" })
        {
            saved["LimitProgress"]!.AsObject().Remove(property);
        }
        string directory = Path.Combine(Path.GetTempPath(), "Memoria.FF8-legacy-initialization-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string source = Path.Combine(directory, "source.json");
        try
        {
            File.WriteAllText(source, saved.ToJsonString(options));
            byte[] original = File.ReadAllBytes(source);
            GameState loaded = PlayerStateLoader.Load(source, TestResources.GameDirectory, assets);
            Assert.Equal(JsonSerializer.Serialize(progress), JsonSerializer.Serialize(loaded.LimitProgress));
            LaunchOptions launch = PlayerStateLoader.Parse(["--game-dir", TestResources.GameDirectory, "--state", source]);
            Assert.Equal(JsonSerializer.Serialize(loaded), JsonSerializer.Serialize(launch.InitialState));
            PlayerSaveStore store = new(Path.Combine(directory, "checkpoint.json"), Path.Combine(directory, "saves"), discoverOriginalSaves: false, loadMemoria: path => PlayerStateLoader.Load(path, TestResources.GameDirectory, assets));
            Directory.CreateDirectory(Path.GetDirectoryName(store.GetPath(1))!);
            File.WriteAllBytes(store.GetPath(1), original);
            Assert.True(store.ReadEntries(includeQuick: false).First(entry => entry.Slot == 1).CanLoad);
            GameState fromBank = store.Load(1);
            Assert.Equal(JsonSerializer.Serialize(loaded), JsonSerializer.Serialize(fromBank));
            NativeMovementProgressResult movement = NativeMovementProgress.Sample(NativeEventMemory.FromDWords(new Dictionary<int, int> { [244] = 592 }), assets.ReadNewGameCharacterVitals(), fromBank.LimitProgress, [4, 255, 255], assets.ReadNewGameSavedBattleFlags(), 0);
            store.Save(2, new GameSession(fromBank with { LimitProgress = movement.Limits }));
            GameState first = store.Load(2);
            store.Save(3, new GameSession(first));
            Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(store.Load(3)));
            Assert.Equal(original, File.ReadAllBytes(source));
            Assert.Equal(original, File.ReadAllBytes(store.GetPath(1)));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
