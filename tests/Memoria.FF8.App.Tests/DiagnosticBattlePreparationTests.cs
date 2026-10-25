using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Tests;

public sealed class DiagnosticBattlePreparationTests
{
    [Fact]
    public void Direct_graphical_launch_skips_startup_without_allocating_a_fixture()
    {
        LaunchOptions options = LaunchOptions.Parse(["--battle-test", "case.json", "--control-dir", "control", "--settings-dir", "settings", "--save-state", "saves/checkpoint.json"]);
        Assert.NotNull(options.BattleTestPath);
        Assert.Equal(SceneKind.Title, options.InitialState.Scene);
        Assert.False(StartupSequence.ShouldPlay(options));
        Assert.False(options.ControlStep);
        Assert.False(options.SoftwareRenderer);
    }

    [Fact]
    public void Diagnostic_launch_requires_isolated_settings_control_and_save_paths()
    {
        Assert.Throws<ArgumentException>(() => LaunchOptions.Parse(["--battle-test", "case.json"]));
        Assert.Throws<ArgumentException>(() => LaunchOptions.Parse(["--battle-test", "case.json", "--control-dir", "shared", "--settings-dir", "shared", "--save-state", "saves/checkpoint.json"]));
        GlobalSettingsPaths paths = new(isolatedDirectory: Path.GetFullPath("isolated-settings"));
        Assert.Equal(Path.GetFullPath("isolated-settings"), paths.DirectoryPath);
    }

    [Theory]
    [InlineData("--control-step")]
    [InlineData("--software-renderer")]
    [InlineData("--validate")]
    [InlineData("--debug-room")]
    public void Diagnostic_launch_rejects_incompatible_host_modes(string argument)
    {
        Assert.Throws<ArgumentException>(() => LaunchOptions.Parse(["--battle-test", "case.json", "--control-dir", "control", argument]));
    }

    [Fact]
    public void Diagnostic_recipe_does_not_default_required_native_inputs()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<DiagnosticBattleRequest>("{}"));
    }

    [Fact]
    public void Music_prerequisites_are_explicit_and_native_keep_music_is_respected()
    {
        DiagnosticBattleRequest request = new(1, 2, 0, 0, 0);
        Assert.Throws<NotSupportedException>(request.Validate);
        (request with { Flags = 0x10 }).Validate();
        (request with { BattleMusic = 1, PrimaryMusicChannel = -1 }).Validate();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(65536)]
    public void Encounter_must_fit_the_native_formation_word(int encounter)
    {
        Assert.Throws<InvalidDataException>(() => new DiagnosticBattleRequest(encounter, 0, 0x10, 0, 0).Validate());
    }
}
