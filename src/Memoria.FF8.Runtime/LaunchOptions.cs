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
    public bool ControlStep { get; init; }
    public bool MuteAudio { get; init; }
    public bool AudioEnabled => !MuteAudio && ControlDirectory is null && !System.Diagnostics.Debugger.IsAttached;
    public int WorldRenderLimit { get; init; } = 2048;
    public bool SoftwareRenderer { get; init; }
    public string? DebugUiScreen { get; init; }

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
                "--control-step" => options with { ControlStep = true },
                "--mute-audio" => options with { MuteAudio = true },
                "--world-render-limit" => options with { WorldRenderLimit = int.Parse(Value()) },
                "--software-renderer" => options with { SoftwareRenderer = true },
                "--debug-room" => options with { DebugUiScreen = "debug-room" },
                "--ui-screen" => options with { DebugUiScreen = Value() },
                _ => throw new ArgumentException($"Unknown argument: {argument}")
            };
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
