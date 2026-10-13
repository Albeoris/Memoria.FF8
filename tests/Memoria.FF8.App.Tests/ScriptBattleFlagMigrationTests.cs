using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class ScriptBattleFlagMigrationTests
{
    [Fact]
    public void Exact_retained_arrival_migrates_after_departing_restore_and_resumes_independently()
    {
        string path = Evidence("player-ifrit-arrival-checkpoint.json");
        Assert.Equal(NativeScriptBattleFlagMigrationProof.RetainedArrivalCheckpointSha256, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
        GameState source = GameSession.Load(path);
        Assert.Null(source.WorldControls!.ScriptBattleDisabled20);
        Assert.Equal(NativeScriptBattleFlagMigrationProof.RetainedArrivalSemanticSha256, NativeScriptBattleFlagMigration.Fingerprint(source));
        NativeWorldControlState upgraded = NativeScriptBattleFlagMigration.UpgradeRetainedArrival(source, NativeFieldRuntimeProfile.FF8En)!;
        Assert.False(upgraded.ScriptBattleDisabled20);
        Assert.NotNull(upgraded.ScriptBattleFlagMigration);
        Assert.Null(NativeScriptBattleFlagMigration.UpgradeRetainedArrival(source with { WorldControls = upgraded }, NativeFieldRuntimeProfile.FF8En));
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original field corpus is required.");
        var session = new GameSession(source);
        using var original = new GameWorld(session, directory);
        original.EnsureField();
        Assert.False(session.State.WorldControls!.ScriptBattleDisabled20);
        Assert.Equal(upgraded.ScriptBattleFlagMigration, session.State.WorldControls.ScriptBattleFlagMigration);
        original.Events.Step();
        original.Events.Step();
        session.SetEventState(original.Events.Capture());
        GameState saved = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State, Options), Options)!;
        var independentSession = new GameSession(saved);
        using var restored = new GameWorld(independentSession, directory);
        restored.EnsureField();
        Assert.True(JsonElement.DeepEquals(original.Events.Capture(), restored.Events.Capture()));
        original.Events.Step();
        restored.Events.Step();
        Assert.True(JsonElement.DeepEquals(original.Events.Capture(), restored.Events.Capture()));
        original.SetRenderSize(320, 240);
        restored.SetRenderSize(320, 240);
        Assert.Equal(original.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
    }

    [Fact]
    public void Changed_or_other_legacy_state_cannot_receive_the_retained_route_bit()
    {
        GameState original = GameSession.Load(Evidence("player-ifrit-arrival-checkpoint.json"));
        Assert.Null(NativeScriptBattleFlagMigration.UpgradeRetainedArrival(original with { EntryPoint = original.EntryPoint + 1 }, NativeFieldRuntimeProfile.FF8En));
        Assert.Null(NativeScriptBattleFlagMigration.UpgradeRetainedArrival(original with { WorldControls = original.WorldControls! with { Menu209 = 255 } }, NativeFieldRuntimeProfile.FF8En));
        Assert.Null(NativeScriptBattleFlagMigration.UpgradeRetainedArrival(original, null));
        GameState other = GameSession.Load(Evidence("player-ifrit-render-boundary-checkpoint.json"));
        Assert.Null(NativeScriptBattleFlagMigration.UpgradeRetainedArrival(other, NativeFieldRuntimeProfile.FF8En));
        NativeWorldControlState malformed = original.WorldControls! with { ScriptBattleDisabled20 = false };
        Assert.Throws<InvalidDataException>(() => malformed.Validate());
        Assert.Null(original.WorldControls!.ScriptBattleDisabled20);
    }

    private static readonly JsonSerializerOptions Options = new() { IncludeFields = true, Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };

    private static string Evidence(string file)
    {
        DirectoryInfo? repository = new(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "docs", "recreate-checkpoint.md")))
        {
            repository = repository.Parent;
        }
        return Path.Combine(repository!.FullName, "../fixtures", file);
    }
}
