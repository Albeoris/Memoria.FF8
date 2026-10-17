using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        Console.SetError(new ConsoleErrorWriter(Console.Error, !Console.IsErrorRedirected));
        try
        {
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
            if (!Directory.Exists(options.GameDirectory))
            {
                throw new DirectoryNotFoundException($"Installed game directory not found: {options.GameDirectory}. Pass --game-dir or set MEMORIA_FF8_GAME_DIR or FF8_GAME_DIRECTORY.");
            }
            Directory.SetCurrentDirectory(options.GameDirectory);
            GameSession session = options.StatePath is null && options.InitialState.Scene == SceneKind.Title ? GameSession.CreateColdProcess() : new GameSession(options.InitialState);
            if (options.ValidateOnly)
            {
                Console.WriteLine($"Memoria - Final Fantasy VIII | {session.State.Scene} | {session.State.FieldId}:{session.State.FieldName} | cwd={Environment.CurrentDirectory}");
                return 0;
            }
            string mediaTools = MediaToolsInstallation.ResolveDirectory(options.MediaToolsDirectory);
            options = options with { MediaToolsDirectory = mediaTools };
            MediaToolsInstallation.PrepareForStartup(mediaTools, message => System.Windows.MessageBox.Show(message, "Memoria — optional FFmpeg installation", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question, System.Windows.MessageBoxResult.No) == System.Windows.MessageBoxResult.Yes, Console.Error.WriteLine);
            using var game = new RecreateGame(options, session);
            game.Run();
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}
