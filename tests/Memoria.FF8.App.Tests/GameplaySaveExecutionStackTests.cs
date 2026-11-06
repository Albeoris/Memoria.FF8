using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

[CollectionDefinition("Execution stack environment", DisableParallelization = true)]
public sealed class ExecutionStackEnvironmentCollection
{
}

[Collection("Execution stack environment")]
public sealed class GameplaySaveExecutionStackTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Semantic_load_and_reload_select_the_current_process_profile_without_serializing_it(bool world)
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameplaySaveComponentDocument document = GameplaySaveSessionLoaderTests.Document(assets, world);
        string folder = Path.Combine(Path.GetTempPath(), "Memoria.FF8-gameplay-stack-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string? previous = Environment.GetEnvironmentVariable("MEMORIA_FF8_EXECUTION_STACK_PROFILE");
        try
        {
            string profilePath = Path.Combine(folder, "profile.json");
            NativeBattleExecutionStack first = Profile(0x200100, 'A');
            File.WriteAllText(profilePath, JsonSerializer.Serialize(first));
            Environment.SetEnvironmentVariable("MEMORIA_FF8_EXECUTION_STACK_PROFILE", profilePath);
            GameState restored = GameplaySaveSessionLoader.Restore(document, directory, sharedAssets: assets);
            AssertProfile(first, restored);
            GameSession session = new(restored);
            GameplaySaveComponentDocument recaptured;
            using (GameWorld host = new(session, directory, sharedAssets: assets))
            {
                host.EnsureField();
                recaptured = host.CaptureGameplaySave();
            }
            string serialized = JsonSerializer.Serialize(recaptured);
            Assert.DoesNotContain("ExecutionStack", serialized);
            Assert.DoesNotContain("HeroCrtStack", serialized);
            Assert.DoesNotContain("CaptureSha256", serialized);
            string semanticPath = Path.Combine(folder, "slot.sav");
            using (FileStream destination = File.Create(semanticPath))
            {
                GameplaySaveCodec.Write(destination, recaptured);
            }
            string standalonePath = Path.Combine(folder, "state.ff8save");
            session.Save(standalonePath, compact: true);
            NativeBattleExecutionStack second = Profile(0x210100, 'B');
            File.WriteAllText(profilePath, JsonSerializer.Serialize(second));
            GameState reloaded = GameplaySaveSessionLoader.Load(semanticPath, directory, sharedAssets: assets);
            AssertProfile(second, reloaded);
            AssertProfile(first, GameSession.Load(standalonePath));
        }
        finally
        {
            Environment.SetEnvironmentVariable("MEMORIA_FF8_EXECUTION_STACK_PROFILE", previous);
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void Semantic_load_rejects_an_explicit_invalid_process_profile()
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameplaySaveComponentDocument document = GameplaySaveSessionLoaderTests.Document(assets, false);
        string path = Path.Combine(Path.GetTempPath(), "Memoria.FF8-invalid-stack-" + Guid.NewGuid().ToString("N") + ".json");
        string? previous = Environment.GetEnvironmentVariable("MEMORIA_FF8_EXECUTION_STACK_PROFILE");
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(Profile(0x200100, 'A') with { FrameReturnAddress = 0 }));
            Environment.SetEnvironmentVariable("MEMORIA_FF8_EXECUTION_STACK_PROFILE", path);
            Assert.Throws<InvalidDataException>(() => GameplaySaveSessionLoader.Restore(document, directory, sharedAssets: assets));
        }
        finally
        {
            Environment.SetEnvironmentVariable("MEMORIA_FF8_EXECUTION_STACK_PROFILE", previous);
            File.Delete(path);
        }
    }

    [Fact]
    public void Original_save_import_selects_the_configured_process_profile()
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameplaySaveComponentDocument document = GameplaySaveSessionLoaderTests.Document(assets, false);
        NativeBattleEffectMemory unselected = NativeBattleEffectMemory.FromColdProcess();
        GameState source = GameplaySaveSessionLoader.Restore(document, directory, sharedAssets: assets, executionEnvironment: unselected);
        byte[] original = NativePcSaveConverter.Export(source, assets.ReadNewGameCharacterVitals(), originalPlaceId: 0);
        string folder = Path.Combine(Path.GetTempPath(), "Memoria.FF8-original-stack-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string? previous = Environment.GetEnvironmentVariable("MEMORIA_FF8_EXECUTION_STACK_PROFILE");
        try
        {
            string profilePath = Path.Combine(folder, "profile.json");
            NativeBattleExecutionStack selected = Profile(0x200100, 'A');
            File.WriteAllText(profilePath, JsonSerializer.Serialize(selected));
            Environment.SetEnvironmentVariable("MEMORIA_FF8_EXECUTION_STACK_PROFILE", profilePath);
            string originalPath = Path.Combine(folder, "original.ff8");
            File.WriteAllBytes(originalPath, original);
            GameState imported = OriginalPlayerSaveLoader.Load(originalPath, directory, sharedAssets: assets);
            AssertProfile(selected, imported);
            Assert.Equal(original, File.ReadAllBytes(originalPath));
        }
        finally
        {
            Environment.SetEnvironmentVariable("MEMORIA_FF8_EXECUTION_STACK_PROFILE", previous);
            Directory.Delete(folder, recursive: true);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Same_process_semantic_reconstruction_preserves_its_environment_without_reading_configuration(bool selected, bool missing)
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameplaySaveComponentDocument document = GameplaySaveSessionLoaderTests.Document(assets, false);
        NativeBattleExecutionStack? profile = selected ? Profile(0x200100, 'A') : null;
        NativeBattleEffectMemory environment = NativeBattleEffectMemory.FromColdProcess() with { ExecutionStack = profile, HeroCrtStack = profile?.HeroCrtStack };
        string path = Path.Combine(Path.GetTempPath(), "Memoria.FF8-reconstruct-stack-" + Guid.NewGuid().ToString("N") + ".json");
        string? previous = Environment.GetEnvironmentVariable("MEMORIA_FF8_EXECUTION_STACK_PROFILE");
        try
        {
            if (!missing)
            {
                File.WriteAllText(path, "invalid execution-stack profile");
            }
            Environment.SetEnvironmentVariable("MEMORIA_FF8_EXECUTION_STACK_PROFILE", path);
            GameState reconstructed = GameplaySaveSessionLoader.Restore(document, directory, sharedAssets: assets, executionEnvironment: environment);
            Assert.Equal(profile, reconstructed.NativeBattleMemory!.EffectMemory!.ExecutionStack);
            Assert.Equal(profile?.HeroCrtStack, reconstructed.NativeBattleMemory.EffectMemory.HeroCrtStack);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MEMORIA_FF8_EXECUTION_STACK_PROFILE", previous);
            File.Delete(path);
        }
    }

    private static NativeBattleExecutionStack Profile(uint esp, char capture)
    {
        NativeBattleExecutionStack profile = new(esp, 0x569C3D, NativeFieldRuntimeProfile.FF8EnExecutableHash, new string(capture, 64), 1, 2);
        return profile with { HeroCrtStack = new(NativeBattleHeroCrtStackContract.Win32X86Profile, profile.CaptureSha256, profile.CapturedThreadId) };
    }

    private static void AssertProfile(NativeBattleExecutionStack expected, GameState state)
    {
        Assert.Equal(expected, state.NativeBattleMemory!.EffectMemory!.ExecutionStack);
        Assert.Equal(expected.HeroCrtStack, state.NativeBattleMemory.EffectMemory.HeroCrtStack);
    }
}
