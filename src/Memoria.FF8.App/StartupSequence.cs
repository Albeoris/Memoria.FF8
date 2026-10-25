using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal enum StartupStage
{
    SquareEnixMovie,
    PublisherMovie,
    SquareLogo,
    Credits,
    Complete
}

/// <summary>Presentation before the title menu; it owns no gameplay or save state.</summary>
internal sealed class StartupSequence(bool enabled)
{
    private bool configured;
    // disc04_00h: Square Enix ends at frame30; the following black interval belongs to the publisher panel.
    public const double PublisherStartSeconds = 30 * 1001d / 30000;
    public StartupStage Stage { get; private set; } = enabled ? StartupStage.SquareEnixMovie : StartupStage.Complete;
    public TimeSpan Elapsed { get; private set; }
    public bool Active => Stage != StartupStage.Complete;
    public bool IsConfigured => configured;
    public StartupPolicy Policy { get; private set; } = StartupPolicy.Default;

    public bool TryConfigure(StartupPolicy policy, long catalogGeneration)
    {
        if (catalogGeneration == 0)
        {
            return false;
        }
        ArgumentOutOfRangeException.ThrowIfNegative(catalogGeneration);
        Configure(policy);
        return true;
    }

    public void Configure(StartupPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (configured)
        {
            throw new InvalidOperationException("Startup policy is fixed for the current presentation.");
        }
        configured = true;
        Policy = policy;
        SkipConfiguredStages();
    }

    public static bool ShouldPlay(LaunchOptions options)
    {
        return options.BattleTestPath is null && options.StatePath is null && options.InitialState.Scene == SceneKind.Title && options.DebugUiScreen is null;
    }

    public bool HandleInput(bool confirm, bool cancel)
    {
        if (!Active || !confirm && !cancel)
        {
            return false;
        }
        if (cancel)
        {
            Complete();
        }
        else
        {
            Next();
        }
        return true;
    }

    public void Advance(TimeSpan elapsed)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(elapsed, TimeSpan.Zero);
        if (Active)
        {
            Elapsed += elapsed;
        }
    }

    public void Next()
    {
        if (Active)
        {
            Stage++;
            Elapsed = TimeSpan.Zero;
            SkipConfiguredStages();
        }
    }

    public void Complete()
    {
        Stage = StartupStage.Complete;
        Elapsed = TimeSpan.Zero;
    }

    public void RepeatCredits()
    {
        if (!Active)
        {
            return;
        }
        Stage = StartupStage.SquareLogo;
        Elapsed = TimeSpan.Zero;
        SkipConfiguredStages();
    }

    private void SkipConfiguredStages()
    {
        while ((Stage is StartupStage.SquareEnixMovie or StartupStage.PublisherMovie && Policy.SkipPublisherMovie) || (Stage == StartupStage.SquareLogo && Policy.SkipSquareLogo) || (Stage == StartupStage.Credits && Policy.SkipCreditsImages))
        {
            Stage++;
            Elapsed = TimeSpan.Zero;
        }
    }
}
