using Memoria.FF8.GameUi;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8;

internal sealed record UiControlCommand(string? Screen = null, bool Demo = true, MenuInput? Input = null, float? X = null, float? Y = null, bool Pressed = false);

internal sealed partial class RecreateGame
{
    private MenuController? gameMenus;
    private MonoGameUiCanvas? uiCanvas;
    private readonly UiTheme uiTheme = new();
    private UiPreferences uiPreferences = new();
    private bool uiMovieSuspended;
    private GamePadState previousUiPad;
    private LiveMenuCatalog? liveMenuCatalog;
    private bool previousUiMousePressed;
    private bool uiControlPointerPressed;
    private bool drainUiButtons;
    private string UiPreferencesPath => Path.Combine(Path.GetDirectoryName(Path.GetFullPath(options.SavePath))!, "ui-settings.json");

    private void InitializeGameUi()
    {
        uiCanvas = new MonoGameUiCanvas(GraphicsDevice, batch, pixel, text);
        uiPreferences = UiPreferences.Load(UiPreferencesPath);
        fieldAudio?.SetOutputPreferences(uiPreferences.MusicVolume, uiPreferences.SoundVolume);
        gameMenus = new MenuController(ReadLiveMenuData, ApplyLiveMenuAction);
        gameMenus.BattleDebugRequested += OpenBattleDebug;
        if (options.DebugUiScreen is string screen)
        {
            gameMenus.Open(screen, true);
        }
    }

    private MenuData ReadLiveMenuData()
    {
        if (liveMenuCatalog is null)
        {
            using var assets = new Memoria.FF8.Assets.GameAssets(options.GameDirectory);
            liveMenuCatalog = LiveMenuCatalog.ReadInstalled(assets);
        }
        MenuData data = LiveMenuData.Build(session.State, events?.Scene.Names, liveMenuCatalog);
        data.Settings["Music volume"] = uiPreferences.MusicVolume + "%";
        data.Settings["Effect volume"] = uiPreferences.SoundVolume + "%";
        data.Settings["Text scale"] = uiPreferences.TextScale + "%";
        data.Settings["Battle ATB mode"] = uiPreferences.AtbMode.ToString();
        data.Settings["Battle policy"] = uiPreferences.BattlePolicy;
        data.Settings["Initiative bar"] = uiPreferences.ShowInitiative ? "On" : "Off";
        data.SupportedActions.Add("config:Music volume");
        data.SupportedActions.Add("config:Effect volume");
        data.SupportedActions.Add("config:Text scale");
        data.SupportedActions.Add("config:Battle ATB mode");
        data.SupportedActions.Add("config:Battle policy");
        data.SupportedActions.Add("config:Initiative bar");
        data.SaveAvailable = session.State.Scene != SceneKind.Title && !fieldFaulted;
        if (data.SaveAvailable)
        {
            data.SupportedActions.Add("save");
        }
        return data;
    }

    private void ApplyLiveMenuAction(MenuAction action)
    {
        if (action.Kind == "save")
        {
            gameMenus!.Close();
            OpenPlayerSaveMenu(PlayerSaveMenuMode.Save);
            return;
        }
        if (action.Kind == "config")
        {
            UiPreferences nextPreferences = uiPreferences.WithSetting(action.SubjectId, action.Value);
            nextPreferences.Save(UiPreferencesPath);
            uiPreferences = nextPreferences;
            fieldAudio?.SetOutputPreferences(uiPreferences.MusicVolume, uiPreferences.SoundVolume);
            if (ActiveBattle is not null && ActiveBattle.Mode != uiPreferences.AtbMode)
            {
                ApplyBattleCommand(new BattleInput(BattleInputKind.SetMode, Mode: uiPreferences.AtbMode));
            }
            return;
        }
        throw new NotSupportedException($"Live menu action '{action.Kind}' requires a proved game-owned command and causal replay contract.");
    }

