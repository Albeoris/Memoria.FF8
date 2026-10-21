using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class GuardianForceTransferMenuTests
{
    [Fact]
    public void AssignedGuardiansShowTheirOwnerAndRemainSelectable()
    {
        MenuData data = Data();
        data.Characters[0].GuardianForceIds.Add("gf-0");
        data.GuardianForces.Add(new MenuGuardianForce { Id = "gf-0", Name = "Quezacotl", NativeId = 0 });
        data.GuardianForces.Add(new MenuGuardianForce { Id = "gf-1", Name = "Shiva", NativeId = 1 });
        MenuController menus = new(() => data);
        menus.Open("junction-gf");
        UiList list = Assert.Single(Descendants(menus.BuildView().Root).OfType<UiList>(), list => list.Id == "commands");
        Assert.All(menus.Entries, entry => Assert.True(entry.Enabled));
        Assert.Empty(list.SecondaryItems);
        Assert.Equal(new UiImageSource?[] { new(UiImageKind.CharacterPortrait, 0), new(UiImageKind.CharacterPortrait, 4), null }, list.TrailingImages);
        Assert.DoesNotContain(menus.Entries, entry => entry.Label.StartsWith("J ", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TransferringRequiresExplicitYesAndRetainsBothIdentities(bool confirm)
    {
        MenuData data = Data();
        List<MenuAction> actions = [];
        MenuController menus = new(() => data, actions.Add);
        menus.Open("junction-gf");
        menus.Navigate(MenuInput.Confirm);
        Assert.Equal("junction-gf-confirm", menus.ScreenId);
        Assert.Equal("no", menus.SelectedEntry?.Id);
        Assert.Empty(actions);
        Assert.Contains("Squall", menus.CurrentScreen.Help);
        Assert.Contains("Quistis", menus.CurrentScreen.Help);
        menus.Navigate(MenuInput.PageRight);
        Assert.Equal("character-4", menus.Character?.Id);
        if (confirm)
        {
            menus.Navigate(MenuInput.Down);
        }
        menus.Navigate(MenuInput.Confirm);
        Assert.Equal("junction-gf", menus.ScreenId);
        if (confirm)
        {
            Assert.Equal(new MenuAction("junction-gf-reassign", "gf-2", "character-4", "character-0"), Assert.Single(actions));
        }
        else
        {
            Assert.Empty(actions);
        }
    }

    [Fact]
    public void CancelAndChangedOwnershipNeverDispatchTransfer()
    {
        MenuData data = Data();
        List<MenuAction> actions = [];
        MenuController menus = new(() => data, actions.Add);
        menus.Open("junction-gf");
        menus.Navigate(MenuInput.Confirm);
        menus.Navigate(MenuInput.Cancel);
        Assert.Equal("junction-gf", menus.ScreenId);
        Assert.Empty(actions);
        menus.Navigate(MenuInput.Confirm);
        data.Characters[1].GuardianForceIds.Clear();
        menus.Navigate(MenuInput.Down);
        menus.Navigate(MenuInput.Confirm);
        Assert.Empty(actions);
        Assert.Equal("junction-gf", menus.ScreenId);
        Assert.Contains("ownership changed", menus.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OwnAndFreeGuardiansKeepImmediateToggle(bool owned)
    {
        MenuData data = Data();
        data.Characters[1].GuardianForceIds.Clear();
        if (owned)
        {
            data.Characters[0].GuardianForceIds.Add("gf-2");
        }
        List<MenuAction> actions = [];
        MenuController menus = new(() => data, actions.Add);
        menus.Open("junction-gf");
        menus.Navigate(MenuInput.Confirm);
        Assert.Equal("junction-gf", menus.ScreenId);
        Assert.Equal(new MenuAction("junction-gf", "gf-2", "character-4"), Assert.Single(actions));
    }

    [Theory]
    [InlineData("100%", 24)]
    [InlineData("150%", 48)]
    public void LearningHasItsOwnVisibleRowAtEveryScale(string scale, int fontSize)
    {
        MenuData data = Data();
        data.Settings["Text scale"] = scale;
        MenuController menus = new(() => data);
        menus.ConnectPreferences(() => new UiPreferences { UiFontSize = fontSize }, _ => { });
        menus.Open("junction-gf");
        UiElement[] elements = Descendants(menus.BuildView().Root).ToArray();
        UiElement detail = Assert.Single(elements, element => element.Id == "junction-gf-details");
        UiLabel learning = Assert.Single(elements.OfType<UiLabel>(), label => label.Id == "junction-gf-learning");
        UiLabel caption = Assert.Single(Descendants(detail).OfType<UiLabel>(), label => label.Text == "Learning");
        Assert.Equal("SumMag+10%", learning.Text);
        Assert.Equal(UiTextFlow.SingleLine, learning.TextFlow);
        Assert.True(learning.Bounds.Height > 0);
        Assert.True(learning.Bounds.Y >= caption.Bounds.Bottom);
        Assert.True(learning.Bounds.Bottom <= detail.Bounds.Bottom);
    }

    private static MenuData Data()
    {
        MenuData data = new();
        data.Characters.Add(new MenuCharacter { Id = "character-4", NativeId = 4, Name = "Quistis", InParty = true });
        MenuCharacter owner = new() { Id = "character-0", NativeId = 0, Name = "Squall", InParty = true };
        owner.GuardianForceIds.Add("gf-2");
        data.Characters.Add(owner);
        data.GuardianForces.Add(new MenuGuardianForce { Id = "gf-2", NativeId = 2, Name = "Ifrit", Learning = "SumMag+10%", Level = 1, CurrentHp = 305, MaximumHp = 305 });
        data.SupportedActions.UnionWith(["junction-gf", "junction-gf-reassign"]);
        return data;
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
