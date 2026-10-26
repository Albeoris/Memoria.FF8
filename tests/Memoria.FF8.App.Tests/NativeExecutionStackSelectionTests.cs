using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeExecutionStackSelectionTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "memoria-execution-stack-" + Guid.NewGuid().ToString("N"));

    public NativeExecutionStackSelectionTests()
    {
        Directory.CreateDirectory(directory);
    }

    [Fact]
    public void Missing_selection_remains_unknown_and_explicit_missing_file_fails()
    {
        NativeBattleRetainedMemory cold = NativeBattleRetainedMemory.FromColdProcess();
        Assert.Null(NativeExecutionStackSelection.Read(null));
        Assert.Same(cold, NativeExecutionStackSelection.Apply(cold, null));
        Assert.Null(cold.EffectMemory!.ExecutionStack);
        Assert.Throws<FileNotFoundException>(() => NativeExecutionStackSelection.Read(Path.Combine(directory, "missing.json")));
    }

    [Fact]
    public void Explicit_verified_CRT_selection_reaches_the_same_cold_process_and_survives_roundtrip()
    {
        NativeBattleExecutionStack original = Profile();
        NativeBattleExecutionStack profile = original with { HeroCrtStack = new(NativeBattleHeroCrtStackContract.Win32X86Profile, original.CaptureSha256, original.CapturedThreadId) };
        string path = Path.Combine(directory, "verified-crt.json");
        File.WriteAllText(path, JsonSerializer.Serialize(profile));
        NativeBattleRetainedMemory memory = NativeExecutionStackSelection.Apply(NativeBattleRetainedMemory.FromColdProcess(), NativeExecutionStackSelection.Read(path));
        Assert.Equal(profile.HeroCrtStack, memory.EffectMemory!.HeroCrtStack);
        Assert.Equal(profile, memory.EffectMemory.ExecutionStack);
        NativeBattleRetainedMemory restored = JsonSerializer.Deserialize<NativeBattleRetainedMemory>(JsonSerializer.Serialize(memory))!;
        Assert.Equal(profile.HeroCrtStack, restored.EffectMemory!.HeroCrtStack);
        restored.EffectMemory.HeroCrtStack!.Validate(restored.EffectMemory.ExecutionStack!);
        GameSession session = new(new GameState { NativeBattleMemory = restored });
        session.StartNewGame();
        Assert.Equal(profile.HeroCrtStack, session.State.NativeBattleMemory!.EffectMemory!.HeroCrtStack);
        Assert.Equal(profile, session.State.NativeBattleMemory.EffectMemory.ExecutionStack);
    }

    [Fact]
    public void External_CRT_selection_rejects_another_capture_and_legacy_selection_keeps_it_unknown()
    {
        NativeBattleExecutionStack original = Profile();
        NativeBattleExecutionStack invalid = original with { HeroCrtStack = new(NativeBattleHeroCrtStackContract.Win32X86Profile, new string('B', 64), original.CapturedThreadId) };
        Assert.Throws<NotSupportedException>(() => NativeExecutionStackSelection.Apply(NativeBattleRetainedMemory.FromColdProcess(), invalid));
        string path = Path.Combine(directory, "invalid-crt.json");
        File.WriteAllText(path, JsonSerializer.Serialize(invalid));
        Assert.Throws<NotSupportedException>(() => NativeExecutionStackSelection.Read(path));
        NativeBattleRetainedMemory legacy = NativeExecutionStackSelection.Apply(NativeBattleRetainedMemory.FromColdProcess(), original);
        Assert.Null(legacy.EffectMemory!.HeroCrtStack);
    }

    [Fact]
    public void Selected_file_requires_valid_native_identity_and_all_capture_fields()
    {
        NativeBattleExecutionStack profile = Profile();
        string path = Path.Combine(directory, "selection.json");
        File.WriteAllText(path, JsonSerializer.Serialize(profile));
        Assert.Equal(profile, NativeExecutionStackSelection.Read(path));
        JsonObject missing = JsonSerializer.SerializeToNode(profile)!.AsObject();
        missing.Remove("CapturedThreadId");
        File.WriteAllText(path, missing.ToJsonString());
        Assert.Throws<JsonException>(() => NativeExecutionStackSelection.Read(path));
        File.WriteAllText(path, JsonSerializer.Serialize(profile with { FrameReturnAddress = 0 }));
        Assert.Throws<InvalidDataException>(() => NativeExecutionStackSelection.Read(path));
        File.WriteAllText(path, "null");
        Assert.Throws<InvalidDataException>(() => NativeExecutionStackSelection.Read(path));
    }

    [Fact]
    public void Explicit_selection_survives_new_game_reset_and_standalone_roundtrip()
    {
        NativeBattleExecutionStack profile = Profile();
        string path = Path.Combine(directory, "selection.json");
        File.WriteAllText(path, JsonSerializer.Serialize(profile));
        NativeBattleRetainedMemory memory = NativeExecutionStackSelection.Apply(NativeBattleRetainedMemory.FromColdProcess(), NativeExecutionStackSelection.Read(path));
        GameSession session = GameSession.CreateColdProcess(memory);
        Assert.Equal(profile, session.State.NativeBattleMemory!.EffectMemory!.ExecutionStack);
        session.StartNewGame();
        Assert.Equal(profile, session.State.NativeBattleMemory!.EffectMemory!.ExecutionStack);
        string save = Path.Combine(directory, "new-game.json");
        session.Save(save, compact: true);
        GameSession independent = new(GameSession.Load(save));
        Assert.Equal(profile, independent.State.NativeBattleMemory!.EffectMemory!.ExecutionStack);
        independent.StartNewGame();
        Assert.Equal(profile, independent.State.NativeBattleMemory!.EffectMemory!.ExecutionStack);
    }

    [Fact]
    public void Configured_selection_reaches_normal_cold_start_new_game_and_saved_state()
    {
        string? gameDirectory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? path = NativeExecutionStackSelection.ConfiguredPath;
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(gameDirectory) && path is not null, "An installed resource directory and explicitly persisted or configured profile are required.");
        NativeBattleExecutionStack profile = NativeExecutionStackSelection.Read(path)!;
        GameSession session = GameWorld.CreateColdProcess(gameDirectory!);
        Assert.Equal(profile, session.State.NativeBattleMemory!.EffectMemory!.ExecutionStack);
        session.StartNewGame();
        string save = Path.Combine(directory, "configured-new-game.json");
        session.Save(save, compact: true);
        Assert.Equal(profile, GameSession.Load(save).NativeBattleMemory!.EffectMemory!.ExecutionStack);
    }

    [Fact]
    public void Installed_cold_factory_applies_only_the_explicit_selection_and_new_game_retains_it()
    {
        string? gameDirectory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(gameDirectory), "An explicitly selected installed resource directory is required.");
        NativeBattleExecutionStack profile = Profile();
        string path = Path.Combine(directory, "selection.json");
        File.WriteAllText(path, JsonSerializer.Serialize(profile));
        GameSession unknown = GameWorld.CreateColdProcess(gameDirectory!, selectedExecutionStackPath: null);
        Assert.Null(unknown.State.NativeBattleMemory!.EffectMemory!.ExecutionStack);
        GameSession selected = GameWorld.CreateColdProcess(gameDirectory!, path);
        Assert.Equal(profile, selected.State.NativeBattleMemory!.EffectMemory!.ExecutionStack);
        selected.StartNewGame();
        string save = Path.Combine(directory, "installed-new-game.json");
        selected.Save(save, compact: true);
        Assert.Equal(profile, GameSession.Load(save).NativeBattleMemory!.EffectMemory!.ExecutionStack);
    }

    private static NativeBattleExecutionStack Profile()
    {
        return new(0x200100, 0x569C3D, NativeFieldRuntimeProfile.FF8EnExecutableHash, new string('A', 64), 1, 2);
    }

    public void Dispose()
    {
        Directory.Delete(directory, recursive: true);
    }
}
