using Memoria.FF8.Ui;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private readonly GameConfirmationOverlay confirmationOverlay = new();

    private Task<bool> ShowGameConfirmationAsync(GameConfirmationRequest request)
    {
        keyboard.SuppressHeldUiInput();
        return confirmationOverlay.ShowAsync(request);
    }

    private void UpdateGameConfirmation()
    {
        UpdateGameConfirmation(confirmationOverlay);
    }

    private void UpdateGameConfirmation(GameConfirmationOverlay overlay)
    {
        if (!overlay.IsOpen)
        {
            return;
        }
        InputFrame input = keyboard.Read(physicalInput.ReadKeyboard(), physicalInput.ReadController(), IsActive);
        if (input.Cancel)
        {
            overlay.Handle(UiKey.Cancel);
        }
        else if (input.Confirm)
        {
            overlay.Handle(UiKey.Confirm);
        }
        else
        {
            if (input.MenuHorizontal != 0)
            {
                overlay.Handle(input.MenuHorizontal < 0 ? UiKey.Left : UiKey.Right);
            }
            if (input.MenuVertical != 0)
            {
                overlay.Handle(input.MenuVertical < 0 ? UiKey.Up : UiKey.Down);
            }
            if (input.MenuPage != 0)
            {
                overlay.Handle(input.MenuPage < 0 ? UiKey.PageUp : UiKey.PageDown);
            }
        }
        MouseState mouse = physicalInput.ReadMouse();
        overlay.Pointer(mouse.X, mouse.Y, IsActive && mouse.LeftButton == ButtonState.Pressed);
    }

    private void DrawGameConfirmation(Rectangle bounds)
    {
        if (uiCanvas is not null)
        {
            confirmationOverlay.Draw(uiCanvas, uiTheme, new UiRect(bounds.X, bounds.Y, bounds.Width, bounds.Height), uiPreferences.UiFontFamily);
        }
    }

    private async void UpdatePlayerSaveMenuAsync(InputFrame input)
    {
        await saveMenu.UpdateAsync(input, SavePlayerSlotAsync, LoadPlayerSlot, TransferPlayerSaveAsync);
    }

    private async Task SavePlayerSlotAsync(int slot)
    {
        if (slot <= PlayerSaveStore.MemoriaSlotCount)
        {
            SavePlayerSlot(slot);
            return;
        }
        PreparePlayerSlotSave();
        await SaveOriginalPlayerSlotWithGuardianForceReviewAsync(slot);
        saveNotice = null;
        saveNoticeSeconds = 0;
    }
}
