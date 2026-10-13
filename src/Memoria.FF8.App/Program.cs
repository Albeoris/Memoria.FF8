using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length >= 4 && args[0] is "--recreate-save" or "--recreate-prefix" && args[2] == "--output")
            {
                string sourcePath = Path.GetFullPath(args[1]);
                string targetPath = Path.GetFullPath(args[3]);
                if (string.Equals(sourcePath, targetPath, StringComparison.OrdinalIgnoreCase) || File.Exists(targetPath))
                {
                    throw new IOException("Save recreation requires a new output path.");
                }
                LaunchOptions recreationOptions = LaunchOptions.Parse(args[4..]);
                var watch = System.Diagnostics.Stopwatch.StartNew();
                GameState source = GameSession.LoadReplaySource(sourcePath);
                GameState recreated = args[0] == "--recreate-prefix" ? GameWorld.RecreateCommittedPrefix(source, recreationOptions.GameDirectory, recreationOptions.MediaToolsDirectory) : GameWorld.RecreateSave(source, recreationOptions.GameDirectory, recreationOptions.MediaToolsDirectory);
                new GameSession(recreated).Save(targetPath);
                string scope = args[0] == "--recreate-prefix" ? "committed prefix" : "save";
                Console.WriteLine($"Recreated {scope} field{recreated.FieldId}/{recreated.Scene} in {watch.Elapsed.TotalSeconds:F3}s: {targetPath}");
                return 0;
            }
            if (args.Length > 0 && args[0] == "--headless-play")
            {
                return HeadlessPlayer.Run(args[1..]);
            }
            if (args.Length == 3 && args[0] is "--upgrade-saved-world-checkpoint" or "--upgrade-world-control-checkpoint")
            {
                string source = Path.GetFullPath(args[1]);
                string target = Path.GetFullPath(args[2]);
                if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase) || File.Exists(target))
                {
                    throw new IOException("Saved-world upgrade requires a new destination checkpoint.");
                }
                GameState upgraded = args[0] == "--upgrade-saved-world-checkpoint" ? LegacySavedWorldProof.Upgrade(File.ReadAllBytes(source)) : LegacyWorldControlProof.Upgrade(File.ReadAllBytes(source));
                new GameSession(upgraded).Save(target);
                Console.WriteLine($"Native world provenance added: {target}");
                return 0;
            }
            LaunchOptions options = LaunchOptions.Parse(args);
            if (!Directory.Exists(options.GameDirectory))
            {
                throw new DirectoryNotFoundException($"Installed game directory not found: {options.GameDirectory}");
            }
            Directory.SetCurrentDirectory(options.GameDirectory);
            var session = new GameSession(options.InitialState);
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
