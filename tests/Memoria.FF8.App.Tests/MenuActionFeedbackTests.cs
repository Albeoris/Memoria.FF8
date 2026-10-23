using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class MenuActionFeedbackTests
{
    [Theory]
    [InlineData("item-use")]
    [InlineData("magic-use")]
    [InlineData("magic-transfer")]
    [InlineData("item-sort")]
    [InlineData("junction-command")]
    [InlineData("junction-ability")]
    [InlineData("junction-exchange")]
    [InlineData("gf-learn")]
    [InlineData("refine")]
    [InlineData("party-switch")]
    [InlineData("shop-buy")]
    [InlineData("shop-sell")]
    public void SuccessfulLiveActionClearsOldErrorAndRestoresOrdinaryHelp(string kind)
    {
        MenuData data = MenuDemoData.Create();
        data.SupportedActions.Clear();
        data.SupportedActions.Add(kind);
        List<MenuAction> requests = [];
        MenuController controller = new(() => data, requests.Add);
        controller.Open("item-use");
        controller.Execute(new MenuAction("select-character", data.Characters[0].Id));
        controller.Execute(new MenuAction("select-item", data.Items[0].Id));
        controller.Execute(new MenuAction("select-magic", data.Magic[0].Id));
        controller.Execute(new MenuAction("unavailable-action"));
        Assert.Contains("Unsupported native contract", controller.Message);

        controller.Execute(new MenuAction(kind, TargetId: data.Characters[0].Id));

        Assert.Equal(kind, Assert.Single(requests).Kind);
        Assert.Empty(controller.Message);
        string help = controller.SelectedEntry?.Help ?? controller.CurrentScreen.Help;
        Assert.False(string.IsNullOrEmpty(help));
        Assert.Contains(Descendants(controller.BuildView().Root).OfType<UiLabel>(), label => label.Text == help);
    }

    [Fact]
    public void ConsumingLastLiveItemReturnsToInventoryWithoutReplacingHelp()
    {
        MenuData data = MenuDemoData.Create();
        MenuItem item = data.Items[0];
        item.Quantity = 1;
        item.NativeFieldUseSupported = true;
        data.Characters[0].NativeFieldItemTargetAllowed = true;
        data.SupportedActions.Add("item-use");
        MenuController controller = new(() => data, action =>
        {
            Assert.Equal(item.Id, action.SubjectId);
            item.Quantity--;
        });
        controller.Open("item-use");
        controller.Navigate(MenuInput.Confirm);
        Assert.Equal("item-target", controller.ScreenId);

        controller.Navigate(MenuInput.Confirm);

        Assert.Equal(0, item.Quantity);
        Assert.Equal("item-use", controller.ScreenId);
        Assert.Empty(controller.Message);
        Assert.NotNull(controller.BuildView());
    }

    [Fact]
    public void UnavailableMagicKeepsItsErrorAndDoesNotDispatch()
    {
        MenuData data = MenuDemoData.Create();
        data.SupportedActions.Add("magic-use");
        MenuController controller = new(() => data, _ => Assert.Fail("Unavailable magic must not dispatch."));
        controller.Open("magic-use");
        controller.Execute(new MenuAction("select-character", data.Characters[0].Id));
        controller.Execute(new MenuAction("select-magic", "unavailable-magic"));

        controller.Execute(new MenuAction("magic-use", TargetId: data.Characters[0].Id));

        Assert.Contains("no longer available", controller.Message);
        Assert.Contains(Descendants(controller.BuildView().Root).OfType<UiLabel>(), label => label.Text == controller.Message);
    }

    private static IEnumerable<UiElement> Descendants(UiElement element)
    {
        yield return element;
        foreach (UiElement child in element.Children)
        {
            foreach (UiElement descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }
}
