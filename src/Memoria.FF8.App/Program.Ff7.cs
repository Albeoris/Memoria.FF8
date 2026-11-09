using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;
using Memoria.FF8.Assets;

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
        if (envelope?.Ff8Payload is { Length: > 0 })
        {
            return RunCombined(options, envelope, mediaTools);
        }
        using GameAssets? appearanceAssets = envelope?.Mappings.Length > 0 ? new GameAssets(options.GameDirectory) : null;
        IFf7ActorAppearance? appearance = Ff7AppearanceSelection.Create(envelope?.Mappings ?? [], installation, appearanceAssets);
        CombinedSessionSave? requested;
        using (Ff7Game game = new(installation, mediaTools, options.CrossoverSavePath, new Ff7ProjectionDepth(native.FieldNearPlane, native.FieldFarPlane), movie => Ff7Scene.ResolveMovie(installation, mediaTools, movie), envelope, appearance, options))
        {
            game.Run();
            requested = game.RequestedCrossover;
        }
        return requested is null ? 0 : RunCombined(options, requested, mediaTools);
    }

    private static int RunCombined(LaunchOptions options, CombinedSessionSave envelope, string mediaTools)
    {
        using GameResourceHost selected = new(options.GameDirectory, watch: false, modsDirectory: options.ModsDirectory, resourceLanguage: options.ResourceLanguage, modelSourceDirectory: options.ModelSourceDirectory);
        GameState ff8 = GameplaySaveSessionLoader.Restore(CrossoverSaveAdapter.ReadFf8(envelope), options.GameDirectory, resources: selected.Resources, modelOverrides: selected, readGameplayPolicy: () => selected.GameplayPolicy, prepareCharacterDefinitions: initialized => GameplaySaveGuardianForceDefinitions.Select(initialized, selected));
        using RecreateGame game = new(options with { Ff7Debug = false, InitialState = ff8, MediaToolsDirectory = mediaTools }, new GameSession(ff8), playerSession: true, initialCrossover: envelope);
        game.RunWithErrorCapture();
        return 0;
    }
}
