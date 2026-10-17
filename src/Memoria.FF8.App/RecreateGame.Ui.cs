using Memoria.FF8.GameUi;
using Memoria.FF8.Assets;
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
    private LiveMenuCatalog? liveMenuCatalog;
    private NativeRefinementDefinitions? nativeRefinementDefinitions;
    private bool previousUiMousePressed;
    private bool uiControlPointerPressed;
    private bool drainUiButtons;
    private DialogueState? nativeNamePrompt;
    private bool suppressNativeNameInput;
    private readonly GlobalSettingsPaths globalSettings = new();
    private string UiPreferencesPath => globalSettings.UiPreferencesPath;
    private string ControlsPreferencesPath => globalSettings.ControlsPreferencesPath;

    private void InitializeGameUi()
    {
        uiCanvas = new MonoGameUiCanvas(GraphicsDevice, batch, pixel, text, options.GameDirectory);
        globalSettings.ImportLegacyFiles(options.SavePath);
        uiPreferences = UiPreferences.Load(UiPreferencesPath);
        if (!File.Exists(UiPreferencesPath))
        {
            uiPreferences.Save(UiPreferencesPath);
        }
        ApplyDisplaySettings();
        text.FontFamily = uiPreferences.UiFontFamily;
        fieldAudio?.SetOutputPreferences(uiPreferences.MusicVolume, uiPreferences.SoundVolume);
        gameMenus = new MenuController(ReadLiveMenuData, ApplyLiveMenuAction);
        InitializeAutoPlayUi();
        ConnectNativeTutorialUi();
        gameMenus.ScreenOpened += screen =>
        {
            if (!gameMenus.IsDemo && screen is "main" or "card")
            {
                if (session.VisitCardMenu())
                {
                    gameMenus.InvalidateLiveData();
                    if (events?.FieldId == session.State.FieldId)
                    {
                        session.SetEventState(events.Capture());
                    }
                }
            }
        };
        gameMenus.ConnectMods(resourceHost!.Mods);
        gameMenus.ConnectPreferences(ReadCurrentUiPreferences, ApplyLiveMenuAction);
        gameMenus.ConnectNaming(ReadDebugNames, (subject, name) => world!.Rename(subject, name));
        gameMenus.ConnectBattleSettings(() => ActiveBattle?.Outcome == BattleOutcome.Active, victory => ApplyBattleCommand(new BattleInput(victory ? BattleInputKind.InstantVictory : BattleInputKind.InstantDefeat)));
        gameMenus.SoundRequested += sound => fieldAudio?.PlayUiSound(sound);
        keyboard.Profile = ControlsProfile.Load(ControlsPreferencesPath);
        if (!File.Exists(ControlsPreferencesPath))
        {
            keyboard.Profile.Save(ControlsPreferencesPath);
        }
        gameMenus.ConnectControls(() => keyboard.Profile, profile =>
        {
            profile.Save(ControlsPreferencesPath);
            keyboard.Profile = profile;
        }, () => keyboard.ControllerPresentation, () => controller!.Generation, () => keyboard.LastInputDevice);
        gameMenus.BattleDebugRequested += OpenBattleDebug;
        if (options.DebugUiScreen is string screen)
        {
            gameMenus.Open(screen, true);
        }
    }

    private MenuData ReadLiveMenuData()
    {
        MenuData data;
        if (IsNativeWeaponShopActive)
        {
            data = NativeWeaponShopMenuProjection.Build(events!.PartyMenu!.WeaponShop!, session.State.CharacterVitals);
        }
        else if (IsNativeShopActive)
        {
            data = NativeShopMenuProjection.Build(events!.PartyMenu!.Shop!);
        }
        else
        {
            if (liveMenuCatalog is null)
            {
                using var assets = new Memoria.FF8.Assets.GameAssets(options.GameDirectory);
                liveMenuCatalog = LiveMenuCatalog.ReadInstalled(assets);
                nativeRefinementDefinitions = assets.ReadNativeRefinementDefinitions();
            }
            data = LiveMenuData.Build(session.State, events?.Scene.Names, liveMenuCatalog, placeId: events?.Scene.PlaceId);
            NativeRefinementMenuProjection.Augment(data, session.State, nativeRefinementDefinitions!, liveMenuCatalog);
        }
        data.Settings["Music volume"] = uiPreferences.MusicVolume + "%";
        data.Settings["Cursor"] = uiPreferences.CursorMemory ? "Memory" : "Initial";
        data.Settings["Field message"] = uiPreferences.FieldMessageSpeed.ToString();
        data.Settings["Vibration"] = keyboard.Profile.VibrationEnabled ? "On" : "Off";
        data.Settings["Effect volume"] = uiPreferences.SoundVolume + "%";
        data.Settings["Text scale"] = uiPreferences.TextScale + "%";
        data.Settings["UI font"] = uiPreferences.UiFontFamily;
        data.Settings["UI font size"] = uiPreferences.UiFontSize.ToString();
        data.Settings["Dialogue font"] = uiPreferences.DialogueFontFamily;
        data.Settings["Dialogue font size"] = uiPreferences.DialogueFontSize.ToString();
        data.Settings["Battle ATB mode"] = ReadCurrentUiPreferences().AtbMode == BattleAtbMode.Normal ? "Classic" : "Turn Based";
        data.Settings["Gunblade trigger"] = ReadCurrentUiPreferences().GunbladeMode.ToString();
        data.Settings["Battle policy"] = ReadCurrentUiPreferences().BattlePolicyLabel;
        data.Settings["Rare card markers"] = uiPreferences.RareCardMarkers ? "On" : "Off";
        data.Settings["Random encounters"] = ReadCurrentUiPreferences().RandomEncounters ? "On" : "Off";
        data.SupportedActions.Add("config:Music volume");
        data.SupportedActions.Add("config:Cursor");
        data.SupportedActions.Add("config:Field message");
        data.SupportedActions.Add("config:Vibration");
        data.SupportedActions.Add("config:Rare card markers");
        data.SupportedActions.Add("config:Effect volume");
        data.SupportedActions.Add("config:Text scale");
        data.SupportedActions.Add("config:UI font");
        data.SupportedActions.Add("config:UI font size");
        data.SupportedActions.Add("config:Dialogue font");
        data.SupportedActions.Add("config:Dialogue font size");
        data.SupportedActions.Add("config:Battle ATB mode");
        data.SupportedActions.Add("config:Gunblade trigger");
        data.SupportedActions.Add("config:Battle policy");
        data.SupportedActions.Add("config:Random encounters");
        data.Settings["V-Sync"] = uiPreferences.VSync ? "On" : "Off";
        data.Settings["Field FPS"] = uiPreferences.FieldFps == 0 ? "Unlimited" : uiPreferences.FieldFps.ToString();
        data.Settings["Battle FPS"] = uiPreferences.BattleFps == 0 ? "Unlimited" : uiPreferences.BattleFps.ToString();
        data.Settings["Menu FPS"] = uiPreferences.MenuFps == 0 ? "Unlimited" : uiPreferences.MenuFps.ToString();
        data.SupportedActions.UnionWith(["config:V-Sync", "config:Field FPS", "config:Battle FPS", "config:Menu FPS"]);
        if (session.State.Scene == SceneKind.Title)
        {
            data.SupportedActions.Add("new-game-start");
        }
        data.SaveAvailable = session.State.Scene != SceneKind.Title && !fieldFaulted;
        if (data.SaveAvailable)
        {
            data.SupportedActions.Add("save");
        }
        return data;
    }

    private IReadOnlyList<DebugNameTarget> ReadDebugNames()
    {
        if (world is null || ActiveBattle is not null || session.State.Scene is not (SceneKind.Field or SceneKind.WorldMap) || fieldFaulted)
        {
            return [];
        }
        List<DebugNameTarget> targets = [];
        foreach (int identity in Enumerable.Range(0, 11))
        {
            int subject = GameNames.CharacterSubject(identity);
            targets.Add(new DebugNameTarget(subject, world.Events.Scene.Names.GetValueOrDefault(subject, GameNames.DefaultName(subject)), "Character"));
        }
        targets.Add(new DebugNameTarget(2, world.Events.Scene.Names.GetValueOrDefault(2, "Angelo"), "Character"));
        foreach (int identity in Enumerable.Range(0, 16))
        {
            int subject = GameNames.GuardianForceSubject(identity);
            targets.Add(new DebugNameTarget(subject, world.Events.Scene.Names.GetValueOrDefault(subject, GameNames.DefaultName(subject)), "GF"));
        }
        targets.Add(new DebugNameTarget(GameNames.GrieverSubject, world.Events.Scene.Names.GetValueOrDefault(GameNames.GrieverSubject, GameNames.DefaultName(GameNames.GrieverSubject)), "Entity"));
        targets.Add(new DebugNameTarget(GameNames.BokoSubject, world.Events.Scene.Names.GetValueOrDefault(GameNames.BokoSubject) ?? NativePocketStationText.ChocoboName(session.State.PocketStation) ?? GameNames.DefaultName(GameNames.BokoSubject), "Entity"));
        return targets;
    }

    private void ApplyLiveMenuAction(MenuAction action)
    {
        if (action.Kind == "new-game-start")
        {
            StartNewGameSession();
            return;
        }
        if (NativeMagicMenuProjection.IsAction(action))
        {
            PlayerActionResult result = world!.Apply(NativeMagicMenuProjection.PlayerAction(action));
            if (!result.Accepted)
            {
                throw new NotSupportedException(result.Detail ?? "The current game state cannot accept a magic action.");
            }
            fieldSeconds = 0;
            return;
        }
        if (action.Kind == "card-mod" && liveMenuCatalog is not null)
        {
            if (!action.SubjectId.StartsWith("card-", StringComparison.Ordinal) || !int.TryParse(action.SubjectId.AsSpan(5), out int cardId) || liveMenuCatalog.CardRefinement(cardId) is not { } recipe || !session.RefineCard(recipe))
            {
                ShowSaveNotice("Card Mod requires its learned GF ability, enough cards and room for the complete item result.");
                return;
            }
            session.SetEventState(events!.Capture());
            return;
        }
        if (NativeJunctionMenuProjection.IsAction(action))
        {
            PlayerActionResult result = world!.Apply(NativeJunctionMenuProjection.PlayerAction(action));
            if (!result.Accepted)
            {
                throw new NotSupportedException(result.Detail ?? "The current game state cannot accept a menu action.");
            }
            fieldSeconds = 0;
            return;
        }
        if (action.Kind == "native-refine")
        {
            PlayerActionResult result = world!.Apply(NativeRefinementMenuProjection.PlayerAction(action));
            if (!result.Accepted)
            {
                throw new NotSupportedException(result.Detail ?? "The current owners cannot refine this recipe.");
            }
            fieldSeconds = 0;
            return;
        }
        if (action.Kind is "shop-buy" or "shop-sell" && IsNativeShopActive)
        {
            PlayerActionResult result = world!.Apply(NativeShopMenuProjection.PurchaseAction(action));
            if (!result.Accepted)
            {
                throw new NotSupportedException(result.Detail);
            }
            fieldSeconds = 0;
            return;
        }
        if (action.Kind is "weapon-character" or "weapon-remodel" && IsNativeWeaponShopActive)
        {
            PlayerActionResult result = world!.Apply(NativeWeaponShopMenuProjection.PlayerAction(action));
            if (!result.Accepted)
            {
                throw new NotSupportedException(result.Detail);
            }
            fieldSeconds = 0;
            return;
        }
        if (action.Kind == "item-use" && session.State.Scene is SceneKind.Field or SceneKind.WorldMap)
        {
            int itemId = int.Parse(action.SubjectId.AsSpan("item-".Length), System.Globalization.CultureInfo.InvariantCulture);
            int row = int.Parse(action.TargetId.AsSpan(action.TargetId.StartsWith("item-target-", StringComparison.Ordinal) ? "item-target-".Length : "character-".Length), System.Globalization.CultureInfo.InvariantCulture);
            PlayerActionResult result = world!.Apply(new PlayerAction(PlayerActionKind.UseFieldItem, itemId, Text: string.IsNullOrEmpty(action.Value) ? null : action.Value, Quantity: row));
            if (!result.Accepted)
            {
                throw new NotSupportedException(result.Detail ?? "The current field cannot accept item use.");
            }
            fieldSeconds = 0;
            return;
        }
        if (action.Kind == "magazine-read" && session.State.Scene is SceneKind.Field or SceneKind.WorldMap)
        {
            int itemId = int.Parse(action.SubjectId.AsSpan("item-".Length), System.Globalization.CultureInfo.InvariantCulture);
            int pageId = int.Parse(action.Value, System.Globalization.CultureInfo.InvariantCulture);
            PlayerActionResult result = world!.Apply(new PlayerAction(PlayerActionKind.ReadMagazinePage, itemId, Quantity: pageId));
            if (!result.Accepted)
            {
                throw new NotSupportedException(result.Detail ?? "The current field cannot open this magazine page.");
            }
            fieldSeconds = 0;
            return;
        }
        if (action.Kind == "save")
        {
            gameMenus!.Close();
            OpenPlayerSaveMenu(PlayerSaveMenuMode.Save);
            return;
        }
        if (action.Kind == "config")
        {
            if (action.SubjectId == "Vibration")
            {
                bool enabled = action.Value switch
                {
                    "On" => true,
                    "Off" => false,
                    _ => throw new ArgumentException("Vibration requires On or Off.")
                };
                ControllerSettings settings = keyboard.Profile.Controller;
                ControlsProfile profile = keyboard.Profile.WithControllerSettings(settings.LeftStickDeadZone, settings.RightStickDeadZone, settings.WalkRunThreshold, vibrationEnabled: enabled);
                profile.Save(ControlsPreferencesPath);
                keyboard.Profile = profile;
                return;
            }
            UiPreferences nextPreferences = ReadCurrentUiPreferences().WithSetting(action.SubjectId, action.Value);
            ApplyCurrentBattleSettings(nextPreferences, action.SubjectId);
            UiPreferences installationPreferences = uiPreferences.WithInstallationSetting(action.SubjectId, action.Value, session.State.Scene != SceneKind.Title);
            installationPreferences.Save(UiPreferencesPath);
            uiPreferences = installationPreferences;
            ApplyDisplaySettings();
            text.FontFamily = uiPreferences.UiFontFamily;
            fieldAudio?.SetOutputPreferences(uiPreferences.MusicVolume, uiPreferences.SoundVolume);
            return;
        }
        throw new NotSupportedException($"Live menu action '{action.Kind}' requires a supported game-owned command.");
    }

    private UiPreferences ReadCurrentUiPreferences()
    {
        UiPreferences current = session.State.Scene == SceneKind.Title ? uiPreferences : uiPreferences with
        {
            BattlePolicy = session.State.FullBattlePlaythrough == true ? "NewGameFullCombat" : "ResearchInstantVictory",
            RandomEncounters = session.State.RandomEncounters?.Enabled == true
        };
        return current with { AtbMode = session.State.Scene == SceneKind.Title ? uiPreferences.AtbMode : session.State.GameplayPreferences.AtbMode, GunbladeMode = session.State.Scene == SceneKind.Title ? uiPreferences.GunbladeMode : session.State.GameplayPreferences.GunbladeMode };
    }

    private void ApplyCurrentBattleSettings(UiPreferences preferences, string key)
    {
        if (session.State.Scene != SceneKind.Title && key is "Battle policy" or "Random encounters")
        {
            world ??= CreateWorld();
            if (key == "Battle policy")
            {
                world.ApplyBattleInput(new BattleInput(BattleInputKind.ConfigurePlaythrough, FullCombat: preferences.BattlePolicy == "NewGameFullCombat"));
            }
            else if (key == "Random encounters")
            {
                world.ApplyBattleInput(new BattleInput(BattleInputKind.ConfigureEncounters, RandomEncounters: preferences.RandomEncounters));
            }
        }
        if (session.State.Scene != SceneKind.Title && key is "Battle ATB mode" or "Gunblade trigger")
        {
            world ??= CreateWorld();
            world.ConfigureGameplayPreferences(session.State.GameplayPreferences with { AtbMode = preferences.AtbMode, GunbladeMode = preferences.GunbladeMode });
        }
        if (key == "Battle ATB mode" && ActiveBattle is not null && ActiveBattle.Mode != preferences.AtbMode)
        {
            ApplyBattleCommand(new BattleInput(BattleInputKind.SetMode, Mode: preferences.AtbMode));
        }
        if (key == "Gunblade trigger" && ActiveBattle is not null && ActiveBattle.GunbladeMode != preferences.GunbladeMode)
        {
            ApplyBattleCommand(new BattleInput(BattleInputKind.SetGunbladeMode, GunbladeMode: preferences.GunbladeMode));
        }
    }

    private bool UpdateGameUi(TimeSpan elapsed, InputFrame input, bool hostOnly = false)
    {
        if (!hostOnly && UpdateNativeTutorialUi(elapsed, input))
        {
            return true;
        }
        if (gameMenus is null)
        {
            return false;
        }
        if (!hostOnly)
        {
            SynchronizeNativeNamePrompt();
        }
        if (!hostOnly && !gameMenus.IsOpen && !saveMenu.IsOpen && IsNativeGridMenuActive)
        {
            gameMenus.Open(IsNativeWeaponShopActive ? "shop-junk" : IsNativeShopActive ? "shop" : "main", false);
        }
        bool nativeMainWasOpen = !hostOnly && IsNativeGridMenuActive && gameMenus.IsOpen && !gameMenus.IsDemo && !gameMenus.IsNamePrompt;
        bool menuWasOpen = gameMenus.IsOpen;
        MouseState mouse = Mouse.GetState();
        bool mousePressed = IsActive && mouse.LeftButton == ButtonState.Pressed;
        bool mousePressEdge = mousePressed && !previousUiMousePressed;
        previousUiMousePressed = mousePressed;
        bool openMenu = keyboard.GameMenuRequested;
        if (keyboard.DebugRoomRequested && !saveMenu.IsOpen && !gameMenus.IsNamePrompt)
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
        else if (openMenu && !saveMenu.IsOpen && !gameMenus.IsNamePrompt)
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
            if (CompleteNativeNormalMenu(nativeMainWasOpen))
            {
                return true;
            }
            if (drainUiButtons && (keyboard.HeldFieldButtons & 0xF0) != 0)
            {
                if (!hostOnly && events?.PartyMenu is not null)
                {
                    UpdatePartyMenu(gameplayClock.Advance(elapsed, session.State.Scene, events?.IsScriptedInterface == true), default);
                }
                return true;
            }
            drainUiButtons = false;
            SynchronizeUiMoviePause();
            return false;
        }
        if (suppressNativeNameInput)
        {
            suppressNativeNameInput = false;
            return true;
        }
        if (input.MenuVertical < 0)
        {
            gameMenus.Navigate(MenuInput.Up);
        }
        if (input.MenuVertical > 0)
        {
            gameMenus.Navigate(MenuInput.Down);
        }
        if (input.MenuHorizontal < 0)
        {
            gameMenus.Navigate(MenuInput.Left);
        }
        if (input.MenuHorizontal > 0)
        {
            gameMenus.Navigate(MenuInput.Right);
        }
        if (input.MenuPage != 0)
        {
            gameMenus.Navigate(input.MenuPage < 0 ? MenuInput.PageUp : MenuInput.PageDown);
        }
        if (keyboard.UiCharacterPage < 0)
        {
            gameMenus.Navigate(MenuInput.PageLeft);
        }
        if (keyboard.UiCharacterPage > 0)
        {
            gameMenus.Navigate(MenuInput.PageRight);
        }
        if (keyboard.UiDetailRequested)
        {
            gameMenus.Navigate(MenuInput.Alternate);
        }
        if (input.Confirm || keyboard.UiConfirmRequested)
        {
            gameMenus.Navigate(MenuInput.Confirm);
        }
        if (input.Cancel || keyboard.UiCancelRequested)
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
        else if (!hostOnly && !gameMenus.IsNamePrompt && !gameMenus.IsDemo && IsNativeGridMenuActive)
        {
            UpdatePartyMenu(gameplayClock.Advance(elapsed, session.State.Scene, events?.IsScriptedInterface == true), default);
        }
        SynchronizeUiMoviePause();
        return true;
    }

    private bool SynchronizeNativeNamePrompt()
    {
        bool queuedOpened = SynchronizeQueuedEntityName();
        if (world?.PendingEntityName is not null || queuedOpened)
        {
            return queuedOpened;
        }
        if (gameMenus is null || events?.CurrentDialogue is not { IsNamePrompt: true } visibleDialogue)
        {
            return false;
        }
        DialogueState dialogue = events.DialogueTemplates.Single(item => item.Channel == visibleDialogue.Channel);
        if (ReferenceEquals(nativeNamePrompt, dialogue) && gameMenus.IsNamePrompt)
        {
            return false;
        }
        nativeNamePrompt = dialogue;
        suppressNativeNameInput = true;
        presentedDialogue = dialogue;
        (selectedChoice, enteredName) = DialoguePresentation.ResolveDraft(dialogue, session.State.DialogueDraft, events.PendingNameDefault ?? throw new InvalidOperationException("A native name prompt requires a default name."));
        int subject = events.PendingNameSubject ?? throw new InvalidOperationException("A native name prompt requires a naming subject.");
        gameMenus.OpenNamePrompt(subject, enteredName, name =>
        {
            PlayerActionResult result = world!.Apply(new PlayerAction(PlayerActionKind.EnterName, Text: name));
            if (!result.Accepted)
            {
                throw new NotSupportedException(result.Detail ?? "The current game state cannot accept the name.");
            }
            enteredName = name;
            dialoguePages.Complete(dialogue);
            session.SetDialogueDraft(null);
            nativeNamePrompt = null;
            fieldSeconds = 0;
            drainUiButtons = true;
        });
        fieldSeconds = 0;
        return true;
    }

    private void UpdateControlsCapturePointer()
    {
        MouseState mouse = Mouse.GetState();
        bool pressed = IsActive && mouse.LeftButton == ButtonState.Pressed;
        UiView view = gameMenus!.BuildView();
        view.Resize(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
        view.Pointer(mouse.X, mouse.Y, pressed && !previousUiMousePressed);
        previousUiMousePressed = pressed;
    }

    private bool IsNativeNormalMenuActive => events?.PartyMenu is { Kind: NativeFieldMenuKind.Normal, Phase: NativePartyMenuPhase.Active };
    private bool IsNativeShopActive => events?.PartyMenu is { Kind: NativeFieldMenuKind.Shop, Phase: NativePartyMenuPhase.Active, Shop: not null };
    private bool IsNativeWeaponShopActive => events?.PartyMenu is { Kind: NativeFieldMenuKind.WeaponShop, Phase: NativePartyMenuPhase.Active, WeaponShop: not null };
    private bool IsNativeGridMenuActive => IsNativeNormalMenuActive || IsNativeShopActive || IsNativeWeaponShopActive;

    private bool CompleteNativeNormalMenu(bool wasPresented)
    {
        if (!wasPresented || gameMenus!.IsOpen || saveMenu.IsOpen || !IsNativeGridMenuActive)
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
        bool pause = userPaused || gameMenus?.IsOpen == true || drainUiButtons || battleDebug is not null || debugBattle is not null;
        if (pause && !uiMovieSuspended)
        {
            movie?.Suspend();
            startupMovie?.Suspend();
            uiMovieSuspended = true;
        }
        else if (!pause && uiMovieSuspended)
        {
            movie?.Resume();
            startupMovie?.Resume();
            uiMovieSuspended = false;
        }
    }

    private void DrawGameUi(Rectangle bounds)
    {
        if (gameMenus?.IsOpen == true && uiCanvas is not null)
        {
            if (ActiveBattle is null)
            {
                batch.Draw(pixel, bounds, gameMenus.ScreenId == "new-game-options" ? Color.Black * 0.65f : Color.Black);
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
        SynchronizeNativeNamePrompt();
        UiControlCommand command = request.Ui ?? new UiControlCommand();
        NativeTutorialPlayer? tutorial = world?.Tutorial;
        if (tutorial is not null)
        {
            PresentTutorial(tutorial);
            if (request.Command.Equals("ui-close", StringComparison.OrdinalIgnoreCase))
            {
                ApplyTutorialInput(new PlayerAction(PlayerActionKind.Cancel));
            }
            else if (request.Command.Equals("ui-input", StringComparison.OrdinalIgnoreCase))
            {
                ApplyTutorialInput(new PlayerAction(command.Input switch { MenuInput.Confirm => PlayerActionKind.Confirm, MenuInput.Cancel => PlayerActionKind.Cancel, null => PlayerActionKind.Wait, _ => throw new InvalidOperationException("The tutorial owns semantic menu selection; only confirmation/cancellation is player input.") }));
            }
            else if (!request.Command.Equals("ui-status", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Close the native tutorial at its original confirmation boundary before opening another menu.");
            }
            return GameUiStatus()!;
        }
        if (gameMenus.IsNamePrompt && (request.Command.Equals("ui-close", StringComparison.OrdinalIgnoreCase) || request.Command.Equals("ui-open", StringComparison.OrdinalIgnoreCase) || request.Command.Equals("debug-room", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Confirm the required name prompt before changing screens.");
        }
        bool nativeMainWasOpen = IsNativeGridMenuActive && gameMenus.IsOpen && !gameMenus.IsDemo && !gameMenus.IsNamePrompt;
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
        return gameMenus is null ? null : new { gameMenus.IsOpen, gameMenus.IsDemo, gameMenus.ScreenId, gameMenus.Message, gameMenus.ScreenIds, Entries = gameMenus.Entries.Select(entry => new { entry.Id, entry.Label, entry.Enabled }).ToArray(), gameMenus.SelectedIndex, AutomaticPlay = ReadAutoPlayMenuState(), Tutorial = world?.Tutorial?.Status };
    }
}
