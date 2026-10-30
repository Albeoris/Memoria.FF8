using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;
using Memoria.FF8.Mods;

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
            using GameResourceHost host = new(options.GameDirectory!, watch: false, modsDirectory: options.ModsDirectory, modelSourceDirectory: options.ModelSourceDirectory);
            if (host.Mods.View.Outcome is ModUpdateOutcome.Failed or ModUpdateOutcome.Blocked or ModUpdateOutcome.ReviewRequired)
            {
                throw new InvalidDataException("Save import requires a successfully prepared mod resource selection: " + string.Join(" ", host.Mods.Diagnostics.Select(diagnostic => diagnostic.Message)));
            }
            using GameAssets assets = new(options.GameDirectory!, resources: host.Resources, modelOverrides: host);
            GameSession session = new(NativePcSaveConverter.Import(File.ReadAllBytes(options.SourcePath), assets));
            using (GameWorld world = new(session, options.GameDirectory!, resources: host.Resources, validateWorldPresentation: false, resolveGameFile: host.ResolveGameFile, sharedAssets: assets, readSceneMods: () => host.SceneMods, modelOverrides: host, readGameplayPolicy: () => host.GameplayPolicy))
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
                using GameWorld world = new(restored, options.GameDirectory!, resources: host.Resources, validateWorldPresentation: false, resolveGameFile: host.ResolveGameFile, sharedAssets: assets, readSceneMods: () => host.SceneMods, modelOverrides: host, readGameplayPolicy: () => host.GameplayPolicy);
                world.EnsureField();
            }));
            Console.WriteLine($"Imported original PC save to {options.OutputPath} | {session.State.Scene} | {session.State.FieldId}:{session.State.FieldName}");
        }
        else
        {
            GameState state = GameSession.Load(options.SourcePath);
            byte[] original;
            if (options.GameDirectory is { } gameDirectory)
            {
                if (!Directory.Exists(gameDirectory))
                {
                    throw new DirectoryNotFoundException("Export requires the selected installed game directory.");
                }
                using GameAssets assets = new(gameDirectory);
                original = NativePcSaveConverter.Export(state, assets.ReadNewGameCharacterVitals(), options.AllowGuardianForceLoss, OriginalSaveExportPlace.Read(state, gameDirectory));
            }
            else
            {
                original = NativePcSaveConverter.Export(state);
            }
            WriteNewOutput(options.OutputPath, temporary => File.WriteAllBytes(temporary, original), original: true);
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
        string? modsDirectory = null;
        string? modelSourceDirectory = null;
        bool allowGuardianForceLoss = false;
        for (int index = 1; index < args.Length; index++)
        {
            string argument = args[index];
            if (argument == "--allow-gf-loss" && !import)
            {
                if (allowGuardianForceLoss)
                {
                    throw new ArgumentException("Specify --allow-gf-loss only once.");
                }
                allowGuardianForceLoss = true;
                continue;
            }
            if (argument is not ("--output" or "--game-dir") && (!import || argument is not ("--mods-dir" or "--model-source-dir")))
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
            else if (argument == "--game-dir")
            {
                if (gameDirectory is not null)
                {
                    throw new ArgumentException("Specify --game-dir only once.");
                }
                gameDirectory = value;
            }
            else if (argument == "--model-source-dir")
            {
                if (modelSourceDirectory is not null)
                {
                    throw new ArgumentException("Specify --model-source-dir only once.");
                }
                modelSourceDirectory = value;
            }
            else
            {
                if (modsDirectory is not null)
                {
                    throw new ArgumentException("Specify --mods-dir only once.");
                }
                modsDirectory = value;
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
        if (allowGuardianForceLoss && gameDirectory is null)
        {
            throw new ArgumentException("Export with --allow-gf-loss requires --game-dir to declare the original GF definitions.");
        }
        return new(source, output, gameDirectory, modsDirectory, modelSourceDirectory, allowGuardianForceLoss);
    }

    private static void WriteNewOutput(string output, Action<string> write, bool original = false)
    {
        string directory = Path.GetDirectoryName(output)!;
        Directory.CreateDirectory(directory);
        string temporary = Path.Combine(directory, "." + Path.GetFileName(output) + "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            write(temporary);
            if (original)
            {
                OriginalSavePublication.Publish(temporary, output, overwrite: false, refreshPreview: true);
            }
            else
            {
                File.Move(temporary, output, overwrite: false);
            }
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    internal sealed record Options(string SourcePath, string OutputPath, string? GameDirectory, string? ModsDirectory, string? ModelSourceDirectory = null, bool AllowGuardianForceLoss = false);
}
