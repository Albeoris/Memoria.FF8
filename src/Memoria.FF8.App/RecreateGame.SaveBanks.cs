using System.Text.Json;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private bool previousSaveMousePressed;

    private string? ReadOriginalSaveDirectory()
    {
        if (!File.Exists(globalSettings.SaveBanksPath))
        {
            return null;
        }
        try
        {
            string? directory = JsonSerializer.Deserialize<SaveBankPreferences>(File.ReadAllText(globalSettings.SaveBanksPath))?.OriginalDirectory;
            return directory is not null && Directory.Exists(directory) ? directory : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private bool EnsurePlayerSaveBank(int bank)
    {
        if (bank == 0 || playerSaves.OriginalDirectory is not null)
        {
            return true;
        }
        BrowseOriginalSaveBank(bank, saveMenu.IsTransferring);
        return false;
    }

    private async void BrowseOriginalSaveBank(int bank, bool transferring)
    {
        try
        {
            string initial = playerSaves.OriginalDirectoryCandidates.FirstOrDefault() ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string? path = await ModelBrowserDialogs.PathAsync(ModelBrowserDialogs.Desktop.SelectFolderAsync(new(LocalizeAuthored("save.original.choose-folder", "Choose the original FINAL FANTASY VIII save folder (containing slot1_save01.ff8)"), initial)));
            if (path is null)
            {
                return;
            }
            playerSaves.SetOriginalDirectory(path);
            Directory.CreateDirectory(globalSettings.DirectoryPath);
            File.WriteAllText(globalSettings.SaveBanksPath, JsonSerializer.Serialize(new SaveBankPreferences(playerSaves.OriginalDirectory)));
            if (transferring)
            {
                saveMenu.RequestTransfer();
            }
            else
            {
                saveMenu.SelectBank(bank);
            }
        }
        catch (Exception error) when (error is IOException or ArgumentException or InvalidOperationException or UnauthorizedAccessException)
        {
            ShowSaveNotice(error.Message);
        }
        finally
        {
            keyboard.SuppressHeldNameInput();
            drainUiButtons = true;
        }
    }

    private GameState LoadOriginalPlayerState(string path)
    {
        return OriginalPlayerSaveLoader.Load(path, options.GameDirectory, world?.ValidationAssets, resourceHost?.Resources, options.MediaToolsDirectory, resourceHost is null ? null : resourceHost.ResolveGameFile);
    }

    private async Task TransferPlayerSaveAsync(int source, int destination)
    {
        if (source <= PlayerSaveStore.MemoriaSlotCount && destination > PlayerSaveStore.MemoriaSlotCount)
        {
            GameState candidate = playerSaves.Load(source);
            (NativeCharacterVitals targetSource, short? placeId) = await ReviewOriginalGuardianForceExportAsync(candidate);
            playerSaves.Transfer(source, destination, ValidatePlayerState, targetSource, allowGuardianForceLoss: true, reviewedSource: candidate, originalPlaceId: placeId);
        }
        else
        {
            playerSaves.Transfer(source, destination, ValidatePlayerState);
        }
    }

    private void UpdatePlayerSavePointer()
    {
        MouseState mouse = Mouse.GetState();
        bool pressed = IsActive && mouse.LeftButton == ButtonState.Pressed;
        if (savesWindowUi is not null)
        {
            savesWindowUi.Resize(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
            savesWindowUi.Pointer(mouse.X, mouse.Y, pressed && !previousSaveMousePressed);
        }
        previousSaveMousePressed = pressed;
    }

    private sealed record SaveBankPreferences(string? OriginalDirectory);
}
