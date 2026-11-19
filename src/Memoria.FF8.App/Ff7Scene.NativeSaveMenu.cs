using Memoria.FF8.GameUi;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private long? nativeSaveMenuRequest;

    private void RegisterNativeSaveMenu(MenuController menu)
    {
        menu.Register(new()
        {
            Id = "ff7-native-save",
            Title = "Save",
            Help = "Save the current continuation or return to the game.",
            Entries = _ =>
            [
                new("save", "Save", "Save the current continuation.", Action: new("ff7-native-save"), Enabled: CanSaveMemoria && SaveRequested is not null),
                new("cancel", "Cancel", "Return without saving.", Action: new("ff7-native-save-cancel"))
            ]
        });
    }

    private bool ApplyNativeSaveMenuAction(MenuAction action)
    {
        if (action.Kind is not ("ff7-native-save" or "ff7-native-save-cancel"))
        {
            return false;
        }
        if (Session?.Host.Process.Menu is not { Mode: 14, Completed: false } menu || nativeSaveMenuRequest != menu.RequestId)
        {
            return true;
        }
        if (action.Kind == "ff7-native-save")
        {
            if (CanSaveMemoria)
            {
                SaveRequested?.Invoke(Capture());
            }
        }
        else
        {
            Session.Host.CancelSaveMenu(menu.RequestId);
            ClosePlayerMenu();
        }
        return true;
    }

    private bool UpdateNativeSaveMenu(GameTime time, KeyboardState keys, GamePadState pad)
    {
        if (Session?.Host.Process.Menu is not { Mode: 14, Completed: false } request || autoPlay is { Running: true, Paused: false })
        {
            return false;
        }
        EnsurePlayerMenu();
        MenuController menu = playerMenu!;
        if (nativeSaveMenuRequest != request.RequestId)
        {
            nativeSaveMenuRequest = request.RequestId;
            menu.Close();
        }
        if (!menu.IsOpen)
        {
            menu.Open("ff7-native-save");
            sharedInput.SuppressHeldUiInput();
            menuPointerPressed = pointerState.LeftButton == ButtonState.Pressed;
        }
        else
        {
            UpdateSharedMenuInput(menu, keys, pad, ref menuPointerPressed);
            if (!menu.IsOpen && Session.Host.Process.Menu is { Mode: 14, Completed: false })
            {
                Session.Host.CancelSaveMenu(request.RequestId);
                ClosePlayerMenu();
            }
        }
        AdvancePlayerMenuClock(time.ElapsedGameTime);
        RefreshPlayerMenuPresentation();
        return true;
    }
}
