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
        string directory = TestResources.GameDirectory;
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
        string directory = TestResources.GameDirectory;
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
    [Fact]
    public void OriginalMagicCursorPreviewsStatsWithoutCommittingUntilNativeConfirm()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original tutorial resources are required.");
        using GameAssets assets = new(directory);
        NativeTutorialCatalog catalog = assets.ReadNativeTutorialCatalog();
        NativeTutorialPlayer player = new(catalog, LiveMenuCatalog.ReadInstalled(assets), 1);
        AdvanceUntilOperation(player, 87);
        NativeCharacterVitals before = player.Party;
        JsonElement original = JsonSerializer.SerializeToElement(before);
        int fire = before.Records[0].MagicInventory[0].SpellId;
        int blizzard = before.Records[0].MagicInventory[1].SpellId;
        int thunder = before.Records[0].MagicInventory[2].SpellId;
        Assert.Equal((1, 7, 4), (fire, blizzard, thunder));
        AdvanceUntilOperation(player, 101);
        int blizzardStrength = catalog.ReadStatistics(before.SetJunctionMagic(0, 1, blizzard), 0).Strength;
        Assert.Equal($"magic-0-{blizzard}", player.Menu.SelectedEntry!.Id);
        Assert.Equal(blizzardStrength, player.Menu.Character!.Stats["Str"]);
        Assert.NotEqual(catalog.ReadStatistics(before, 0).Strength, blizzardStrength);
        Assert.True(JsonElement.DeepEquals(original, JsonSerializer.SerializeToElement(player.Party)));
        AdvanceUntilOperation(player, 103);
        Assert.Equal($"magic-0-{thunder}", player.Menu.SelectedEntry!.Id);
        Assert.Equal(catalog.ReadStatistics(before.SetJunctionMagic(0, 1, thunder), 0).Strength, player.Menu.Character!.Stats["Str"]);
        Assert.True(JsonElement.DeepEquals(original, JsonSerializer.SerializeToElement(player.Party)));
        AdvanceUntilOperation(player, 115);
        Assert.Equal(fire, player.Party.Records[0].Junction.MagicJunctions[1]);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(before.SetJunctionMagic(0, 1, fire)), JsonSerializer.SerializeToElement(player.Party)));
    }

    [Fact]
    public void OriginalCureElementCursorRetainsItsPreviousEligibleJunctionUntilCancel()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original tutorial resources are required.");
        using GameAssets assets = new(directory);
        NativeTutorialCatalog catalog = assets.ReadNativeTutorialCatalog();
        LiveMenuCatalog menus = LiveMenuCatalog.ReadInstalled(assets);
        NativeTutorialPlayer player = new(catalog, menus, 2);
        AdvanceUntilOperation(player, 100);
        NativeCharacterVitals before = player.Party;
        JsonElement original = JsonSerializer.SerializeToElement(before);
        int thunder = before.Records[0].MagicInventory[2].SpellId;
        int cure = before.Records[0].MagicInventory[3].SpellId;
        Assert.Equal("Cure", menus.MagicName(cure));
        Assert.Equal(thunder, before.Records[0].Junction.MagicJunctions[9]);
        Assert.Equal((byte)0, catalog.ReadStatRules(before, 0).MagicRecords[cure][0x20]);
        AdvanceUntilOperation(player, 102);
        Assert.Equal($"magic-0-{cure}", player.Menu.SelectedEntry!.Id);
        Assert.Equal(menus.MagicName(thunder), player.Menu.Character!.Junctions["Elem-Atk"]);
        Assert.True(JsonElement.DeepEquals(original, JsonSerializer.SerializeToElement(player.Party)));
        NativeTutorialPlayer restored = new(catalog, menus, 2, player.Capture());
        Assert.Equal(menus.MagicName(thunder), restored.Menu.Character!.Junctions["Elem-Atk"]);
        Assert.True(JsonElement.DeepEquals(original, JsonSerializer.SerializeToElement(restored.Party)));
        AdvanceUntilOperation(player, 111);
        Assert.Equal("junction-element", player.Menu.ScreenId);
        Assert.True(JsonElement.DeepEquals(original, JsonSerializer.SerializeToElement(player.Party)));
    }

    private static void AdvanceUntilOperation(NativeTutorialPlayer player, int operation)
    {
        bool reached = false;
        void Observe(int pc, ushort word)
        {
            reached |= pc == operation;
        }
        player.OperationExecuted += Observe;
        try
        {
            for (int tick = 0; tick < 10000 && !reached && !player.IsComplete; tick++)
            {
                Assert.True(player.Apply(new PlayerAction(player.WaitingForInput ? PlayerActionKind.Confirm : PlayerActionKind.Wait)));
            }
        }
        finally
        {
            player.OperationExecuted -= Observe;
        }
        Assert.True(reached, $"Original tutorial operation{operation} was not reached.");
    }

}
