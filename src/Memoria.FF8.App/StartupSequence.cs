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
    // disc04_00h: Square Enix ends at frame30; the following black interval belongs to the publisher panel.
    public const double PublisherStartSeconds = 30 * 1001d / 30000;
    public StartupStage Stage { get; private set; } = enabled ? StartupStage.SquareEnixMovie : StartupStage.Complete;
    public TimeSpan Elapsed { get; private set; }
    public bool Active => Stage != StartupStage.Complete;

    public static bool ShouldPlay(LaunchOptions options)
    {
        return options.StatePath is null && options.InitialState.Scene == SceneKind.Title && options.DebugUiScreen is null;
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
        }
    }

    public void Complete()
    {
        Stage = StartupStage.Complete;
        Elapsed = TimeSpan.Zero;
    }

    public void RepeatCredits()
    {
        Stage = StartupStage.SquareLogo;
        Elapsed = TimeSpan.Zero;
    }
}
