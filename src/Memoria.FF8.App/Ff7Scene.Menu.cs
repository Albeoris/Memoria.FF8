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
    private static readonly IReadOnlySet<string> PlayerPreferenceKeys = new[] { "config-graphics", "config-frame-limits", "config-audio", "config-interface", "config-text", "config-speed" }.SelectMany(page => SettingsPlacement.Pages[page].Items).Where(key => !key.StartsWith("config-", StringComparison.Ordinal)).ToHashSet(StringComparer.Ordinal);

    internal bool IsPlayerMenuOpen => playerMenu?.IsOpen == true;

    private bool CanOpenPlayerMenu => Session is { HasPendingTick: false } session && (session.World is { } world
        ? CanOpenWorldPlayerMenu(world.Process, world.ReadPersistentByte(31))
        : CanOpenFieldPlayerMenu(session.Host.Process, session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 31)));

    internal static bool CanOpenFieldPlayerMenu(Ff7FieldProcess process, byte nativeControlFlags) => !process.MenuDisabled && !process.PlayerLocked && process.GlobalCommand == Ff7GlobalCommand.None && process.Transition is null && process.Movie is not { Playing: true } && process.Dialogues.All(dialogue => dialogue.Completed) && (nativeControlFlags & 2) == 0;

    internal static bool CanOpenWorldPlayerMenu(Ff7WorldProcess process, byte nativeControlFlags) => process.MainState == 1 && process.FadeRate == 0 && process.FadeDisplayedAlpha == 0 && process.MovementEnabled && process.ControlLock == 0 && process.Dialogue is not { Completed: false } && (nativeControlFlags & 2) == 0;

    private void EnsurePlayerMenu()
    {
        if (playerMenu is not null)
        {
            return;
        }
        playerMenu = new(ReadPlayerMenuData, ApplyPlayerMenuAction);
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
            Title = "Menu",
            Help = "Select a command.",
            Layout = MenuLayout.Main,
            Entries = _ =>
            [
                new("items", "Items", "View the items carried in this world.", "ff7-items"),
                new("status", "Status", "View the current party.", "ff7-status"),
                new("config", "Config", "Shared controls, graphics, audio and interface preferences.", "config"),
                new("save", "Save", "Save the current continuation.", Action: new("ff7-save"), Enabled: CanSave),
                new("load", "Load", "Load a saved continuation.", Action: new("ff7-load"), Enabled: ContinueRequested is not null),
                new("world", "Switch world: FFVIII", "Choose associations before returning to the retained world.", Action: new("ff7-world"), Enabled: CanSave && envelope.Ff8Payload is { Length: > 0 }),
                new("materia", "Materia", "Equip and link materia in the current party.", "ff7-materia"),
                new("close", "Return", "Return to the game.", Action: new("ff7-close"))
            ]
        });
        playerMenu.Register(new() { Id = "ff7-items", Title = "Items", Help = "Items carried in this world.", Entries = menu => menu.Data.Items.Select(item => new MenuEntry(item.Id, $"{item.Name}   {item.Quantity}", item.Description)).ToArray(), Details = menu => [menu.SelectedEntry?.Help ?? ""] });
        playerMenu.Register(new() { Id = "ff7-status", Title = "Status", Help = "Current party.", Entries = menu => menu.Data.Characters.Where(character => character.InParty).Select(character => new MenuEntry(character.Id, $"{character.Name}   Lv {character.Level}   HP {character.CurrentHp}", $"MP {character.Stats["MP"]}   EXP {character.Experience}")).ToArray(), Details = menu => [menu.SelectedEntry?.Help ?? ""] });
        RegisterMateriaMenus(playerMenu);
        playerMenu.Register(new()
        {
            Id = "config",
            Title = "Config",
            Help = "Shared presentation and control preferences.",
            Entries = menu => menu.ConfigEntries().Where(entry => entry.Id is "config-graphics" or "config-audio" or "config-interface" or "config-controls").Append(new("config-speed", "Speed", "Movement and fast-forward preferences.", "config-speed")).ToArray()
        });
    }

    private MenuData ReadPlayerMenuData()
    {
        Ff7Session session = Session ?? throw new InvalidOperationException("FFVII party menus require an active continuation.");
        menuText ??= Ff7KernelTextResources.Load(installation);
        MenuData data = Ff7PlayerMenuProjection.Create(session.Progress, session.Progress.Party, characterIdentity, menuText.ItemNames, menuText.ItemDescriptions, session.World is null ? session.Field.Name : "World map", TimeSpan.FromSeconds(session.Clock.ElapsedSeconds), CanSave, PlayerMenuPortrait);
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
        if (ApplyMateriaMenuAction(action))
        {
            return;
        }
        if (action.Kind == "config")
        {
            if (!PlayerPreferenceKeys.Contains(action.SubjectId))
            {
                throw new NotSupportedException("This FFVII menu exposes only shared presentation and speed preferences.");
            }
            UiPreferences current = PresentationPreferences;
            UiPreferences updated = current.WithSetting(action.SubjectId, action.Value);
            if (updated != current)
            {
                (setDialoguePreferences ?? throw new InvalidOperationException("Shared preferences are read-only in this host."))(updated);
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
        if (sharedInput.GameMenuRequested && playerMenu?.IsCapturingControls != true)
        {
            if (IsPlayerMenuOpen)
            {
                AdvancePlayerMenuClock(time.ElapsedGameTime);
                ClosePlayerMenu();
                RefreshPlayerMenuPresentation();
                return true;
            }
            if (!CanOpenPlayerMenu)
            {
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
        if (!IsPlayerMenuOpen)
        {
            return false;
        }
        AdvancePlayerMenuClock(time.ElapsedGameTime);
        UpdateSharedMenuInput(playerMenu!, keys, pad, ref menuPointerPressed);
        if (!playerMenu!.IsOpen)
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
        pendingPressedKeys = 0;
        moveHorizontal = 0;
        moveVertical = 0;
        sharedInput.SuppressHeldUiInput();
        SetAudioPaused(focusPaused || userPaused || debugMenu || error is not null);
    }

    private void DrawPlayerMenu(Rectangle bounds)
    {
        if (!IsPlayerMenuOpen)
        {
            return;
        }
        playerMenu!.Presentation = PresentationPreferences.MenuPresentation;
        playerMenu.PrepareViewport(bounds.Width, bounds.Height);
        UiView view = playerMenu.BuildView();
        view.Resize(bounds.Width, bounds.Height);
        view.Draw(dialogueCanvas!, SharedTheme());
    }

    private void DisposePlayerMenu()
    {
        playerMenu?.Close();
        playerMenu = null;
        playerMenuClockChanged = false;
        foreach (int id in menuPortraits.Values)
        {
            dialogueCanvas?.UnregisterHostImage(id);
        }
        menuPortraits.Clear();
    }
}
