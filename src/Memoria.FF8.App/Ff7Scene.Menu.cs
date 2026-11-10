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

    internal bool IsPlayerMenuOpen => playerMenu?.IsOpen == true;

    private bool CanOpenPlayerMenu => Session is { HasPendingTick: false } session && (session.World?.CanSave ?? (!session.Host.Process.MenuDisabled && !session.Host.Process.PlayerLocked && session.Host.Process.GlobalCommand == Ff7GlobalCommand.None && session.Host.Process.Transition is null && session.Host.Process.Movie is not { Playing: true } && session.Host.Process.Dialogues.All(dialogue => dialogue.Completed) && (session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 31) & 2) == 0));

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
                new("close", "Return", "Return to the game.", Action: new("ff7-close"))
            ]
        });
        playerMenu.Register(new() { Id = "ff7-items", Title = "Items", Help = "Items carried in this world.", Entries = menu => menu.Data.Items.Select(item => new MenuEntry(item.Id, $"{item.Name}   {item.Quantity}", item.Description)).ToArray(), Details = menu => [menu.SelectedEntry?.Help ?? ""] });
        playerMenu.Register(new() { Id = "ff7-status", Title = "Status", Help = "Current party.", Entries = menu => menu.Data.Characters.Where(character => character.InParty).Select(character => new MenuEntry(character.Id, $"{character.Name}   Lv {character.Level}   HP {character.CurrentHp}", $"MP {character.Stats["MP"]}   EXP {character.Experience}")).ToArray(), Details = menu => [menu.SelectedEntry?.Help ?? ""] });
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
        MenuData data = new() { Location = session.World is null ? session.Field.Name : "World map", Currency = checked((int)session.Progress.Gil), PlayTime = TimeSpan.FromSeconds(session.Clock.ElapsedSeconds), SaveAvailable = CanSave };
        data.SupportedActions.UnionWith(["config", "ff7-save", "ff7-load", "ff7-world", "ff7-close"]);
        HashSet<int> party = Enumerable.Range(9, 3).Select(address => (int)(session.World?.ReadPersistentByte(256 + address) ?? session.Machine.Variables.ReadByte(FieldBank.Persistent1Byte, (byte)address))).ToHashSet();
        foreach (Ff7CharacterState character in session.Progress.Characters)
        {
            Ff7CharacterDisplayIdentity identity = characterIdentity.Resolve(character.Id, character.Name);
            MenuCharacter display = new() { Id = "ff7-character/" + character.Id, Name = identity.Name, InParty = party.Contains(character.Id), Level = character.Level, CurrentHp = character.HitPoints, HpDisplay = character.HitPoints.ToString(System.Globalization.CultureInfo.InvariantCulture), Experience = checked((int)character.Experience), Portrait = PlayerMenuPortrait(identity) };
            display.Stats["MP"] = character.MagicPoints;
            data.Characters.Add(display);
        }
        foreach (Ff7InventorySlot item in session.Progress.Inventory.Where(item => item.Quantity > 0))
        {
            if (!menuText.ItemNames.TryGetValue(item.ItemId, out string? name) || string.IsNullOrWhiteSpace(name))
            {
                throw new NotSupportedException("The saved FFVII item requires its original menu text entry.");
            }
            data.Items.Add(new() { Id = "ff7-item/" + item.ItemId, Name = name, Quantity = item.Quantity, Description = menuText.ItemDescriptions[item.ItemId] });
        }
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
        if (action.Kind == "config")
        {
            setDialoguePreferences?.Invoke(PresentationPreferences.WithSetting(action.SubjectId, action.Value));
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

    private bool UpdatePlayerMenu(KeyboardState keys, GamePadState pad)
    {
        if (sharedInput.GameMenuRequested && playerMenu?.IsCapturingControls != true)
        {
            if (IsPlayerMenuOpen)
            {
                ClosePlayerMenu();
                return true;
            }
            if (!CanOpenPlayerMenu)
            {
                return false;
            }
            EnsurePlayerMenu();
            playerMenu!.Presentation = PresentationPreferences.MenuPresentation;
            playerMenu.Open();
            pendingPressedKeys = 0;
            sharedInput.SuppressHeldUiInput();
            menuPointerPressed = Mouse.GetState().LeftButton == ButtonState.Pressed;
            SetAudioPaused(true);
            return true;
        }
        if (!IsPlayerMenuOpen)
        {
            return false;
        }
        if (!playerMenu!.UpdateControlsCapture(keys, pad, !focusPaused))
        {
            if (semanticInput.MenuVertical != 0)
            {
                playerMenu.Navigate(semanticInput.MenuVertical < 0 ? MenuInput.Up : MenuInput.Down);
            }
            if (semanticInput.MenuHorizontal != 0)
            {
                playerMenu.Navigate(semanticInput.MenuHorizontal < 0 ? MenuInput.Left : MenuInput.Right);
            }
            if (semanticInput.MenuPage != 0)
            {
                playerMenu.Navigate(semanticInput.MenuPage < 0 ? MenuInput.PageUp : MenuInput.PageDown);
            }
            if (semanticInput.Confirm)
            {
                playerMenu.Navigate(MenuInput.Confirm);
            }
            if (semanticInput.Cancel)
            {
                playerMenu.Navigate(MenuInput.Cancel);
            }
            MouseState mouse = Mouse.GetState();
            bool pressed = mouse.LeftButton == ButtonState.Pressed;
            if (pressed && !menuPointerPressed && playerMenu.View is { } view)
            {
                view.Resize(device.Viewport.Width, device.Viewport.Height);
                view.Pointer(mouse.X, mouse.Y, true);
            }
            menuPointerPressed = pressed;
        }
        if (!playerMenu.IsOpen)
        {
            ClosePlayerMenu();
        }
        UpdateAudio();
        return true;
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
        playerMenu!.PrepareViewport(bounds.Width, bounds.Height);
        UiView view = playerMenu.BuildView();
        view.Resize(bounds.Width, bounds.Height);
        view.Draw(dialogueCanvas!, SharedTheme());
    }

    private void DisposePlayerMenu()
    {
        playerMenu?.Close();
        playerMenu = null;
        foreach (int id in menuPortraits.Values)
        {
            dialogueCanvas?.UnregisterHostImage(id);
        }
        menuPortraits.Clear();
    }
}
