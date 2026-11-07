using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class NativeTutorialJunctionPlaybackTests
{
    [Theory]
    [InlineData(2, 3, 100, 111, "Elem-Atk")]
    [InlineData(3, 0, 97, 108, "ST-Atk")]
    public void DemonstrationUsesRegularMagicChoicesAndCancelKeepsTheAssignment(int id, int inventoryIndex, int reopenPc, int cancelPc, string slot)
    {
        using GameAssets assets = OpenInstalledAssets();
        NativeTutorialCatalog catalog = assets.ReadNativeTutorialCatalog();
        LiveMenuCatalog menus = LiveMenuCatalog.ReadInstalled(assets);
        NativeTutorialPlayer player = new(catalog, menus, id);
        int row = catalog.Get(id).Descriptor.CharacterId;
        int spell = player.Party.Records[row].MagicInventory[inventoryIndex].SpellId;
        string candidate = $"candidate-magic-{row}-{spell}";
        bool reopened = false;
        bool canceled = false;
        player.OperationExecuted += (pc, _) =>
        {
            reopened |= pc == reopenPc;
            canceled |= pc == cancelPc;
        };
        AdvanceUntil(player, () => reopened && player.WaitingForInput);

        MenuController ordinary = new(() => player.Menu.Data);
        ordinary.Open("junction");
        ordinary.Execute(new MenuAction("select-character", $"character-{row}"));
        ordinary.SelectEntry("junction-dashboard", "slot-" + slot);
        ordinary.Navigate(MenuInput.Confirm);

        UiButton focused = Assert.IsType<UiButton>(player.Menu.BuildView().FocusedElement);
        Assert.True(focused.IsFocused);
        Assert.False(focused.Bounds.IsEmpty);
        Assert.Equal(ordinary.BuildView().FocusedElement?.Id, focused.Id);
        Assert.Equal(ordinary.BuildView().FocusedElement?.Bounds, focused.Bounds);
        Assert.Equal(ordinary.Entries.Select(entry => entry.Id), player.Menu.Entries.Select(entry => entry.Id));
        Assert.Equal(ordinary.Entries.Where(entry => entry.Id.StartsWith("candidate-", StringComparison.Ordinal)), player.Menu.Entries.Where(entry => entry.Id.StartsWith("candidate-", StringComparison.Ordinal)));
        Assert.DoesNotContain(player.Menu.Entries, entry => entry.Id == candidate);

        NativeTutorialSnapshot saved = player.Capture().Deserialize<NativeTutorialSnapshot>()!;
        JunctionDashboardNavigationState oldPicker = saved.Navigation.JunctionDashboard! with { Candidate = candidate[10..], Focus = candidate };
        NativeTutorialSnapshot earlier = saved with { Navigation = saved.Navigation with { JunctionDashboard = oldPicker } };
        NativeTutorialPlayer restored = new(catalog, menus, id, JsonSerializer.SerializeToElement(earlier));
        Assert.Equal(focused.Id, restored.Menu.BuildView().FocusedElement?.Id);
        Assert.Equal(saved.ProgramCounter, restored.Capture().Deserialize<NativeTutorialSnapshot>()!.ProgramCounter);
        Assert.Equal(player.Party.Records[row].Junction.MagicJunctions, restored.Party.Records[row].Junction.MagicJunctions);

        NativeTutorialSnapshot legacy = saved with { Version = 1, Navigation = saved.Navigation with { ScreenId = "junction-stock", MagicId = candidate[10..], JunctionSlot = slot, JunctionDashboard = null } };
        NativeTutorialPlayer restoredLegacy = new(catalog, menus, id, JsonSerializer.SerializeToElement(legacy));
        Assert.Equal(focused.Id, restoredLegacy.Menu.BuildView().FocusedElement?.Id);
        Assert.Equal(saved.ProgramCounter, restoredLegacy.Capture().Deserialize<NativeTutorialSnapshot>()!.ProgramCounter);
        Assert.Equal(player.Party.Records[row].Junction.MagicJunctions, restoredLegacy.Party.Records[row].Junction.MagicJunctions);

        byte[] assigned = player.Party.Records[row].Junction.MagicJunctions.ToArray();
        AdvanceUntil(player, () => canceled);

        Assert.Equal("", player.Menu.CaptureJunctionDashboard().Editor);
        Assert.Equal("slot-" + slot, player.Menu.BuildView().FocusedElement?.Id);
        Assert.Equal(assigned, player.Party.Records[row].Junction.MagicJunctions);
    }

    [Fact]
    public void StatOptimizationDisplaysApplyReviewBeforeCommitting()
    {
        using GameAssets assets = OpenInstalledAssets();
        NativeTutorialCatalog catalog = assets.ReadNativeTutorialCatalog();
        NativeTutorialPlayer player = new(catalog, LiveMenuCatalog.ReadInstalled(assets), 1);
        int row = catalog.Get(1).Descriptor.CharacterId;
        AdvanceUntil(player, () => player.Menu.BuildView().FocusedElement?.Id == "junction-apply");

        Assert.True(player.Menu.CaptureJunctionDashboard().Review);
        byte[] assigned = player.Party.Records[row].Junction.MagicJunctions.ToArray();
        player.Apply(new PlayerAction(PlayerActionKind.Wait));
        Assert.Equal("junction-apply", player.Menu.BuildView().FocusedElement?.Id);
        Assert.Equal(assigned, player.Party.Records[row].Junction.MagicJunctions);

        AdvanceUntil(player, () => !player.Menu.CaptureJunctionDashboard().Review);
        Assert.False(assigned.SequenceEqual(player.Party.Records[row].Junction.MagicJunctions));
    }

    private static GameAssets OpenInstalledAssets()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original tutorial resources are required.");
        return new GameAssets(directory);
    }

    private static void AdvanceUntil(NativeTutorialPlayer player, Func<bool> condition)
    {
        for (int tick = 0; tick < 30000 && !player.IsComplete; tick++)
        {
            if (condition())
            {
                return;
            }
            player.Apply(new PlayerAction(player.WaitingForInput ? PlayerActionKind.Confirm : PlayerActionKind.Wait));
        }
        Assert.Fail($"The Junction lesson did not reach the expected presentation boundary: {player.Status}.");
    }
}
