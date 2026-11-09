using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal static partial class Program
{
    private static int RunFf7(LaunchOptions options)
    {
        string directory = options.Ff7GameDirectory ?? throw new ArgumentException("FFVII requires --ff7-game-dir or MEMORIA_FF7_GAME_DIR alongside the primary FFVIII installation.");
        Ff7Installation installation = Ff7Installation.Discover(directory);
        Ff7PcExecutableData native = Ff7PcExecutableData.Load(installation);
        if (options.ValidateOnly)
        {
            Console.WriteLine($"Memoria - Final Fantasy VII | {installation.Layout}/{installation.Language} | initial field={native.InitialField}");
            return 0;
        }
        string mediaTools = MediaToolsInstallation.ResolveDirectoryAsync(options.MediaToolsDirectory, Console.Error.WriteLine).GetAwaiter().GetResult();
        CombinedSessionSave? envelope = null;
        if (options.CrossoverStatePath is string statePath)
        {
            using FileStream stream = File.OpenRead(statePath);
            envelope = CombinedSessionCodec.Read(stream);
        }
        using Ff7Game game = new(installation, mediaTools, options.CrossoverSavePath, new Ff7ProjectionDepth(native.FieldNearPlane, native.FieldFarPlane), movie => Ff7Scene.ResolveMovie(installation, mediaTools, movie), envelope, launchOptions: options);
        game.Run();
        return 0;
    }
}
