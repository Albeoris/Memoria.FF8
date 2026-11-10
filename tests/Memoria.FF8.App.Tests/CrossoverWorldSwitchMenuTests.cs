using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverWorldSwitchMenuTests
{
    [Theory]
    [InlineData(MenuPresentation.Vector, false)]
    [InlineData(MenuPresentation.Vector, true)]
    [InlineData(MenuPresentation.Classic, false)]
    [InlineData(MenuPresentation.Classic, true)]
    public void DisabledWorldSwitchShowsTheInstallationIssueInTheRightPanel(MenuPresentation presentation, bool missingDirectory)
    {
        string? directory = missingDirectory ? Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")) : null;
        string issue = Assert.IsType<string>(RecreateGame.GetFf7InstallationIssue(directory));
        Assert.StartsWith(missingDirectory ? "The selected FF7 resource root does not exist." : "No FINAL FANTASY VII folder was selected.", issue);
        Assert.Contains("launcher's FINAL FANTASY VII tab", issue);

        bool opened = false;
        MenuController menu = new() { Presentation = presentation };
        menu.ConnectWorldSwitch(() => issue, () => opened = true);
        menu.Open("debug-room", demo: true);
        Assert.Equal("world-switch", menu.SelectedEntry?.Id);
        Assert.False(menu.SelectedEntry!.Enabled);
        UiLabel explanation = Assert.Single(Descendants(menu.BuildView().Root).OfType<UiLabel>(), label => label.Id == "world-switch-explanation");
        Assert.Equal(issue, explanation.Text);
        Assert.Equal(UiTextFlow.Wrap, explanation.TextFlow);
        menu.Navigate(MenuInput.Confirm);
        Assert.False(opened);
        menu.Navigate(MenuInput.Down);
        Assert.DoesNotContain(Descendants(menu.BuildView().Root), element => element.Id == "world-switch-explanation");
    }

    private static IEnumerable<UiElement> Descendants(UiElement root)
    {
        yield return root;
        foreach (UiElement child in root.Children)
        {
            foreach (UiElement element in Descendants(child))
            {
                yield return element;
            }
        }
    }
}
