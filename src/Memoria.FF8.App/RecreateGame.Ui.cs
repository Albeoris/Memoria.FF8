using Memoria.FF8.GameUi;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8;

internal sealed record UiControlCommand(string? Screen = null, bool Demo = true, MenuInput? Input = null, float? X = null, float? Y = null, bool Pressed = false, string? Search = null, int? Variant = null);

internal sealed partial class RecreateGame
{
    private MenuController? gameMenus;
    private int? previousMenuWheel;
    private MonoGameUiCanvas? uiCanvas;
    private UiTheme uiTheme => CreateUiTheme();

    private UiTheme CreateUiTheme(float fontSize = 16, byte backgroundAlpha = 255)
    {
        return uiPreferences.MenuPresentation == MenuPresentation.Vector
            ? new VectorUiTheme { FontSize = fontSize, PanelBackgroundAlpha = backgroundAlpha, FontFamily = uiPreferences.UiFontFamily }
            : new UiTheme { FontSize = fontSize, PanelBackgroundAlpha = backgroundAlpha, FontFamily = uiPreferences.UiFontFamily };
    }
    private bool previousPresentationTogglePressed;
    private UiPreferences uiPreferences = new();
    private GameplayPreferences newGameMagicPreferences = new();
    private bool uiMovieSuspended;
    private LiveMenuCatalog? liveMenuCatalog;
    private NativeRefinementDefinitions? nativeRefinementDefinitions;
    private bool previousUiMousePressed;
    private bool uiControlPointerPressed;
    private bool drainUiButtons;
    private DialogueState? nativeNamePrompt;
    private bool suppressNativeNameInput;
    private readonly GlobalSettingsPaths globalSettings;
    private string UiPreferencesPath => globalSettings.UiPreferencesPath;
    private string ControlsPreferencesPath => globalSettings.ControlsPreferencesPath;

    private void InitializeGameUi()
    {
        newGameMagicPreferences = newGameMagicPreferences with { ChocoboWorldMode = DefaultChocoboWorldMode };
        session.Restore(ChocoboWorldSettings.Migrate(session.State, resourceHost!.Resources.Source.Edition));
        uiCanvas = new MonoGameUiCanvas(GraphicsDevice, batch, pixel, text, options.GameDirectory, resourceHost?.Resources);
        if (!File.Exists(UiPreferencesPath))
        {
            uiPreferences.Save(UiPreferencesPath);
        }
        ApplyDisplaySettings();
        text.FontFamily = uiPreferences.UiFontFamily;
        fieldAudio?.SetOutputPreferences(uiPreferences.MusicVolume, uiPreferences.SoundVolume);
        keyboard.Profile = ControlsProfile.Load(ControlsPreferencesPath);
        if (!File.Exists(ControlsPreferencesPath))
        {
            keyboard.Profile.Save(ControlsPreferencesPath);
        }
        InitializeSessionMenus();
        if (options.DebugUiScreen is string screen)
        {
            gameMenus!.Open(screen, true);
        }
    }

