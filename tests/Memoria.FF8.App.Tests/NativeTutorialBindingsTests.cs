using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeTutorialBindingsTests
{
    [Fact]
    public void CopiedCustomNamesRemainPrivateAndRestoreExactly()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original tutorial resources are required.");
        using GameAssets assets = new(directory);
        NativeTutorialCatalog catalog = assets.ReadNativeTutorialCatalog();
        LiveMenuCatalog menus = LiveMenuCatalog.ReadInstalled(assets);
        Dictionary<int, string> names = new() { [0] = "Leon", [1] = "Punch", [3] = "Custom guest", [4] = "Angel" };
        NativeTutorialPlayer player = new(catalog, menus, 5, names: names);
        Assert.Equal("Leon", player.Menu.Character!.Name);
        names[0] = "Changed outside lesson";
        player.Menu.InvalidateLiveData();
        Assert.Equal("Leon", player.Menu.Character.Name);
        Assert.True(player.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 48)));
        JsonElement snapshot = player.Capture();
        NativeTutorialPlayer restored = new(catalog, menus, 5, snapshot, new Dictionary<int, string> { [0] = "Different current name" });
        Assert.True(JsonElement.DeepEquals(snapshot, restored.Capture()));
        Assert.Equal("Leon", restored.Menu.Character!.Name);
        Assert.Equal("Changed outside lesson", names[0]);
    }

    [Fact]
    public void PreparedOriginalGfLearningInputKeepsItsSemanticTargetWhenRowsReorder()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original tutorial resources are required.");
        using GameAssets assets = new(directory);
        NativeTutorialCatalog catalog = assets.ReadNativeTutorialCatalog();
        NativeTutorialPlayer player = new(catalog, LiveMenuCatalog.ReadInstalled(assets), 4);
        bool reached = false;
        for (int tick = 0; tick < 10000 && !player.IsComplete; tick++)
        {
            JsonElement state = player.Capture();
            if (state.GetProperty("ProgramCounter").GetInt32() == 213 && state.GetProperty("InputPrepared").GetBoolean())
            {
                reached = true;
                break;
            }
            player.Apply(new PlayerAction(player.WaitingForInput ? PlayerActionKind.Confirm : PlayerActionKind.Wait));
        }
        Assert.True(reached);
        Assert.Equal("10", player.Menu.SelectedEntry!.Id);
        MenuScreenDefinition original = player.Menu.Definitions.Single(screen => screen.Id == "gf-learn");
        player.Menu.Register(new MenuScreenDefinition { Id = original.Id, Title = original.Title, Help = original.Help, Layout = original.Layout, Columns = original.Columns, MainCommand = original.MainCommand, Details = original.Details, BuildContent = original.BuildContent, Entries = controller => original.Entries(controller).Reverse().ToArray() });
        Assert.True(player.Apply(new PlayerAction(PlayerActionKind.Wait)));
        Assert.Equal("10", player.Menu.SelectedEntry!.Id);
        Assert.Equal((byte)83, player.Party.GuardianForces[1].Learning);
        Assert.True(player.Apply(new PlayerAction(PlayerActionKind.Wait)));
        Assert.Equal((byte)10, player.Party.GuardianForces[1].Learning);
    }
}