    private bool UpdateGameUi(TimeSpan elapsed, InputFrame input)
    {
        if (gameMenus is null)
        {
            return false;
        }
        GamePadState pad = GamePad.GetState(PlayerIndex.One);
        if (!gameMenus.IsOpen && !saveMenu.IsOpen && IsNativeNormalMenuActive)
        {
            gameMenus.Open("main", false);
        }
        bool nativeMainWasOpen = IsNativeNormalMenuActive && gameMenus.IsOpen && !gameMenus.IsDemo;
        bool menuWasOpen = gameMenus.IsOpen;
        MouseState mouse = Mouse.GetState();
        bool mousePressed = IsActive && mouse.LeftButton == ButtonState.Pressed;
        bool mousePressEdge = mousePressed && !previousUiMousePressed;
        previousUiMousePressed = mousePressed;
        bool PadPressed(Buttons button) => IsActive && pad.IsButtonDown(button) && previousUiPad.IsButtonUp(button);
        bool openMenu = keyboard.GameMenuRequested || PadPressed(Buttons.Start);
        if (keyboard.DebugRoomRequested && !saveMenu.IsOpen)
        {
            if (gameMenus.IsOpen)
            {
                gameMenus.Close();
            }
            else
            {
                gameMenus.Open("debug-room", true);
            }
        }
        else if (openMenu && !saveMenu.IsOpen)
        {
            if (gameMenus.IsOpen)
            {
                gameMenus.Close();
            }
            else if (ActiveBattle is not null)
            {
                gameMenus.Open("config", false);
            }
            else if (session.State.Scene == SceneKind.Title || session.State.Scene == SceneKind.WorldMap || session.State.Scene == SceneKind.Field && events is { IsUserControlEnabled: true, CurrentDialogue: null } && events.Scene.MenuEnabled)
            {
                gameMenus.Open("main", false);
            }
        }
        if (menuWasOpen && !gameMenus.IsOpen)
        {
            drainUiButtons = true;
        }
        SynchronizeUiMoviePause();
        if (!gameMenus.IsOpen)
        {
            previousUiPad = pad;
            if (CompleteNativeNormalMenu(nativeMainWasOpen))
            {
                return true;
            }
            if (drainUiButtons && (keyboard.HeldFieldButtons & 0xF0) != 0)
            {
                if (events?.PartyMenu is not null)
                {
                    UpdatePartyMenu(gameplayClock.Advance(elapsed, session.State.Scene), default);
                }
                return true;
            }
            drainUiButtons = false;
            SynchronizeUiMoviePause();
            return false;
        }
        if (input.MenuVertical < 0 || PadPressed(Buttons.DPadUp))
        {
            gameMenus.Navigate(MenuInput.Up);
        }
        if (input.MenuVertical > 0 || PadPressed(Buttons.DPadDown))
        {
            gameMenus.Navigate(MenuInput.Down);
        }
        if (input.MenuHorizontal < 0 || PadPressed(Buttons.DPadLeft))
        {
            gameMenus.Navigate(MenuInput.Left);
        }
        if (input.MenuHorizontal > 0 || PadPressed(Buttons.DPadRight))
        {
            gameMenus.Navigate(MenuInput.Right);
        }
        if (input.MenuPage != 0)
        {
            gameMenus.Navigate(input.MenuPage < 0 ? MenuInput.PageUp : MenuInput.PageDown);
        }
        if (keyboard.UiCharacterPage < 0 || PadPressed(Buttons.LeftShoulder))
        {
            gameMenus.Navigate(MenuInput.PageLeft);
        }
        if (keyboard.UiCharacterPage > 0 || PadPressed(Buttons.RightShoulder))
        {
            gameMenus.Navigate(MenuInput.PageRight);
        }
        if (keyboard.UiDetailRequested)
        {
            gameMenus.Navigate(MenuInput.Alternate);
        }
        if (input.Confirm || keyboard.UiConfirmRequested || PadPressed(Buttons.A))
        {
            gameMenus.Navigate(MenuInput.Confirm);
        }
        if (input.Cancel || keyboard.UiCancelRequested || PadPressed(Buttons.B))
        {
            gameMenus.Navigate(MenuInput.Cancel);
        }
        if (gameMenus.IsOpen)
        {
            UiView view = gameMenus.BuildView();
            view.Resize(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
            if (keyboard.UiTabRequested)
            {
                view.Key(UiKey.Tab);
            }
            view.Pointer(mouse.X, mouse.Y, mousePressEdge);
        }
        if (!gameMenus.IsOpen)
        {
            drainUiButtons = true;
            CompleteNativeNormalMenu(nativeMainWasOpen);
        }
        else if (!gameMenus.IsDemo && IsNativeNormalMenuActive)
        {
            UpdatePartyMenu(gameplayClock.Advance(elapsed, session.State.Scene), default);
        }
        SynchronizeUiMoviePause();
        previousUiPad = pad;
        return true;
    }

    private bool IsNativeNormalMenuActive => events?.PartyMenu is { Kind: NativeFieldMenuKind.Normal, Phase: NativePartyMenuPhase.Active };

    private bool CompleteNativeNormalMenu(bool wasPresented)
    {
        if (!wasPresented || gameMenus!.IsOpen || saveMenu.IsOpen || !IsNativeNormalMenuActive)
        {
            return false;
        }
        PlayerActionResult result = world!.Apply(new PlayerAction(PlayerActionKind.Cancel));
        if (!result.Accepted)
        {
            throw new NotSupportedException(result.Detail);
        }
        fieldSeconds = 0;
        drainUiButtons = true;
        return true;
    }

    private void SynchronizeUiMoviePause()
    {
        bool pause = gameMenus?.IsOpen == true || drainUiButtons || battleDebug is not null || debugBattle is not null;
        if (pause && !uiMovieSuspended)
        {
            movie?.Suspend();
            uiMovieSuspended = true;
        }
        else if (!pause && uiMovieSuspended)
        {
            movie?.Resume();
            uiMovieSuspended = false;
        }
    }

    private void DrawGameUi(Rectangle bounds)
    {
        if (gameMenus?.IsOpen == true && uiCanvas is not null)
        {
            if (ActiveBattle is null)
            {
                batch.Draw(pixel, bounds, Color.Black);
            }
            UiView view = gameMenus.BuildView();
            view.Resize(bounds.Width, bounds.Height);
            view.Draw(uiCanvas, new UiTheme { FontSize = 16 * gameMenus.TextScale });
        }
    }

    private object DispatchUiControl(ControlRequest request)
    {
        if (gameMenus is null)
        {
            throw new InvalidOperationException("UI is not initialized.");
        }
        UiControlCommand command = request.Ui ?? new UiControlCommand();
        bool nativeMainWasOpen = IsNativeNormalMenuActive && gameMenus.IsOpen && !gameMenus.IsDemo;
        if (request.Command.Equals("ui-close", StringComparison.OrdinalIgnoreCase))
        {
            gameMenus.Close();
        }
        else if (request.Command.Equals("ui-open", StringComparison.OrdinalIgnoreCase) || request.Command.Equals("debug-room", StringComparison.OrdinalIgnoreCase))
        {
            gameMenus.Open(command.Screen ?? "debug-room", command.Demo);
        }
        else if (request.Command.Equals("ui-input", StringComparison.OrdinalIgnoreCase))
        {
            if (!gameMenus.IsOpen)
            {
                throw new InvalidOperationException("Open a menu before sending UI input.");
            }
            if (command.Input is MenuInput input)
            {
                gameMenus.Navigate(input);
            }
            if (command.X is float x && command.Y is float y)
            {
                UiView view = gameMenus.BuildView();
                view.Resize(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
                view.Pointer(x, y, command.Pressed && !uiControlPointerPressed);
                uiControlPointerPressed = command.Pressed;
            }
        }
        else if (!request.Command.Equals("ui-status", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("UI commands: debug-room, ui-open, ui-close, ui-input, ui-status.");
        }
        CompleteNativeNormalMenu(nativeMainWasOpen);
        SynchronizeUiMoviePause();
        return GameUiStatus()!;
    }

    private object? GameUiStatus()
    {
        return gameMenus is null ? null : new { gameMenus.IsOpen, gameMenus.IsDemo, gameMenus.ScreenId, gameMenus.Message, gameMenus.ScreenIds, Entries = gameMenus.Entries.Select(entry => new { entry.Id, entry.Label, entry.Enabled }).ToArray(), gameMenus.SelectedIndex };
    }
}
