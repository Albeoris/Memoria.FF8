using System.Drawing;
using System.Drawing.Imaging;
using Memoria.FF8.GameUi;
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
        string gameDirectory = LaunchOptions.DefaultGameDirectory;
        int width = 960;
        int height = 672;
        bool expanded = false;
        string? statePath = null;
        string? characterId = null;
        int nameSubject = 0;
        int namePage = 0;
        for (int index = 0; index < args.Length; index++)
        {
            string argument = args[index];
            if (argument == "--expanded")
            {
                expanded = true;
                continue;
            }
            if (argument is not ("--screen" or "--output" or "--resolution" or "--game-dir" or "--state" or "--character" or "--name-subject" or "--name-page") || ++index == args.Length)
            {
                throw new ArgumentException($"Unknown or incomplete menu preview argument '{argument}'.");
            }
            string value = args[index];
            switch (argument)
            {
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
        if (output is null || width is < 320 or > 4096 || height is < 240 or > 4096)
        {
            throw new ArgumentException("Menu preview requires --output <PNG>, optional --screen <screen-id|battle-debug>, --state <standalone.json>, --character <menu-id>, --expanded and --resolution WxH.");
        }
        UiView view;
        using GameWorld? tutorialWorld = screen == "native-tutorial" && statePath is not null ? new(new GameSession(GameSession.Load(statePath)), gameDirectory) : null;
        using var mods = new Memoria.FF8.Mods.ModRuntime(watch: false);
        mods.PreparePending(true);
        mods.CommitPending();
        if (screen == "native-tutorial")
        {
            if (tutorialWorld is null)
            {
                throw new ArgumentException("Native tutorial preview requires a standalone --state with a pending tutorial.");
            }
            tutorialWorld.EnsureField();
            NativeTutorialPlayer tutorial = tutorialWorld.Tutorial ?? throw new InvalidOperationException("No native tutorial is active in this snapshot.");
            tutorial.RefreshPresentation();
            view = tutorial.Menu.BuildView();
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
            UiPreferences preferences = new();
            MenuData? liveData = null;
            if (statePath is not null)
            {
                GameState state = GameSession.Load(statePath);
                using GameAssets assets = new(gameDirectory);
                liveData = LiveMenuData.Build(state, catalog: LiveMenuCatalog.ReadInstalled(assets));
            }
            MenuController menus = liveData is null ? new() : new(() => liveData);
            if (screen.StartsWith("tutorial", StringComparison.Ordinal))
            {
                using GameAssets assets = new(gameDirectory);
                IReadOnlySet<int> previewUnlocks = statePath is null ? Enumerable.Range(62, 9).ToHashSet() : new HashSet<int>();
                menus.ConnectNativeTutorials(assets.ReadNativeTutorialCatalog(), assets.ReadNativeTutorialBrowserCatalog(), () => previewUnlocks, _ => throw new NotSupportedException("Start a native tutorial through its saved request for playback preview."));
            }
            menus.ConnectMods(mods);
            menus.ConnectPreferences(() => preferences, action => preferences = preferences.WithSetting(action.SubjectId, action.Value));
            Dictionary<int, string> names = Enumerable.Range(0, GameNames.BokoSubject + 1).Where(GameNames.IsValidSubject).ToDictionary(subject => subject, GameNames.DefaultName);
            menus.ConnectNaming(() => names.Select(pair => new DebugNameTarget(pair.Key, pair.Value, pair.Key is >= 16 and <= 31 ? "GF" : "Character")).ToArray(), (subject, name) => names[subject] = name);
            menus.BattleDebugRequested += () => { };
            menus.Open(screen, liveData is null);
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
            view = menus.BuildView();
        }
        using Bitmap bitmap = new(width, height, PixelFormat.Format32bppArgb);
        using (BattlePreviewCanvas canvas = new(bitmap, gameDirectory))
        {
            view.Resize(width, height);
            view.Draw(canvas, new UiTheme());
        }
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        bitmap.Save(output, ImageFormat.Png);
        Console.WriteLine($"Menu {(statePath is null ? "fixture" : "standalone-save")} preview {screen}, {width}x{height}: {output}");
        return 0;
    }
}
