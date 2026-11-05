using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class ConfiguredStateLaunchOptionsTests
{
    [Fact]
    public void Configured_loader_receives_all_final_resource_options_independent_of_argument_order()
    {
        GameState state = new() { Scene = SceneKind.Field, FieldId = 165 };
        LaunchOptions? selected = null;
        LaunchOptions result = LaunchOptions.Parse(["--state", "fixture.sav", "--mods-dir", "fixture-mods", "--resource-language", "FR", "--game-dir", "fixture-game", "--model-source-dir", "fixture-models"], loadConfiguredState: (path, options) =>
        {
            Assert.Equal(Path.GetFullPath("fixture.sav"), path);
            selected = options;
            return state;
        });
        Assert.NotNull(selected);
        Assert.Equal(Path.GetFullPath("fixture-game"), selected.GameDirectory);
        Assert.Equal(Path.GetFullPath("fixture-mods"), selected.ModsDirectory);
        Assert.Equal(Path.GetFullPath("fixture-models"), selected.ModelSourceDirectory);
        Assert.Equal("fr", selected.ResourceLanguage);
        Assert.Same(state, result.InitialState);
    }

    [Fact]
    public void Existing_directory_only_loader_remains_supported()
    {
        GameState state = new() { Scene = SceneKind.Field, FieldId = 165 };
        int calls = 0;
        LaunchOptions result = LaunchOptions.Parse(["--state", "fixture.sav", "--game-dir", "fixture-game"], (path, directory) =>
        {
            calls++;
            Assert.Equal(Path.GetFullPath("fixture.sav"), path);
            Assert.Equal(Path.GetFullPath("fixture-game"), directory);
            return state;
        });
        Assert.Equal(1, calls);
        Assert.Same(state, result.InitialState);
    }
}
