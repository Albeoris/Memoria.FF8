using System.Text.Json;
using Memoria.FF7.Runtime;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8;

internal sealed partial class Ff7Game
{
    private PlayerSaveStore playerSaves = null!;
    private PlayerSaveMenu saveMenu = null!;
    private UiView? playerSaveView;
    private string? playerSaveViewKey;
    private bool previousSaveMousePressed;
    private bool choosingOriginalSaveDirectory;

    private string Ff7SaveBankPreferencesPath => Path.Combine(new GlobalSettingsPaths(isolatedDirectory: launchOptions?.SettingsDirectory).DirectoryPath, "ff7-save-banks.json");

    private void InitializePlayerSaves()
    {
        string? originalDirectory = null;
        try
        {
            if (launchOptions?.ControlExclusive != true && File.Exists(Ff7SaveBankPreferencesPath))
            {
                string? configured = JsonSerializer.Deserialize<Ff7SaveBankPreferences>(File.ReadAllText(Ff7SaveBankPreferencesPath))?.OriginalDirectory;
                if (configured is not null && Directory.Exists(configured))
                {
                    originalDirectory = configured;
                }
            }
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            scene.ShowNotice(exception.Message);
        }
        playerSaves = new PlayerSaveStore(savePath, directory: launchOptions?.IsolatedPlayerSaveDirectory, originalDirectory: originalDirectory, discoverOriginalSaves: launchOptions?.ControlExclusive != true, gameDirectory: installation.RootDirectory, game: SessionGame.Ff7);
        playerSaves.SetQuickSaveCount(scene.PresentationPreferences.QuickSaveCount);
        saveMenu = new PlayerSaveMenu(playerSaves) { BeforeBankChange = EnsureOriginalPlayerSaveBank };
        scene.PlayerSaveRequested += input => HandlePlayerSaveInput(input);
        scene.SaveRequested += _ => OpenPlayerSaveMenu(PlayerSaveMenuMode.Save);
        scene.ContinueRequested += () => OpenPlayerSaveMenu(PlayerSaveMenuMode.Load);
    }

    private void OpenPlayerSaveMenu(PlayerSaveMenuMode mode)
    {
        if (mode is PlayerSaveMenuMode.Save or PlayerSaveMenuMode.QuickSave && !scene.CanSave)
        {
            scene.ShowNotice("Saving is unavailable at the current FFVII boundary.");
            return;
        }
        saveMenu.Open(mode);
        playerSaveView = null;
        previousSaveMousePressed = Mouse.GetState().LeftButton == ButtonState.Pressed;
        keyboard.SuppressHeldUiInput();
        scene.SetFocusPaused(true);
    }

