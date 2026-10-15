using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
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
                throw new DirectoryNotFoundException($"Installed game directory not found: {options.GameDirectory}");
            }
            Directory.SetCurrentDirectory(options.GameDirectory);
            GameSession session = options.StatePath is null && options.InitialState.Scene == SceneKind.Title ? GameSession.CreateColdProcess() : new GameSession(options.InitialState);
            if (options.ValidateOnly)
            {
                Console.WriteLine($"Memoria - Final Fantasy VIII | {session.State.Scene} | {session.State.FieldId}:{session.State.FieldName} | cwd={Environment.CurrentDirectory}");
                return 0;
            }
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
