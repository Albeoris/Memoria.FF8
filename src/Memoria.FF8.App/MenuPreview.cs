using SkiaSharp;
using System.Text.Json;
using Memoria.FF8.GameUi;
using Memoria.FF8.Events;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8;

/// <summary>GPU-independent diagnostics for the same menu and laboratory views used by the host.</summary>
internal static class MenuPreview
{
    public static int Run(string[] args)
    {
        string screen = "debug-room";
        string? output = null;
        string gameDirectory = LaunchOptions.ConfiguredGameDirectory;
        int width = 960;
        int height = 540;
        bool resolutionSpecified = false;
        MenuPresentation presentation = MenuPresentation.Vector;
        UiTheme theme;
        bool expanded = false;
        int textScale = 100;
        int fontSize = 24;
        double previewTime = 0;
        string? statePath = null;
        string? characterId = null;
        int nameSubject = 0;
        int namePage = 0;
        string? inputs = null;
        for (int index = 0; index < args.Length; index++)
        {
            string argument = args[index];
            if (argument == "--expanded")
            {
                expanded = true;
                continue;
            }
            if (argument is not ("--ui" or "--screen" or "--output" or "--resolution" or "--game-dir" or "--state" or "--character" or "--name-subject" or "--name-page" or "--inputs" or "--text-scale" or "--font-size" or "--time") || ++index == args.Length)
            {
                throw new ArgumentException($"Unknown or incomplete menu preview argument '{argument}'.");
            }
            string value = args[index];
            switch (argument)
            {
                case "--ui":
                    presentation = value.ToLowerInvariant() switch
                    {
                        "vector" => MenuPresentation.Vector,
                        "classic" => MenuPresentation.Classic,
                        _ => throw new ArgumentException("Menu preview --ui requires vector or classic.")
                    };
                    break;
                case "--text-scale":
                    textScale = int.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case "--font-size":
                    fontSize = int.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case "--time":
                    previewTime = double.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case "--inputs":
                    inputs = value;
                    break;
                case "--name-subject":
                    nameSubject = int.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case "--name-page":
                    namePage = int.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case "--game-dir":
                    gameDirectory = Path.GetFullPath(value);
                    break;
                case "--state":
                    statePath = Path.GetFullPath(value);
                    break;
                case "--character":
                    characterId = value;
                    break;
                case "--screen":
                    screen = value;
                    break;
                case "--output":
                    output = Path.GetFullPath(value);
                    break;
                case "--resolution":
                    resolutionSpecified = true;
                    string[] size = value.Split('x', 'X');
                    if (size.Length != 2)
                    {
                        throw new ArgumentException("Menu preview resolution requires WxH.");
                    }
                    width = int.Parse(size[0], System.Globalization.CultureInfo.InvariantCulture);
                    height = int.Parse(size[1], System.Globalization.CultureInfo.InvariantCulture);
                    break;
            }
        }
        if (!resolutionSpecified)
        {
            height = presentation == MenuPresentation.Vector ? 540 : 672;
        }
        theme = presentation == MenuPresentation.Vector ? new VectorUiTheme() : new UiTheme();
        if (output is null || width is < 320 or > 4096 || height is < 240 or > 4096 || textScale is not (100 or 125 or 150) || fontSize is < 6 or > 48 || !double.IsFinite(previewTime) || previewTime is < 0 or > 86400)
        {
            throw new ArgumentException("Menu preview requires --output <PNG>, optional --ui <vector|classic>, --screen <screen-id|battle-debug>, --state <standalone.json>, --character <menu-id>, --expanded, --resolution WxH, --text-scale <100|125|150>, --font-size <6..48> and --time <0..86400 seconds>.");
        }
        UiView view;
        using GameWorld? tutorialWorld = screen == "native-tutorial" && statePath is not null ? new(new GameSession(GameSession.Load(statePath)), gameDirectory) : null;
        using var mods = new Memoria.FF8.Mods.ModRuntime(watch: false, additionalBaseDirectories: [gameDirectory]);
        mods.PreparePending(true);
        mods.CommitPending();
        if (screen.StartsWith("player-save", StringComparison.Ordinal))
        {
            view = BuildPlayerSavePreview(screen, statePath, fontSize / 24f * textScale / 100f);
        }
        else if (screen == "party-selection")
        {
            NativePartyMenuState menu = new(0, string.Empty, string.Empty, 0, 0, 0, 0, 0, NativePartyMenuPhase.Active, 2, [0, 4, 255], 0x3F, 1, 1, false, NativeFieldMenuKind.PartySelection, 2, 0, null, null, null, [1, 2, 3, 5, 255, 255, 255, 255], expanded ? 2 : null);
            MenuData data;
            if (statePath is not null)
            {
                GameState state = GameSession.Load(statePath);
                menu = state.EventState?.GetProperty("PartyMenu").Deserialize<NativePartyMenuState>() ?? throw new ArgumentException("Party selection preview requires an active party menu.");
                if (menu is not { Kind: NativeFieldMenuKind.PartySelection, TemporaryCombatParty.Length: 3, TemporaryReserveParty.Length: 8 })
                {
                    throw new ArgumentException("Party selection preview requires initialized combat and reserve panes.");
                }
                using GameAssets assets = new(gameDirectory);
                IReadOnlySet<int> visible = menu.TemporaryCombatParty!.Concat(menu.TemporaryReserveParty!).Where(row => row != 255).Select(row => (int)row).ToHashSet();
                data = LiveMenuData.Build(state, catalog: LiveMenuCatalog.ReadInstalled(assets), visibleCharacterRows: visible);
            }
            else
            {
                data = MenuDemoData.Create();
                foreach (MenuCharacter character in data.Characters)
                {
                    character.Id = $"character-{character.NativeId}";
                }
            }
            int candidate = 0;
            if (inputs is not null)
            {
                foreach (string input in inputs.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    MenuInput direction = Enum.Parse<MenuInput>(input, ignoreCase: true);
                    candidate = PartySelectionView.Navigate(menu, candidate, direction == MenuInput.Left ? -1 : direction == MenuInput.Right ? 1 : 0, direction == MenuInput.Up ? -1 : direction == MenuInput.Down ? 1 : 0);
                }
            }
            view = PartySelectionView.Build(menu, data, candidate, textScale: fontSize / 24f * textScale / 100f);
        }
        else if (screen == "native-tutorial")
        {
            if (tutorialWorld is null)
            {
                throw new ArgumentException("Native tutorial preview requires a standalone --state with a pending tutorial.");
            }
            tutorialWorld.EnsureField();
            NativeTutorialPlayer tutorial = tutorialWorld.Tutorial ?? throw new InvalidOperationException("No native tutorial is active in this snapshot.");
            tutorial.RefreshPresentation();
            tutorial.Menu.Presentation = presentation;
            view = tutorial.Menu.BuildView();
            theme = tutorial.Menu.CreateTheme();
        }
        else if (screen == "triple-triad")
        {
            if (statePath is null)
            {
                throw new ArgumentException("Triple Triad preview requires a standalone --state with a pending CARDGAME.");
            }
            GameSession session = new(GameSession.Load(statePath));
            using GameWorld world = new(session, gameDirectory);
            world.EnsureField();
            view = world.EnsureTripleTriad()?.BuildView() ?? throw new InvalidOperationException("No CARDGAME is active in this snapshot.");
        }
        else if (screen == "battle-debug")
        {
            MenuData fixture = MenuDemoData.Create();
            BattleCombatantDefinition[] party = fixture.Characters.Where(character => character.InParty).Select((character, index) => new BattleCombatantDefinition { Id = $"party-{index}", Name = character.Name, IsPlayer = true, CurrentHp = character.CurrentHp!.Value, MaximumHp = character.MaximumHp!.Value }).ToArray();
            BattleDebugController editor = new(BattleDebugDefaults.Create(party, null, 0), new BattleDebugCatalog { Players = party, Enemies = [BattleDebugDefaults.Dummy] }, BattleAtbMode.Normal, (_, _) => { }, () => { });
            if (expanded)
            {
                int row = editor.Rows.Select((entry, index) => (entry, index)).Single(pair => pair.entry.Label == "Enemy 1").index;
                editor.Select(row, true);
                editor.Navigate(MenuInput.Down);
            }
            view = editor.BuildView();
        }
        else
        {
            UiPreferences preferences = new() { TextScale = textScale, UiFontSize = fontSize, MenuPresentation = presentation };
            MenuData? liveData = null;
            GameState? previewState = null;
            if (statePath is not null)
            {
                previewState = GameSession.Load(statePath);
                using GameAssets assets = new(gameDirectory);
                liveData = LiveMenuData.Build(previewState, catalog: LiveMenuCatalog.ReadInstalled(assets));
            }
            MenuController menus = liveData is null ? new() : new(() => liveData);
            menus.Presentation = presentation;
            if (screen.StartsWith("tutorial", StringComparison.Ordinal))
            {
                using GameAssets assets = new(gameDirectory);
                IReadOnlySet<int> previewUnlocks = previewState is null ? Enumerable.Range(62, 9).ToHashSet() : previewState.EventState is JsonElement eventState && eventState.TryGetProperty("Scene", out JsonElement scene) && scene.TryGetProperty("UnlockedTutorials", out JsonElement unlocked) ? unlocked.EnumerateArray().Select(value => value.GetInt32()).ToHashSet() : new HashSet<int>();
                menus.ConnectNativeTutorials(assets.ReadNativeTutorialCatalog(), assets.ReadNativeTutorialBrowserCatalog(), () => previewUnlocks, _ => throw new NotSupportedException("Start a native tutorial through its saved request for playback preview."));
                menus.ConnectNativeTutorialExam(assets.ReadNativeTutorialExamCatalog(), () => new(0, previewState is null ? 0 : previewState.SeeD is { SalaryUiEnabled: true } seeD ? seeD.RankPoints / 100 : -1, liveData?.Characters.FirstOrDefault()?.Level ?? 1, previewState?.CharacterVitals?.DeepCopy()));
                if (screen is "tutorial-native-information" or "tutorial-native-test" or "tutorial-native-review")
                {
                    menus.Execute(new MenuAction("native-exam-open", "information"));
                }
            }
            menus.ConnectMods(mods);
            menus.ConnectPreferences(() => preferences, action => preferences = preferences.WithSetting(action.SubjectId, action.Value));
            Dictionary<int, string> names = Enumerable.Range(0, GameNames.BokoSubject + 1).Where(GameNames.IsValidSubject).ToDictionary(subject => subject, GameNames.DefaultName);
            menus.ConnectNaming(() => names.Select(pair => new DebugNameTarget(pair.Key, pair.Value, pair.Key is >= 16 and <= 31 ? "GF" : "Character")).ToArray(), (subject, name) => names[subject] = name);
            menus.BattleDebugRequested += () => { };
            menus.Open(screen, liveData is null);
            menus.Data.Settings["UI presentation"] = presentation.ToString();
            menus.Data.Settings["UI font"] = preferences.UiFontFamily;
            menus.Data.Settings["UI font size"] = fontSize.ToString(System.Globalization.CultureInfo.InvariantCulture);
            menus.Data.Settings["Dialogue font"] = preferences.DialogueFontFamily;
            menus.Data.Settings["Dialogue font size"] = preferences.DialogueFontSize.ToString(System.Globalization.CultureInfo.InvariantCulture);
            menus.Data.Settings["Text scale"] = textScale.ToString(System.Globalization.CultureInfo.InvariantCulture) + "%";
            if (characterId is not null)
            {
                menus.Execute(new MenuAction("select-character", characterId));
            }
            if (screen == "main" && expanded)
            {
                menus.Navigate(MenuInput.Confirm);
            }
            if (screen == "debug-name-input")
            {
                menus.Execute(new MenuAction("name-edit", nameSubject.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            }
            if (screen == "name-input")
            {
                menus.OpenNamePrompt(nameSubject, GameNames.DefaultName(nameSubject), _ => { });
            }
            if (menus.IsEditingName)
            {
                for (int page = 0; page < namePage; page++)
                {
                    menus.Navigate(MenuInput.PageRight);
                }
            }
            if (inputs is not null)
            {
                foreach (string input in inputs.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    menus.Navigate(Enum.Parse<MenuInput>(input, ignoreCase: true));
                }
            }
            menus.Presentation = preferences.MenuPresentation;
            view = menus.BuildView();
            theme = menus.CreateTheme();
        }
        using SKBitmap bitmap = new(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using (BattlePreviewCanvas canvas = new(bitmap, gameDirectory))
        {
            view.Resize(width, height);
            canvas.BeginFrame(TimeSpan.Zero);
            view.Draw(canvas, theme);
            if (previewTime > 0)
            {
                canvas.Fill(new UiRect(0, 0, width, height), new UiColor(0, 0, 0));
                canvas.BeginFrame(TimeSpan.FromSeconds(previewTime));
                view.Draw(canvas, theme);
            }
        }
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        bitmap.SavePng(output);
        Console.WriteLine($"Menu {(statePath is null ? "fixture" : "standalone-save")} preview {screen}, {width}x{height}: {output}");
        return 0;
    }

    private static UiView BuildPlayerSavePreview(string screen, string? statePath, float textScale)
    {
        string directory = Path.Combine(Path.GetTempPath(), "Memoria.FF8-save-preview-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            PlayerSaveStore store = new(Path.Combine(directory, "checkpoint.json"), Path.Combine(directory, "memoria"), _ => "Balamb Garden", directory);
            GameState state = statePath is null ? new GameState { Scene = SceneKind.Field, FieldId = 165, Playtime = new NativePlaytimeState(3723, 0, 0), ExamTimer = NativeExamTimerState.FromColdProcess() } : GameSession.Load(statePath);
            store.Save(1, new GameSession(state));
            if (screen is "player-save-quick-load" or "player-save-mixed")
            {
                for (int index = 0; index < 7; index++)
                {
                    store.Save(0, new GameSession(statePath is null ? state with { Playtime = new NativePlaytimeState((uint)(3723 + index * 60), 0, 0) } : state));
                }
            }
            PlayerSaveMenu menu = new(store);
            menu.Open(PlayerSaveMenuMode.Load);
            menu.SelectSlot(1);
            switch (screen)
            {
                case "player-save":
                    break;
                case "player-save-manual":
                    menu.Open(PlayerSaveMenuMode.Save);
                    break;
                case "player-save-quick-load":
                    menu.Open(PlayerSaveMenuMode.QuickLoad);
                    break;
                case "player-save-quick-save":
                    menu.Open(PlayerSaveMenuMode.QuickSave);
                    break;
                case "player-save-mixed":
                    store.Save(2, new GameSession(statePath is null ? state with { Playtime = new NativePlaytimeState(4200, 0, 0) } : state));
                    menu = new PlayerSaveMenu(store);
                    menu.Open(PlayerSaveMenuMode.Load);
                    break;
                case "player-save-original-i":
                    menu.SelectBank(1);
                    break;
                case "player-save-original-ii":
                    menu.SelectBank(2);
                    break;
                case "player-save-transfer":
                    menu.Update(new InputFrame(0, 0, false, false, false, false, CardChallenge: true), _ => { }, _ => { });
                    break;
                default:
                    throw new ArgumentException($"Unknown player save preview '{screen}'.");
            }
            KeyboardInput controls = new();
            return PlayerSaveView.Build(menu, $"{controls.BindingPrompt(ControlAction.Card)}: Transfer", $"{controls.BindingPrompt(ControlAction.MoveLeft)} / {controls.BindingPrompt(ControlAction.MoveRight)}: Bank    {controls.BindingPrompt(ControlAction.UiPageUp)} / {controls.BindingPrompt(ControlAction.UiPageDown)}: Page", textScale: textScale);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