    private void HandlePlayerSaveInput(InputFrame input)
    {
        try
        {
            if (input.LoadMenu || input.SaveMenu)
            {
                OpenPlayerSaveMenu(input.LoadMenu ? PlayerSaveMenuMode.Load : PlayerSaveMenuMode.Save);
            }
            else if (saveMenu.IsOpen)
            {
                if (choosingOriginalSaveDirectory)
                {
                    return;
                }
                MouseState mouse = launchOptions?.AcceptsPhysicalInput == false ? default : Mouse.GetState();
                bool pressed = IsActive && mouse.LeftButton == ButtonState.Pressed;
                playerSaveView?.Resize(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
                playerSaveView?.Pointer(mouse.X, mouse.Y, pressed && !previousSaveMousePressed);
                previousSaveMousePressed = pressed;
                saveMenu.Update(input, SavePlayerSlot, LoadPlayerSlot, playerSaves.TransferFf7);
                if (!saveMenu.IsOpen)
                {
                    playerSaveView = null;
                    keyboard.SuppressHeldUiInput();
                    scene.SuppressInitialPresses(ReadControlKeys());
                    scene.SetFocusPaused(!IsActive);
                }
            }
            else if (input.QuickLoad)
            {
                OpenPlayerSaveMenu(PlayerSaveMenuMode.QuickLoad);
            }
            else if (input.Save)
            {
                SavePlayerSlot(0);
            }
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or InvalidOperationException or NotSupportedException or UnauthorizedAccessException or ArgumentException)
        {
            scene.ShowNotice(exception.Message);
        }
    }

    private void SavePlayerSlot(int slot)
    {
        if (!scene.CanSave)
        {
            throw new InvalidOperationException("Saving is unavailable at the current FFVII boundary.");
        }
        playerSaves.SetQuickSaveCount(scene.PresentationPreferences.QuickSaveCount);
        playerSaves.SaveFf7(slot, scene.Capture());
        if (slot == 0)
        {
            scene.ShowNotice("Quick Saved", NoticeWindow.QuickSaveDurationSeconds);
        }
    }

    private void LoadPlayerSlot(int slot)
    {
        CombinedSessionSave saved = playerSaves.LoadFf7(slot);
        RestoreStandalone(scene.RetainDormantWorld(saved));
    }

    private bool EnsureOriginalPlayerSaveBank(int bank)
    {
        if (bank == 0 || playerSaves.OriginalDirectory is not null)
        {
            return true;
        }
        if (!choosingOriginalSaveDirectory)
        {
            BrowseOriginalPlayerSaveBank(bank, saveMenu.IsTransferring);
        }
        return false;
    }

    private async void BrowseOriginalPlayerSaveBank(int bank, bool transferring)
    {
        choosingOriginalSaveDirectory = true;
        try
        {
            string initial = playerSaves.OriginalDirectoryCandidates.FirstOrDefault() ?? installation.RootDirectory;
            string? path = await ModelBrowserDialogs.PathAsync(ModelBrowserDialogs.Desktop.SelectFolderAsync(new("Choose the original FINAL FANTASY VII save folder (containing save00.ff7)", initial)));
            if (path is null)
            {
                return;
            }
            playerSaves.SetOriginalDirectory(path);
            Directory.CreateDirectory(Path.GetDirectoryName(Ff7SaveBankPreferencesPath)!);
            File.WriteAllText(Ff7SaveBankPreferencesPath, JsonSerializer.Serialize(new Ff7SaveBankPreferences(playerSaves.OriginalDirectory)));
            if (transferring)
            {
                saveMenu.RequestTransfer();
            }
            else
            {
                saveMenu.SelectBank(bank);
            }
        }
        catch (Exception exception) when (exception is IOException or ArgumentException or InvalidOperationException or UnauthorizedAccessException)
        {
            scene.ShowNotice(exception.Message);
        }
        finally
        {
            choosingOriginalSaveDirectory = false;
            keyboard.SuppressHeldUiInput();
            previousSaveMousePressed = true;
        }
    }

    private void DrawPlayerSaveMenu(GameTime gameTime, Rectangle bounds)
    {
        if (!saveMenu.IsOpen)
        {
            return;
        }
        UiPreferences preferences = scene.PresentationPreferences;
        float scale = preferences.TextScale / 100f * preferences.UiFontSize / 24f;
        PlayerSaveView.ConfigureTextScale(saveMenu, scale);
        string key = string.Join("|", scale, preferences.UiFontFamily, preferences.MenuPresentation, saveMenu.Mode, saveMenu.Bank, saveMenu.Page, saveMenu.SelectedSlot, saveMenu.ConfirmOverwrite, saveMenu.TransferSource, saveMenu.Message, string.Join(";", saveMenu.VisibleEntries), keyboard.Profile.GetHashCode(), keyboard.ControllerPresentation, keyboard.LastInputDevice);
        if (playerSaveView is null || playerSaveViewKey != key)
        {
            string transfer = $"{keyboard.BindingPrompt(ControlAction.Card)}: Transfer";
            string navigation = $"{keyboard.BindingPrompt(ControlAction.MoveLeft)} / {keyboard.BindingPrompt(ControlAction.MoveRight)}: Bank    {keyboard.BindingPrompt(ControlAction.UiPageUp)} / {keyboard.BindingPrompt(ControlAction.UiPageDown)}: Page";
            playerSaveView = PlayerSaveView.Build(saveMenu, transfer, navigation, keyboard.BindingPrompt(ControlAction.Confirm), keyboard.BindingPrompt(ControlAction.Cancel), scale);
            playerSaveView.ViewportMode = preferences.MenuPresentation == MenuPresentation.Vector ? UiViewportMode.Expand : UiViewportMode.Fit;
            playerSaveViewKey = key;
        }
        scene.DrawPlayerSaveView(playerSaveView, batch, bounds, gameTime);
    }

    private sealed record Ff7SaveBankPreferences(string? OriginalDirectory);
}

internal sealed partial class Ff7Scene
{
    internal CombinedSessionSave RetainDormantWorld(CombinedSessionSave saved)
    {
        if (saved.Ff8Payload is { Length: > 0 } || envelope.Ff8Payload is not { Length: > 0 })
        {
            return saved;
        }
        return saved with
        {
            Ff8Payload = envelope.Ff8Payload,
            Ff8PayloadFormat = envelope.Ff8PayloadFormat,
            Mappings = envelope.Mappings,
            CharacterNameOverrides = envelope.CharacterNameOverrides
        };
    }

    internal void DrawPlayerSaveView(UiView view, SpriteBatch batch, Rectangle bounds, GameTime gameTime)
    {
        ownedDialogueText?.BeginFrame();
        ownedDialogueCanvas?.BeginFrame(gameTime.TotalGameTime);
        batch.Begin(samplerState: SamplerState.PointClamp, blendState: BlendState.AlphaBlend);
        try
        {
            batch.Draw(pixel, bounds, Color.Black);
            view.Resize(bounds.Width, bounds.Height);
            view.Draw(dialogueCanvas ?? throw new InvalidOperationException("FFVII saves require the shared presentation context."), SharedTheme());
        }
        finally
        {
            batch.End();
        }
    }
}
