using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class JunctionNavigationTests
{
    [Theory]
    [InlineData("junction-magic", "slot-HP")]
    [InlineData("junction-element", "slot-Elem-Atk")]
    [InlineData("junction-status", "slot-ST-Atk")]
    [InlineData("junction-command", "command-0")]
    [InlineData("junction-ability", "passive-0")]
    [InlineData("junction-gf", "gf-portrait-0")]
    public void OrdinaryLegacyEntriesSelectTheCorrespondingPersistentDashboardGroup(string screen, string focus)
    {
        MenuController menu = new();
        menu.Open(screen, demo: true);
        Assert.Equal("junction-dashboard", menu.ScreenId);
        Assert.Equal(focus, menu.BuildView().FocusedElement?.Id);
        Assert.Contains(menu.Entries, entry => entry.Id == "slot-HP");
        Assert.Contains(menu.Entries, entry => entry.Id == "slot-Elem-Atk");
        Assert.Contains(menu.Entries, entry => entry.Id == "slot-ST-Atk");
        Assert.Contains(menu.Entries, entry => entry.Id == "command-0");
        Assert.Contains(menu.Entries, entry => entry.Id == "passive-0");
    }

    [Fact]
    public void ClosingAnOrdinaryPickerReturnsToTheSameDashboardDestination()
    {
        MenuController menu = new();
        menu.Open("junction-magic", demo: true);
        menu.Execute(new("junction-ui", "slot-Str"));
        Assert.Contains(menu.Entries, entry => entry.Id.StartsWith("candidate-", StringComparison.Ordinal));
        menu.Navigate(MenuInput.Cancel);
        Assert.Equal("junction-dashboard", menu.ScreenId);
        Assert.Equal("slot-Str", menu.BuildView().FocusedElement?.Id);
        Assert.DoesNotContain(menu.Entries, entry => entry.Id.StartsWith("candidate-", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("junction-magic")]
    [InlineData("junction-element")]
    [InlineData("junction-status")]
    [InlineData("junction-stock")]
    public void ShoulderInputsSwitchCharactersWithinTheDashboard(string screen)
    {
        MenuController menu = new();
        menu.Open(screen, demo: true);
        string character = menu.Character!.Id;
        string? focus = menu.BuildView().FocusedElement?.Id;
        menu.Navigate(MenuInput.PageRight);
        Assert.Equal("junction-dashboard", menu.ScreenId);
        Assert.NotEqual(character, menu.Character!.Id);
        Assert.Equal(focus, menu.BuildView().FocusedElement?.Id);
        menu.Navigate(MenuInput.PageLeft);
        Assert.Equal(character, menu.Character!.Id);
    }

    [Fact]
    public void StockColumnScrollingKeepsAllCandidatesAccessibleAndPreservesTheCharacterAndDashboardGroups()
    {
        MenuController menu = new();
        menu.Open("junction-magic", demo: true);
        string character = menu.Character!.Id;
        menu.Data.Magic.Clear();
        for (int index = 1; index <= 32; index++)
        {
            menu.Data.Magic.Add(new MenuMagic { Id = "spell-" + index, Name = $"A long magic name {index}", CharacterId = character, Quantity = 100 });
        }
        menu.Execute(new("junction-ui", "slot-HP"));
        string[] candidates = CandidateIds();
        Assert.Equal(33, candidates.Length);
        string? initialFocus = menu.BuildView().FocusedElement?.Id;
        int rightSteps = 0;
        while (Picker().FirstVisibleColumn == 0 && rightSteps < candidates.Length)
        {
            menu.Navigate(MenuInput.Right);
            rightSteps++;
            AssertDashboardAndVisibleFocus();
        }
        Assert.True(Picker().FirstVisibleColumn > 0);
        Assert.Contains(Picker().Children, child => !child.Visible);
        string? beforeDown = menu.BuildView().FocusedElement?.Id;
        menu.Navigate(MenuInput.Down);
        AssertDashboardAndVisibleFocus();
        Assert.NotEqual(beforeDown, menu.BuildView().FocusedElement?.Id);
        menu.Navigate(MenuInput.Up);
        for (int step = 0; step < rightSteps; step++)
        {
            menu.Navigate(MenuInput.Left);
            AssertDashboardAndVisibleFocus();
        }
        Assert.Equal(initialFocus, menu.BuildView().FocusedElement?.Id);
        Assert.Equal(0, Picker().FirstVisibleColumn);

        string[] CandidateIds() => menu.Entries.Where(entry => entry.Id.StartsWith("candidate-", StringComparison.Ordinal) && entry.Id is not ("candidate-previous" or "candidate-next")).Select(entry => entry.Id).ToArray();

        UiColumnPicker Picker() => Assert.Single(Descendants(menu.BuildView().Root).OfType<UiColumnPicker>());

        void AssertDashboardAndVisibleFocus()
        {
            Assert.Equal("junction-dashboard", menu.ScreenId);
            Assert.Equal(character, menu.Character!.Id);
            Assert.Equal(candidates, CandidateIds());
            Assert.DoesNotContain(menu.Entries, entry => entry.Id is "candidate-previous" or "candidate-next");
            foreach (string group in new[] { "slot-HP", "slot-Elem-Atk", "slot-ST-Atk", "command-0", "passive-0" })
            {
                Assert.Contains(menu.Entries, entry => entry.Id == group);
            }
            UiElement focused = Assert.IsType<UiButton>(menu.BuildView().FocusedElement);
            Assert.True(focused.Visible);
            Assert.Contains(focused, Picker().Children);
            UiRect viewport = Picker().Bounds;
            Assert.True(focused.Bounds.X >= viewport.X - 0.01f && focused.Bounds.Right <= viewport.Right + 0.01f);
            Assert.True(focused.Bounds.Y >= viewport.Y - 0.01f && focused.Bounds.Bottom <= viewport.Bottom + 0.01f);
        }
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
