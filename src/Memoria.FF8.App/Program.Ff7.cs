using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;
using Memoria.FF8.Assets;

namespace Memoria.FF8;

internal sealed record PreparedCrossover(CombinedSessionSave Envelope, GameState Ff8);

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
            if (options.Ff7StartNewGame)
            {
                using Ff7Session fresh = Ff7Session.NewGame(installation);
                envelope = envelope with { ActiveGame = SessionGame.Ff7, Ff7 = fresh.Capture() };
            }
            return RunCombined(options, envelope, mediaTools);
        }
        using GameAssets? appearanceAssets = envelope?.Mappings.Length > 0 ? new GameAssets(options.GameDirectory) : null;
        IFf7ActorAppearance? appearance = Ff7AppearanceSelection.Create(envelope?.Mappings ?? [], installation, appearanceAssets);
        using Ff7Game game = new(installation, mediaTools, options.ControlExclusive ? options.SavePath : options.CrossoverSavePath, new Ff7ProjectionDepth(native.FieldNearPlane, native.FieldFarPlane), movie => Ff7Scene.ResolveMovie(installation, mediaTools, movie), envelope, appearance, options);
        game.Run();
        return 0;
    }

    private static int RunCombined(LaunchOptions options, CombinedSessionSave envelope, string mediaTools)
    {
        return RunCombined(options, PrepareCombined(options, envelope), mediaTools);
    }

    private static PreparedCrossover PrepareCombined(LaunchOptions options, CombinedSessionSave envelope)
    {
        envelope.Validate();
        GameplaySaveComponentDocument document = CrossoverSaveAdapter.ReadFf8ForResume(envelope);
        using GameResourceHost selected = new(options.GameDirectory, watch: false, modsDirectory: options.ModsDirectory, resourceLanguage: options.ResourceLanguage, modelSourceDirectory: options.ModelSourceDirectory);
        GameState ff8 = GameplaySaveSessionLoader.Restore(document, options.GameDirectory, resources: selected.Resources, modelOverrides: selected, readGameplayPolicy: () => selected.GameplayPolicy, prepareCharacterDefinitions: initialized => GameplaySaveGuardianForceDefinitions.Select(initialized, selected));
        if (ff8.Scene != SceneKind.Movie || ff8.EventState is not null)
        {
            GameSession candidateSession = new(ff8);
            using MovieClock clock = new(candidateSession, options.GameDirectory, options.MediaToolsDirectory, selected.ResolveGameFile);
            using GameWorld candidate = new(candidateSession, options.GameDirectory, clock, selected.Resources, readSceneMods: () => selected.SceneMods, modelOverrides: selected, readGameplayPolicy: () => selected.GameplayPolicy, applyEncounterMods: selected.ApplyEncounterMods);
            candidate.EnsureField();
        }
        Ff7Installation installation = Ff7Installation.Discover(options.Ff7GameDirectory ?? throw new InvalidOperationException("Select a FINAL FANTASY VII installation before loading this world."));
        if (envelope.Ff7 is not null)
        {
            using Ff7Session restored = Ff7Session.Restore(installation, envelope.Ff7);
        }
        using GameAssets? assets = envelope.Mappings.Length > 0 ? new GameAssets(options.GameDirectory, resources: selected.Resources, modelOverrides: selected) : null;
        _ = Ff7AppearanceSelection.Create(envelope.Mappings, installation, assets);
        return new PreparedCrossover(envelope, ff8);
    }

    private static int RunCombined(LaunchOptions options, PreparedCrossover prepared, string mediaTools)
    {
        GameState ff8 = prepared.Ff8;
        CombinedSessionSave envelope = prepared.Envelope.ActiveGame == SessionGame.Ff8 ? prepared.Envelope with { TurnBasedBattle = null } : prepared.Envelope;
        using RecreateGame game = new(options with { Ff7Debug = false, InitialState = ff8, MediaToolsDirectory = mediaTools }, new GameSession(ff8), playerSession: true, initialCrossover: envelope);
        game.RunWithErrorCapture();
        return 0;
    }
}
