using System.Text.Json;
using Memoria.FF8.Runtime;
using Microsoft.Win32;
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
        OpenFolderDialog picker = new()
        {
            Title = LocalizeAuthored("save.original.choose-folder", "Choose the original FINAL FANTASY VIII save folder (containing slot1_save01.ff8)"),
            Multiselect = false,
            InitialDirectory = playerSaves.OriginalDirectoryCandidates.FirstOrDefault() ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };
        if (picker.ShowDialog() != true)
        {
            return false;
        }
        playerSaves.SetOriginalDirectory(picker.FolderName);
        Directory.CreateDirectory(globalSettings.DirectoryPath);
        File.WriteAllText(globalSettings.SaveBanksPath, JsonSerializer.Serialize(new SaveBankPreferences(playerSaves.OriginalDirectory)));
        keyboard.SuppressHeldNameInput();
        return true;
    }

    private GameState LoadOriginalPlayerState(string path)
    {
        return OriginalPlayerSaveLoader.Load(path, options.GameDirectory, world?.ValidationAssets, resourceHost?.Resources, options.MediaToolsDirectory, resourceHost is null ? null : resourceHost.ResolveGameFile);
    }

    private void TransferPlayerSave(int source, int destination)
    {
        if (source <= PlayerSaveStore.MemoriaSlotCount && destination > PlayerSaveStore.MemoriaSlotCount)
        {
            GameState candidate = playerSaves.Load(source);
            NativeCharacterVitals targetSource = ReviewOriginalGuardianForceExport(candidate);
            playerSaves.Transfer(source, destination, ValidatePlayerState, targetSource, allowGuardianForceLoss: true, reviewedSource: candidate);
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
