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
    public void IneligibleMagicHasVisibleFocusAndCancelKeepsTheAssignment(int id, int inventoryIndex, int reopenPc, int cancelPc, string slot)
    {
        using GameAssets assets = OpenInstalledAssets();
        NativeTutorialCatalog catalog = assets.ReadNativeTutorialCatalog();
        NativeTutorialPlayer player = new(catalog, LiveMenuCatalog.ReadInstalled(assets), id);
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
        AdvanceUntil(player, () => reopened && player.Menu.BuildView().FocusedElement?.Id == candidate);

        UiButton focused = Assert.IsType<UiButton>(player.Menu.BuildView().FocusedElement);
        Assert.True(focused.IsFocused);
        Assert.False(focused.Bounds.IsEmpty);
        Assert.Null(focused.Click);
        Assert.Equal("magic", player.Menu.CaptureJunctionDashboard().Editor);
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

    [Theory]
    [InlineData(1280, 720)]
    [InlineData(640, 480)]
    public void JunctionLessonDialogLeavesTheDashboardFocusUncovered(int width, int height)
    {
        using GameAssets assets = OpenInstalledAssets();
        NativeTutorialPlayer player = new(assets.ReadNativeTutorialCatalog(), LiveMenuCatalog.ReadInstalled(assets), 0);
        player.Menu.PrepareViewport(width, height);
        AdvanceUntil(player, () => player.WaitingForInput);

        UiView view = player.Menu.BuildView();
        UiElement focused = Assert.IsAssignableFrom<UiElement>(view.FocusedElement);
        UiTextPopup message = Assert.Single(Descendants(view.Root).OfType<UiTextPopup>());
        Assert.Equal("native-tutorial-message", message.Id);
        Assert.False(message.Bounds.IsEmpty);
        Assert.True(message.Bounds.Intersect(focused.Bounds).IsEmpty);
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

    private static IEnumerable<UiElement> Descendants(UiElement root)
    {
        yield return root;
        foreach (UiElement child in root.Children)
        {
            foreach (UiElement descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }
}
