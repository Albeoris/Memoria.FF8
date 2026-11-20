using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal enum TitleAction
{
    NewGamePlus,
    NewGame,
    Continue,
    Config
}
internal sealed record TitleChoice(TitleAction Action, string Text);

internal sealed partial class RecreateGame
{
    private readonly List<(Microsoft.Xna.Framework.Rectangle Bounds, int Index)> titleHitTargets = [];
    private bool previousTitlePointerPressed;

    private bool ReadTitlePointer()
    {
        Microsoft.Xna.Framework.Input.MouseState mouse = physicalInput.ReadMouse();
        bool pressed = physicalInput.CanRead && mouse.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed;
        bool clicked = pressed && !previousTitlePointerPressed;
        previousTitlePointerPressed = pressed;
        if (!clicked)
        {
            return false;
        }
        foreach (var target in titleHitTargets)
        {
            if (target.Bounds.Contains(mouse.X, mouse.Y))
            {
                titleSelection = target.Index;
                return true;
            }
        }
        return false;
    }

    private TitleChoice[] titleActions = [new(TitleAction.NewGame, "NEW GAME"), new(TitleAction.Continue, "Continue"), new(TitleAction.Config, "Config")];
    private DateTime nextCompletedDiscovery;
    private NewGamePlusDraft? newGamePlusDraft;
    private GameplayPreferences? newGamePlusPreferences;
    private GameplaySaveComponentDocument? completedTransfer;
    private bool completedPromptStarted;

    private void RefreshCompletedDiscovery(bool force = false)
    {
        if (!force && DateTime.UtcNow < nextCompletedDiscovery)
        {
            return;
        }
        nextCompletedDiscovery = DateTime.UtcNow.AddSeconds(2);
        TitleAction selected = titleActions[Math.Clamp(titleSelection, 0, titleActions.Length - 1)].Action;
        bool available = playerSaves.ReadCompletedEntries().Count > 0;
        titleActions = available
            ? [new(TitleAction.NewGamePlus, LocalizeUiText("ngplus.title", "New Game+")), new(TitleAction.NewGame, "NEW GAME"), new(TitleAction.Continue, "Continue"), new(TitleAction.Config, "Config")]
            : [new(TitleAction.NewGame, "NEW GAME"), new(TitleAction.Continue, "Continue"), new(TitleAction.Config, "Config")];
        titleSelection = Math.Max(0, Array.FindIndex(titleActions, choice => choice.Action == selected));
    }

    private GameState CreateNewGamePlusDefaults(GameplayPreferences preferences)
    {
        using GameResourceHost target = new(options.GameDirectory, watch: false, modsDirectory: options.ModsDirectory, resourceLanguage: options.ResourceLanguage, modelSourceDirectory: options.ModelSourceDirectory, modLocations: options.ModLocations);
        if (!target.SetGameplayDifficulty(preferences.Difficulty))
        {
            throw new InvalidOperationException(GameResourceHost.GameplayDifficultyProfilePending);
        }
        using Memoria.FF8.Assets.GameAssets assets = new(options.GameDirectory, resources: target.Resources, modelOverrides: target);
        GameSession cold = GameWorld.CreateColdProcess(assets);
        cold.StartNewGame(cold.State.NativeBattleMemory);
        cold.SetGameplayPreferences(preferences);
        using (MovieClock clock = new(cold, options.GameDirectory))
        using (GameWorld allocation = new(cold, options.GameDirectory, clock, target.Resources, sharedAssets: assets, modelOverrides: target, readGameplayPolicy: () => target.GameplayPolicy))
        {
            // Allocate fresh owners before the first script transaction.
        }
        return cold.State;
    }

