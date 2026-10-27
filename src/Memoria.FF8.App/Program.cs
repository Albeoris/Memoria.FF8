using Memoria.FF8.Runtime;
using System.Text;

namespace Memoria.FF8;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
#if MEMORIA_VULKAN
        if (OperatingSystem.IsMacOS())
        {
            Environment.SetEnvironmentVariable("SDL_VULKAN_LIBRARY", Path.Combine(AppContext.BaseDirectory, "libMoltenVK.dylib"));
        }
#endif
        Console.OutputEncoding = new UTF8Encoding(false);
        Console.SetError(new ConsoleErrorWriter(Console.Error, !Console.IsErrorRedirected));
        try
        {
            if (args.Length > 0 && args[0] == "--probe-steam-achievements")
            {
                return SteamAchievementProbe.Run(args[1..]);
            }
            if (args.Length > 0 && args[0] is "--import-save" or "--export-save")
            {
                return SaveTransferCommand.Run(args[1..], import: args[0] == "--import-save");
            }
            SystemFontCatalog.PrepareAsync().GetAwaiter().GetResult();
            if (args.Length > 0 && args[0] == "--auto-play")
            {
                return AutoPlayCommand.Run(args[1..]);
            }
            if (args.Length > 0 && args[0] == "--triple-triad")
            {
                return TripleTriadPractice.Run(args[1..]);
            }
            if (args.Length > 0 && args[0] == "--menu-preview")
            {
                return MenuPreview.Run(args[1..]);
            }
            if (args.Length > 0 && args[0] == "--battle-preview")
            {
                return BattlePreview.Run(args[1..]);
            }
            if (args.Length > 0 && args[0] == "--headless-play")
            {
                return HeadlessPlayer.Run(args[1..]);
            }
            if (args.Length > 0 && args[0] == "--headless-control")
            {
                return HeadlessControlHost.Run(args[1..]);
            }
            LaunchOptions options = LaunchOptions.Parse(args);
            if (options.RequestedGraphicsBackend is Memoria.FF8.Platform.GraphicsBackend requested && requested != Memoria.FF8.Platform.GraphicsBackend.Auto && requested.ToString() != GraphicsHostReadiness.Backend)
            {
                throw new ArgumentException($"This executable is the {GraphicsHostReadiness.Backend} host, but {requested} was requested. Start Data/Memoria.FF8.exe to select a backend, or try Auto.");
            }
            if (!Directory.Exists(options.GameDirectory))
            {
                throw new DirectoryNotFoundException($"Installed game directory not found: {options.GameDirectory}. Pass --game-dir or set MEMORIA_FF8_GAME_DIR or FF8_GAME_DIRECTORY.");
            }
            Directory.SetCurrentDirectory(options.GameDirectory);
            GameSession session = options.StatePath is null && options.InitialState.Scene == SceneKind.Title ? GameWorld.CreateColdProcess(options.GameDirectory) : new GameSession(options.InitialState);
            if (options.ValidateOnly)
            {
                Console.WriteLine($"Memoria - Final Fantasy VIII | {session.State.Scene} | {session.State.FieldId}:{session.State.FieldName} | cwd={Environment.CurrentDirectory}");
                return 0;
            }
            string mediaDestination = MediaToolsInstallation.ResolveDirectory(options.MediaToolsDirectory);
            string mediaTools = MediaToolsInstallation.ResolveDirectoryAsync(options.MediaToolsDirectory, Console.Error.WriteLine).GetAwaiter().GetResult();
            mediaTools = MediaToolsInstallation.PrepareForStartup(mediaTools, message => PortableMessageBox.Confirm(message, "Memoria — optional FFmpeg installation"), Console.Error.WriteLine, mediaDestination);
            options = options with { MediaToolsDirectory = mediaTools };
            using var game = new RecreateGame(options, session, playerSession: true);
            game.Run();
            return 0;
        }
        catch (Exception exception)
        {
            string? failurePath = Environment.GetEnvironmentVariable("MEMORIA_FF8_HOST_FAILURE_FILE");
            if (failurePath is not null)
            {
                string temporary = failurePath + ".tmp";
                File.WriteAllText(temporary, System.Text.Json.JsonSerializer.Serialize(new { schemaVersion = 1, processId = Environment.ProcessId, backend = GraphicsHostReadiness.Backend, graphicsInitialization = GraphicsHostReadiness.IsInitializationFailure(exception), message = exception.Message }));
                File.Move(temporary, failurePath, true);
            }
            DiagnosticHostFailure.Write(exception, Environment.GetEnvironmentVariable("MEMORIA_FF8_OUTPUT_DIRECTORY"), Console.Error);
            return 1;
        }
    }
}
