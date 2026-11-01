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
    [InlineData("junction-gf", "manage-gfs")]
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
    public void StockPaginationKeepsTheSelectedCharacterAndEveryDashboardGroup()
    {
        MenuController menu = new();
        menu.Open("junction-magic", demo: true);
        string character = menu.Character!.Id;
        menu.Data.Magic.Clear();
        for (int index = 1; index <= 32; index++)
        {
            menu.Data.Magic.Add(new MenuMagic { Id = "spell-" + index, Name = $"Magic {index}", CharacterId = character, Quantity = 100 });
        }
        menu.Execute(new("junction-ui", "slot-HP"));
        string[] firstPage = CandidateIds();
        menu.Navigate(MenuInput.PageDown);
        Assert.Equal("junction-dashboard", menu.ScreenId);
        Assert.Equal(character, menu.Character!.Id);
        string[] secondPage = CandidateIds();
        Assert.Equal(8, firstPage.Length);
        Assert.Equal(8, secondPage.Length);
        Assert.Empty(firstPage.Intersect(secondPage));
        Assert.Contains(menu.Entries, entry => entry.Id == "slot-ST-Atk");
        menu.Navigate(MenuInput.PageUp);
        Assert.Equal(firstPage, CandidateIds());
        Assert.Equal(character, menu.Character!.Id);

        string[] CandidateIds() => menu.Entries.Where(entry => entry.Id.StartsWith("candidate-", StringComparison.Ordinal) && entry.Id is not ("candidate-previous" or "candidate-next")).Select(entry => entry.Id).ToArray();
    }
}
