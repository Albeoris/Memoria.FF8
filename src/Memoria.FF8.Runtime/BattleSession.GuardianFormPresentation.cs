using System.Collections.Immutable;
using System.Diagnostics;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    public bool CanSkipGuardianFormFinisher => Presentation is { } process && (process.CanSkipAttachedGuardianFormFinisher || process.CanSkipOrdinaryGuardianAnimation);

    private bool SkippingGuardianFormPresentation => Presentation is { } process && (process.SkippingAttachedGuardianFormFinisher || process.SkippingOrdinaryGuardianAnimation);

    public bool GuardianAnimationSkipPending => SkippingGuardianFormPresentation;

    private void AdvanceSkippedGuardianFormPresentation(TimeSpan? workBudget = null)
    {
        long started = workBudget is null ? 0 : Stopwatch.GetTimestamp();
        for (int callback = 0; callback < 4096 && SkippingGuardianFormPresentation; callback++)
        {
            DispatchTurnBasedFrame();
            Presentation!.RetainGuardianAnimationFinale();
            if (workBudget is { } budget && Stopwatch.GetElapsedTime(started) >= budget)
            {
                break;
            }
        }
        Presentation!.RetainGuardianAnimationFinale();
    }



    public bool SkipGuardianFormFinisher()
    {
        RequireExpandedPresentationDispatchIdle();
        return Presentation is { } process &&
            (process.RequestAttachedGuardianFormSkip(CaptureExpandedHostedGuardianFormFacts()) || process.SkipOrdinaryGuardianAnimation());
    }
}
