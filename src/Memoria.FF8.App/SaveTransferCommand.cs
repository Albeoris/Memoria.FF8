using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal static class SaveTransferCommand
{
    public static int Run(string[] args, bool import)
    {
        Options options = Parse(args, import);
        if (!File.Exists(options.SourcePath))
        {
            throw new FileNotFoundException("Save source not found.", options.SourcePath);
        }
        if (File.Exists(options.OutputPath) || Directory.Exists(options.OutputPath))
        {
            throw new IOException($"Save output already exists: {options.OutputPath}. Choose a new filename; the original will be preserved.");
        }
        if (import)
        {
            if (!Directory.Exists(options.GameDirectory))
            {
                throw new DirectoryNotFoundException("Import requires an installed game directory. Pass --game-dir or set MEMORIA_FF8_GAME_DIR or FF8_GAME_DIRECTORY.");
            }
            using GameAssets assets = new(options.GameDirectory!);
            GameSession session = new(NativePcSaveConverter.Import(File.ReadAllBytes(options.SourcePath), assets));
            using (GameWorld world = new(session, options.GameDirectory!, validateWorldPresentation: false, sharedAssets: assets))
            {
                world.EnsureField();
                if (session.State.Scene == SceneKind.Field)
                {
                    session.SetEventState(world.Events.Capture());
                }
            }
            WriteNewOutput(options.OutputPath, temporary => session.Save(temporary, compact: true, candidate =>
            {
                GameSession restored = new(candidate);
                using GameWorld world = new(restored, options.GameDirectory!, validateWorldPresentation: false, sharedAssets: assets);
                world.EnsureField();
            }));
            Console.WriteLine($"Imported original PC save to {options.OutputPath} | {session.State.Scene} | {session.State.FieldId}:{session.State.FieldName}");
        }
        else
        {
            byte[] original = NativePcSaveConverter.Export(GameSession.Load(options.SourcePath));
            WriteNewOutput(options.OutputPath, temporary => File.WriteAllBytes(temporary, original));
            Console.WriteLine($"Exported original PC save to {options.OutputPath}");
        }
        return 0;
    }

    internal static Options Parse(string[] args, bool import)
    {
        if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0]) || args[0].StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException("Save transfer requires a source file followed by --output <new-file>.");
        }
        string source = Path.GetFullPath(args[0]);
        string? output = null;
        string? gameDirectory = null;
        for (int index = 1; index < args.Length; index++)
        {
            string argument = args[index];
            if (argument != "--output" && (argument != "--game-dir" || !import))
            {
                throw new ArgumentException($"Unknown save transfer argument: {argument}");
            }
            if (++index >= args.Length || string.IsNullOrWhiteSpace(args[index]) || args[index].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Missing value for {argument}.");
            }
            string value = Path.GetFullPath(args[index]);
            if (argument == "--output")
            {
                if (output is not null)
                {
                    throw new ArgumentException("Specify --output only once.");
                }
                output = value;
            }
            else
            {
                if (gameDirectory is not null)
                {
                    throw new ArgumentException("Specify --game-dir only once.");
                }
                gameDirectory = value;
            }
        }
        if (output is null)
        {
            throw new ArgumentException("Save transfer requires --output <new-file>.");
        }
        if (string.Equals(source, output, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Save source and output must be different files. The original will be preserved.");
        }
        if (import && gameDirectory is null && !string.IsNullOrWhiteSpace(LaunchOptions.ConfiguredGameDirectory))
        {
            gameDirectory = Path.GetFullPath(LaunchOptions.ConfiguredGameDirectory);
        }
        return new(source, output, gameDirectory);
    }

    private static void WriteNewOutput(string output, Action<string> write)
    {
        string directory = Path.GetDirectoryName(output)!;
        Directory.CreateDirectory(directory);
        string temporary = Path.Combine(directory, "." + Path.GetFileName(output) + "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            write(temporary);
            File.Move(temporary, output, overwrite: false);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    internal sealed record Options(string SourcePath, string OutputPath, string? GameDirectory);
}