    private void InitializeSessionMenus()
    {
        gameMenus?.DisconnectMods();
        gameMenus = new MenuController(ReadLiveMenuData, ApplyLiveMenuAction) { Presentation = uiPreferences.MenuPresentation };
        InitializeChocoboWorldIntegration();
        InitializeAutoPlayUi();
        ConnectNativeTutorialUi();
        gameMenus.ScreenOpened += screen =>
        {
            if (!gameMenus.IsFixtureContext && !gameMenus.IsDemo && screen is "main" or "card")
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
        ConnectGuardianAbilityEditor();
        ConnectLocalizationTools();
        gameMenus.ConnectPreferences(ReadCurrentUiPreferences, ApplyLiveMenuAction);
        gameMenus.ConnectQuickSaveRetention(playerSaves.PreviewQuickSaveCount, CommitQuickSaveRetention);
        gameMenus.ConnectNaming(ReadDebugNames, (subject, name) => world!.Rename(subject, name));
        gameMenus.ConnectEncounterPolicyConsequence(() => world?.HasPendingBattle == true || world?.Battle?.Outcome == BattleOutcome.Active);
        gameMenus.ConnectBattleSource(() => ActiveBattle is null ? "No active battle" : debugBattle is not null ? "Laboratory battle" : "Game battle");
        gameMenus.ConnectBattleSettings(() => ActiveBattle?.Outcome == BattleOutcome.Active, victory => ApplyBattleCommand(new BattleInput(victory ? BattleInputKind.InstantVictory : BattleInputKind.InstantDefeat)));
        gameMenus.ConnectInstantOutcomes(() => ActiveBattle is not null || world?.ActiveTripleTriad is not null, () => CanCompleteInstantOutcome, CompleteInstantOutcome);
        gameMenus.SoundRequested += sound => fieldAudio?.PlayUiSound(sound);
        gameMenus.ConnectControls(() => keyboard.Profile, profile =>
        {
            profile.Save(ControlsPreferencesPath);
            keyboard.Profile = profile;
        }, () => keyboard.ControllerPresentation, () => controller!.Generation, () => keyboard.LastInputDevice);
        gameMenus.BattleDebugRequested += OpenBattleDebug;
        gameMenus.ModelBrowserRequested += OpenModelBrowser;
        gameMenus.ConnectSceneInspector(() => SceneInspectorAvailable, OpenSceneInspector);
    }

    private MenuData ReadLiveMenuData()
    {
        MenuData data;
        if (IsNativeWeaponShopActive)
        {
            EnsureLiveMenuCatalog();
            data = NativeWeaponShopMenuProjection.Build(events!.PartyMenu!.WeaponShop!, session.State.CharacterVitals, liveMenuCatalog!);
        }
        else if (IsNativeShopActive)
        {
            EnsureLiveMenuCatalog();
            data = NativeShopMenuProjection.Build(events!.PartyMenu!.Shop!, liveMenuCatalog!);
        }
        else
        {
            EnsureLiveMenuCatalog();
            GameState menuState = events?.PartyMenu is { Kind: NativeFieldMenuKind.Normal, Phase: NativePartyMenuPhase.Active } activeMenu && session.State.WorldControls is { } controls ? session.State with { WorldControls = controls with { Menu209 = activeMenu.NativeEntrySelection } } : session.State;
            data = LiveMenuData.Build(menuState, events?.Scene.Names, liveMenuCatalog!, placeId: events?.Scene.PlaceId);
            NativeRefinementMenuProjection.Augment(data, session.State, nativeRefinementDefinitions!, liveMenuCatalog!);
        }
        data.Settings["Music volume"] = uiPreferences.MusicVolume + "%";
        data.Settings["Cursor"] = uiPreferences.CursorMemory ? "Memory" : "Initial";
        data.Settings["Quick saves"] = uiPreferences.QuickSaveCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
        data.SupportedActions.Add("config:Quick saves");
        data.Settings["Field message"] = uiPreferences.FieldMessageSpeed.ToString();
        data.Settings["Vibration"] = keyboard.Profile.VibrationEnabled ? "On" : "Off";
        data.Settings["Effect volume"] = uiPreferences.SoundVolume + "%";
        data.Settings["UI presentation"] = uiPreferences.MenuPresentation.ToString();
        data.SupportedActions.Add("config:UI presentation");
        data.Settings["Text scale"] = uiPreferences.TextScale + "%";
        data.Settings["UI font"] = uiPreferences.UiFontFamily;
        data.Settings["UI font size"] = uiPreferences.UiFontSize.ToString();
        data.Settings["Dialogue font"] = uiPreferences.DialogueFontFamily;
        data.Settings["Dialogue font size"] = uiPreferences.DialogueFontSize.ToString();
        GameplayPreferences magicPreferences = session.State.Scene == SceneKind.Title ? newGameMagicPreferences : session.State.PendingGameplayPreferences ?? session.State.GameplayPreferences;
        magicPreferences = (magicPreferences with { Difficulty = ReadCurrentUiPreferences().Difficulty }).NormalizeForDifficulty();
        data.Settings["Shared magic stock"] = magicPreferences.SharedMagicStock ? "On" : "Off";
        data.Settings["Infinite magic"] = magicPreferences.InfiniteMagic ? "On" : "Off";
        data.Settings["Smart Attack"] = magicPreferences.SmartAttack ? "On" : "Off";
        data.MagicPreferencesUnavailableReason = MagicRuleBoundaryUnavailableReason;
        data.Settings["Chocobo World"] = ChocoboWorldSettings.Label(magicPreferences.ChocoboWorldMode ?? DefaultChocoboWorldMode);
        data.ChocoboWorldUnavailableReason = ChocoboWorldBoundaryUnavailableReason;
        data.SupportedActions.Add("config:Chocobo World");
        data.SupportedActions.UnionWith(["config:Shared magic stock", "config:Infinite magic", "config:Smart Attack", "attack-mode"]);
        data.Settings["Limit once per turn"] = ReadCurrentUiPreferences().LimitOncePerTurn ? "On" : "Off";
        data.SupportedActions.Add("config:Limit once per turn");
        data.Settings["Selphie card Slots"] = ReadCurrentUiPreferences().SelphieCardSlots ? "On" : "Off";
        data.SupportedActions.Add("config:Selphie card Slots");
        data.SupportedActions.UnionWith(["config:Weapon magazine required", "config:Weapon upgrade order"]);
        data.Settings["Weapon magazine required"] = ReadCurrentUiPreferences().WeaponMagazineRequired ? "On" : "Off";
        data.Settings["Weapon upgrade order"] = ReadCurrentUiPreferences().WeaponUpgradeOrder ? "On" : "Off";
        data.Settings["Card Mod once"] = ReadCurrentUiPreferences().CardModOnce ? "On" : "Off";
        data.SupportedActions.Add("config:Card Mod once");
        data.Settings["Draw once per spell"] = ReadCurrentUiPreferences().DrawOncePerSpell ? "On" : "Off";
        data.SupportedActions.Add("config:Draw once per spell");
        data.Settings["Difficulty"] = ReadCurrentUiPreferences().Difficulty.ToString();
        data.SupportedActions.Add("config:Difficulty");
        data.Settings["Battle ATB mode"] = ReadCurrentUiPreferences().AtbMode == BattleAtbMode.Normal ? "Classic" : "Turn Based";
        data.Settings["Gunblade trigger"] = ReadCurrentUiPreferences().GunbladeMode.ToString();
        data.Settings["GF Transformations"] = ReadCurrentUiPreferences().GuardianFormsEnabled ? "On" : "Off";
        data.GuardianFormsPreferenceLocked = ActiveBattle is not null || session.State.Battle is not null || session.State.PendingBattleEncounter is not null;
        data.LimitOncePerTurnPreferenceLocked = data.GuardianFormsPreferenceLocked;
        data.SelphieCardSlotsPreferenceLocked = data.GuardianFormsPreferenceLocked;
        data.DrawOncePerSpellPreferenceLocked = data.GuardianFormsPreferenceLocked;
        data.SupportedActions.Add("config:GF Transformations");
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
        data.Settings["Window mode"] = uiPreferences.BorderlessWindow ? "Borderless" : "Windowed";
        data.SupportedActions.Add("config:Window mode");
        data.Settings["V-Sync"] = uiPreferences.VSync ? "On" : "Off";
        data.Settings["Anti-aliasing"] = uiPreferences.Graphics.AntiAliasingLabel;
        data.SupportedActions.Add("config:Anti-aliasing");
        data.Settings["Field FPS"] = uiPreferences.FieldFps.ToString(System.Globalization.CultureInfo.InvariantCulture);
        data.Settings["World FPS"] = uiPreferences.WorldFps.ToString();
        data.Settings["Battle FPS"] = uiPreferences.BattleFps == 0 ? "Unlimited" : uiPreferences.BattleFps.ToString();
        data.Settings["Menu FPS"] = uiPreferences.MenuFps == 0 ? "Unlimited" : uiPreferences.MenuFps.ToString();
        data.Settings["Show FPS"] = uiPreferences.ShowFps ? "On" : "Off";
        data.SupportedActions.Add("config:Show FPS");
        data.SupportedActions.UnionWith(["config:V-Sync", "config:Field FPS", "config:World FPS", "config:Battle FPS", "config:Menu FPS"]);
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

    private void EnsureLiveMenuCatalog()
    {
        if (liveMenuCatalog is null)
        {
            using var assets = new Memoria.FF8.Assets.GameAssets(options.GameDirectory);
            liveMenuCatalog = LiveMenuCatalog.ReadInstalled(assets);
            nativeRefinementDefinitions = assets.ReadNativeRefinementDefinitions();
        }
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
        if (ApplyChocoboWorldMenuAction(action))
        {
            return;
        }
        if (ApplyHardcoreMenuAction(action))
        {
            return;
        }
        if (ApplyMagicRulesMenuAction(action))
        {
            return;
        }
        if (action.Kind == "angelo-learn")
        {
            if (!action.SubjectId.StartsWith("limit-", StringComparison.Ordinal) || !int.TryParse(action.SubjectId.AsSpan("limit-".Length), out int index))
            {
                throw new InvalidDataException("Invalid Angelo training ability.");
            }
            session.SelectAngeloLearning(index);
            return;
        }
        if (action.Kind == "new-game-start")
        {
            StartNewGameSession();
            return;
        }
        if (action.Kind == "party-switch")
        {
            PlayerActionResult result = world!.Apply(NativePartySwitchMenuProjection.PlayerAction(action));
            if (!result.Accepted)
            {
                ShowSaveNotice("The current menu does not permit this party-member exchange.");
            }
            fieldSeconds = 0;
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
                ShowSaveNotice("Card Mod requires its learned GF ability, enough cards and room for the complete item result. With Card Mod once enabled, this card must not have been refined in this game.");
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
                ShowSaveNotice(result.Detail ?? "The current interaction does not permit refinement.");
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
        if (action.Kind == "item-sort" && session.State.Scene is SceneKind.Field or SceneKind.WorldMap)
        {
            NativeEconomy economy = session.State.Economy ?? throw new NotSupportedException("Item sorting requires the saved inventory.");
            session.SetEconomy(economy.SortItems());
            if (session.State.Scene == SceneKind.Field)
            {
                session.SetEventState(events!.Capture());
            }
            return;
        }
        if (action.Kind is "item-use" or "item-battle" && session.State.Scene is SceneKind.Field or SceneKind.WorldMap)
        {
            int itemId = int.Parse(action.SubjectId.AsSpan("item-".Length), System.Globalization.CultureInfo.InvariantCulture);
            int row = action.Kind == "item-battle" ? 0 : int.Parse(action.TargetId.AsSpan(action.TargetId.StartsWith("item-target-", StringComparison.Ordinal) ? "item-target-".Length : "character-".Length), System.Globalization.CultureInfo.InvariantCulture);
            PlayerActionResult result = world!.Apply(new PlayerAction(PlayerActionKind.UseFieldItem, itemId, Text: string.IsNullOrEmpty(action.Value) ? null : action.Value, Quantity: row));
            if (!result.Accepted)
            {
                throw new NotSupportedException(result.Detail ?? "The current field cannot accept item use.");
            }
            if (action.Kind == "item-battle")
            {
                gameMenus!.Close();
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
            if (action.SubjectId == "Quick saves")
            {
                throw new InvalidOperationException("Use the quick-save retention editor to review and save this limit.");
            }
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
            SynchronizeMenuPresentation();
            gameMenus?.InvalidateLiveData();
            ApplyDisplaySettings();
            text.FontFamily = uiPreferences.UiFontFamily;
            fieldAudio?.SetOutputPreferences(uiPreferences.MusicVolume, uiPreferences.SoundVolume);
            return;
        }
        throw new NotSupportedException($"Live menu action '{action.Kind}' requires a supported game-owned command.");
    }

    private QuickSaveCleanupResult CommitQuickSaveRetention(int count, int confirmedRemovalCount)
    {
        UiPreferences accepted = uiPreferences.WithInstallationSetting("Quick saves", count.ToString(System.Globalization.CultureInfo.InvariantCulture), session.State.Scene != SceneKind.Title);
        QuickSaveCleanupResult result = playerSaves.CommitQuickSaveCount(count, confirmedRemovalCount, () =>
        {
            accepted.Save(UiPreferencesPath);
            uiPreferences = accepted;
        });
        gameMenus?.InvalidateLiveData();
        return result;
    }

    private UiPreferences ReadCurrentUiPreferences()
    {
        UiPreferences current = session.State.Scene == SceneKind.Title ? uiPreferences : uiPreferences with
        {
            BattlePolicy = session.State.FullBattlePlaythrough == true ? "NewGameFullCombat" : "ResearchInstantVictory",
            RandomEncounters = session.State.RandomEncounters?.Enabled == true
        };
        GameplayPreferences gameplay = session.State.PendingGameplayPreferences ?? session.State.GameplayPreferences;
        return current with { WeaponMagazineRequired = session.State.Scene == SceneKind.Title ? uiPreferences.WeaponMagazineRequired : gameplay.WeaponMagazineRequired, WeaponUpgradeOrder = session.State.Scene == SceneKind.Title ? uiPreferences.WeaponUpgradeOrder : gameplay.WeaponUpgradeOrder, CardModOnce = session.State.Scene == SceneKind.Title ? uiPreferences.CardModOnce : gameplay.CardModOnce, LimitOncePerTurn = session.State.Scene == SceneKind.Title ? uiPreferences.LimitOncePerTurn : gameplay.LimitOncePerTurn, SelphieCardSlots = session.State.Scene == SceneKind.Title ? uiPreferences.SelphieCardSlots : gameplay.SelphieCardSlots, DrawOncePerSpell = session.State.Scene == SceneKind.Title ? uiPreferences.DrawOncePerSpell : gameplay.DrawOncePerSpell, Difficulty = session.State.Scene == SceneKind.Title ? uiPreferences.Difficulty : gameplay.Difficulty, AtbMode = session.State.Scene == SceneKind.Title ? uiPreferences.AtbMode : gameplay.AtbMode, GunbladeMode = session.State.Scene == SceneKind.Title ? uiPreferences.GunbladeMode : gameplay.GunbladeMode, GuardianFormsEnabled = session.State.Scene == SceneKind.Title ? uiPreferences.GuardianFormsEnabled : gameplay.GuardianFormsEnabled };
    }

    private void ApplyCurrentBattleSettings(UiPreferences preferences, string key)
    {
        if (key == "Draw once per spell" && (ActiveBattle is not null || session.State.Battle is not null || session.State.PendingBattleEncounter is not null) && preferences.DrawOncePerSpell != session.State.GameplayPreferences.DrawOncePerSpell)
        {
            throw new InvalidOperationException("Draw once per spell cannot change until the battle ends.");
        }
        if (key == "Limit once per turn" && (ActiveBattle is not null || session.State.Battle is not null || session.State.PendingBattleEncounter is not null) && preferences.LimitOncePerTurn != session.State.GameplayPreferences.LimitOncePerTurn)
        {
            throw new InvalidOperationException("Limit once per turn cannot change until the battle ends.");
        }
        if (key == "Selphie card Slots" && (ActiveBattle is not null || session.State.Battle is not null || session.State.PendingBattleEncounter is not null) && preferences.SelphieCardSlots != session.State.GameplayPreferences.SelphieCardSlots)
        {
            throw new InvalidOperationException("Selphie card Slots cannot change until the battle ends.");
        }
        if (key == "GF Transformations" && ActiveBattle is { } active && preferences.GuardianFormsEnabled != active.GuardianFormsEnabled)
        {
            throw new InvalidOperationException("GF Transformations cannot change until the battle ends.");
        }
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
        if (session.State.Scene != SceneKind.Title && key is "Weapon magazine required" or "Weapon upgrade order" or "Card Mod once" or "Draw once per spell" or "Limit once per turn" or "Selphie card Slots" or "Difficulty" or "Battle ATB mode" or "Gunblade trigger" or "GF Transformations")
        {
            world ??= CreateWorld();
            world.ConfigureGameplayPreferences((session.State.PendingGameplayPreferences ?? session.State.GameplayPreferences) with { WeaponMagazineRequired = preferences.WeaponMagazineRequired, WeaponUpgradeOrder = preferences.WeaponUpgradeOrder, CardModOnce = preferences.CardModOnce, LimitOncePerTurn = preferences.LimitOncePerTurn, SelphieCardSlots = preferences.SelphieCardSlots, DrawOncePerSpell = preferences.DrawOncePerSpell, Difficulty = preferences.Difficulty, AtbMode = preferences.AtbMode, GunbladeMode = preferences.GunbladeMode, GuardianFormsEnabled = preferences.GuardianFormsEnabled });
        }
        if (key == "Difficulty" && debugBattle is not null)
        {
            debugBattle.SetDifficulty(preferences.Difficulty);
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
            PresentPendingMagicModeReview();
            SynchronizeNativeNamePrompt();
        }
        if (!hostOnly && !gameMenus.IsOpen && !saveMenu.IsOpen && IsNativeGridMenuActive)
        {
            gameMenus.Open(IsNativeWeaponShopActive ? "shop-junk" : IsNativeShopActive ? "shop" : "main", false);
        }
        bool nativeMainWasOpen = !hostOnly && IsNativeGridMenuActive && gameMenus.IsOpen && !gameMenus.IsDemo && !gameMenus.IsNamePrompt;
        bool menuWasOpen = gameMenus.IsOpen;
        MouseState mouse = Mouse.GetState();
        int wheelDelta = previousMenuWheel is int previousWheel ? mouse.ScrollWheelValue - previousWheel : 0;
        previousMenuWheel = mouse.ScrollWheelValue;
        bool mousePressed = IsActive && mouse.LeftButton == ButtonState.Pressed;
        bool mousePressEdge = mousePressed && !previousUiMousePressed;
        previousUiMousePressed = mousePressed;
        bool searchOwnedInput = gameMenus.IsGuardianSearchEditing || gameMenus.IsModSearchEditing;
        bool searchOwnershipChanged = false;
        void DispatchMenuInput(MenuInput command)
        {
            if (searchOwnershipChanged)
            {
                return;
            }
            string previousScreen = gameMenus.ScreenId;
            gameMenus.Navigate(command);
            if (previousScreen != gameMenus.ScreenId && previousScreen is "mods-actions" or "mods-diagnostics" or "gf-mod-review")
            {
                searchOwnershipChanged = true;
                keyboard.SuppressHeldUiInput();
            }
            if (searchOwnedInput != (gameMenus.IsGuardianSearchEditing || gameMenus.IsModSearchEditing))
            {
                searchOwnershipChanged = true;
                keyboard.SuppressHeldUiInput();
            }
        }
        bool openMenu = keyboard.GameMenuRequested;
        if (openMenu && !saveMenu.IsOpen && (gameMenus.IsModSearchEditing || gameMenus.ScreenId is "mods-actions" or "mods-diagnostics" or "gf-mod-review"))
        {
            DispatchMenuInput(MenuInput.Cancel);
            return true;
        }
        if (openMenu && !saveMenu.IsOpen && (gameMenus.IsGuardianAbilityWorkspace || gameMenus.IsModManager))
        {
            DispatchMenuInput(MenuInput.Actions);
            return true;
        }
        if (keyboard.DebugRoomRequested && !saveMenu.IsOpen && !gameMenus.IsNamePrompt)
        {
            if (gameMenus.IsOpen)
            {
                if (gameMenus.IsGuardianAbilityWorkspace)
                {
                    DispatchMenuInput(MenuInput.Cancel);
                }
                else
                {
                    gameMenus.Close();
                }
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
                if (gameMenus.IsGuardianAbilityWorkspace)
                {
                    DispatchMenuInput(MenuInput.Cancel);
                }
                else
                {
                    gameMenus.Close();
                }
            }
            else if (ActiveBattle is not null)
            {
                gameMenus.Open("config", false);
            }
            else if (session.State.Scene == SceneKind.Title || session.State.Scene == SceneKind.WorldMap || session.State.Scene == SceneKind.Field && events?.CanOpenFieldMenu == true)
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
        bool wasTypingName = gameMenus.IsTypingName || gameMenus.IsGuardianSearchEditing || gameMenus.IsModSearchEditing;
        if (keyboard.UiSwitchColumnRequested && gameMenus.IsGuardianAbilityEditor)
        {
            DispatchMenuInput(MenuInput.SwitchColumn);
        }
        if (keyboard.UiPaneFocus != 0 && (gameMenus.IsGuardianAbilityEditor || gameMenus.IsModManager))
        {
            DispatchMenuInput(keyboard.UiPaneFocus < 0 ? MenuInput.FocusLeft : MenuInput.FocusRight);
        }
        if (input.MenuVertical < 0)
        {
            DispatchMenuInput(MenuInput.Up);
        }
        if (input.MenuVertical > 0)
        {
            DispatchMenuInput(MenuInput.Down);
        }
        if (input.MenuHorizontal < 0)
        {
            DispatchMenuInput(MenuInput.Left);
        }
        if (input.MenuHorizontal > 0)
        {
            DispatchMenuInput(MenuInput.Right);
        }
        if (input.MenuPage != 0)
        {
            DispatchMenuInput(input.MenuPage < 0 ? MenuInput.PageUp : MenuInput.PageDown);
        }
        if (keyboard.UiCharacterPage < 0)
        {
            DispatchMenuInput(MenuInput.PageLeft);
        }
        if (keyboard.UiCharacterPage > 0)
        {
            DispatchMenuInput(MenuInput.PageRight);
        }
        if (keyboard.UiDetailRequested)
        {
            DispatchMenuInput(MenuInput.Alternate);
        }
        if (input.Confirm || keyboard.UiConfirmRequested)
        {
            DispatchMenuInput(MenuInput.Confirm);
        }
        if (input.Cancel || keyboard.UiCancelRequested)
        {
            DispatchMenuInput(MenuInput.Cancel);
        }
        if (gameMenus.IsOpen)
        {
            if (keyboard.UiTabRequested && gameMenus.IsEditingName)
            {
                DispatchMenuInput(MenuInput.Tab);
            }
            UiView view = gameMenus.BuildView();
            view.Resize(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
            if ((keyboard.UiTabRequested || keyboard.UiReverseTabRequested) && !gameMenus.IsEditingName)
            {
                if (gameMenus.IsGuardianAbilityEditor || gameMenus.IsModManager)
                {
                    DispatchMenuInput(keyboard.UiReverseTabRequested ? MenuInput.ReverseTab : MenuInput.Tab);
                }
                else
                {
                    view.Key(UiKey.Tab);
                }
            }
            if (!searchOwnershipChanged)
            {
                view.Pointer(mouse.X, mouse.Y, mousePressEdge);
                if (searchOwnedInput != (gameMenus.IsGuardianSearchEditing || gameMenus.IsModSearchEditing))
                {
                    searchOwnershipChanged = true;
                    keyboard.SuppressHeldUiInput();
                }
            }
            if (!searchOwnershipChanged && IsActive && gameMenus.ScreenId == "mods" && wheelDelta != 0)
            {
                gameMenus.HandleModsWheel(mouse.X, mouse.Y, wheelDelta);
            }
            if (!searchOwnershipChanged && IsActive && gameMenus.IsGuardianAbilityEditor && wheelDelta != 0)
            {
                gameMenus.HandleGuardianAbilityEditorWheel(mouse.X, mouse.Y, wheelDelta);
            }
        }
        if (wasTypingName != (gameMenus.IsTypingName || gameMenus.IsGuardianSearchEditing || gameMenus.IsModSearchEditing))
        {
            keyboard.SuppressHeldNameInput();
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
        bool pause = focusPaused || userPaused || updateSave.Paused || gameMenus?.IsOpen == true || drainUiButtons || battleDebug is not null || debugBattle is not null || sceneInspector is not null || modelBrowser is not null;
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

    private void UpdateMenuPresentationHotkey(KeyboardState keys, bool capturing)
    {
        bool pressed = keys.IsKeyDown(Keys.F7);
        if (IsActive && !capturing && pressed && !previousPresentationTogglePressed)
        {
            MenuPresentation presentation = uiPreferences.MenuPresentation == MenuPresentation.Vector ? MenuPresentation.Classic : MenuPresentation.Vector;
            uiPreferences = uiPreferences with { MenuPresentation = presentation };
            text.FontFamily = uiPreferences.UiFontFamily;
            uiPreferences.Save(UiPreferencesPath);
            SynchronizeMenuPresentation();
            gameMenus?.InvalidateLiveData();
        }
        previousPresentationTogglePressed = pressed;
    }

    private void SynchronizeMenuPresentation()
    {
        if (gameMenus is not null)
        {
            gameMenus.Presentation = uiPreferences.MenuPresentation;
        }
    }

    private void DrawGameUi(Rectangle bounds)
    {
        SynchronizeMenuPresentation();
        if (gameMenus?.IsOpen == true && uiCanvas is not null)
        {
            batch.Draw(pixel, bounds, Color.Black);
            UiView view = gameMenus.BuildView();
            view.Resize(bounds.Width, bounds.Height);
            view.Draw(uiCanvas, gameMenus.CreateTheme());
        }
    }

    private object DispatchUiControl(ControlRequest request)
    {
        SynchronizeMenuPresentation();
        if (gameMenus is null)
        {
            throw new InvalidOperationException("UI is not initialized.");
        }
        SynchronizeNativeNamePrompt();
        UiControlCommand command = request.Ui ?? new UiControlCommand();
        if (TryDispatchModelBrowserControl(request, command) || TryDispatchSceneInspectorControl(request, command))
        {
            SynchronizeUiMoviePause();
            return GameUiStatus()!;
        }
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
        return gameMenus is null ? null : new { gameMenus.IsOpen, gameMenus.IsDemo, gameMenus.ScreenId, gameMenus.Message, gameMenus.ScreenIds, Entries = gameMenus.Entries.Select(entry => new { entry.Id, entry.Label, entry.Enabled }).ToArray(), gameMenus.SelectedIndex, AutomaticPlay = ReadAutoPlayMenuState(), Tutorial = world?.Tutorial?.Status, SceneInspector = SceneInspectorUiStatus(), ModelBrowser = ModelBrowserUiStatus() };
    }
}
