using System.Text.Json;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private bool previousSaveMousePressed;

    private string? ReadOriginalSaveDirectory(bool ff7 = false)
    {
        string preferencesPath = ff7 ? Path.Combine(globalSettings.DirectoryPath, "ff7-save-banks.json") : globalSettings.SaveBanksPath;
        if (!File.Exists(preferencesPath))
        {
            return null;
        }
        try
        {
            string? directory = JsonSerializer.Deserialize<SaveBankPreferences>(File.ReadAllText(preferencesPath))?.OriginalDirectory;
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
            string prompt = InFf7 ? "Choose the original FINAL FANTASY VII save folder (containing save00.ff7)" : LocalizeAuthored("save.original.choose-folder", "Choose the original FINAL FANTASY VIII save folder (containing slot1_save01.ff8)");
            string? path = await ModelBrowserDialogs.PathAsync(ModelBrowserDialogs.Desktop.SelectFolderAsync(new(prompt, initial)));
            if (path is null)
            {
                return;
            }
            playerSaves.SetOriginalDirectory(path);
            Directory.CreateDirectory(globalSettings.DirectoryPath);
            File.WriteAllText(InFf7 ? Path.Combine(globalSettings.DirectoryPath, "ff7-save-banks.json") : globalSettings.SaveBanksPath, JsonSerializer.Serialize(new SaveBankPreferences(playerSaves.OriginalDirectory)));
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
        if (InFf7)
        {
            playerSaves.TransferFf7(source, destination);
            return;
        }
        if (source <= PlayerSaveStore.MemoriaSlotCount && destination > PlayerSaveStore.MemoriaSlotCount)
        {
            CrossoverSaveAdapter.EnsureOriginalExportAllowed(playerSaves.GetPath(source));
            GameState candidate = playerSaves.Load(source);
            (NativeCharacterVitals targetSource, short? placeId) = await ReviewOriginalGuardianForceExportAsync(candidate);
            playerSaves.Transfer(source, destination, ValidatePlayerState, targetSource, allowGuardianForceLoss: true, reviewedSource: candidate, originalPlaceId: placeId);
        }
        else
        {
            playerSaves.Transfer(source, destination, ValidatePlayerState, writeGameplay: WriteImportedGameplaySave);
        }
    }

    private GameState WriteImportedGameplaySave(GameState state, string path, Action<GameState>? validateContinuation)
    {
        GameSession imported = new(state);
        using MovieClock clock = new(imported, options.GameDirectory, options.MediaToolsDirectory, resourceHost is null ? null : resourceHost.ResolveGameFile);
        using GameWorld candidate = new(imported, options.GameDirectory, clock, resourceHost?.Resources, sharedAssets: world?.ValidationAssets, readSceneMods: () => resourceHost?.SceneMods ?? Memoria.FF8.Mods.FieldSceneMods.Empty, modelOverrides: resourceHost, readGameplayPolicy: () => resourceHost?.GameplayPolicy ?? GameplayTuningPolicy.Empty, applyEncounterMods: resourceHost is null ? null : resourceHost.ApplyEncounterMods);
        candidate.EnsureField();
        return candidate.WriteGameplaySave(path, validateContinuation);
    }

    private void UpdatePlayerSavePointer()
    {
        MouseState mouse = physicalInput.ReadMouse();
        bool pressed = physicalInput.CanRead && mouse.LeftButton == ButtonState.Pressed;
        if (savesWindowUi is not null)
        {
            savesWindowUi.Resize(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
            savesWindowUi.Pointer(mouse.X, mouse.Y, pressed && !previousSaveMousePressed);
        }
        previousSaveMousePressed = pressed;
    }

    private sealed record SaveBankPreferences(string? OriginalDirectory);
}
