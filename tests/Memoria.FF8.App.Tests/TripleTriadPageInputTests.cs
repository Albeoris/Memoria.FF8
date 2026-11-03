using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.App.Tests;

public sealed class TripleTriadPageInputTests
{
    [Theory]
    [InlineData(Keys.D, 2)]
    [InlineData(Keys.F, 1)]
    [InlineData(Keys.H, 2)]
    [InlineData(Keys.G, 1)]
    public void EscapeAndRotationKeysPageTradeOncePerPress(Keys key, int page)
    {
        TripleTriadController controller = CreateTrade();
        UiView view = BuildView(controller);
        KeyboardInput keyboard = new();
        Route(keyboard.Read(new KeyboardState(key), true));
        Assert.Equal(page, controller.TradePage);
        Route(keyboard.Read(new KeyboardState(key), true));
        Assert.Equal(page, controller.TradePage);

        void Route(InputFrame input) => RecreateGame.RouteTripleTriadPageInput(controller, view, input, keyboard);
    }

    [Theory]
    [InlineData(Buttons.LeftTrigger, 2)]
    [InlineData(Buttons.RightTrigger, 1)]
    [InlineData(Buttons.LeftShoulder, 2)]
    [InlineData(Buttons.RightShoulder, 1)]
    public void EscapeAndRotationControllerBindingsPageTrade(Buttons button, int page)
    {
        TripleTriadController controller = CreateTrade();
        KeyboardInput keyboard = new();
        keyboard.Read(default, new GamePadState(Vector2.Zero, Vector2.Zero, 0, 0, Buttons.None), true);
        GamePadState pressed = new(Vector2.Zero, Vector2.Zero, button == Buttons.LeftTrigger ? 1 : 0, button == Buttons.RightTrigger ? 1 : 0, button);
        InputFrame input = keyboard.Read(default, pressed, true);
        RecreateGame.RouteTripleTriadPageInput(controller, BuildView(controller), input, keyboard);
        Assert.Equal(page, controller.TradePage);
    }

    [Fact]
    public void RemappedPageBindingAndPageShortcutDispatchOnePage()
    {
        TripleTriadController controller = CreateTrade();
        KeyboardInput keyboard = new()
        {
            Profile = ControlsProfile.CreateDefault().WithBinding(ControlAction.CameraRight, ControlDevice.Keyboard, new InputChord([Keys.Q], []))
        };
        InputFrame input = keyboard.Read(new KeyboardState(Keys.Q, Keys.G, Keys.PageDown), true);
        RecreateGame.RouteTripleTriadPageInput(controller, BuildView(controller), input, keyboard);
        Assert.Equal(1, controller.TradePage);
    }

    [Fact]
    public void RotationBindingDoesNotChangeDeckSelectionPage()
    {
        TripleTriadCard[] cards = Enumerable.Range(0, 110).Select(id => new TripleTriadCard(id, 5, 5, 5, 5, TripleTriadElement.None)).ToArray();
        TripleTriadController controller = new(cards, Enumerable.Range(0, 20).ToDictionary(id => id, _ => 1), [20, 21, 22, 23, 24], id => id.ToString());
        KeyboardInput keyboard = new();
        InputFrame input = keyboard.Read(new KeyboardState(Keys.G), true);
        RecreateGame.RouteTripleTriadPageInput(controller, BuildView(controller), input, keyboard);
        Assert.Equal(0, controller.SelectionPage);
    }

    [Fact]
    public void LossAcknowledgementBrowsesEveryPageWithoutChangingTransfer()
    {
        TripleTriadController controller = CreateTrade();
        TripleTriadCard[] cards = Enumerable.Range(0, 110).Select(id => new TripleTriadCard(id, 5, 5, 5, 5, TripleTriadElement.None)).ToArray();
        int[] player = Enumerable.Range(0, 13).ToArray();
        int[] opponent = Enumerable.Range(13, 13).ToArray();
        TripleTriadMatch match = TripleTriadMatch.Create(cards, player, opponent, width: 4, height: 4);
        match.CompleteInstantly(false);
        controller.RestoreMatch(match);
        controller.EnablePresentation();
        int completed = 0;
        controller.BeginTradeSettlement(player, opponent, match.OriginalInstanceFinalOwners, TripleTriadTradeRule.One, 1, false, [12], _ => completed++, Enumerable.Range(0, 26).Select(index => index == 12).ToArray());
        for (int frame = 0; frame < 5 && !controller.AwaitingTradeConfirmation; frame++)
        {
            controller.AdvancePresentation(TimeSpan.FromSeconds(10));
        }
        Assert.True(controller.AwaitingTradeConfirmation);
        Assert.False(controller.TradingToPlayer);
        Assert.Equal(12, controller.TradingCardIndex);
        Assert.Equal(2, controller.TradePage);
        KeyboardInput keyboard = new();
        UiView view = BuildView(controller);
        InputFrame input = keyboard.Read(new KeyboardState(Keys.F), true);
        RecreateGame.RouteTripleTriadPageInput(controller, view, input, keyboard);
        Assert.Equal(0, controller.TradePage);
        view.Pointer(580, 610, true);
        view.Pointer(580, 610, false);
        Assert.Equal(1, controller.TradePage);
        Assert.True(controller.AwaitingTradeConfirmation);
        Assert.Equal(12, controller.TradingCardIndex);
        Assert.Equal(0, completed);
        view.Key(UiKey.Confirm);
        Assert.Equal(TripleTriadTradePhase.Exiting, controller.TradePhase);
        Assert.Equal(2, controller.TradePage);
        controller.AdvancePresentation(TimeSpan.FromSeconds(10));
        Assert.Equal(1, completed);
    }

    private static UiView BuildView(TripleTriadController controller)
    {
        UiView view = controller.BuildView();
        view.Resize(960, 672);
        return view;
    }

    private static TripleTriadController CreateTrade()
    {
        TripleTriadCard[] cards = Enumerable.Range(0, 110).Select(id => new TripleTriadCard(id, 5, 5, 5, 5, TripleTriadElement.None)).ToArray();
        int[] player = Enumerable.Range(0, 13).ToArray();
        int[] opponent = Enumerable.Range(13, 13).ToArray();
        TripleTriadController controller = new(cards, player.ToDictionary(id => id, _ => 1), opponent, id => id.ToString(), options: new TripleTriadSettings { Width = 4, Height = 4, HandSize = 13 });
        controller.BeginTradeSelection(opponent, 1, _ =>
        {
        });
        return controller;
    }
}