    private void OpenNewGamePlusSource(GameState source, string? revision = null)
    {
        if (source.CompletedGame is null)
        {
            throw new InvalidDataException("The selected file is no longer a Completed Game save. Choose another source.");
        }
        GameplayPreferences preferences = source.GameplayPreferences;
        UiPreferences settings = uiPreferences with { BattlePolicy = source.FullBattlePlaythrough == false ? "ResearchInstantVictory" : "NewGameFullCombat", RandomEncounters = source.RandomEncounters?.Enabled ?? true, Difficulty = preferences.Difficulty, AtbMode = preferences.AtbMode, GunbladeMode = preferences.GunbladeMode, TripleTriad = preferences.TripleTriad, BattleAllyCount = preferences.BattleAllyCount, AdditionalEnemies = preferences.AdditionalEnemies };
        foreach (NewGameOption option in GameplayPreferences.NewGameOptions)
        {
            typeof(UiPreferences).GetProperty(option.PropertyName)?.SetValue(settings, option.IsEnabled(preferences));
        }
        GameState native = CreateNewGamePlusDefaults(preferences);
        newGamePlusDraft = new(source, native, settings) { SourceRevision = revision ?? Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { source.CompletedGame, Characters = GameplaySaveCharacters.Capture(source.CharacterVitals!), Economy = GameplaySaveEconomy.Capture(source.Economy!) }))) };
        newGamePlusPreferences = preferences;
        newGamePlusDraft.ConvertMagic(preferences);
        EnsureLiveMenuCatalog();
        gameMenus!.NewGamePlusItemIds = Enumerable.Range(1, 201).Where(id => !string.IsNullOrWhiteSpace(liveMenuCatalog!.ItemName(id))).ToArray();
        gameMenus.NewGamePlusItemIcon = id => liveMenuCatalog!.ItemIcon(id);
        gameMenus.NewGamePlusItemName = id => liveMenuCatalog!.ItemName(id) ?? $"Item {id}";
        gameMenus.NewGamePlusCharacterName = id => liveMenuCatalog!.CharacterName(id) ?? GameNames.DefaultName(GameNames.CharacterSubject(id));
        gameMenus.NewGamePlusCardName = id => liveMenuCatalog!.CardName(id) ?? $"Card {id}";
        gameMenus.NewGamePlusMagicName = id => liveMenuCatalog!.MagicName(id) ?? $"Magic {id}";
        gameMenus.CancelNewGamePlusSetup = CancelNewGamePlusSetup;
        gameMenus.OpenNewGamePlusInitialSettings();
    }

    private void ConfigureNewGamePlus()
    {
        NewGamePlusDraft draft = newGamePlusDraft ?? throw new InvalidOperationException("Select a completed game first.");
        draft.RevalidateNative(CreateNewGamePlusDefaults(ResolveNewGamePlusPreferences(draft.Settings)));
        gameMenus!.OpenNewGamePlus(draft, BeginNewGamePlus, () => gameMenus.OpenNewGamePlusInitialSettings());
    }

    private async void CancelNewGamePlusSetup()
    {
        if (newGamePlusDraft is null)
        {
            return;
        }
        bool discard = await ShowGameConfirmationAsync(new(LocalizeUiText("ngplus.discard.message", "Discard your New Game+ setup changes?"), LocalizeUiText("ngplus.title", "New Game+"), LocalizeUiText("common.cancel", "Cancel"), LocalizeUiText("ngplus.discard", "Discard")));
        if (!discard)
        {
            gameMenus!.OpenNewGamePlusInitialSettings();
            return;
        }
        newGamePlusDraft = null;
        newGamePlusPreferences = null;
        gameMenus!.EndNewGamePlusSetup();
        gameMenus!.Close();
        if (session.State.CompletedGame is not null)
        {
            ReturnToTitleScreen();
        }
    }

    private void BeginNewGamePlus(NewGamePlusDraft draft)
    {
        try
        {
            _ = draft.ResolveDefinition();
            GameplayPreferences preferences = ResolveNewGamePlusPreferences(draft.Settings);
            GameState cold = CreateNewGamePlusDefaults(preferences);
            GameState state = draft.ApplyTo(cold) with
            {
                Ff8Completion = null,
                CompletedGame = null,
                NewGamePlusRewards = new() { ExperienceTenths = draft.ExperienceMultiplierTenths, AbilityPointTenths = draft.ApMultiplierTenths }
            };
            GameSession candidate = new(state);
            if (!resourceHost!.SetGameplayDifficulty(preferences.Difficulty))
            {
                resourceHost.SetGameplayDifficulty(session.State.GameplayPreferences.Difficulty);
                throw new InvalidOperationException(GameResourceHost.GameplayDifficultyProfilePending);
            }
            StopAutoPlayForPlayerSession();
            ReplacePlayerSession(candidate);
            RestoreCrossover(null);
            newGamePlusDraft = null;
            newGamePlusPreferences = null;
            promptNewGameChocoboClaim = preferences.ChocoboWorldMode == ChocoboWorldMode.Memoria;
            error = null;
            fieldFaulted = false;
            fieldRenderFaulted = false;
        }
        catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException or NotSupportedException or ArgumentException)
        {
            ShowSaveNotice(exception.Message);
        }
    }

    private GameplayPreferences ResolveNewGamePlusPreferences(UiPreferences settings)
    {
        GameplayPreferences result = (newGamePlusPreferences ?? new()) with { Difficulty = settings.Difficulty, AtbMode = settings.AtbMode, GunbladeMode = settings.GunbladeMode, TripleTriad = settings.TripleTriad, BattleAllyCount = settings.BattleAllyCount, AdditionalEnemies = settings.AdditionalEnemies };
        foreach (NewGameOption option in GameplayPreferences.NewGameOptions.Where(option => option.Label is not ("Shared magic stock" or "Infinite magic")))
        {
            typeof(GameplayPreferences).GetProperty(option.PropertyName)!.SetValue(result, typeof(UiPreferences).GetProperty(option.PropertyName)!.GetValue(settings));
        }
        return result.NormalizeForDifficulty();
    }

    private bool UpdateCompletedGame(InputFrame input)
    {
        if (session.State.Ff8Completion is null)
        {
            return false;
        }
        completedTransfer ??= CompletedGameData.Capture(session.State);
        if (saveMenu.IsOpen)
        {
            UpdatePlayerSavePointer();
            UpdatePlayerSaveMenuAsync(input);
        }
        else if (!completedPromptStarted)
        {
            ShowCompletedGamePrompt();
        }
        else
        {
            completedPromptStarted = false;
        }
        return true;
    }

    private async void ShowCompletedGamePrompt()
    {
        completedPromptStarted = true;
        bool save = await ShowGameConfirmationAsync(new(LocalizeUiText("ngplus.completion.prompt", "Save your completed game?"), LocalizeUiText("ngplus.completed", "Completed Game"), LocalizeUiText("ngplus.return-title", "Return to Title"), LocalizeUiText("ngplus.save", "Save Completed Game")));
        if (save)
        {
            saveMenu.SetStore(playerSaves);
            saveMenu.Open(PlayerSaveMenuMode.SaveCompleted);
        }
        else
        {
            completedTransfer = null;
            completedPromptStarted = false;
            ReturnToTitleScreen();
        }
    }

    private async Task ReviewCompletedOverwriteAsync(int slot)
    {
        string? revision = playerSaves.CompletedRevision(slot);
        if (revision is null)
        {
            return;
        }
        bool overwrite = await ShowGameConfirmationAsync(new(LocalizeUiText("ngplus.overwrite.message", "This save was created after completing the game. Are you sure you want to overwrite it?"), LocalizeUiText("ngplus.completed", "Completed Game"), LocalizeUiText("common.cancel", "Cancel"), LocalizeUiText("save.view.overwrite", "Overwrite")));
        if (!overwrite)
        {
            throw new OperationCanceledException();
        }
        playerSaves.ApproveCompletedOverwrite(slot, revision);
    }
}
