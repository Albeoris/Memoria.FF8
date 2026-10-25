namespace Memoria.FF8.Runtime;

public sealed record LaunchOptions
{
    public static string ConfiguredGameDirectory => Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? Environment.GetEnvironmentVariable("FF8_GAME_DIRECTORY") ?? string.Empty;
    public string GameDirectory { get; init; } = ConfiguredGameDirectory;
    public string? ModsDirectory { get; init; }
    public string? StatePath { get; init; }
    public string SavePath { get; init; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Memoria.FF8", "checkpoint.json");
    public string? MediaToolsDirectory { get; init; }
    public GameState InitialState { get; init; } = new();
    public double? ExitAfterSeconds { get; init; }
    public bool ValidateOnly { get; init; }
    public string? CaptureFramePath { get; init; }
    public double CaptureAfterSeconds { get; init; } = 2;
    public int? ExitAfterFrames { get; init; }
    public int Width { get; init; } = 1024;
    public int Height { get; init; } = 768;
    public bool ResolutionSpecified { get; init; }
    public string? ControlDirectory { get; init; }
    public string? UpdateControlDirectory { get; init; }
    public bool ControlStep { get; init; }
    public bool MuteAudio { get; init; }
    public bool AudioEnabled => !MuteAudio && (ControlDirectory is null || BattleTestPath is not null) && !System.Diagnostics.Debugger.IsAttached;
    public int WorldRenderLimit { get; init; } = 2048;
    public bool SoftwareRenderer { get; init; }
    public string? DebugUiScreen { get; init; }
    public string? BattleTestPath { get; init; }
    public string? SettingsDirectory { get; init; } = Environment.GetEnvironmentVariable("MEMORIA_FF8_SETTINGS_DIRECTORY");
    public bool AchievementsLocalOnly { get; init; }
    public bool IsAchievementToolSession => ValidateOnly || ControlDirectory is not null || CaptureFramePath is not null || ExitAfterSeconds is not null || ExitAfterFrames is not null || DebugUiScreen is not null || BattleTestPath is not null;

    public static LaunchOptions Parse(string[] args)
    {
        var options = new LaunchOptions();
        for (int index = 0; index < args.Length; index++)
        {
            string argument = args[index];
            string Value()
            {
                if (++index >= args.Length)
                {
                    throw new ArgumentException($"Missing value for {argument}.");
                }
                return args[index];
            }
            options = argument switch
            {
                "--game-dir" => options with { GameDirectory = Path.GetFullPath(Value()) },
                "--achievements-local-only" => options with { AchievementsLocalOnly = true },
                "--mods-dir" => options with { ModsDirectory = Path.GetFullPath(Value()) },
                "--state" => options with { StatePath = Path.GetFullPath(Value()) },
                "--save-state" => options with { SavePath = Path.GetFullPath(Value()) },
                "--media-tools" => options with { MediaToolsDirectory = Path.GetFullPath(Value()) },
                "--scene" => options with { InitialState = options.InitialState with { Scene = Enum.Parse<SceneKind>(Value(), true) } },
                "--field" => SetField(options, Value()),
                "--entry" => options with { InitialState = options.InitialState with { EntryPoint = int.Parse(Value()) } },
                "--movie" => options with { InitialState = options.InitialState with { Scene = SceneKind.Movie, MovieIndex = int.Parse(Value()) } },
                "--movie-time" => options with { InitialState = options.InitialState with { MovieSeconds = double.Parse(Value(), System.Globalization.CultureInfo.InvariantCulture) } },
                "--exit-after" => options with { ExitAfterSeconds = double.Parse(Value(), System.Globalization.CultureInfo.InvariantCulture) },
                "--validate" => options with { ValidateOnly = true },
                "--capture-frame" => options with { CaptureFramePath = Path.GetFullPath(Value()) },
                "--capture-after" => options with { CaptureAfterSeconds = double.Parse(Value(), System.Globalization.CultureInfo.InvariantCulture) },
                "--frames" => options with { ExitAfterFrames = int.Parse(Value()) },
                "--resolution" => SetResolution(options, Value()),
                "--control-dir" => options with { ControlDirectory = Path.GetFullPath(Value()) },
                "--update-control-dir" => options with { UpdateControlDirectory = Path.GetFullPath(Value()) },
                "--control-step" => options with { ControlStep = true },
                "--mute-audio" => options with { MuteAudio = true },
                "--world-render-limit" => options with { WorldRenderLimit = int.Parse(Value()) },
                "--software-renderer" => options with { SoftwareRenderer = true },
                "--battle-test" => options with { BattleTestPath = Path.GetFullPath(Value()) },
                "--settings-dir" => options with { SettingsDirectory = Path.GetFullPath(Value()) },
                "--debug-room" => options with { DebugUiScreen = "debug-room" },
                "--ui-screen" => options with { DebugUiScreen = Value() },
                _ => throw new ArgumentException($"Unknown argument: {argument}")
            };
        }
        if (options.BattleTestPath is not null && (options.StatePath is not null || options.DebugUiScreen is not null || options.ControlStep || options.SoftwareRenderer || options.ValidateOnly || options.InitialState.Scene != SceneKind.Title))
        {
            throw new ArgumentException("--battle-test requires a fresh graphical GPU host with normal real-time pacing; state, scene, debug UI, validation, software rendering and control stepping are incompatible.");
        }
        if (options.BattleTestPath is not null && (options.SettingsDirectory is null || options.ControlDirectory is null || !args.Contains("--save-state", StringComparer.Ordinal)))
        {
            throw new ArgumentException("--battle-test requires isolated --settings-dir, --control-dir and an explicit disposable --save-state path.");
        }
        if (options.BattleTestPath is not null)
        {
            string[] paths = [Path.GetFullPath(options.SettingsDirectory!), Path.GetFullPath(options.ControlDirectory!), Path.GetDirectoryName(Path.GetFullPath(options.SavePath))!];
            string global = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Memoria", "FF8");
            if (paths.Any(path => IsWithin(path, global) || IsWithin(global, path)))
            {
                throw new ArgumentException("Diagnostic output and settings must not overlap the user's global settings directory.");
            }
            for (int i = 0; i < paths.Length; i++)
            {
                for (int j = i + 1; j < paths.Length; j++)
                {
                    if (IsWithin(paths[i], paths[j]) || IsWithin(paths[j], paths[i]))
                    {
                        throw new ArgumentException("Diagnostic settings, control and disposable save directories must be distinct and non-overlapping.");
                    }
                }
            }
        }
        if (options.StatePath is not null)
        {
            options = options with { InitialState = GameSession.Load(options.StatePath) };
        }
        if (options.InitialState.EntryPoint != 0)
        {
            throw new NotSupportedException("--entry currently supports zero only. Use --state for exact VM continuation.");
        }
        if (options.ControlStep && options.ControlDirectory is null)
        {
            throw new ArgumentException("--control-step requires --control-dir.");
        }
        if (options.WorldRenderLimit is < 64 or > 4096)
        {
            throw new ArgumentOutOfRangeException(nameof(args), "World render limit must be within64..4096 pixels on the longest side.");
        }
        return options;
    }

    private static bool IsWithin(string path, string root) => string.Equals(Path.TrimEndingDirectorySeparator(path), Path.TrimEndingDirectorySeparator(root), StringComparison.OrdinalIgnoreCase) || path.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static LaunchOptions SetField(LaunchOptions options, string value)
    {
        int fieldId = value switch { "start0" => 74, "bghoke_1" => 179, "bghoke_2" => 180, "bghoke_3" => 181, "bgroad_6" => 229, _ => int.Parse(value) };
        var session = new GameSession(options.InitialState);
        session.RequestField(fieldId, options.InitialState.EntryPoint);
        return options with { InitialState = session.State };
    }

    private static LaunchOptions SetResolution(LaunchOptions options, string value)
    {
        string[] dimensions = value.ToLowerInvariant().Split('x');
        if (dimensions.Length != 2 || !int.TryParse(dimensions[0], out int width) || !int.TryParse(dimensions[1], out int height) || width < 1 || height < 1)
        {
            throw new ArgumentException("Resolution must be positive WIDTHxHEIGHT.");
        }
        return options with { Width = width, Height = height, ResolutionSpecified = true };
    }
}
