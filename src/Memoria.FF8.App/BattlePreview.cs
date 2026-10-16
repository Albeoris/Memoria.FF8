using System.Drawing;
using System.Drawing.Imaging;
using System.Text.Json;
using System.Text.Json.Serialization;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8;

internal sealed record BattlePreviewScenario
{
    public BattleDefinition Definition { get; init; } = new();
    public BattleAtbMode Mode { get; init; } = BattleAtbMode.Normal;
    public int TextScale { get; init; } = 100;
    public bool UseEncounter { get; init; }
    public MenuInput[] HudInputs { get; init; } = [];
}

/// <summary>Renders actual battle assets and the shared live HUD without initializing a graphics device.</summary>
internal static class BattlePreview
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } };

    public static int Run(string[] args)
    {
        string? scenarioPath = null;
        string? snapshotPath = null;
        string? outputPath = null;
        string gameDirectory = LaunchOptions.ConfiguredGameDirectory;
        int width = 960;
        int height = 672;
        int frames = 0;
        bool advanceTurn = false;
        bool selectTarget = false;
        for (int index = 0; index < args.Length; index++)
        {
            string argument = args[index];
            if (argument == "--advance-turn")
            {
                advanceTurn = true;
                continue;
            }
            if (argument == "--select-target")
            {
                selectTarget = true;
                continue;
            }
            if (argument is not ("--scenario" or "--snapshot" or "--output" or "--game-dir" or "--resolution" or "--frames"))
            {
                throw new ArgumentException($"Unknown battle preview argument '{argument}'.");
            }
            if (++index == args.Length)
            {
                throw new ArgumentException($"Missing value for {argument}.");
            }
            string value = args[index];
            switch (argument)
            {
                case "--scenario":
                    scenarioPath = Path.GetFullPath(value);
                    break;
                case "--snapshot":
                    snapshotPath = Path.GetFullPath(value);
                    break;
                case "--output":
                    outputPath = Path.GetFullPath(value);
                    break;
                case "--game-dir":
                    gameDirectory = Path.GetFullPath(value);
                    break;
                case "--frames":
                    frames = int.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case "--resolution":
                    string[] dimensions = value.Split('x', 'X');
                    if (dimensions.Length != 2)
                    {
                        throw new ArgumentException("Battle preview resolution requires WxH.");
                    }
                    width = int.Parse(dimensions[0], System.Globalization.CultureInfo.InvariantCulture);
                    height = int.Parse(dimensions[1], System.Globalization.CultureInfo.InvariantCulture);
                    break;
            }
        }
        if ((scenarioPath is null) == (snapshotPath is null) || outputPath is null || frames is < 0 or > 30_000 || width is < 320 or > 4096 || height is < 240 or > 4096)
        {
            throw new ArgumentException("Battle preview requires --scenario <JSON> or --snapshot <JSON>, --output <PNG>, and optional --resolution WxH, --frames 0..30000, --advance-turn, --select-target, --game-dir <directory>.");
        }
        string sourcePath = scenarioPath ?? snapshotPath!;
        if (string.Equals(sourcePath, outputPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Battle preview output must differ from its source JSON.");
        }
        BattlePreviewScenario? scenario = scenarioPath is null ? null : JsonSerializer.Deserialize<BattlePreviewScenario>(File.ReadAllText(scenarioPath), JsonOptions) ?? throw new InvalidDataException("Empty battle preview scenario.");
        BattleSession session = scenario is null ? BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllText(snapshotPath!), JsonOptions) ?? throw new InvalidDataException("Empty battle snapshot.")) : new BattleSession(scenario.Definition, scenario.Mode);
        if (scenario is { TextScale: not (100 or 125 or 150) })
        {
            throw new InvalidDataException("Battle preview text scale requires 100, 125 or 150.");
        }
        if (advanceTurn)
        {
            session.AdvanceTurn();
        }
        for (int frame = 0; frame < frames; frame++)
        {
            session.Tick();
        }
        BattleDefinition definition = session.Capture().Definition;
        using BattleAssets assets = new(gameDirectory);
        BattleScenePresentation scene = new(assets, definition, scenario?.UseEncounter ?? (definition.EncounterId is not null));
        using Bitmap output = new(width, height, PixelFormat.Format32bppArgb);
        using (BattlePreviewCanvas canvas = new(output, gameDirectory))
        {
            PixelBounds viewport = ViewportLayout.Fit(320, 240, width, height);
            canvas.DrawRgba(scene.Render(session, viewport.Width, viewport.Height), viewport.Width, viewport.Height, new UiRect(viewport.X, viewport.Y, viewport.Width, viewport.Height));
            scene.DrawEffects(session, canvas, new UiRect(viewport.X, viewport.Y, viewport.Width, viewport.Height));
            BattleHudController hud = new(session, session.QueueAttack, () => { }, () => session.AdvanceTurn())
            {
                TextScale = (scenario?.TextScale ?? 100) / 100f
            };
            if (selectTarget)
            {
                hud.Navigate(MenuInput.Confirm);
            }
            foreach (MenuInput input in scenario?.HudInputs ?? [])
            {
                hud.Navigate(input);
            }
            UiView view = hud.BuildView();
            view.Resize(width, height);
            view.Draw(canvas, new UiTheme());
        }
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        output.Save(outputPath, ImageFormat.Png);
        Console.WriteLine($"Battle preview {width}x{height}, frame {session.Frame}, {session.Mode}, {session.Outcome}: {outputPath}");
        return 0;
    }
}
