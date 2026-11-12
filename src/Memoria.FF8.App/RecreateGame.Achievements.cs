using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;
using Memoria.FF7.Runtime;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private AchievementService? achievements;
    private bool achievementToolSession;
    private DateTime nextAchievementRetry;
    private DateTime nextAchievementWrite;
    private string? achievementFailure;
    private string? achievementBackendStatus;
    private string? isolatedAchievementDirectory;
    private bool observeOriginalAchievements;
    private DateTime nextOriginalAchievementObservation;
    private bool achievementsSuspended;
    private bool achievementsDisposed;
    private SteamPlatformSession? platformSession;
    private bool achievementWorldIsFf7;

    internal static SessionGame ReadAchievementGame(LaunchOptions options, CombinedSessionSave? initialCrossover)
    {
        CombinedSessionSave? saved = initialCrossover;
        string? path = options.CrossoverStatePath ?? options.StatePath;
        if (saved is null && path is not null)
        {
            saved = CrossoverSaveAdapter.ReadEnvelope(path);
        }
        return saved?.ActiveGame ?? (options.Ff7Debug ? SessionGame.Ff7 : SessionGame.Ff8);
    }

    private void InitializeAchievements()
    {
        if (achievementsSuspended || achievementsDisposed || achievements is not null || platformSession is not null)
        {
            return;
        }
        achievementWorldIsFf7 = ReadAchievementGame(options, initialCrossover) == SessionGame.Ff7;
        GamePlatformContext context = achievementWorldIsFf7
            ? GamePlatformContextResolver.ResolveFf7(options.Ff7GameDirectory ?? throw new InvalidOperationException("Select a FINAL FANTASY VII installation before initializing its platform session."), options.AchievementsLocalOnly || achievementToolSession)
            : GamePlatformContextResolver.Resolve(string.IsNullOrWhiteSpace(options.GameDirectory) ? Environment.CurrentDirectory : options.GameDirectory, options.AchievementsLocalOnly || achievementToolSession);
        Console.WriteLine($"achievements provider={context.Provider} product={context.ProductId ?? "unknown"} transport={context.Transport} diagnostic={context.Diagnostic}");
        if (achievementWorldIsFf7)
        {
            if (context.CanInitializeExternalBackend && context.Provider == GameDistributionProvider.Steam)
            {
                platformSession = new SteamPlatformSession(Path.Combine(Memoria.FF8.Platform.ApplicationInstallation.DataDirectory, "Steam", "steam_api64.dll"), context.ProductId!);
                Console.WriteLine($"platform product={context.ProductId} diagnostic={platformSession.Diagnostic}");
            }
            return;
        }
        string directory = Path.Combine(globalSettings.DirectoryPath, "achievements");
        if (achievementToolSession)
        {
            directory = isolatedAchievementDirectory ??= Path.Combine(Path.GetTempPath(), "Memoria.FF8", "achievements", Guid.NewGuid().ToString("N"));
        }
        // Storage follows the selected product; loose folders remain an explicitly unbound local profile.
        string profile = context.ProductId is "39150" or "1026680" or "1086370078" ? context.ProductId : "local";
        FileAchievementRepository? repository = null;
        try
        {
            repository = new FileAchievementRepository(Path.Combine(directory, profile + ".json"));
            observeOriginalAchievements = context.ResourceEdition != GameDataEdition.Remastered;
            AchievementRegistry registry = observeOriginalAchievements ? OriginalAchievementCatalog.CreateRegistry() : AchievementRegistry.CreateCombatRegistry(remastered: true);
            IAchievementBackend backend = context.CanInitializeExternalBackend && context.Provider == GameDistributionProvider.Steam
                ? new SteamAchievementBackend(Path.Combine(Memoria.FF8.Platform.ApplicationInstallation.DataDirectory, "Steam", "steam_api64.dll"), context.ProductId!, "default")
                : new NoAchievementBackend(context.Diagnostic);
            AchievementBinding[] bindings = SteamAchievementMappings.ForProduct(context.ProductId);
            achievements = new AchievementService(registry, repository, backend, bindings);
            achievements.Committed += PresentAchievementChange;
            session.CaptureAchievementOwner = achievements.CaptureOwner;
            achievementFailure = null;
            Console.WriteLine($"achievements state={achievements.BackendState} pending={achievements.PendingDeliveryCount}");
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            repository?.Dispose();
            ReportAchievementFailure(exception.Message);
        }
    }

    private void UpdateAchievements(bool force = false)
    {
        if (achievementsSuspended || achievementsDisposed)
        {
            return;
        }
        if (achievementWorldIsFf7)
        {
            platformSession?.Pump();
            return;
        }
        if (achievements is null)
        {
            if (!force && DateTime.UtcNow < nextAchievementRetry)
            {
                return;
            }
            InitializeAchievements();
        }
        if (achievements is null)
        {
            return;
        }
        try
        {
            achievements.Pump();
            if (observeOriginalAchievements && (force || DateTime.UtcNow >= nextOriginalAchievementObservation))
            {
                session.ObserveOriginalAchievements(nativeLanguage12: false);
                nextOriginalAchievementObservation = DateTime.UtcNow.AddSeconds(1);
            }
            string status = $"{achievements.BackendState}: {achievements.Diagnostic}";
            if (achievementBackendStatus != status)
            {
                Console.WriteLine($"achievements state={achievements.BackendState} pending={achievements.PendingDeliveryCount} diagnostic={achievements.Diagnostic}");
                achievementBackendStatus = status;
            }
            if (!force && DateTime.UtcNow < nextAchievementRetry)
            {
                return;
            }
            EnemyDefeatedEvent[] finalized = session.State.PendingAchievementFacts.Where(fact => fact.Facts.AcceptedOutcome is not null).ToArray();
            BattleKillTotalObservedEvent[] observations = session.State.PendingAchievementObservations.ToArray();
            OriginalAchievementObservedEvent[] originalObservations = session.State.PendingOriginalAchievementObservations.ToArray();
            OriginalAchievementActionEvent[] originalActions = session.State.PendingOriginalAchievementActions.ToArray();
            if (finalized.Length == 0 && observations.Length == 0 && originalObservations.Length == 0 && originalActions.Length == 0 || !force && DateTime.UtcNow < nextAchievementWrite && !observations.Any(fact => fact.NativeKillTotal >= 100 && !achievements.Get(AchievementRegistry.EnemiesDefeated100).Unlocked))
            {
                return;
            }
            BattleKillTotalObservedEvent[] acceptedObservations = observations.Take(force ? observations.Length : 256).ToArray();
            achievements.RecordBatch(acceptedObservations);
            session.AcknowledgeAchievementObservations(acceptedObservations);
            EnemyDefeatedEvent[] accepted = finalized.Take(force ? finalized.Length : 256).ToArray();
            achievements.RecordBatch(accepted);
            session.AcknowledgeAchievementFacts(accepted);
            OriginalAchievementObservedEvent[] acceptedOriginal = originalObservations.Take(force ? originalObservations.Length : 256).ToArray();
            achievements.RecordBatch(acceptedOriginal);
            session.AcknowledgeOriginalAchievementObservations(acceptedOriginal);
            OriginalAchievementActionEvent[] acceptedActions = originalActions.Take(force ? originalActions.Length : 256).ToArray();
            achievements.RecordBatch(acceptedActions);
            session.AcknowledgeOriginalAchievementActions(acceptedActions);
            nextAchievementWrite = DateTime.UtcNow.AddSeconds(1);
            achievementFailure = null;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            ReportAchievementFailure(exception.Message);
        }
    }

    private void ReportAchievementFailure(string message)
    {
        nextAchievementRetry = DateTime.UtcNow.AddSeconds(5);
        if (achievementFailure != message)
        {
            Console.Error.WriteLine("achievements persistence unavailable; accepted facts remain pending: " + message);
            ShowSaveNotice("Achievement progress pending: " + message);
            achievementFailure = message;
        }
    }

    private void PresentAchievementChange(AchievementChange change)
    {
        if (change.NewlyUnlocked)
        {
            ShowSaveNotice("Achievement unlocked: " + change.Current.Name, 6);
        }
        else if (uiPreferences.ShowAchievementProgress && !change.Current.Unlocked && change.Current.ProgressKnown)
        {
            ShowSaveNotice($"{change.Current.Name}: {change.Current.DisplayProgress}/{change.Current.Target}", 3);
        }
        Console.WriteLine($"achievement={change.Current.Id.Value} progress={change.Current.DisplayProgress}/{change.Current.Target} unlocked={change.Current.Unlocked} state={achievements!.BackendState} pending={achievements.PendingDeliveryCount}");
    }

    private void DisposeAchievements()
    {
        if (achievementsDisposed)
        {
            return;
        }
        try
        {
            if (achievements is not null)
            {
                UpdateAchievements(force: true);
            }
        }
        finally
        {
            achievementsDisposed = true;
            session.CaptureAchievementOwner = null;
            try
            {
                achievements?.Dispose();
            }
            finally
            {
                achievements = null;
                platformSession?.Dispose();
                platformSession = null;
            }
        }
    }

    private void EnterAchievementToolSession()
    {
        if (achievementToolSession)
        {
            return;
        }
        UpdateAchievements(force: true);
        if (session.State.PendingAchievementObservations.Length != 0 || session.State.PendingAchievementFacts.Any(fact => fact.Facts.AcceptedOutcome is not null) || session.State.PendingOriginalAchievementObservations.Length != 0 || session.State.PendingOriginalAchievementActions.Length != 0)
        {
            throw new IOException("Player achievement receipts must be saved before starting an isolated diagnostic session.");
        }
        DisposeAchievements();
        achievementsDisposed = false;
        achievementToolSession = true;
        isolatedAchievementDirectory = null;
        InitializeAchievements();
        ShowSaveNotice("Achievements use an isolated profile for this diagnostic session. Restart to return to the player profile.");
    }
}
