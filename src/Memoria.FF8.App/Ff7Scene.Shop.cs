using Memoria.FF7.Runtime;
using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private MenuController? shopMenu;
    private long shopRequest;
    private double shopRemainder;
    private bool shopExitRequested;
    private bool shopConfirmedExit;
    private bool shopPointerPressed;
    private bool shopActionOwnsSound;

    private bool IsShopMenuPresentation => Session?.Host?.Process.Menu is { Mode: 8, Completed: false };

    private void EnsureShopMenu(Ff7ShopState state)
    {
        if (shopMenu is not null && shopRequest == state.RequestId)
        {
            return;
        }
        DisposeShopMenu();
        shopRequest = state.RequestId;
        shopMenu = new(() => Ff7ShopMenuProjection.Create(Session!), ApplyShopAction);
        shopMenu.Register(new()
        {
            Id = "shop",
            Title = "Shop",
            Help = "Select a command.",
            Entries = _ =>
            [
                new("buy", "Buy", "Choose an item to purchase.", "shop-buy"),
                new("sell", "Sell", "Choose an item to sell.", "shop-sell"),
                new("exit", "Exit", "Return to the game.", Action: new("ff7-shop-exit"))
            ]
        });
        shopMenu.ConnectReadOnlyTutorialControls(() => sharedInput.Profile, () => sharedInput.ControllerPresentation, () => sharedInput.LastInputDevice);
        shopMenu.SoundRequested += PlayShopMenuSound;
        shopMenu.Presentation = PresentationPreferences.MenuPresentation;
        shopMenu.Open("shop");
        shopPointerPressed = Mouse.GetState().LeftButton == ButtonState.Pressed;
        pendingPressedKeys = 0;
        moveHorizontal = 0;
        moveVertical = 0;
        tickRemainder = 0;
        sharedInput.SuppressHeldUiInput();
    }

    private void ApplyShopAction(MenuAction action)
    {
        shopActionOwnsSound = true;
        if (action.Kind == "ff7-shop-exit")
        {
            shopExitRequested = true;
            shopConfirmedExit = true;
            return;
        }
        Ff7Session session = Session!;
        Ff7ShopState state = session.Shop ?? throw new InvalidOperationException("The shop action lost its native module owner.");
        _ = session.ApplyShopTransaction(Ff7ShopMenuProjection.Transaction(state, action));
    }

    private void PlayShopMenuSound(MenuSound sound)
    {
        if (shopActionOwnsSound)
        {
            shopActionOwnsSound = false;
            return;
        }
        int nativeSound = sound switch
        {
            MenuSound.Confirm => 1,
            MenuSound.Cancel when shopMenu!.ScreenId != "shop" => 4,
            MenuSound.Error => 3,
            _ => 0
        };
        if (nativeSound != 0)
        {
            Session!.Host.SubmitNativeSound(new(5, nativeSound));
        }
    }

    private bool UpdateShop(GameTime time, KeyboardState keys, GamePadState pad)
    {
        if (Session!.Shop is not { Phase: not Ff7ShopPhase.Completed } state)
        {
            if (shopMenu is not null)
            {
                DisposeShopMenu();
                tickRemainder = 0;
                sharedInput.SuppressHeldUiInput();
            }
            return false;
        }
        EnsureShopMenu(state);
        Session.AdvanceClock(time.ElapsedGameTime);
        UpdateAudio();
        bool automatic = autoPlay?.Running == true && !autoPlay.Paused;
        if (state.Phase == Ff7ShopPhase.Interactive && !automatic && !shopExitRequested)
        {
            shopActionOwnsSound = false;
            UpdateSharedMenuInput(shopMenu!, keys, pad, ref shopPointerPressed);
            shopExitRequested = !shopMenu!.IsOpen || shopExitRequested;
        }
        shopRemainder += Math.Min(time.ElapsedGameTime.TotalSeconds, 0.25);
        while (shopRemainder + 1e-10 >= 1d / 60 && Session.Shop is { Phase: not Ff7ShopPhase.Completed })
        {
            shopRemainder = Math.Max(0, shopRemainder - 1d / 60);
            if (automatic)
            {
                AdvanceAutoPlay();
            }
            else
            {
                Session.AdvanceShop(shopExitRequested && !shopConfirmedExit, confirmedExit: shopConfirmedExit);
            }
            if (error is not null)
            {
                break;
            }
            RememberReportCheckpoint();
            UpdateAudio();
        }
        return true;
    }

    private void DrawShop(SpriteBatch batch, Rectangle bounds)
    {
        if (Session?.Shop is not { Phase: not Ff7ShopPhase.Completed } state)
        {
            return;
        }
        EnsureShopMenu(state);
        shopMenu!.Presentation = PresentationPreferences.MenuPresentation;
        shopMenu.PrepareViewport(bounds.Width, bounds.Height);
        UiView view = shopMenu.BuildView();
        view.Resize(bounds.Width, bounds.Height);
        view.Draw(dialogueCanvas!, SharedTheme());
        if (state.DisplayedAlpha != 0)
        {
            batch.Draw(pixel, bounds, Color.Black * (state.DisplayedAlpha / 255f));
        }
    }

    private void DisposeShopMenu()
    {
        if (shopMenu is not null)
        {
            shopMenu.SoundRequested -= PlayShopMenuSound;
            shopMenu.Close();
            shopMenu = null;
        }
        shopRequest = 0;
        shopRemainder = 0;
        shopExitRequested = false;
        shopConfirmedExit = false;
        shopActionOwnsSound = false;
    }
}
