using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private MenuController? playerMenu;
    private Ff7KernelTextResources? menuText;
    private readonly Dictionary<int, int> menuPortraits = [];
    private Action<ControlsProfile>? writeSharedControls;
    private bool menuPointerPressed;
    private bool playerMenuClockChanged;
    private bool pendingPlayerMenuRequest;
    private static readonly IReadOnlySet<string> PlayerPreferenceKeys = new[] { "config-graphics", "config-frame-limits", "config-audio", "config-interface", "config-text", "config-speed" }.SelectMany(page => SettingsPlacement.Pages[page].Items).Where(key => !key.StartsWith("config-", StringComparison.Ordinal)).Append("Battle ATB mode").ToHashSet(StringComparer.Ordinal);

    internal MenuController? ActivePlayerMenu => guestPlayerMenu?.IsOpen == true ? guestPlayerMenu : playerMenu?.IsOpen == true ? playerMenu : null;
    internal bool IsPlayerMenuOpen => ActivePlayerMenu is not null;
    private bool CanUsePlayerMenuPhs => Session?.CanUseMenuPhs == true;

    private bool CanOpenPlayerMenu => Session is { HasPendingTick: false } session && (session.World is { } world
        ? CanOpenWorldPlayerMenu(world.Process)
        : CanOpenFieldPlayerMenu(session.Host.Process));

    internal static bool CanOpenFieldPlayerMenu(Ff7FieldProcess process) => !process.MenuDisabled && !process.PlayerLocked && process.GlobalCommand == Ff7GlobalCommand.None && process.Transition is null && process.Movie is not { Playing: true } && process.Dialogues.All(dialogue => dialogue.Completed);

    internal static bool CanOpenWorldPlayerMenu(Ff7WorldProcess process) => process.MainState == 1 && process.FadeRate == 0 && process.FadeDisplayedAlpha == 0 && process.MovementEnabled && process.ControlLock == 0 && process.Dialogue is not { Completed: false };

    private void EnsurePlayerMenu()
    {
        if (playerMenu is not null)
        {
            return;
        }
        playerMenu = new(ReadPlayerMenuData, ApplyPlayerMenuAction) { ShowMainScreenTitle = true };
        playerMenu.ConnectPreferences(() => PresentationPreferences, ApplyPlayerMenuAction);
        if (writeSharedControls is not null)
        {
            playerMenu.ConnectControls(() => sharedInput.Profile, profile =>
            {
                writeSharedControls(profile);
                sharedInput.Profile = profile;
            }, () => sharedInput.ControllerPresentation, () => sharedInput.ControllerGeneration, () => sharedInput.LastInputDevice);
        }
        else
        {
            playerMenu.ConnectReadOnlyTutorialControls(() => sharedInput.Profile, () => sharedInput.ControllerPresentation, () => sharedInput.LastInputDevice);
        }
        playerMenu.Register(new()
        {
            Id = "main",
            Title = "Final Fantasy VII",
            Help = "Select a command.",
            Layout = MenuLayout.Main,
            Entries = _ =>
            [
                new("items", "Item", "View the items carried in this world.", "ff7-items"),
                new("magic", "Magic", "View spells granted by equipped materia.", "ff7-magic"),
                new("materia", "Materia", "Equip and link materia in the current party.", "ff7-materia"),
                new("equip", "Equip", "View equipped weapons, armor and accessories.", "ff7-equip"),
                new("status", "Status", "View the current party.", "ff7-status"),
                new("order", "Order", "Change the party order or battle row.", "ff7-order"),
                new("limit", "Limit", "View current Limit levels and learned slots.", "ff7-limit"),
                new("config", "Config", "Shared controls, graphics, audio and interface preferences.", "config"),
                new("phs", "PHS", CanUsePlayerMenuPhs ? "Exchange active and reserve party members." : "PHS is unavailable at this location.", "ff7-phs", Enabled: CanUsePlayerMenuPhs),
                new("save", "Save", "Save the current continuation.", Action: new("ff7-save"), Enabled: CanSave),
                new("load", "Load", "Load a saved continuation.", Action: new("ff7-load"), Enabled: ContinueRequested is not null),
                new("world", "Switch world: FFVIII", "Choose associations before returning to the retained world.", Action: new("ff7-world"), Enabled: CanSave && envelope.Ff8Payload is { Length: > 0 }),
                new("close", "Return", "Return to the game.", Action: new("ff7-close"))
            ]
        });
        playerMenu.Register(new() { Id = "ff7-items", Title = "Items", Help = "Items carried in this world.", Entries = menu => menu.Data.Items.Select(item => new MenuEntry(item.Id, $"{item.Name}   {item.Quantity}", item.Description)).ToArray(), Details = menu => [menu.SelectedEntry?.Help ?? ""] });
        playerMenu.Register(new() { Id = "ff7-status", Title = "Status", Help = "Current party.", Entries = menu => menu.Data.Characters.Where(character => character.InParty).Select(character => new MenuEntry(character.Id, $"{character.Name}   Lv {character.Level}   HP {character.CurrentHp}", $"MP {character.Stats["MP"]}   EXP {character.Experience}")).ToArray(), Details = menu => [menu.SelectedEntry?.Help ?? ""] });
        RegisterMateriaMenus(playerMenu);
        RegisterNativeCharacterMenus(playerMenu);
        playerMenu.Register(new()
        {
            Id = "config",
            Title = "Config",
            Help = "Shared presentation and control preferences.",
            Entries = menu => [.. menu.ConfigEntries().Where(entry => entry.Id is "config-graphics" or "config-audio" or "config-interface" or "config-controls"), new("config-speed", "Speed", "Movement and fast-forward preferences.", "config-speed"), new("config-combat", "Combat", "Battle ATB mode for the current host.", "config-combat")]
        });
        playerMenu.Register(new()
        {
            Id = "config-combat",
            Title = "Combat",
            Help = "Select the Battle ATB mode.",
            Entries = menu => menu.ConfigEntries().Where(entry => entry.Id == "Battle ATB mode").ToArray()
        });
    }

    private MenuData ReadPlayerMenuData()
    {
        Ff7Session session = Session ?? throw new InvalidOperationException("FFVII party menus require an active continuation.");
        menuText ??= Ff7KernelTextResources.Load(installation);
        MenuData data = Ff7PlayerMenuProjection.Create(session.Progress, session.Progress.Party, characterIdentity, menuText.ItemNames, menuText.ItemDescriptions, session.World is null ? session.Field.Name : "World map", TimeSpan.FromSeconds(session.Clock.ElapsedSeconds), CanSave, PlayerMenuPortrait, session.GuestFieldVitals?.ReadAll());
        playerMenu!.PopulateHostSettingValues(data, PlayerPreferenceKeys);
        data.Settings["Difficulty"] = PresentationPreferences.Difficulty.ToString();
        return data;
    }

    private UiImageSource PlayerMenuPortrait(Ff7CharacterDisplayIdentity identity)
    {
        if (identity.PortraitGame == SessionGame.Ff8)
        {
            return new(UiImageKind.CharacterPortrait, identity.PortraitCharacter);
        }
        if (!menuPortraits.TryGetValue(identity.PortraitCharacter, out int id))
        {
            Ff7MenuPortrait portrait = Ff7MenuPortrait.Decode(Ff7MenuResources.LoadPortraits(installation).GetPortrait(identity.PortraitCharacter));
            Color[] colors = portrait.Pixels.ToArray().Select(value => Color.FromNonPremultiplied((byte)(value >> 16), (byte)(value >> 8), (byte)value, (byte)(value >> 24))).ToArray();
            id = Interlocked.Increment(ref nextNamingPortrait);
            dialogueCanvas!.RegisterHostImage(id, UploadTexture(portrait.Width, portrait.Height, colors));
            menuPortraits.Add(identity.PortraitCharacter, id);
        }
        return new(UiImageKind.HostArtwork, id);
    }

    private void ApplyPlayerMenuAction(MenuAction action)
    {
        if (ApplyMateriaMenuAction(action) || ApplyNativeCharacterMenuAction(action))
        {
            return;
        }
        if (action.Kind == "config")
        {
            if (!PlayerPreferenceKeys.Contains(action.SubjectId))
            {
                throw new NotSupportedException("This FFVII menu exposes only shared presentation and speed preferences.");
            }
            UiPreferences current = action.SubjectId == "Battle ATB mode" ? PresentationPreferences : dialoguePreferences?.Invoke() ?? new UiPreferences();
            UiPreferences updated = current.WithSetting(action.SubjectId, action.Value);
            if (updated != current)
            {
                (setDialoguePreferences ?? throw new InvalidOperationException("Shared preferences are read-only in this host."))(updated);
                if (action.SubjectId == "Battle ATB mode")
                {
                    SetSharedBattleAtbMode(updated.AtbMode);
                }
                playerMenu!.Presentation = updated.MenuPresentation;
            }
            return;
        }
        if (action.Kind == "ff7-save")
        {
            if (CanSave)
            {
                SaveRequested?.Invoke(Capture());
            }
            return;
        }
        CombinedSessionSave? outgoing = action.Kind == "ff7-world" && CanSave ? Capture() : null;
        ClosePlayerMenu();
        if (action.Kind == "ff7-load")
        {
            ContinueRequested?.Invoke();
        }
        else if (outgoing is not null && envelope.Ff8Payload is { Length: > 0 })
        {
            WorldSwitchRequested?.Invoke(outgoing);
        }
    }

    private bool UpdatePlayerMenu(GameTime time, KeyboardState keys, GamePadState pad)
    {
        if ((sharedInput.GameMenuRequested || pendingPlayerMenuRequest) && ActivePlayerMenu?.IsCapturingControls != true)
        {
            pendingPlayerMenuRequest = false;
            if (IsPlayerMenuOpen)
            {
                AdvancePlayerMenuClock(time.ElapsedGameTime);
                ClosePlayerMenu();
                RefreshPlayerMenuPresentation();
                return true;
            }
            if (!CanOpenPlayerMenu)
            {
                pendingPlayerMenuRequest = Session is { } session && (session.HasPendingTick || session.Host.Process.GlobalCommand == Ff7GlobalCommand.Battle);
                return false;
            }
            EnsurePlayerMenu();
            Session!.PublishNativeMenuPartyStatistics();
            Session.Host.InitializeNativeMenuAudio();
            playerMenu!.Presentation = PresentationPreferences.MenuPresentation;
            playerMenu.Open();
            pendingPressedKeys = 0;
            sharedInput.SuppressHeldUiInput();
            menuPointerPressed = Mouse.GetState().LeftButton == ButtonState.Pressed;
            SetAudioPaused(focusPaused || userPaused || debugMenu || error is not null);
            AdvancePlayerMenuClock(time.ElapsedGameTime);
            RefreshPlayerMenuPresentation();
            return true;
        }
        if (ActivePlayerMenu is not { } menu)
        {
            return false;
        }
        AdvancePlayerMenuClock(time.ElapsedGameTime);
        if (menu.ScreenId == "main" && !menu.IsSelectingCharacter && (sharedInput.UiCharacterPage != 0 || sharedInput.UiPaneFocus != 0) && HasActiveGuestMenu)
        {
            MenuController next = ReferenceEquals(menu, playerMenu) ? EnsureGuestPlayerMenu() : playerMenu!;
            menu.Close();
            next.Presentation = PresentationPreferences.MenuPresentation;
            next.Open();
        }
        else
        {
            UpdateSharedMenuInput(menu, keys, pad, ref menuPointerPressed);
        }
        if (!IsPlayerMenuOpen)
        {
            ClosePlayerMenu();
        }
        RefreshPlayerMenuPresentation();
        return true;
    }

    private void AdvancePlayerMenuClock(TimeSpan elapsed)
    {
        uint previous = Session!.Clock.ElapsedSeconds;
        Session.AdvanceClock(elapsed);
        if (Session.Clock.ElapsedSeconds != previous)
        {
            playerMenuClockChanged = true;
        }
    }

    private void RefreshPlayerMenuPresentation()
    {
        if (playerMenuClockChanged)
        {
            playerMenu?.InvalidateLiveData();
            guestPlayerMenu?.InvalidateLiveData();
            playerMenuClockChanged = false;
        }
        UpdateAudio();
    }

    private void UpdateSharedMenuInput(MenuController menu, KeyboardState keys, GamePadState pad, ref bool pointerPressed)
    {
        if (!menu.UpdateControlsCapture(keys, pad, !focusPaused))
        {
            if (semanticInput.MenuVertical != 0)
            {
                menu.Navigate(semanticInput.MenuVertical < 0 ? MenuInput.Up : MenuInput.Down);
            }
            if (semanticInput.MenuHorizontal != 0)
            {
                menu.Navigate(semanticInput.MenuHorizontal < 0 ? MenuInput.Left : MenuInput.Right);
            }
            if (semanticInput.MenuPage != 0)
            {
                menu.Navigate(semanticInput.MenuPage < 0 ? MenuInput.PageUp : MenuInput.PageDown);
            }
            if (ReferenceEquals(menu, guestPlayerMenu) && sharedInput.UiCharacterPage != 0)
            {
                menu.Navigate(sharedInput.UiCharacterPage < 0 ? MenuInput.PageLeft : MenuInput.PageRight);
            }
            if (semanticInput.Confirm)
            {
                menu.Navigate(MenuInput.Confirm);
            }
            if (semanticInput.Cancel)
            {
                menu.Navigate(MenuInput.Cancel);
            }
            MouseState mouse = Mouse.GetState();
            bool pressed = mouse.LeftButton == ButtonState.Pressed;
            if (pressed && !pointerPressed && menu.View is { } view)
            {
                view.Resize(device.Viewport.Width, device.Viewport.Height);
                view.Pointer(mouse.X, mouse.Y, true);
            }
            pointerPressed = pressed;
        }
    }

    private void ClosePlayerMenu()
    {
        playerMenu?.Close();
        guestPlayerMenu?.Close();
        pendingPressedKeys = 0;
        moveHorizontal = 0;
        moveVertical = 0;
        sharedInput.SuppressHeldUiInput();
        SetAudioPaused(focusPaused || userPaused || debugMenu || error is not null);
    }

    private void DrawPlayerMenu(Rectangle bounds)
    {
        if (ActivePlayerMenu is not { } menu)
        {
            return;
        }
        menu.Presentation = PresentationPreferences.MenuPresentation;
        menu.PrepareViewport(bounds.Width, bounds.Height);
        UiView view = menu.BuildView();
        view.Resize(bounds.Width, bounds.Height);
        view.Draw(dialogueCanvas!, SharedTheme());
    }

    private void DisposePlayerMenu()
    {
        playerMenu?.Close();
        playerMenu = null;
        guestPlayerMenu?.Close();
        guestPlayerMenu = null;
        guestMenuAssets?.Dispose();
        guestMenuAssets = null;
        guestMenuCatalog = null;
        pendingPlayerMenuRequest = false;
        playerMenuClockChanged = false;
        foreach (int id in menuPortraits.Values)
        {
            dialogueCanvas?.UnregisterHostImage(id);
        }
        menuPortraits.Clear();
    }
}
