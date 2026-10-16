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
        for (int index = 0; index < args.Length; index++)
        {
            string argument = args[index];
            if (argument == "--expanded")
            {
                expanded = true;
                continue;
            }
            if (argument is not ("--screen" or "--output" or "--resolution" or "--game-dir" or "--state" or "--character") || ++index == args.Length)
            {
                throw new ArgumentException($"Unknown or incomplete menu preview argument '{argument}'.");
            }
            string value = args[index];
            switch (argument)
            {
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
        using var mods = new Memoria.FF8.Mods.ModRuntime(watch: false);
        mods.PreparePending(true);
        mods.CommitPending();
        if (screen == "battle-debug")
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
            menus.ConnectMods(mods);
            menus.ConnectPreferences(() => preferences, action => preferences = preferences.WithSetting(action.SubjectId, action.Value));
            Dictionary<int, string> names = Enumerable.Range(0, 43).Where(GameNames.IsValidSubject).ToDictionary(subject => subject, GameNames.DefaultName);
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
                menus.Execute(new MenuAction("name-edit", "0"));
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
